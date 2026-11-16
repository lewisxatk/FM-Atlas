using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Clubs;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Players;
using FMAtlas.Domain.Staff;

namespace FMAtlas.Application.Projection;

/// <summary>
/// Deterministically projects typed records into searchable scalar field values.
/// Source provenance is retained per field. Missing values are omitted rather
/// than materialised as defaults.
/// </summary>
public sealed class EntityRecordProjector : IEntityRecordProjector
{
    public EntityRecordProjection Project(IEntityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return record switch
        {
            PlayerRecord player => ProjectPlayer(player),
            StaffRecord staff => ProjectStaff(staff),
            ClubRecord club => ProjectClub(club),
            _ => throw new NotSupportedException(
                $"No field projection is defined for record type {record.GetType().FullName}.")
        };
    }

    private static EntityRecordProjection ProjectPlayer(PlayerRecord player)
    {
        RequireKind(player.Identity, EntityKind.Player);
        var fields = new List<EntityFieldProjection>();

        Add(fields, "full_name", player.FullName);
        Add(fields, "date_of_birth", player.DateOfBirth);
        Add(fields, "age", player.Age);
        Add(fields, "nationality", player.Nationality);
        Add(fields, "second_nationality", player.SecondNationality);
        Add(fields, "gender", player.Gender);
        Add(fields, "continent", player.Continent);
        Add(fields, "region", player.Region);
        Add(fields, "city_of_birth", player.CityOfBirth);
        Add(fields, "ethnicity", player.Ethnicity);
        Add(fields, "club_name", player.ClubName);
        Add(fields, "team_name", player.TeamName);
        Add(fields, "league_name", player.LeagueName);
        Add(fields, "primary_position", player.PrimaryPosition);
        Add(fields, "preferred_foot", player.PreferredFoot);
        Add(fields, "height_cm", player.HeightCentimetres);
        Add(fields, "weight_kg", player.WeightKilograms);
        Add(fields, "current_ability", player.CurrentAbility);
        Add(fields, "potential_ability", player.PotentialAbility);
        Add(fields, "international_caps", player.InternationalCaps);
        Add(fields, "international_goals", player.InternationalGoals);
        Add(fields, "eu_nationality", player.IsEuNationality);
        Add(fields, "wonderkid", player.IsWonderkid);
        Add(fields, "contract_expires", player.ContractExpires);
        Add(fields, "wage", player.Wage);
        Add(fields, "transfer_value", player.TransferValue);
        Add(fields, "is_newgen", player.IsNewgen);

        AddIntMap(fields, "attribute_", player.Attributes);
        AddIntMap(fields, "attribute_", player.HiddenAttributes);
        AddIntMap(fields, "personality_", player.Personality);
        AddIntMap(fields, string.Empty, player.FootRatings);
        AddIntMap(fields, "position_", player.PositionRatings);
        AddDecimalMap(fields, "stat_", player.SeasonStatistics);
        AddStringMap(fields, "extra_", player.ExtendedFields);
        AddIntMap(fields, string.Empty, player.SearchIntegerFields);
        AddDecimalMap(fields, string.Empty, player.SearchDecimalFields);
        AddBooleanMap(fields, string.Empty, player.SearchBooleanFields);
        AddDateMap(fields, string.Empty, player.SearchDateFields);
        AddStringMap(fields, string.Empty, player.SearchTextFields);

        for (var index = 0; index < player.PreferredMoves.Count; index++)
            Add(fields, $"preferred_move_{index:D4}", player.PreferredMoves[index]);

        return new EntityRecordProjection(
            player.Identity,
            player.FullName?.Value,
            fields.AsReadOnly());
    }

    private static EntityRecordProjection ProjectStaff(StaffRecord staff)
    {
        RequireKind(staff.Identity, EntityKind.Staff);
        var fields = new List<EntityFieldProjection>();

        Add(fields, "full_name", staff.FullName);
        Add(fields, "date_of_birth", staff.DateOfBirth);
        Add(fields, "age", staff.Age);
        Add(fields, "nationality", staff.Nationality);
        Add(fields, "continent", staff.Continent);
        Add(fields, "region", staff.Region);
        Add(fields, "city_of_birth", staff.CityOfBirth);
        Add(fields, "club_name", staff.ClubName);
        Add(fields, "primary_role", staff.PrimaryRole);
        Add(fields, "current_ability", staff.CurrentAbility);
        Add(fields, "potential_ability", staff.PotentialAbility);
        Add(fields, "contract_expires", staff.ContractExpires);
        Add(fields, "wage", staff.Wage);
        Add(fields, "is_newgen", staff.IsNewgen);

        AddIntMap(fields, "attribute_", staff.Attributes);
        AddIntMap(fields, "knowledge_", staff.Knowledge);
        AddIntMap(fields, "personality_", staff.Personality);
        AddIntMap(fields, "role_preference_", staff.RolePreferences);
        AddStringMap(fields, "extra_", staff.ExtendedFields);
        AddIntMap(fields, string.Empty, staff.SearchIntegerFields);
        AddDecimalMap(fields, string.Empty, staff.SearchDecimalFields);
        AddBooleanMap(fields, string.Empty, staff.SearchBooleanFields);
        AddDateMap(fields, string.Empty, staff.SearchDateFields);
        AddStringMap(fields, string.Empty, staff.SearchTextFields);

        return new EntityRecordProjection(
            staff.Identity,
            staff.FullName?.Value,
            fields.AsReadOnly());
    }

