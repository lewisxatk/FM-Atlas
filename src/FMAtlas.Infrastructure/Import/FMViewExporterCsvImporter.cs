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

    private enum DynamicFieldKind { Text, Integer, Decimal, Boolean, Date }
    private sealed record DynamicColumnDefinition(string Key, DynamicFieldKind Kind);

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
        var provenance = Createprovenance();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var imported = 0;
        var skipped = 0;
        var duplicateIds = 0;
        var rowsRead = 0;

        var mappedColumns = BuildMappedColumnSet(headers);
        var dynamicColumns = BuildDynamicColumnMap(headers, mappedColumns, rows, delimiter);

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

            var record = BuildPlayerRecord(
                row, headers, headerLookup, dynamicColumns, uid, providerKey, name,
                warnings, rowIndex + 1, provenance, useGermanDecimalSeparator: delimiter == ';');
            try
            {
                await _snapshotStore.SaveSnapshotAsync(record, name, cancellationToken).ConfigureAwait(false);
                imported++;
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
            imported > 0 ? dynamicColumns.Count : 0,
            warnings.AsReadOnly());
    }

    private static FieldProvenance CreateCsvEvidence() => new(
        DataSourceKind.GameExport,
        FieldEvidenceStatus.Mapped,
        SourceVersion: "FM View Exporter CSV",
        ObservedAtUtc: DateTimeOffset.UtcNow,
        Description: "Imported from a Football Manager player-list CSV. Field mapping is not a live-memory verification.");

    private static PlayerRecord BuildPlayerRecord(
        IReadOnlyList<string> row,
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, int> headerLookup,
        IReadOnlyDictionary<int, DynamicColumnDefinition> dynamicColumns,
        string uid,
        string providerKey,
        string name,
        ICollection<string> warnings,
        int rowNumber,
        FieldProvenance provenance,
        bool useGermanDecimalSeparator)
    {
        var identity = new EntityIdentity(EntityKind.Player, uid, providerKey);
        var attributes = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var hidden = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var personality = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var foot = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var dynamicIntegers = new Dictionary<string, SourcedValue<int>>(StringComparer.OrdinalIgnoreCase);
        var dynamicDecimals = new Dictionary<string, SourcedValue<decimal>>(StringComparer.OrdinalIgnoreCase);
        var dynamicBooleans = new Dictionary<string, SourcedValue<bool>>(StringComparer.OrdinalIgnoreCase);
        var dynamicDates = new Dictionary<string, SourcedValue<DateOnly>>(StringComparer.OrdinalIgnoreCase);
        var dynamicText = new Dictionary<string, SourcedValue<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (headerKey, fieldKey) in AttributeAliases)
        {
            if (!headerLookup.TryGetValue(headerKey, out var columnIndex))
                continue;

            var header = headers[columnIndex];
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
                target[fieldKey] = new SourcedValue<int>(value, provenance);
            }
        }

        foreach (var (columnIndex, definition) in dynamicColumns)
        {
            var raw = GetCell(row, columnIndex).Trim();
            if (IsMissingValue(raw))
                continue;

            switch (definition.Kind)
            {
                case DynamicFieldKind.Integer when TryInt(raw, out var integer):
                    dynamicIntegers[definition.Key] = new SourcedValue<int>(integer, provenance);
                    break;
                case DynamicFieldKind.Decimal when TryDecimalText(raw, useGermanDecimalSeparator, out var decimalValue):
                    dynamicDecimals[definition.Key] = new SourcedValue<decimal>(decimalValue, provenance);
                    break;
                case DynamicFieldKind.Boolean when TryBoolean(raw, out var boolean):
                    dynamicBooleans[definition.Key] = new SourcedValue<bool>(boolean, provenance);
                    break;
                case DynamicFieldKind.Date when TryDateText(raw, out var date):
                    dynamicDates[definition.Key] = new SourcedValue<DateOnly>(date, provenance);
                    break;
                default:
                    dynamicText[definition.Key] = new SourcedValue<string>(raw, provenance);
                    break;
            }
        }

        var dob = GetDate(row, FindColumn(headerLookup, "Date of Birth", "DOB", "Birth Date"));
        var height = GetDecimal(row, FindColumn(headerLookup, "Height", "Height (cm)"), useGermanDecimalSeparator, allowCentimetres: true);
        var weight = GetDecimal(row, FindColumn(headerLookup, "Weight", "Weight (kg)"), useGermanDecimalSeparator);
        var age = GetInt(row, FindColumn(headerLookup, "Age"));
        var ca = GetInt(row, FindColumn(headerLookup, "CA", "Current Ability"));
        var pa = GetInt(row, FindColumn(headerLookup, "PA", "Potential Ability"));
        var caps = GetInt(row, FindColumn(headerLookup, "International Caps", "Caps"));
        var goals = GetInt(row, FindColumn(headerLookup, "International Goals"));
        return new PlayerRecord(identity)
        {
            FullName = new SourcedValue<string>(name, provenance),
            GameUid = new SourcedValue<string>(uid, provenance),
            DateOfBirth = dob is DateOnly birth ? new SourcedValue<DateOnly>(birth, provenance) : null,
            Age = age is int playerAge ? new SourcedValue<int>(playerAge, provenance) : null,
            Nationality = GetTextAtColumn(row, FindLastColumn(headers, "Nation", "Nationality"), provenance),
            SecondNationality = GetText(row, headerLookup, provenance, "2nd Nat", "Second Nationality"),
            Gender = GetText(row, headerLookup, provenance, "Gender"),
            ClubName = GetText(row, headerLookup, provenance, "Club"),
            TeamName = GetText(row, headerLookup, provenance, "Team"),
            LeagueName = GetText(row, headerLookup, provenance, "Division", "League"),
            PrimaryPosition = GetText(row, headerLookup, provenance, "Position", "Best Pos"),
            PreferredFoot = GetText(row, headerLookup, provenance, "Preferred Foot"),
            HeightCentimetres = height is decimal cm ? new SourcedValue<decimal>(cm, provenance) : null,
            WeightKilograms = weight is decimal kg ? new SourcedValue<decimal>(kg, provenance) : null,
            CurrentAbility = ca is int current ? new SourcedValue<int>(current, provenance) : null,
            PotentialAbility = pa is int potential ? new SourcedValue<int>(potential, provenance) : null,
            InternationalCaps = caps is int internationalCaps ? new SourcedValue<int>(internationalCaps, provenance) : null,
            InternationalGoals = goals is int internationalGoals ? new SourcedValue<int>(internationalGoals, provenance) : null,
            Attributes = attributes,
            HiddenAttributes = hidden,
            Personality = personality,
            FootRatings = foot,
            SearchIntegerFields = dynamicIntegers,
            SearchDecimalFields = dynamicDecimals,
            SearchBooleanFields = dynamicBooleans,
            SearchDateFields = dynamicDates,
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

    private static IReadOnlyDictionary<int, DynamicColumnDefinition> BuildDynamicColumnMap(
        IReadOnlyList<string> headers,
        IReadOnlySet<int> mappedColumns,
        IReadOnlyList<string[]> rows,
        char delimiter)
    {
        var result = new Dictionary<int, DynamicColumnDefinition>();
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

            var values = rows.Skip(1)
                .Select(row => GetCell(row, index).Trim())
                .Where(value => !IsMissingValue(value))
                .ToArray();
            if (values.Length == 0)
                continue;

            // Type inference is based on every non-empty value in this one export
            // column. Any mixed/ambiguous column remains text so search never
            // silently coerces labels, ranges, money strings or mixed formats.
            var kind = values.All(value => TryBoolean(value, out _))
                ? DynamicFieldKind.Boolean
                : values.All(value => TryInt(value, out _))
                    ? DynamicFieldKind.Integer
                    : values.All(value => TryDecimalText(value, delimiter == ';', out _))
                        ? DynamicFieldKind.Decimal
                        : values.All(value => TryDateText(value, out _))
                            ? DynamicFieldKind.Date
                            : DynamicFieldKind.Text;

            result[index] = new DynamicColumnDefinition(key, kind);
        }

        return result;
    }

    private static IReadOnlySet<int> BuildMappedColumnSet(IReadOnlyList<string> headers)
    {
        var mapped = new HashSet<int>();
        var knownHeaders = new HashSet<string>(
            new[]
            {
                "Player", "Name", "Full Name", "Player Name", "Unique ID", "UID", "Player UID", "Game UID",
                "Date of Birth", "DOB", "Birth Date", "Age", "Nation", "Nationality", "2nd Nat", "Second Nationality",
                "Gender", "Club", "Team", "Division", "League", "Position", "Best Pos", "Preferred Foot",
                "Height", "Height (cm)", "Weight", "Weight (kg)", "CA", "Current Ability", "PA", "Potential Ability",
                "International Caps", "Caps", "International Goals"
            }.Select(NormaliseHeader),
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headers.Count; index++)
        {
            var normalized = NormaliseHeader(headers[index]);
            if (knownHeaders.Contains(normalized) || AttributeAliases.ContainsKey(normalized))
                mapped.Add(index);
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

    private static int FindLastColumn(IReadOnlyList<string> headers, params string[] aliases)
    {
        var normalizedAliases = aliases.Select(NormaliseHeader).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = headers.Count - 1; index >= 0; index--)
        {
            if (normalizedAliases.Contains(NormaliseHeader(headers[index])))
                return index;
        }

        return -1;
    }

    private static SourcedValue<string>? GetTextAtColumn(
        IReadOnlyList<string> row,
        int index,
        FieldProvenance provenance)
    {
        var value = GetCell(row, index).Trim();
        return value.Length == 0 ? null : new SourcedValue<string>(value, provenance);
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

    private static decimal? GetDecimal(IReadOnlyList<string> row, int index, bool useGermanDecimalSeparator, bool allowCentimetres = false)
    {
        var value = GetCell(row, index).Trim();
        if (value.Length == 0)
            return null;

        if (allowCentimetres && value.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
            value = value[..^2].Trim();

        return TryDecimalText(value, useGermanDecimalSeparator, out var parsed) ? parsed : null;
    }

    private static bool TryDecimalText(string value, bool useGermanDecimalSeparator, out decimal parsed)
    {
        var normalized = value.Trim();
        if (normalized.EndsWith('%'))
            normalized = normalized[..^1].Trim();

        var preferredCulture = useGermanDecimalSeparator
            ? CultureInfo.GetCultureInfo("de-DE")
            : CultureInfo.InvariantCulture;
        var fallbackCulture = useGermanDecimalSeparator
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo("de-DE");

        return decimal.TryParse(normalized, NumberStyles.Number, preferredCulture, out parsed) ||
               decimal.TryParse(normalized, NumberStyles.Number, fallbackCulture, out parsed);
    }

    private static bool TryBoolean(string value, out bool parsed)
    {
        var normalized = value.Trim();
        if (normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            parsed = true;
            return true;
        }

        if (normalized.Equals("no", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            parsed = false;
            return true;
        }

        parsed = false;
        return false;
    }

    private static bool TryDateText(string value, out DateOnly parsed)
    {
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "d.M.yyyy", "d MMM yyyy", "dd MMM yyyy" };
        return DateOnly.TryParseExact(value.Trim(), formats, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.AllowWhiteSpaces, out parsed) ||
               DateOnly.TryParse(value.Trim(), CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.AllowWhiteSpaces, out parsed);
    }

    private static bool IsMissingValue(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim() is "-" or "—" or "–" ||
        value.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase);

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
