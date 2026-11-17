using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Players;

namespace FMAtlas.Desktop.ViewModels;

/// <summary>
/// Read-only presentation of an imported player snapshot. Missing values remain
/// visible as "Not imported"; every populated attribute retains its evidence label.
/// </summary>
public sealed class PlayerProfileViewModel : INotifyPropertyChanged
{
    private static readonly string[] CanonicalPositionOrder =
        ["GK", "SW", "DL", "DC", "DR", "DM", "ML", "MC", "MR", "AML", "AMC", "AMR", "ST", "WBL", "WBR"];

    // Full-back view, with the tiles at the actual FM positions rather than
    // decorative circles. The displayed value is FM's 1–20 familiarity rating.
    private static readonly (string Position, double Left, double Top)[] PositionPitchLayout =
    [
        ("ST", 81, 0),
        ("AML", 0, 30), ("AMC", 81, 30), ("AMR", 162, 30),
        ("ML", 0, 60), ("MC", 81, 60), ("MR", 162, 60),
        ("WBL", 0, 90), ("DM", 81, 90), ("WBR", 162, 90),
        ("DL", 0, 120), ("DC", 81, 120), ("DR", 162, 120),
        ("SW", 81, 150),
        ("GK", 81, 180)
    ];

    private string _selectedSection = "Information";
    private readonly ObservableCollection<ProfileValueRowViewModel> _selectedSectionRows = [];
    private string _sectionDescription = "Player information and the full supported FM attribute set.";
    private string _sectionEmptyText = string.Empty;
    private string _selectedPositionDetail = "Select a position to inspect its FM familiarity rating.";
    private readonly string? _portraitImagePath;
    private bool _isGoalkeepingAttributeMode;

    public PlayerProfileViewModel(PlayerRecord player, string? portraitImagePath = null)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        _portraitImagePath = portraitImagePath;
        OpenPlayers = [];
        Sections = new ObservableCollection<ProfileSectionTabViewModel>
        {
            new("Information", isSelected: "Information" == "Information"),
            new("Contract", isSelected: "Contract" == "Information"),
            new("Stats", isSelected: "Stats" == "Information"),
            new("Unavailability", isSelected: "Unavailability" == "Information"),
            new("Transfer Proposals", isSelected: "Transfer Proposals" == "Information"),
            new("Retire Date", isSelected: "Retire Date" == "Information"),
            new("Positions", isSelected: "Positions" == "Information"),
            new("Preferred Moves", isSelected: "Preferred Moves" == "Information"),
            new("Relationships", isSelected: "Relationships" == "Information"),
            new("Languages", isSelected: "Languages" == "Information"),
            new("Scout Information", isSelected: "Scout Information" == "Information"),
            new("Staff", isSelected: "Staff" == "Information"),
            new("Clone Center", isSelected: "Clone Center" == "Information"),
            new("Personality / Hidden", isSelected: "Personality / Hidden" == "Information"),
            new("History", isSelected: "History" == "Information")
        };

        // The public FMLibreEditor profile catalogue uses GK >= 15 as the
        // default presentation. Users can still switch to either attribute group.
        _isGoalkeepingAttributeMode = player.PositionRatings.TryGetValue("gk", out var goalkeeperRating) &&
                                      goalkeeperRating.Value >= 15;
        AttributeGroups = [];
        RebuildAttributeGroups();

