using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Players;

namespace FMAtlas.Desktop.ViewModels;

/// <summary>
/// Read-only presentation of an imported player snapshot. Missing values remain
/// visible as "Not imported"; every populated attribute retains its evidence label.
/// </summary>
public sealed class PlayerProfileViewModel : INotifyPropertyChanged
{
    private string _selectedSection = "Overview";

    public PlayerProfileViewModel(PlayerRecord player)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        OpenPlayers = [];
        Sections =
        [
            "Overview", "Attributes", "Contract", "Positions", "Preferred Moves",
            "Season Stats", "History", "Personality", "Hidden Attributes", "Relations",
            "Staff", "Clone Center", "Role Attributes", "Languages", "Unavailability",
            "Transfer Proposals", "Retire Date", "Scout Information"
        ];

        var isGoalkeeper = player.PositionRatings.TryGetValue("gk", out var goalkeeperRating) &&
                           goalkeeperRating.Value > 0;
        AttributeGroups = new ObservableCollection<PlayerAttributeGroupViewModel>(
            PlayerAttributeCatalog.GetProfileGroups(isGoalkeeper)
                .Select(group => new PlayerAttributeGroupViewModel(
                    group.DisplayName,
                    group.Attributes.Select(attribute => new PlayerAttributeRowViewModel(
                        attribute.Key,
                        attribute.DisplayName,
                        attribute.IsHidden,
                        ResolveAttribute(player, group.Kind, attribute.Key))).ToArray())));

