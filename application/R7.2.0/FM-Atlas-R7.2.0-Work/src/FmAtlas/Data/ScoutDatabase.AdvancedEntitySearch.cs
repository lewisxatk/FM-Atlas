using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using FmAtlas.Models;

namespace FmAtlas.Data;

public sealed partial class ScoutDatabase
{
    private sealed record AdvancedEntityValue(double? Number, string? Text);

    private static void AddAdvancedValue(Dictionary<string, AdvancedEntityValue> values, string key, double? number, string? text)
    {
        if (string.IsNullOrWhiteSpace(key) || (!number.HasValue && text is null)) return;
        values[key] = new AdvancedEntityValue(number, text);
    }

    private static void AddAdvancedJsonValues(Dictionary<string, AdvancedEntityValue> values, JsonElement root, string prefix)
    {
        foreach (var fact in FlattenScalarFacts(root))
            AddAdvancedValue(values, prefix + fact.Key, fact.NumericValue, fact.TextValue);
    }

    private static string HumanizeAdvancedField(string path)
    {
        var leaf = path.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? path;
        return string.Join(" ", leaf.Replace('_', ' ').Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Length == 0 ? x : char.ToUpperInvariant(x[0]) + x[1..]));
    }

    private static string AdvancedCategory(string path, string fallback)
    {
        var group = path.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(group) ? fallback : HumanizeAdvancedField(group);
    }

    private static void RegisterAdvancedField(
        Dictionary<string, (string Display, string Category, bool Number, bool Text, bool Boolean)> fields,
        string key, string path, double? number, string? text, string categoryFallback)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        var isBoolean = text is not null &&
            (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("false", StringComparison.OrdinalIgnoreCase));
        var old = fields.TryGetValue(key, out var present)
            ? present
            : (HumanizeAdvancedField(path), AdvancedCategory(path, categoryFallback), false, false, false);
        fields[key] = (old.Display, old.Category, old.Number || number.HasValue, old.Text || (text is not null && !isBoolean), old.Boolean || isBoolean);
    }

    private static List<AdvancedSearchField> BuildAdvancedFieldList(
        Dictionary<string, (string Display, string Category, bool Number, bool Text, bool Boolean)> fields,
        string source)
    {
        return fields.Select(pair =>
        {
            var v = pair.Value;
            var type = v.Number && !v.Text && !v.Boolean
                ? AdvancedSearchFieldType.Number
                : v.Boolean && !v.Number && !v.Text
                    ? AdvancedSearchFieldType.Boolean
                    : AdvancedSearchFieldType.Text;
            return new AdvancedSearchField(pair.Key, v.Display, v.Category, type, source);
        })
        .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
        .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    private static bool MatchesAdvancedEntityCriteria(
        IReadOnlyDictionary<string, AdvancedEntityValue> values,
        IReadOnlyList<AdvancedSearchCriterion> criteria,
        bool matchAll)
    {
        if (criteria.Count == 0) return true;
        var matched = new List<bool>(criteria.Count);
        foreach (var criterion in criteria)
        {
            if (!values.TryGetValue(criterion.FieldKey, out var value))
            {
                matched.Add(false);
                continue;
            }

            bool current;
            if (criterion.Minimum.HasValue || criterion.Maximum.HasValue)
            {
                current = value.Number.HasValue
                    && (!criterion.Minimum.HasValue || value.Number.Value >= criterion.Minimum.Value)
                    && (!criterion.Maximum.HasValue || value.Number.Value <= criterion.Maximum.Value);
            }
            else if (criterion.BooleanValue.HasValue)
            {
                var text = value.Text ?? value.Number?.ToString(CultureInfo.InvariantCulture);
                current = bool.TryParse(text, out var parsed) && parsed == criterion.BooleanValue.Value;
            }
            else if (!string.IsNullOrWhiteSpace(criterion.TextValue))
            {
                var text = value.Text ?? value.Number?.ToString(CultureInfo.InvariantCulture);
                current = text?.Contains(criterion.TextValue, StringComparison.OrdinalIgnoreCase) == true;
            }
            else
            {
                // An empty condition is ignored by the UI and must never narrow results.
                current = true;
            }
            matched.Add(current);
        }

        return matchAll ? matched.All(x => x) : matched.Any(x => x);
    }

    private static bool MatchesAdvancedEntityText(
        IReadOnlyDictionary<string, AdvancedEntityValue> values, string? search)
    {
        var q = (search ?? string.Empty).Trim();
        if (q.Length == 0) return true;
        return values.Values.Any(value =>
        {
            var text = value.Text ?? value.Number?.ToString(CultureInfo.InvariantCulture);
            return text?.Contains(q, StringComparison.OrdinalIgnoreCase) == true;
        });
    }

    public async Task<List<AdvancedSearchField>> GetAdvancedStaffSearchFieldsAsync()
    {
        var fields = new Dictionary<string, (string Display, string Category, bool Number, bool Text, bool Boolean)>(StringComparer.OrdinalIgnoreCase)
        {
            ["staff:Name"] = ("Name", "Identity", false, true, false),
            ["staff:Age"] = ("Age", "Identity", true, false, false),
            ["staff:Club"] = ("Club", "Identity", false, true, false),
            ["staff:Job"] = ("Job title", "Identity", false, true, false),
            ["staff:Nation"] = ("Nationality", "Identity", false, true, false),
            ["staff:CurrentAbility"] = ("Current ability", "Ability", true, false, false),
            ["staff:PotentialAbility"] = ("Potential ability", "Ability", true, false, false),
            ["staff:IsHumanManager"] = ("Human manager", "Identity", false, false, true)
        };

        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);

        var raw = db.CreateCommand();
        raw.CommandText = "SELECT RawJson FROM FmsaveStaffDirectory WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns);";
        await using (var reader = await raw.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var json = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(json)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    foreach (var fact in FlattenScalarFacts(doc.RootElement))
                        RegisterAdvancedField(fields, "staff:raw:" + fact.Key, fact.Key, fact.NumericValue, fact.TextValue, "Imported fields");
                }
                catch (JsonException) { /* Invalid optional rows are omitted from the searchable catalogue. */ }
            }
        }

        var live = db.CreateCommand();
        live.CommandText = """
            SELECT FieldKey,
                   MAX(CASE WHEN NumericValue IS NOT NULL THEN 1 ELSE 0 END),
                   MAX(CASE WHEN TextValue IS NOT NULL THEN 1 ELSE 0 END),
                   MAX(CASE WHEN lower(COALESCE(TextValue,'')) IN ('true','false') THEN 1 ELSE 0 END)
            FROM LiveStaffFacts
            WHERE StaffUid IN (
                SELECT StaffUid FROM LiveStaff
                WHERE SourceRunId=(SELECT MAX(RunId) FROM LiveImportRuns WHERE Status='Completed')
            )
            GROUP BY FieldKey;
            """;
        await using (var reader = await live.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var key = reader.GetString(0);
                var number = reader.GetInt32(1) == 1;
                var text = reader.GetInt32(2) == 1;
                var boolean = reader.GetInt32(3) == 1;
                var category = AdvancedCategory(key, "Live fields");
                fields["staff:live:" + key] = (HumanizeAdvancedField(key), category, number, text && !boolean, boolean);
            }
        }

        return BuildAdvancedFieldList(fields, "Latest FM save snapshot and current live career fields");
    }

    public async Task<List<AtlasStaffRecord>> SearchAdvancedStaffAsync(
        string? search,
        IReadOnlyList<AdvancedSearchCriterion> criteria,
        bool matchAll = true,
        int limit = 250,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (criteria.Count > 60) throw new ArgumentOutOfRangeException(nameof(criteria), "Use up to 60 conditions in one advanced staff search.");
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var safeOffset = Math.Clamp(offset, 0, 100000);
        var records = await SearchFmsaveStaffAsync(null).ConfigureAwait(false);
        var maps = new Dictionary<string, Dictionary<string, AdvancedEntityValue>>(StringComparer.OrdinalIgnoreCase);

        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        var raw = db.CreateCommand();
        raw.CommandText = "SELECT StaffUid,RawJson FROM FmsaveStaffDirectory WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns);";
        await using (var reader = await raw.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                var values = new Dictionary<string, AdvancedEntityValue>(StringComparer.OrdinalIgnoreCase);
                var json = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try { using var doc = JsonDocument.Parse(json); AddAdvancedJsonValues(values, doc.RootElement, "staff:raw:"); }
                    catch (JsonException) { /* Keep indexed directory identity fields. */ }
                }
                maps[id] = values;
            }
        }

        var live = db.CreateCommand();
        live.CommandText = """
            SELECT StaffUid,FieldKey,NumericValue,TextValue
            FROM LiveStaffFacts
            WHERE StaffUid IN (
                SELECT StaffUid FROM LiveStaff
                WHERE SourceRunId=(SELECT MAX(RunId) FROM LiveImportRuns WHERE Status='Completed')
            );
            """;
        await using (var reader = await live.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = "LIVE:" + reader.GetInt64(0).ToString(CultureInfo.InvariantCulture);
                if (!maps.TryGetValue(id, out var values)) maps[id] = values = new Dictionary<string, AdvancedEntityValue>(StringComparer.OrdinalIgnoreCase);
                AddAdvancedValue(values, "staff:live:" + reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetDouble(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3));
            }
        }

        var matches = new List<AtlasStaffRecord>();
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!maps.TryGetValue(record.SourceId, out var values)) maps[record.SourceId] = values = new Dictionary<string, AdvancedEntityValue>(StringComparer.OrdinalIgnoreCase);

            AddAdvancedValue(values, "staff:Name", null, record.Name);
            AddAdvancedValue(values, "staff:Age", record.Age, null);
            AddAdvancedValue(values, "staff:Club", null, record.Club);
            AddAdvancedValue(values, "staff:Job", null, record.Job);
            AddAdvancedValue(values, "staff:Nation", null, record.Nation);
            AddAdvancedValue(values, "staff:CurrentAbility", record.CurrentAbility, null);
            AddAdvancedValue(values, "staff:PotentialAbility", record.PotentialAbility, null);
            AddAdvancedValue(values, "staff:IsHumanManager", null, record.IsHumanManager ? "true" : "false");

            if (!MatchesAdvancedEntityText(values, search)) continue;
            if (!MatchesAdvancedEntityCriteria(values, criteria, matchAll)) continue;
            matches.Add(record);
        }

        return matches.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(safeOffset).Take(safeLimit).ToList();
    }

    public async Task<List<AdvancedSearchField>> GetAdvancedClubSearchFieldsAsync()
    {
        var fields = new Dictionary<string, (string Display, string Category, bool Number, bool Text, bool Boolean)>(StringComparer.OrdinalIgnoreCase)
        {
            ["club:Name"] = ("Club name", "Identity", false, true, false),
            ["club:Nation"] = ("Nation", "Identity", false, true, false),
            ["club:PlayerCount"] = ("Linked players", "Squad", true, false, false),
            ["club:DataSource"] = ("Data source", "Identity", false, true, false)
        };

        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);

        var raw = db.CreateCommand();
        raw.CommandText = "SELECT RawJson FROM FmsaveClubDirectory WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns);";
        await using (var reader = await raw.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var json = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(json)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    foreach (var fact in FlattenScalarFacts(doc.RootElement))
                        RegisterAdvancedField(fields, "club:raw:" + fact.Key, fact.Key, fact.NumericValue, fact.TextValue, "Imported fields");
                }
                catch (JsonException) { /* Invalid optional rows are omitted. */ }
            }
        }

        var extra = db.CreateCommand();
        extra.CommandText = """
            SELECT TableName,RawJson FROM FmsaveExtraTables
            WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
              AND TableName IN ('finances','facilities','stadiums','affiliates','managed_clubs','sponsorships','competition_rules');
            """;
        await using (var reader = await extra.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var table = reader.GetString(0);
                var json = reader.GetString(1);
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    foreach (var fact in FlattenScalarFacts(doc.RootElement))
                        RegisterAdvancedField(fields, "club:extra:" + table + "." + fact.Key, table + "." + fact.Key,
                            fact.NumericValue, fact.TextValue, "Club linked data");
                }
                catch (JsonException) { /* Invalid optional rows are omitted. */ }
            }
        }

        var live = db.CreateCommand();
        live.CommandText = """
            SELECT FieldKey,
                   MAX(CASE WHEN NumericValue IS NOT NULL THEN 1 ELSE 0 END),
                   MAX(CASE WHEN TextValue IS NOT NULL THEN 1 ELSE 0 END),
                   MAX(CASE WHEN lower(COALESCE(TextValue,'')) IN ('true','false') THEN 1 ELSE 0 END)
            FROM LiveClubFacts
            GROUP BY FieldKey;
            """;
        await using (var reader = await live.ExecuteReaderAsync(cancellationToken: default).ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var key = reader.GetString(0);
                var number = reader.GetInt32(1) == 1;
                var text = reader.GetInt32(2) == 1;
                var boolean = reader.GetInt32(3) == 1;
                fields["club:live:" + key] = (HumanizeAdvancedField(key), AdvancedCategory(key, "Live fields"), number, text && !boolean, boolean);
            }
        }

        return BuildAdvancedFieldList(fields, "Latest FM save, linked club tables and live career fields");
    }

    public async Task<List<AtlasClubRecord>> SearchAdvancedClubsAsync(
        string? search,
        IReadOnlyList<AdvancedSearchCriterion> criteria,
        bool matchAll = true,
        int limit = 250,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (criteria.Count > 60) throw new ArgumentOutOfRangeException(nameof(criteria), "Use up to 60 conditions in one advanced club search.");
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var safeOffset = Math.Clamp(offset, 0, 100000);

        var all = new List<AtlasClubRecord>();
        for (var skip = 0; skip < 100000; skip += 1000)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await SearchClubDirectoryAsync(null, 1000, skip).ConfigureAwait(false);
            all.AddRange(page);
            if (page.Count < 1000) break;
        }

        var maps = new Dictionary<string, Dictionary<string, AdvancedEntityValue>>(StringComparer.OrdinalIgnoreCase);
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        var raw = db.CreateCommand();
        raw.CommandText = "SELECT ClubUid,RawJson FROM FmsaveClubDirectory WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns);";
        await using (var reader = await raw.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                var values = new Dictionary<string, AdvancedEntityValue>(StringComparer.OrdinalIgnoreCase);
                try { using var doc = JsonDocument.Parse(reader.GetString(1)); AddAdvancedJsonValues(values, doc.RootElement, "club:raw:"); }
                catch (JsonException) { }
                maps[id] = values;
            }
        }

        var live = db.CreateCommand();
        live.CommandText = "SELECT ClubUid,FieldKey,NumericValue,TextValue FROM LiveClubFacts;";
        await using (var reader = await live.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetInt64(0).ToString(CultureInfo.InvariantCulture);
                if (!maps.TryGetValue(id, out var values)) maps[id] = values = new Dictionary<string, AdvancedEntityValue>(StringComparer.OrdinalIgnoreCase);
                AddAdvancedValue(values, "club:live:" + reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetDouble(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3));
            }
        }

        var extras = db.CreateCommand();
        extras.CommandText = """
            SELECT TableName,RawJson FROM FmsaveExtraTables
            WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
              AND TableName IN ('finances','facilities','stadiums','affiliates','managed_clubs','sponsorships','competition_rules');
            """;
        var extraRows = new List<(string Table, string Json)>();
        await using (var reader = await extras.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                extraRows.Add((reader.GetString(0), reader.GetString(1)));

        var matches = new List<AtlasClubRecord>();
        foreach (var club in all)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!maps.TryGetValue(club.SourceId, out var values)) maps[club.SourceId] = values = new Dictionary<string, AdvancedEntityValue>(StringComparer.OrdinalIgnoreCase);
            AddAdvancedValue(values, "club:Name", null, club.Name);
            AddAdvancedValue(values, "club:Nation", null, club.Nation);
            AddAdvancedValue(values, "club:PlayerCount", club.PlayerCount, null);
            AddAdvancedValue(values, "club:DataSource", null, club.DataSource);

            if (club.DataSource.StartsWith("FMSave", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var extraRow in extraRows)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(extraRow.Json);
                        if (!ReferencesClub(doc.RootElement, club.SourceId)) continue;
                        AddAdvancedJsonValues(values, doc.RootElement, "club:extra:" + extraRow.Table + ".");
                    }
                    catch (JsonException) { /* Skip malformed unrelated rows. */ }
                }
            }

            if (!MatchesAdvancedEntityText(values, search)) continue;
            if (!MatchesAdvancedEntityCriteria(values, criteria, matchAll)) continue;
            matches.Add(club);
        }

        return matches.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(safeOffset).Take(safeLimit).ToList();
    }
}
