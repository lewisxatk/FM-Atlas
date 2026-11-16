using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FMAtlas.Application.Abstractions;
using FMAtlas.Application.Models;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;

namespace FMAtlas.Infrastructure.Import;

/// <summary>
/// Imports a Football Manager player-list CSV produced by FM View Exporter.
/// The importer only reads the selected file and writes local Atlas snapshots.
/// It does not inspect or modify the Football Manager process or career file.
/// </summary>
public sealed class FMViewExporterCsvImporter : IPlayerExportImporter
{
    private const int MaximumFileBytes = 64 * 1024 * 1024;
    private const int MaximumRows = 100_000;
    private const int MaximumColumns = 512;
    private const int MaximumWarnings = 50;

    private static readonly FieldProvenance CsvEvidence = new(
        DataSourceKind.GameExport,
        FieldEvidenceStatus.Mapped,
        SourceVersion: "FM View Exporter CSV",
        ObservedAtUtc: DateTimeOffset.UtcNow,
        Description: "Imported from a Football Manager player-list CSV. Field mapping is not a live-memory verification.");

    private static readonly IReadOnlyDictionary<string, string> AttributeAliases = BuildAttributeAliases();

    private readonly IEntityRecordSnapshotStore _snapshotStore;

    public FMViewExporterCsvImporter(IEntityRecordSnapshotStore snapshotStore)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
    }

    public async Task<PlayerExportImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Choose an FM View Exporter CSV file.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);
        var file = new FileInfo(fullPath);
        if (!file.Exists)
            throw new FileNotFoundException("The selected player export could not be found.", fullPath);
        if (file.Length == 0)
            throw new InvalidDataException("The selected CSV file is empty.");
        if (file.Length > MaximumFileBytes)
            throw new InvalidDataException($"The selected CSV exceeds the {MaximumFileBytes / 1024 / 1024} MB import limit.");

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        string csv;
        try
        {
            csv = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes)
                .TrimStart('\uFEFF');
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The CSV must be UTF-8 encoded. Re-export it using FM View Exporter.", exception);
        }

        var delimiter = DetectDelimiter(csv);
        var rows = ParseCsv(csv, delimiter);
        if (rows.Count < 2)
            throw new InvalidDataException("The CSV contains a header but no player rows.");
        if (rows[0].Length is < 2 or > MaximumColumns)
            throw new InvalidDataException($"The CSV header must contain between 2 and {MaximumColumns} columns.");

        var headers = rows[0].Select(value => value.Trim()).ToArray();
        var headerLookup = BuildHeaderLookup(headers);
        var nameColumn = FindColumn(headerLookup, "Player", "Name", "Full Name", "Player Name");
        var uidColumn = FindColumn(headerLookup, "Unique ID", "UID", "Player UID", "Game UID");
        if (nameColumn < 0 || uidColumn < 0)
            throw new InvalidDataException(
                "This does not look like an FM player export. Required columns are 'Player' (or 'Name') and 'Unique ID'.");

        var fileHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        // Each exact export is an isolated provider namespace. Re-importing the same
        // file updates the same snapshots; a different export cannot overwrite another.
        var providerKey = $"fm27-view-export:{fileHash[..20]}";
        var warnings = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var imported = 0;
        var skipped = 0;
        var duplicateIds = 0;
        var dynamicFieldsIndexed = 0;
        var rowsRead = 0;

        var mappedColumns = BuildMappedColumnSet(headerLookup);
        var dynamicColumns = BuildDynamicColumnMap(headers, mappedColumns);

        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = rows[rowIndex];
            if (row.All(string.IsNullOrWhiteSpace))
                continue;

            rowsRead++;
            if (rowsRead > MaximumRows)
                throw new InvalidDataException($"The CSV exceeds the {MaximumRows:N0} player-row import limit.");

            var uid = GetCell(row, uidColumn).Trim();
            var name = GetCell(row, nameColumn).Trim();
            if (uid.Length == 0 || name.Length == 0)
            {
                skipped++;
                AddWarning(warnings, $"Row {rowIndex + 1}: skipped because Player or Unique ID is blank.");
                continue;
            }

            if (!seenIds.Add(uid))
            {
                duplicateIds++;
                skipped++;
                AddWarning(warnings, $"Row {rowIndex + 1}: skipped duplicate Unique ID '{uid}'.");
                continue;
            }

            var record = BuildPlayerRecord(row, headers, headerLookup, dynamicColumns, uid, providerKey, name, warnings, rowIndex + 1);
            try
            {
                await _snapshotStore.SaveSnapshotAsync(record, name, cancellationToken).ConfigureAwait(false);
                imported++;
                dynamicFieldsIndexed += record.SearchTextFields.Count;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                skipped++;
                AddWarning(warnings, $"Row {rowIndex + 1} ({name}): could not save the record: {exception.Message}");
            }
        }

        return new PlayerExportImportResult(
            providerKey,
            rowsRead,
            imported,
            skipped,
            duplicateIds,
            dynamicFieldsIndexed,
            warnings.AsReadOnly());
    }

    private static PlayerRecord BuildPlayerRecord(
        IReadOnlyList<string> row,
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, int> headerLookup,
        IReadOnlyDictionary<int, string> dynamicColumns,
        string uid,
        string providerKey,
        string name,
        ICollection<string> warnings,
        int rowNumber)
    {
        var identity = new EntityIdentity(EntityKind.Player, uid, providerKey);
        var attributes = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var hidden = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var personality = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var foot = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var dynamicText = new Dictionary<string, SourcedValue<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (columnIndex, fieldKey) in AttributeAliases)
        {
            if (!headerLookup.TryGetValue(columnIndex, out var header))
                continue;

            var raw = GetCell(row, columnIndex).Trim();
            if (raw.Length == 0)
                continue;

            if (!TryInt(raw, out var value) || value is < 0 or > 20)
            {
                AddWarning(warnings, $"Row {rowNumber}: '{header}' is not a valid 0–20 player rating and was left blank.");
                continue;
            }

            if (PlayerAttributeCatalog.TryGetByKey(fieldKey, out var definition) && definition is not null)
            {
                var target = definition.Group switch
                {
                    PlayerAttributeGroupKind.Hidden => hidden,
                    PlayerAttributeGroupKind.Personality => personality,
                    PlayerAttributeGroupKind.Feet => foot,
                    _ => attributes
                };
                target[fieldKey] = new SourcedValue<int>(value, CsvEvidence);
            }
        }

        foreach (var (columnIndex, fieldKey) in dynamicColumns)
        {
            var raw = GetCell(row, columnIndex).Trim();
            if (raw.Length == 0)
                continue;

            dynamicText[fieldKey] = new SourcedValue<string>(raw, CsvEvidence);
        }

        var dob = GetDate(row, FindColumn(headerLookup, "Date of Birth", "DOB", "Birth Date"));
        var height = GetDecimal(row, FindColumn(headerLookup, "Height", "Height (cm)"), allowCentimetres: true);
        var weight = GetDecimal(row, FindColumn(headerLookup, "Weight", "Weight (kg)"));
        var age = GetInt(row, FindColumn(headerLookup, "Age"));
        var ca = GetInt(row, FindColumn(headerLookup, "CA", "Current Ability"));
        var pa = GetInt(row, FindColumn(headerLookup, "PA", "Potential Ability"));
        var caps = GetInt(row, FindColumn(headerLookup, "International Caps", "Caps"));
        var goals = GetInt(row, FindColumn(headerLookup, "International Goals"));
        var leftFoot = foot.TryGetValue("left_foot", out var left) ? left : null;
        var rightFoot = foot.TryGetValue("right_foot", out var right) ? right : null;

        return new PlayerRecord(identity)
        {
            FullName = new SourcedValue<string>(name, CsvEvidence),
            GameUid = new SourcedValue<string>(uid, CsvEvidence),
            DateOfBirth = dob is DateOnly birth ? new SourcedValue<DateOnly>(birth, CsvEvidence) : null,
            Age = age is int playerAge ? new SourcedValue<int>(playerAge, CsvEvidence) : null,
            Nationality = GetText(row, headerLookup, CsvEvidence, "Nation", "Nationality"),
            SecondNationality = GetText(row, headerLookup, CsvEvidence, "2nd Nat", "Second Nationality"),
            Gender = GetText(row, headerLookup, CsvEvidence, "Gender"),
            ClubName = GetText(row, headerLookup, CsvEvidence, "Club"),
            TeamName = GetText(row, headerLookup, CsvEvidence, "Team"),
            LeagueName = GetText(row, headerLookup, CsvEvidence, "Division", "League"),
            PrimaryPosition = GetText(row, headerLookup, CsvEvidence, "Position", "Best Pos"),
            PreferredFoot = GetText(row, headerLookup, CsvEvidence, "Preferred Foot"),
            HeightCentimetres = height is decimal cm ? new SourcedValue<decimal>(cm, CsvEvidence) : null,
            WeightKilograms = weight is decimal kg ? new SourcedValue<decimal>(kg, CsvEvidence) : null,
            CurrentAbility = ca is int current ? new SourcedValue<int>(current, CsvEvidence) : null,
            PotentialAbility = pa is int potential ? new SourcedValue<int>(potential, CsvEvidence) : null,
            InternationalCaps = caps is int internationalCaps ? new SourcedValue<int>(internationalCaps, CsvEvidence) : null,
            InternationalGoals = goals is int internationalGoals ? new SourcedValue<int>(internationalGoals, CsvEvidence) : null,
            Attributes = attributes,
            HiddenAttributes = hidden,
            Personality = personality,
            FootRatings = foot,
            SearchTextFields = dynamicText
        };
    }

    private static IReadOnlyDictionary<string, string> BuildAttributeAliases()
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in PlayerAttributeCatalog.AllUniqueNonFootAttributes
                     .Concat(PlayerAttributeCatalog.PersonalityAttributes)
                     .Concat(PlayerAttributeCatalog.FootAttributes))
        {
            aliases[NormaliseHeader(definition.DisplayName)] = definition.Key;
        }

        aliases[NormaliseHeader("Free Kicks")] = "free_kicks";
        aliases[NormaliseHeader("Free Kick Taking")] = "free_kicks";
        aliases[NormaliseHeader("Team Work")] = "teamwork";
        aliases[NormaliseHeader("One On Ones")] = "one_on_ones";
        aliases[NormaliseHeader("Punching")] = "punching";
        aliases[NormaliseHeader("Rushing Out (Tendency)")] = "rushing_out";
        return aliases;
    }

    private static IReadOnlyDictionary<int, string> BuildDynamicColumnMap(
        IReadOnlyList<string> headers,
        IReadOnlySet<int> mappedColumns)
    {
        var result = new Dictionary<int, string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            if (mappedColumns.Contains(index))
                continue;

            var slug = Slug(headers[index]);
            if (slug.Length == 0)
                continue;

            var key = $"export_{slug}";
            var suffix = 2;
            while (!used.Add(key))
                key = $"export_{slug}_{suffix++}";

            result[index] = key;
        }

        return result;
    }

    private static IReadOnlySet<int> BuildMappedColumnSet(IReadOnlyDictionary<string, int> lookup)
    {
        var mapped = new HashSet<int>();
        foreach (var alias in new[]
        {
            "Player", "Name", "Full Name", "Player Name", "Unique ID", "UID", "Player UID", "Game UID",
            "Date of Birth", "DOB", "Birth Date", "Age", "Nation", "Nationality", "2nd Nat", "Second Nationality",
            "Gender", "Club", "Team", "Division", "League", "Position", "Best Pos", "Preferred Foot",
            "Height", "Height (cm)", "Weight", "Weight (kg)", "CA", "Current Ability", "PA", "Potential Ability",
            "International Caps", "Caps", "International Goals"
        })
        {
            var index = FindColumn(lookup, alias);
            if (index >= 0)
                mapped.Add(index);
        }

        foreach (var header in lookup.Keys)
        {
            if (AttributeAliases.ContainsKey(header))
                mapped.Add(lookup[header]);
        }

        return mapped;
    }

    private static IReadOnlyDictionary<string, int> BuildHeaderLookup(IReadOnlyList<string> headers)
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            var key = NormaliseHeader(headers[index]);
            if (key.Length > 0 && !lookup.ContainsKey(key))
                lookup[key] = index;
        }

        return lookup;
    }

    private static int FindColumn(IReadOnlyDictionary<string, int> lookup, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var key = NormaliseHeader(alias);
            if (lookup.TryGetValue(key, out var index))
                return index;
        }

        return -1;
    }

    private static SourcedValue<string>? GetText(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> lookup,
        FieldProvenance provenance,
        params string[] aliases)
    {
        var index = FindColumn(lookup, aliases);
        var value = GetCell(row, index).Trim();
        return value.Length == 0 ? null : new SourcedValue<string>(value, provenance);
    }

    private static int? GetInt(IReadOnlyList<string> row, int index)
    {
        var value = GetCell(row, index).Trim();
        return value.Length > 0 && TryInt(value, out var parsed) ? parsed : null;
    }

    private static DateOnly? GetDate(IReadOnlyList<string> row, int index)
    {
        var value = GetCell(row, index).Trim();
        if (value.Length == 0)
            return null;

        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "d.M.yyyy", "d MMM yyyy", "dd MMM yyyy" };
        return DateOnly.TryParseExact(value, formats, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.AllowWhiteSpaces, out var date)
            ? date
            : DateOnly.TryParse(value, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.AllowWhiteSpaces, out date)
                ? date
                : null;
    }

    private static decimal? GetDecimal(IReadOnlyList<string> row, int index, bool allowCentimetres = false)
    {
        var value = GetCell(row, index).Trim();
        if (value.Length == 0)
            return null;

        if (allowCentimetres && value.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
            value = value[..^2].Trim();

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out parsed)
            ? parsed
            : null;
    }

    private static bool TryInt(string value, out int parsed) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);

    private static string GetCell(IReadOnlyList<string> row, int index) =>
        index >= 0 && index < row.Count ? row[index] : string.Empty;

    private static char DetectDelimiter(string csv)
    {
        var firstLine = csv.AsSpan();
        var lineEnd = firstLine.IndexOfAny('\r', '\n');
        if (lineEnd >= 0)
            firstLine = firstLine[..lineEnd];

        var commas = 0;
        var semicolons = 0;
        var quoted = false;
        for (var index = 0; index < firstLine.Length; index++)
        {
            var character = firstLine[index];
            if (character == '"')
            {
                if (quoted && index + 1 < firstLine.Length && firstLine[index + 1] == '"')
                {
                    index++;
                    continue;
                }

                quoted = !quoted;
                continue;
            }

            if (!quoted)
            {
                if (character == ',') commas++;
                if (character == ';') semicolons++;
            }
        }

        return semicolons > commas ? ';' : ',';
    }

    private static List<string[]> ParseCsv(string csv, char delimiter)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            if (character == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (character == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (character is '\r' or '\n')
            {
                fields.Add(field.ToString());
                field.Clear();
                if (fields.Any(value => value.Length > 0))
                    rows.Add(fields.ToArray());
                fields.Clear();

                if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                    index++;
            }
            else
            {
                field.Append(character);
            }
        }

        if (quoted)
            throw new InvalidDataException("The CSV contains an unclosed quoted field.");

        fields.Add(field.ToString());
        if (fields.Any(value => value.Length > 0))
            rows.Add(fields.ToArray());

        return rows;
    }

    private static string NormaliseHeader(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        var previousUnderscore = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousUnderscore = false;
            }
            else if (!previousUnderscore && builder.Length > 0)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }

        return builder.ToString().Trim('_');
    }

    private static void AddWarning(ICollection<string> warnings, string message)
    {
        if (warnings.Count < MaximumWarnings)
            warnings.Add(message);
    }
}