        SeasonStatistics = new ObservableCollection<ProfileValueRowViewModel>(
            player.SeasonStatistics
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new ProfileValueRowViewModel(
                    Humanise(pair.Key),
                    Format(pair.Value.Value),
                    Evidence(pair.Value.Provenance))));

        PositionRatings = new ObservableCollection<ProfileValueRowViewModel>(
            player.PositionRatings
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new ProfileValueRowViewModel(
                    pair.Key.ToUpperInvariant(),
                    Format(pair.Value.Value),
                    Evidence(pair.Value.Provenance))));

        PreferredMoves = new ObservableCollection<string>(
            player.PreferredMoves.Select(move => move.Value));

        SelectSectionCommand = new ProfileSectionCommand(section =>
        {
            if (section is string name && Sections.Contains(name))
                SelectedSection = name;
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private PlayerRecord Player { get; }

    public ObservableCollection<OpenPlayerTabViewModel> OpenPlayers { get; }
    public IReadOnlyList<string> Sections { get; }
    public ObservableCollection<PlayerAttributeGroupViewModel> AttributeGroups { get; }
    public ObservableCollection<ProfileValueRowViewModel> SeasonStatistics { get; }
    public ObservableCollection<ProfileValueRowViewModel> PositionRatings { get; }
    public ObservableCollection<string> PreferredMoves { get; }
    public ICommand SelectSectionCommand { get; }

    public string SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (_selectedSection == value)
                return;
            _selectedSection = value;
            OnPropertyChanged();
        }
    }

    public string PlayerTitle => Player.FullName?.Value ?? "Unnamed player record";
    public string PlayerSubtitle => $"{Player.Identity.ProviderKey} · Source ID {Player.Identity.SourceId}";
    public string AgeInfoText => $"Age  ·  {Format(Player.Age?.Value)}";
    public string DateOfBirthInfoText => $"Date of birth  ·  {Format(Player.DateOfBirth?.Value)}";
    public string NationalityInfoText => $"Nationality  ·  {Format(Player.Nationality?.Value)}";
    public string ClubInfoText => $"Club / Team  ·  {Format(Player.ClubName?.Value ?? Player.TeamName?.Value)}";
    public string LeagueInfoText => $"League  ·  {Format(Player.LeagueName?.Value)}";
    public string PositionInfoText => $"Position  ·  {Format(Player.PrimaryPosition?.Value)}";
    public string PreferredFootInfoText => $"Preferred foot  ·  {Format(Player.PreferredFoot?.Value)}";
    public string HeightWeightInfoText =>
        $"Height / Weight  ·  {Format(Player.HeightCentimetres?.Value, " cm")} / {Format(Player.WeightKilograms?.Value, " kg")}";
    public string UniqueIdInfoText => $"Unique ID  ·  {Format(Player.GameUid?.Value ?? Player.Identity.SourceId)}";
    public string WageInfoText => $"Wage  ·  {Format(Player.Wage?.Value)}";
    public string ExpiryInfoText => $"Expiry  ·  {Format(Player.ContractExpires?.Value)}";
    public string TransferValueInfoText => $"Transfer value  ·  {Format(Player.TransferValue?.Value)}";
    public string CurrentAbilityText => Format(Player.CurrentAbility?.Value);
    public string PotentialAbilityText => Format(Player.PotentialAbility?.Value);
    public string NewgenStatusText => Player.IsNewgen?.Value switch
    {
        true => "GENERATED / NEWGEN",
        false => "ORIGINAL DATABASE",
        null => "UNKNOWN / UNCLASSIFIED"
    };
    public string NewgenEvidenceText => Player.IsNewgen is null
        ? "The imported source did not provide a verified generation classification."
        : Evidence(Player.IsNewgen.Provenance);
    public string SeasonStatisticsEmptyText => SeasonStatistics.Count == 0
        ? "No season statistics have been imported for this player."
        : string.Empty;
    public string PositionRatingsEmptyText => PositionRatings.Count == 0
        ? "No position ratings have been imported for this player."
        : string.Empty;
    public string PreferredMovesText => PreferredMoves.Count == 0
        ? "No preferred moves have been imported for this player."
        : string.Join(Environment.NewLine, PreferredMoves);
    public string DataState => "LOCAL SNAPSHOT · READ-ONLY";
    public string DataEvidenceSummary
    {
        get
        {
            var values = AttributeGroups.SelectMany(group => group.Attributes).Where(row => row.HasValue).ToArray();
            var verified = values.Count(row => row.IsReadVerified);
            return $"{verified} of {values.Length} displayed attribute values carry read-verified evidence. Live editing is not enabled in this profile.";
        }
    }
    public bool HasPlayer => true;

    private static SourcedValue<int>? ResolveAttribute(
        PlayerRecord player,
        PlayerAttributeGroupKind group,
        string key) =>
        group switch
        {
            PlayerAttributeGroupKind.Hidden => player.HiddenAttributes.GetValueOrDefault(key),
            PlayerAttributeGroupKind.Personality => player.Personality.GetValueOrDefault(key),
            PlayerAttributeGroupKind.Feet => player.FootRatings.GetValueOrDefault(key),
            _ => player.Attributes.GetValueOrDefault(key) ?? player.SearchIntegerFields.GetValueOrDefault(key)
        };

    private static string Humanise(string key) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' '));

    private static string Evidence(FieldProvenance provenance) =>
        $"{provenance.Source} · {provenance.EvidenceStatus}";

    private static string Format(int? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "Not imported";

    private static string Format(decimal? value, string suffix = "") =>
        value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) + suffix : "Not imported";

    private static string Format(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not imported" : value;

    private static string Format(DateOnly? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "Not imported";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class OpenPlayerTabViewModel
{
    public OpenPlayerTabViewModel(uint uniqueId, string displayName)
    {
        UniqueId = uniqueId;
        DisplayName = displayName;
    }

    public uint UniqueId { get; }
    public string DisplayName { get; }
    public bool HasPendingChanges { get; init; }
}

public sealed class PlayerAttributeGroupViewModel
{
    public PlayerAttributeGroupViewModel(string displayName, IReadOnlyList<PlayerAttributeRowViewModel> attributes)
    {
        DisplayName = displayName;
        Attributes = attributes;
    }

    public string DisplayName { get; }
    public IReadOnlyList<PlayerAttributeRowViewModel> Attributes { get; }
}

public sealed class PlayerAttributeRowViewModel
{
    private readonly SourcedValue<int>? _value;

    public PlayerAttributeRowViewModel(
        string key,
        string displayName,
        bool isHidden,
        SourcedValue<int>? value)
    {
        Key = key;
        DisplayName = displayName;
        IsHidden = isHidden;
        _value = value;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public bool IsHidden { get; }
    public string ValueText => _value?.Value.ToString(CultureInfo.InvariantCulture) ?? "—";
    public string EvidenceText => _value is null ? "No value imported" : $"{_value.Provenance.Source} · {_value.Provenance.EvidenceStatus}";
    public bool HasValue => _value is not null;
    public bool IsReadVerified => _value?.IsReadVerified == true;
    public bool CanEdit => false;
}

public sealed record ProfileValueRowViewModel(string DisplayName, string ValueText, string EvidenceText);

/// <summary>Small synchronous command for selecting a profile sub-tab.</summary>
public sealed class ProfileSectionCommand : ICommand
{
    private readonly Action<object?> _execute;
    public ProfileSectionCommand(Action<object?> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