        SeasonStatistics = new ObservableCollection<ProfileValueRowViewModel>(
            player.SeasonStatistics
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new ProfileValueRowViewModel(
                    Humanise(pair.Key),
                    Format(pair.Value.Value),
                    Evidence(pair.Value.Provenance))));

        PositionRatings = new ObservableCollection<ProfileValueRowViewModel>(
            player.PositionRatings
                .OrderBy(pair =>
                {
                    var index = Array.IndexOf(CanonicalPositionOrder, pair.Key.ToUpperInvariant());
                    return index < 0 ? int.MaxValue : index;
                })
                .Select(pair => new ProfileValueRowViewModel(
                    pair.Key.ToUpperInvariant(),
                    Format(pair.Value.Value),
                    Evidence(pair.Value.Provenance))));

        PreferredMoves = new ObservableCollection<string>(
            player.PreferredMoves.Select(move => move.Value));

        var ratingsByPosition = player.PositionRatings.ToDictionary(
            pair => pair.Key.ToUpperInvariant(), pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        PositionPitchTiles = new ObservableCollection<PlayerPositionTileViewModel>(
            PositionPitchLayout.Select(position =>
            {
                ratingsByPosition.TryGetValue(position.Position, out var value);
                return new PlayerPositionTileViewModel(position.Position, value, position.Left, position.Top);
            }));

        SelectPositionCommand = new ProfileSectionCommand(parameter =>
        {
            if (parameter is not PlayerPositionTileViewModel selected)
                return;

            foreach (var tile in PositionPitchTiles)
                tile.IsSelected = ReferenceEquals(tile, selected);

            SelectedPositionDetail = selected.Rating is int rating
                ? $"{selected.Position}: {rating}/20 · {selected.TierText}. {selected.EvidenceText}"
                : $"{selected.Position}: no position-familiarity rating was imported for this player.";
        });

        SelectAttributeModeCommand = new ProfileSectionCommand(mode =>
        {
            if (mode is string name && name is "Technical" or "Goalkeeping")
                IsGoalkeepingAttributeMode = name == "Goalkeeping";
        });
        SelectSectionCommand = new ProfileSectionCommand(section =>
        {
            if (section is string name && Sections.Any(tab => tab.Name == name))
                SelectedSection = name;
        });
        var primaryPosition = player.PrimaryPosition?.Value?.ToUpperInvariant();
        var primaryTile = PositionPitchTiles.FirstOrDefault(tile => tile.Position == primaryPosition);
        if (primaryTile is not null)
            SelectPositionCommand.Execute(primaryTile);

        UpdateSelectedSectionRows();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private PlayerRecord Player { get; }

    public ObservableCollection<OpenPlayerTabViewModel> OpenPlayers { get; }
    public ObservableCollection<ProfileSectionTabViewModel> Sections { get; }
    public ObservableCollection<PlayerAttributeGroupViewModel> AttributeGroups { get; }
    public ObservableCollection<ProfileValueRowViewModel> SeasonStatistics { get; }
    public ObservableCollection<ProfileValueRowViewModel> PositionRatings { get; }
    public ObservableCollection<PlayerPositionTileViewModel> PositionPitchTiles { get; }
    public ObservableCollection<string> PreferredMoves { get; }
    public ICommand SelectSectionCommand { get; }
    public ICommand SelectAttributeModeCommand { get; }
    public ICommand SelectPositionCommand { get; }
    public string SelectedPositionDetail
    {
        get => _selectedPositionDetail;
        private set
        {
            if (_selectedPositionDetail == value)
                return;
            _selectedPositionDetail = value;
            OnPropertyChanged();
        }
    }
    public bool IsGoalkeepingAttributeMode
    {
        get => _isGoalkeepingAttributeMode;
        private set
        {
            if (_isGoalkeepingAttributeMode == value)
                return;
            _isGoalkeepingAttributeMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTechnicalAttributeMode));
            RebuildAttributeGroups();
        }
    }
    public bool IsTechnicalAttributeMode => !IsGoalkeepingAttributeMode;


    public string SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (_selectedSection == value)
                return;
            _selectedSection = value;
            foreach (var tab in Sections)
                tab.IsSelected = tab.Name == value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInformationSection));
            UpdateSelectedSectionRows();
        }
    }

    public ObservableCollection<ProfileValueRowViewModel> SelectedSectionRows => _selectedSectionRows;
    public string SectionDescription
    {
        get => _sectionDescription;
        private set { _sectionDescription = value; OnPropertyChanged(); }
    }
    public string SectionEmptyText
    {
        get => _sectionEmptyText;
        private set
        {
            _sectionEmptyText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSectionEmptyText));
        }
    }
    public bool HasSectionEmptyText => !string.IsNullOrWhiteSpace(SectionEmptyText);
    public bool IsInformationSection => SelectedSection == "Information";

    public string PlayerTitle => Player.FullName?.Value ?? "Unnamed player record";
    public Uri? PortraitUri => !string.IsNullOrWhiteSpace(_portraitImagePath) && File.Exists(_portraitImagePath)
        ? new Uri(_portraitImagePath, UriKind.Absolute)
        : null;
    public bool HasPortrait => PortraitUri is not null;
    public bool HasNoPortrait => !HasPortrait;
    public string PlayerSubtitle => $"{Player.Identity.ProviderKey} · Source ID {Player.Identity.SourceId}";
    public string AgeInfoText => $"Age  ·  {Format(Player.Age?.Value)}";
    public string DateOfBirthInfoText => $"Date of birth  ·  {Format(Player.DateOfBirth?.Value)}";
    public string NationalityInfoText => $"Nationality  ·  {Format(Player.Nationality?.Value)}";
    public string SecondNationalityInfoText => $"Second nationality  ·  {Format(Player.SecondNationality?.Value)}";
    public string CityOfBirthInfoText => $"Born  ·  {Format(Player.CityOfBirth?.Value)}";
    public string AskingPriceInfoText => $"Asking price  ·  {Format(Player.SearchIntegerFields.GetValueOrDefault("asking_price")?.Value)}";
    public string ClubInfoText => $"Club / Team  ·  {Format(Player.ClubName?.Value ?? Player.TeamName?.Value)}";
    public string LeagueInfoText => $"League  ·  {Format(Player.LeagueName?.Value)}";
    public string PositionInfoText => $"Position  ·  {Format(Player.PrimaryPosition?.Value)}";
    public string PreferredFootInfoText => $"Preferred foot  ·  {Format(Player.PreferredFoot?.Value)}";
    public string HeightWeightInfoText =>
        "Height / Weight  ·  " +
        Format(Player.HeightCentimetres?.Value, " cm") + " / " +
        Format(Player.WeightKilograms?.Value, " kg");
    public string UniqueIdInfoText => $"Unique ID  ·  {Format(Player.GameUid?.Value ?? Player.Identity.SourceId)}";
    public string WageInfoText => $"Wage  ·  {Format(Player.Wage?.Value)}";
    public string ExpiryInfoText => $"Expiry  ·  {Format(Player.ContractExpires?.Value)}";
    public string TransferValueInfoText => $"Transfer value  ·  {Format(Player.TransferValue?.Value)}";
    public string CurrentAbilityText => Format(Player.CurrentAbility?.Value);
    public string PotentialAbilityText => Format(Player.PotentialAbility?.Value);
    public string CurrentAbilityEvidenceText => Player.CurrentAbility is { } current
        ? Evidence(current.Provenance)
        : "No CA value imported.";
    public string PotentialAbilityEvidenceText => Player.PotentialAbility is { } potential
        ? (potential.Value < 0
            ? $"FM stores negative PA values as random-potential range markers (raw value {potential.Value}); this is not a fixed 1–200 PA. {Evidence(potential.Provenance)}"
            : Evidence(potential.Provenance))
        : "No PA value imported.";
    public string NewgenStatusText => Player.IsNewgen?.Value switch
    {
        true => "GENERATED / NEWGEN",
        false => "ORIGINAL DATABASE",
        null => "UNKNOWN / UNCLASSIFIED"
    };
    public string NewgenEvidenceText => Player.IsNewgen is { } newgen
        ? Evidence(newgen.Provenance)
        : "The imported source did not provide a verified generation classification.";
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

    private void RebuildAttributeGroups()
    {
        AttributeGroups.Clear();
        foreach (var group in PlayerAttributeCatalog.GetProfileGroups(IsGoalkeepingAttributeMode))
        {
            AttributeGroups.Add(new PlayerAttributeGroupViewModel(
                group.DisplayName,
                group.Attributes.Select(attribute => new PlayerAttributeRowViewModel(
                    attribute.Key,
                    attribute.DisplayName,
                    attribute.IsHidden,
                    ResolveAttribute(Player, group.Kind, attribute.Key))).ToArray()));
        }
        OnPropertyChanged(nameof(AttributeGroups));
        OnPropertyChanged(nameof(DataEvidenceSummary));
    }

    private void UpdateSelectedSectionRows()
    {
        _selectedSectionRows.Clear();
        var rows = new List<ProfileValueRowViewModel>();
        void Add(string name, string? value, FieldProvenance? provenance = null)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "Not imported")
                return;
            rows.Add(new ProfileValueRowViewModel(name, value, provenance is null ? "Mapped value" : Evidence(provenance)));
        }
        void AddMap<T>(string prefix, IReadOnlyDictionary<string, SourcedValue<T>> map)
        {
            foreach (var pair in map.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                if (pair.Key.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                    Add(Humanise(pair.Key), FormatObject(pair.Value.Value), pair.Value.Provenance);
        }

        switch (SelectedSection)
        {
            case "Information":
                SectionDescription = "Player identity, CA/PA, personal information and the complete supported FM attribute catalogue.";
                break;
            case "Contract":
                SectionDescription = "Current contract, value and wage fields supplied by the loaded save.";
                Add("Wage", Format(Player.Wage?.Value), Player.Wage?.Provenance);
                Add("Contract expiry", Format(Player.ContractExpires?.Value), Player.ContractExpires?.Provenance);
                Add("Transfer value", Format(Player.TransferValue?.Value), Player.TransferValue?.Provenance);
                AddMap("contract", Player.SearchDateFields);
                AddMap("contract", Player.SearchIntegerFields);
                AddMap("contract", Player.SearchTextFields);
                AddMap("contract", Player.SearchDecimalFields);
                break;
            case "Stats":
                SectionDescription = "Season statistics from the loaded save. This area is read-only until verified stat records are available.";
                rows.AddRange(SeasonStatistics);
                break;
            case "Unavailability":
                SectionDescription = "Bans and availability restrictions from the loaded career.";
                AddMap("ban", Player.SearchTextFields);
                AddMap("unavail", Player.SearchTextFields);
                AddMap("ban", Player.SearchDateFields);
                AddMap("unavail", Player.SearchDateFields);
                AddMap("ban", Player.SearchIntegerFields);
                break;
            case "Transfer Proposals":
                SectionDescription = "Existing offers and transfer proposals involving this player.";
                AddMap("offer", Player.SearchTextFields);
                AddMap("transfer_proposal", Player.SearchTextFields);
                AddMap("offer", Player.SearchDecimalFields);
                break;
            case "Retire Date":
                SectionDescription = "Scheduled retirement information, if the loaded career exposes it.";
                AddMap("retire", Player.SearchDateFields);
                AddMap("retire", Player.SearchTextFields);
                break;
            case "Positions":
                SectionDescription = "FM position familiarity, in the canonical position catalogue order.";
                rows.AddRange(PositionRatings);
                break;
            case "Preferred Moves":
                SectionDescription = "Preferred moves / player traits recorded in the loaded save.";
                foreach (var move in Player.PreferredMoves)
                    Add("Preferred move", move.Value, move.Provenance);
                break;
            case "Relationships":
                SectionDescription = "Second nationality, training history and supported person, club and nation relationships.";
                Add("Second nationality", Player.SecondNationality?.Value, Player.SecondNationality?.Provenance);
                AddMap("", Player.SearchTextFields.Where(x => x.Key.Contains("trained", StringComparison.OrdinalIgnoreCase) ||
                    x.Key.Contains("relation", StringComparison.OrdinalIgnoreCase) ||
                    x.Key.Contains("work_permit", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(x => x.Key, x => x.Value));
                break;
            case "Languages":
                SectionDescription = "Spoken languages and proficiency, when the loaded save exposes them.";
                AddMap("language", Player.SearchTextFields);
                AddMap("language", Player.SearchIntegerFields);
                break;
            case "Scout Information":
                SectionDescription = "Scout, positional and role ratings only when the source supplies the actual values.";
                AddMap("scout", Player.SearchIntegerFields);
                AddMap("role", Player.SearchIntegerFields);
                AddMap("scout", Player.SearchDecimalFields);
                break;
            case "Staff":
                SectionDescription = "Staff-side information for player-coaches. This player snapshot does not currently contain a linked staff record.";
                AddMap("staff", Player.SearchTextFields);
                AddMap("staff", Player.SearchIntegerFields);
                break;
            case "Clone Center":
                SectionDescription = "Clone Center is conditional and will only copy supported fields to existing target players after safe editing is verified.";
                break;
            case "Personality / Hidden":
                SectionDescription = "Hidden and personality fields are shown separately from ordinary visible attributes.";
                foreach (var pair in Player.HiddenAttributes.OrderBy(x => x.Key))
                    Add(Humanise(pair.Key), FormatObject(pair.Value.Value), pair.Value.Provenance);
                foreach (var pair in Player.Personality.OrderBy(x => x.Key))
                    Add(Humanise(pair.Key), FormatObject(pair.Value.Value), pair.Value.Provenance);
                break;
            case "History":
                SectionDescription = "Career and transfer history from the loaded save.";
                AddMap("history", Player.SearchTextFields);
                AddMap("history", Player.SearchDateFields);
                break;
        }

        foreach (var row in rows)
            _selectedSectionRows.Add(row);

        SectionEmptyText = rows.Count == 0
            ? SelectedSection switch
            {
                "Information" => string.Empty,
                "Stats" => "No season statistics were imported. This section will populate when the live provider exposes season/competition stat records.",
                "Unavailability" => "No ban or unavailability records were supplied by the loaded source.",
                "Transfer Proposals" => "No transfer proposal data is available in this live snapshot.",
                "Retire Date" => "No scheduled retirement date was supplied by the loaded source.",
                "Positions" => "No position familiarity values were imported.",
                "Preferred Moves" => "No preferred moves were imported.",
                "Relationships" => "No relationship records were supplied beyond the supported identity/training fields.",
                "Languages" => "Language information is not currently mapped for this player.",
                "Scout Information" => "No verified scout or role-rating calculations are available in this snapshot.",
                "Staff" => "No linked staff record is available for this player.",
                "Clone Center" => "This conditional operation is not implemented. No player creation or copy action is offered.",
                "Personality / Hidden" => "No hidden or personality values were imported.",
                "History" => "Career history records are not currently mapped by the live provider.",
                _ => "No data is available for this section."
            }
            : string.Empty;

        OnPropertyChanged(nameof(SelectedSectionRows));
    }

    private static string FormatObject<T>(T value) => value switch
    {
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal amount => amount.ToString("0.##", CultureInfo.InvariantCulture),
        bool flag => flag ? "Yes" : "No",
        null => "Not imported",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "Not imported"
    };

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

public sealed class PlayerPositionTileViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public PlayerPositionTileViewModel(
        string position,
        SourcedValue<int>? rating,
        double left,
        double top)
    {
        Position = position;
        Rating = rating?.Value;
        EvidenceText = rating is null
            ? "No position familiarity was imported."
            : $"{rating.Provenance.Source} · {rating.Provenance.EvidenceStatus}";
        Left = left;
        Top = top;
        (TierText, Fill) = Rating switch
        {
            >= 18 and <= 20 => ("Natural", CreateBrush(0x2E, 0x7C, 0x57)),
            >= 15 and <= 17 => ("Accomplished", CreateBrush(0x3C, 0x74, 0x86)),
            >= 12 and <= 14 => ("Competent", CreateBrush(0x60, 0x75, 0x89)),
            >= 9 and <= 11 => ("Unconvincing", CreateBrush(0x91, 0x7A, 0x42)),
            >= 5 and <= 8 => ("Awkward", CreateBrush(0x9D, 0x63, 0x3A)),
            >= 1 and <= 4 => ("Makeshift", CreateBrush(0x91, 0x47, 0x49)),
            _ => ("Not rated", CreateBrush(0x4B, 0x52, 0x5A))
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Position { get; }
    public int? Rating { get; }
    public string RatingText => Rating?.ToString(CultureInfo.InvariantCulture) ?? "—";
    public string TierText { get; }
    public string EvidenceText { get; }
    public double Left { get; }
    public double Top { get; }
    public Brush Fill { get; }
    public Brush TextBrush { get; } = Brushes.WhiteSmoke;
    public string ToolTip => Rating is int rating
        ? $"{Position}: {rating}/20 · {TierText}. {EvidenceText}"
        : $"{Position}: not rated. {EvidenceText}";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderThickness)));
        }
    }

    public Brush BorderBrush => IsSelected
        ? CreateBrush(0xF1, 0xA0, 0x64)
        : CreateBrush(0x83, 0x8B, 0x94);
    public Thickness BorderThickness => IsSelected ? new Thickness(2) : new Thickness(1);

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
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
    public string ValueText => _value is null ? "—" : _value.Value.ToString(CultureInfo.InvariantCulture);
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
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