    private static EntityRecordProjection ProjectClub(ClubRecord club)
    {
        RequireKind(club.Identity, EntityKind.Club);
        var fields = new List<EntityFieldProjection>();

        Add(fields, "name", club.Name);
        Add(fields, "short_name", club.ShortName);
        Add(fields, "nation_name", club.NationName);
        Add(fields, "competition_name", club.CompetitionName);
        Add(fields, "stadium_name", club.StadiumName);
        Add(fields, "stadium_capacity", club.StadiumCapacity);
        Add(fields, "founded_year", club.FoundedYear);
        Add(fields, "reputation", club.Reputation);
        Add(fields, "minimum_attendance", club.MinimumAttendance);
        Add(fields, "average_attendance", club.AverageAttendance);
        Add(fields, "maximum_attendance", club.MaximumAttendance);
        Add(fields, "ownership_type", club.OwnershipType);
        Add(fields, "chairperson_title", club.ChairpersonTitle);

        AddDecimalMap(fields, "finance_", club.Finances);
        AddIntMap(fields, "facility_", club.Facilities);
        AddStringMap(fields, "extra_", club.ExtendedFields);
        AddIntMap(fields, string.Empty, club.SearchIntegerFields);
        AddDecimalMap(fields, string.Empty, club.SearchDecimalFields);
        AddBooleanMap(fields, string.Empty, club.SearchBooleanFields);
        AddDateMap(fields, string.Empty, club.SearchDateFields);
        AddStringMap(fields, string.Empty, club.SearchTextFields);

        return new EntityRecordProjection(
            club.Identity,
            club.Name?.Value,
            fields.AsReadOnly());
    }

    private static void RequireKind(EntityIdentity identity, EntityKind expected)
    {
        if (identity.Kind != expected)
            throw new ArgumentException(
                $"Record type requires identity kind {expected}, but received {identity.Kind}.",
                nameof(identity));

        if (!identity.IsUsable)
            throw new ArgumentException("A valid provider-scoped identity is required.", nameof(identity));
    }

    private static void Add(
        ICollection<EntityFieldProjection> fields,
        string key,
        SourcedValue<string>? value)
    {
        if (value is not null)
            fields.Add(new EntityFieldProjection(
                key, EntityFieldValueKind.Text, value.Value, null, null, null, null, value.Provenance));
    }

    private static void Add(
        ICollection<EntityFieldProjection> fields,
        string key,
        SourcedValue<int>? value)
    {
        if (value is not null)
            fields.Add(new EntityFieldProjection(
                key, EntityFieldValueKind.Integer, null, value.Value, null, null, null, value.Provenance));
    }

    private static void Add(
        ICollection<EntityFieldProjection> fields,
        string key,
        SourcedValue<decimal>? value)
    {
        if (value is not null)
            fields.Add(new EntityFieldProjection(
                key, EntityFieldValueKind.Decimal, null, null, value.Value, null, null, value.Provenance));
    }

    private static void Add(
        ICollection<EntityFieldProjection> fields,
        string key,
        SourcedValue<DateOnly>? value)
    {
        if (value is not null)
            fields.Add(new EntityFieldProjection(
                key, EntityFieldValueKind.Date, null, null, null, value.Value, null, value.Provenance));
    }

    private static void Add(
        ICollection<EntityFieldProjection> fields,
        string key,
        SourcedValue<bool>? value)
    {
        if (value is not null)
            fields.Add(new EntityFieldProjection(
                key, EntityFieldValueKind.Boolean, null, null, null, null, value.Value, value.Provenance));
    }

    private static void AddBooleanMap(
        ICollection<EntityFieldProjection> fields,
        string prefix,
        IReadOnlyDictionary<string, SourcedValue<bool>> values)
    {
        foreach (var (key, value) in values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            Add(fields, ComposeKey(prefix, key), value);
    }

    private static void AddDateMap(
        ICollection<EntityFieldProjection> fields,
        string prefix,
        IReadOnlyDictionary<string, SourcedValue<DateOnly>> values)
    {
        foreach (var (key, value) in values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            Add(fields, ComposeKey(prefix, key), value);
    }

    private static void AddIntMap(
        ICollection<EntityFieldProjection> fields,
        string prefix,
        IReadOnlyDictionary<string, SourcedValue<int>> values)
    {
        foreach (var (key, value) in values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            Add(fields, ComposeKey(prefix, key), value);
    }

    private static void AddDecimalMap(
        ICollection<EntityFieldProjection> fields,
        string prefix,
        IReadOnlyDictionary<string, SourcedValue<decimal>> values)
    {
        foreach (var (key, value) in values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            Add(fields, ComposeKey(prefix, key), value);
    }

    private static string ComposeKey(string prefix, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("A projection field key is required.", nameof(key));

        var normalisedPrefix = (prefix ?? string.Empty).Trim().ToLowerInvariant();
        var normalisedKey = key.Trim()
            .Replace(' ', '_')
            .Replace('-', '_')
            .ToLowerInvariant();

        return normalisedPrefix.Length > 0 &&
               normalisedKey.StartsWith(normalisedPrefix, StringComparison.OrdinalIgnoreCase)
            ? normalisedKey
            : normalisedPrefix + normalisedKey;
    }

    private static void AddStringMap(
        ICollection<EntityFieldProjection> fields,
        string prefix,
        IReadOnlyDictionary<string, SourcedValue<string>> values)
    {
        foreach (var (key, value) in values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            Add(fields, ComposeKey(prefix, key), value);
    }
}
