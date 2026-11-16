using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Clubs;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Entities;
using FMAtlas.Domain.Search;

namespace FMAtlas.Desktop.ViewModels;

public sealed class ClubProfileViewModel : INotifyPropertyChanged
{
    private string _selectedSection = "Information";
    private string _sectionDescription = string.Empty;
    private string _sectionEmptyText = string.Empty;
    private readonly string? _clubLogoImagePath;
    private readonly IAdvancedSearchProvider? _advancedSearchProvider;
    private CancellationTokenSource? _sectionLoadCancellation;
    private int _sectionLoadVersion;

    public ClubProfileViewModel(
        ClubRecord club,
        string? clubLogoImagePath = null,
        IAdvancedSearchProvider? advancedSearchProvider = null)
    {
        Club = club ?? throw new ArgumentNullException(nameof(club));
        _clubLogoImagePath = clubLogoImagePath;
        _advancedSearchProvider = advancedSearchProvider;
        Sections = new ObservableCollection<ProfileSectionTabViewModel>
        {
            new("Information", isSelected: "Information" == "Information"),
            new("Squads", isSelected: "Squads" == "Information"),
            new("Youth Intake", isSelected: "Youth Intake" == "Information"),
            new("Finances", isSelected: "Finances" == "Information"),
            new("Club Vision", isSelected: "Club Vision" == "Information"),
            new("Possible Signings", isSelected: "Possible Signings" == "Information"),
            new("Player Clauses", isSelected: "Player Clauses" == "Information"),
            new("Transfer Center", isSelected: "Transfer Center" == "Information"),
            new("Staff", isSelected: "Staff" == "Information"),
            new("Wage Contributions", isSelected: "Wage Contributions" == "Information"),
            new("Kits & Colours", isSelected: "Kits & Colours" == "Information"),
            new("Affiliations", isSelected: "Affiliations" == "Information"),
            new("Match Preparation", isSelected: "Match Preparation" == "Information"),
            new("Stadium", isSelected: "Stadium" == "Information")
        };
        SelectSectionCommand = new ProfileSectionCommand(section =>
        {
            if (section is string name && Sections.Any(tab => tab.Name == name))
                SelectedSection = name;
        });
        UpdateRows();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private ClubRecord Club { get; }
    public ObservableCollection<ProfileSectionTabViewModel> Sections { get; }
    public ObservableCollection<ProfileValueRowViewModel> SectionRows { get; } = [];
    public ObservableCollection<EntitySearchResult> SquadPlayers { get; } = [];
    public ObservableCollection<EntitySearchResult> ClubStaff { get; } = [];
    public bool IsSquadsSection => SelectedSection == "Squads";
    public bool IsStaffSection => SelectedSection == "Staff";
    public bool IsOtherProfileSection => !IsSquadsSection && !IsStaffSection;
    public ICommand SelectSectionCommand { get; }
    public string SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (_selectedSection == value) return;
            _selectedSection = value;
            foreach (var tab in Sections)
                tab.IsSelected = tab.Name == value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSquadsSection));
            OnPropertyChanged(nameof(IsStaffSection));
            OnPropertyChanged(nameof(IsOtherProfileSection));
            UpdateRows();
        }
    }
    public string SectionDescription { get => _sectionDescription; private set { _sectionDescription = value; OnPropertyChanged(); } }
    public string SectionEmptyText { get => _sectionEmptyText; private set { _sectionEmptyText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSectionEmptyText)); } }
    public bool HasSectionEmptyText => !string.IsNullOrWhiteSpace(SectionEmptyText);
    public string Name => Club.Name?.Value ?? "Unnamed club record";
    public Uri? ClubLogoUri => !string.IsNullOrWhiteSpace(_clubLogoImagePath) && File.Exists(_clubLogoImagePath)
        ? new Uri(_clubLogoImagePath, UriKind.Absolute)
        : null;
    public bool HasClubLogo => ClubLogoUri is not null;
    public bool HasNoClubLogo => !HasClubLogo;
    public string Subtitle => $"{Club.NationName?.Value ?? "Nation not imported"} · {Club.CompetitionName?.Value ?? "Competition not imported"} · {Club.Identity.ProviderKey}";
    public string FoundedText => Format(Club.FoundedYear?.Value);
    public string ReputationText => Format(Club.Reputation?.Value);
    public string StadiumText => Club.StadiumName?.Value ?? "Not imported";
    public string CapacityText => Format(Club.StadiumCapacity?.Value);

    private void UpdateRows()
    {
        _sectionLoadCancellation?.Cancel();
        _sectionLoadCancellation?.Dispose();
        _sectionLoadCancellation = null;
        var sectionLoadVersion = ++_sectionLoadVersion;
        var isAsyncSection = false;

        SectionRows.Clear();
        SquadPlayers.Clear();
        ClubStaff.Clear();
        var rows = new List<ProfileValueRowViewModel>();
        var seenFieldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, string? value, FieldProvenance? evidence = null)
        {
            if (!string.IsNullOrWhiteSpace(value) && value != "Not imported")
                rows.Add(new ProfileValueRowViewModel(name, value, evidence is null ? "Mapped value" : Evidence(evidence)));
        }
        void AddMap<T>(string prefix, IReadOnlyDictionary<string, SourcedValue<T>> map)
        {
            foreach (var pair in map.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                if ((prefix.Length == 0 || pair.Key.Contains(prefix, StringComparison.OrdinalIgnoreCase)) &&
                    seenFieldKeys.Add(pair.Key))
                    Add(Humanise(pair.Key), FormatObject(pair.Value.Value), pair.Value.Provenance);
        }

        switch (SelectedSection)
        {
            case "Information":
                SectionDescription = "Club identity, competition, reputation, attendance and the facilities available in the loaded record.";
                Add("Club", Club.Name?.Value, Club.Name?.Provenance);
                Add("Short name", Club.ShortName?.Value, Club.ShortName?.Provenance);
                Add("Nation", Club.NationName?.Value, Club.NationName?.Provenance);
                Add("Competition", Club.CompetitionName?.Value, Club.CompetitionName?.Provenance);
                Add("Founded", Format(Club.FoundedYear?.Value), Club.FoundedYear?.Provenance);
                Add("Reputation", Format(Club.Reputation?.Value), Club.Reputation?.Provenance);
                Add("Ownership", Club.OwnershipType?.Value, Club.OwnershipType?.Provenance);
                Add("Chairperson", Club.ChairpersonTitle?.Value, Club.ChairpersonTitle?.Provenance);
                Add("Minimum attendance", Format(Club.MinimumAttendance?.Value), Club.MinimumAttendance?.Provenance);
                Add("Average attendance", Format(Club.AverageAttendance?.Value), Club.AverageAttendance?.Provenance);
                Add("Maximum attendance", Format(Club.MaximumAttendance?.Value), Club.MaximumAttendance?.Provenance);
                AddMap("", Club.Facilities);
                break;
            case "Squads":
                SectionDescription = "Players linked to this club by the current team’s numeric club ID in the loaded career.";
                isAsyncSection = true;
                break;
            case "Youth Intake":
                SectionDescription = "Youth intake candidates and attributes when present in the loaded career.";
                AddMap("youth_intake", Club.SearchTextFields);
                AddMap("youth", Club.SearchTextFields);
                AddMap("intake", Club.SearchTextFields);
                AddMap("youth", Club.SearchIntegerFields);
                AddMap("intake", Club.SearchIntegerFields);
                AddMap("youth", Club.SearchDecimalFields);
                AddMap("intake", Club.SearchDateFields);
                AddMap("youth", Club.SearchBooleanFields);
                break;
            case "Finances":
                SectionDescription = "Club balance, budgets, loans, debt and sponsorship values supplied by the save.";
                AddMap("", Club.Finances);
                foreach (var pair in Club.SearchDecimalFields.Where(pair => !Club.Finances.ContainsKey(pair.Key)))
                    Add(Humanise(pair.Key), FormatObject(pair.Value.Value), pair.Value.Provenance);
                break;
            case "Club Vision":
                SectionDescription = "Club culture, board expectations and dated vision objectives.";
                AddMap("vision", Club.SearchTextFields);
                AddMap("culture", Club.SearchTextFields);
                AddMap("objective", Club.SearchTextFields);
                AddMap("vision", Club.SearchDateFields);
                AddMap("objective", Club.SearchDateFields);
                AddMap("vision", Club.SearchIntegerFields);
                AddMap("objective", Club.SearchIntegerFields);
                AddMap("vision", Club.SearchBooleanFields);
                AddMap("objective", Club.SearchBooleanFields);
                break;
            case "Possible Signings":
                SectionDescription = "Players the club is interested in signing.";
                AddMap("possible_signing", Club.SearchTextFields);
                AddMap("signing", Club.SearchTextFields);
                AddMap("signing", Club.SearchIntegerFields);
                AddMap("signing", Club.SearchDecimalFields);
                AddMap("signing", Club.SearchDateFields);
                break;
            case "Player Clauses":
                SectionDescription = "Clauses attached to existing player contracts.";
                AddMap("clause", Club.SearchTextFields);
                AddMap("clause", Club.SearchDecimalFields);
                AddMap("clause", Club.SearchDateFields);
                AddMap("clause", Club.SearchIntegerFields);
                AddMap("clause", Club.SearchBooleanFields);
                break;
            case "Transfer Center":
                SectionDescription = "Existing incoming and outgoing transfer proposals.";
                AddMap("transfer", Club.SearchTextFields);
                AddMap("offer", Club.SearchTextFields);
                AddMap("transfer", Club.SearchDecimalFields);
                AddMap("offer", Club.SearchDecimalFields);
                AddMap("transfer", Club.SearchDateFields);
                AddMap("offer", Club.SearchDateFields);
                AddMap("transfer", Club.SearchIntegerFields);
                AddMap("offer", Club.SearchIntegerFields);
                AddMap("transfer", Club.SearchBooleanFields);
                break;
            case "Staff":
                SectionDescription = "Staff attached to this club by the current contract’s numeric club ID in the loaded career.";
                isAsyncSection = true;
                break;
            case "Wage Contributions":
                SectionDescription = "Wage contributions and shared salary terms where supported.";
                AddMap("wage_contribution", Club.SearchDecimalFields);
                AddMap("wage_contribution", Club.SearchTextFields);
                AddMap("wage_contribution", Club.SearchIntegerFields);
                AddMap("wage_contribution", Club.SearchBooleanFields);
                break;
            case "Kits & Colours":
                SectionDescription = "Home, away and alternate kit colours and kit details.";
                AddMap("kit", Club.SearchTextFields);
                AddMap("colour", Club.SearchTextFields);
                AddMap("kit", Club.SearchIntegerFields);
                AddMap("colour", Club.SearchIntegerFields);
                AddMap("kit", Club.SearchBooleanFields);
                break;
            case "Affiliations":
                SectionDescription = "Parent, feeder and other club affiliations.";
                AddMap("affiliation", Club.SearchTextFields);
                AddMap("affiliation", Club.SearchIntegerFields);
                AddMap("affiliation", Club.SearchBooleanFields);
                AddMap("affiliation", Club.SearchDateFields);
                break;
            case "Match Preparation":
                SectionDescription = "Tactical familiarity and match-preparation information, when exposed by the save.";
                AddMap("match_preparation", Club.SearchTextFields);
                AddMap("tactical_familiarity", Club.SearchIntegerFields);
                AddMap("match_preparation", Club.SearchIntegerFields);
                AddMap("match_preparation", Club.SearchDecimalFields);
                AddMap("match_preparation", Club.SearchBooleanFields);
                break;
            case "Stadium":
                SectionDescription = "Ground name, capacity, seating and attendance details.";
                Add("Stadium", Club.StadiumName?.Value, Club.StadiumName?.Provenance);
                Add("Capacity", Format(Club.StadiumCapacity?.Value), Club.StadiumCapacity?.Provenance);
                AddMap("stadium", Club.SearchIntegerFields);
                AddMap("stadium", Club.SearchTextFields);
                AddMap("stadium", Club.SearchDecimalFields);
                AddMap("stadium", Club.SearchDateFields);
                break;
        }

        foreach (var row in rows) SectionRows.Add(row);
        if (isAsyncSection)
        {
            SectionEmptyText = SelectedSection == "Staff"
                ? "Loading staff linked to this club…"
                : "Loading players linked to this club…";
        }
        else if (rows.Count > 0)
        {
            SectionEmptyText = string.Empty;
        }
        else
        {
            SectionEmptyText = SelectedSection switch
            {
                "Squads" => "No squad/player membership records are currently imported.",
                "Youth Intake" => "No youth intake records have been imported for this club.",
                "Finances" => "No finance record or supported finance values are available in this snapshot.",
                "Club Vision" => "No board vision or club culture records are available.",
                "Possible Signings" => "No possible-signing data was supplied.",
                "Player Clauses" => "No player clause data was supplied.",
                "Transfer Center" => "No transfer proposal records are available.",
                "Staff" => "No linked staff list is available for this club.",
                "Wage Contributions" => "No wage contribution records are available.",
                "Kits & Colours" => "Kit and club colour records are not imported yet.",
                "Affiliations" => "No affiliation records are available.",
                "Match Preparation" => "No match-preparation or tactical familiarity data is available.",
                "Stadium" => "No stadium details were imported for this club.",
                _ => "No values are available for this section in the current snapshot."
            };
        }
        OnPropertyChanged(nameof(SectionRows));
        OnPropertyChanged(nameof(HasSectionEmptyText));

        if (isAsyncSection)
            _ = LoadLinkedPeopleAsync(
                SelectedSection == "Staff" ? SearchEntityKind.Coach : SearchEntityKind.Player,
                sectionLoadVersion);
    }

    private async Task LoadLinkedPeopleAsync(SearchEntityKind entityKind, int sectionLoadVersion)
    {
        var expectedSection = entityKind == SearchEntityKind.Coach ? "Staff" : "Squads";
        if (SelectedSection != expectedSection || sectionLoadVersion != _sectionLoadVersion)
            return;

        if (!uint.TryParse(Club.Identity.SourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var clubId) ||
            clubId == 0)
        {
            SectionEmptyText = "This club record does not have a numeric game ID, so linked records cannot be resolved safely.";
            return;
        }

        if (_advancedSearchProvider is not IIndexedAdvancedSearchFieldProvider indexedProvider)
        {
            SectionEmptyText = "The indexed Advanced Search provider is not available for this workspace.";
            return;
        }

        var cancellation = new CancellationTokenSource();
        _sectionLoadCancellation = cancellation;
        try
        {
            var fields = await indexedProvider.GetIndexedFieldsAsync(
                entityKind,
                cancellation.Token).ConfigureAwait(true);
            if (cancellation.IsCancellationRequested ||
                SelectedSection != expectedSection ||
                sectionLoadVersion != _sectionLoadVersion)
                return;

            var clubIdField = fields.FirstOrDefault(field =>
                string.Equals(field.Key, "club_uid", StringComparison.OrdinalIgnoreCase) &&
                field.ValueKind == SearchFieldValueKind.IntegerRange &&
                field.CanExecute);
            if (clubIdField is null)
            {
                SectionEmptyText = entityKind == SearchEntityKind.Coach
                    ? "The current staff index does not contain a queryable club ID. Reload the live career after the staff contract link is available."
                    : "The current player index does not contain a queryable club ID. Reload the live career after the player-team link is available.";
                return;
            }

            var query = new AdvancedSearchQuery(
                entityKind,
                SearchFilterLogic.All,
                [
                    new SearchFilterCondition(
                        "club_uid",
                        SearchFilterOperator.Range,
                        Minimum: clubId,
                        Maximum: clubId)
                ],
                IndexedFields: fields);

            var linkedRecords = await _advancedSearchProvider.ExecuteAsync(
                query,
                limit: 200,
                cancellationToken: cancellation.Token).ConfigureAwait(true);

            if (cancellation.IsCancellationRequested ||
                SelectedSection != expectedSection ||
                sectionLoadVersion != _sectionLoadVersion)
                return;

            if (entityKind == SearchEntityKind.Coach)
            {
                ClubStaff.Clear();
                foreach (var staff in linkedRecords.OrderBy(result => result.DisplayName, StringComparer.OrdinalIgnoreCase))
                    ClubStaff.Add(staff);
                OnPropertyChanged(nameof(ClubStaff));
            }
            else
            {
                SquadPlayers.Clear();
                foreach (var player in linkedRecords.OrderBy(result => result.DisplayName, StringComparer.OrdinalIgnoreCase))
                    SquadPlayers.Add(player);
                OnPropertyChanged(nameof(SquadPlayers));
            }

            SectionEmptyText = linkedRecords.Count == 0
                ? entityKind == SearchEntityKind.Coach
                    ? "No staff records in the indexed snapshot are linked to this club ID. The active source may not have supplied their contract relationship."
                    : "No players in the indexed snapshot are linked to this club ID. The active source may not have supplied their current-team relationship."
                : string.Empty;
            OnPropertyChanged(nameof(SectionRows));
            OnPropertyChanged(nameof(HasSectionEmptyText));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (SelectedSection == expectedSection && sectionLoadVersion == _sectionLoadVersion)
                SectionEmptyText = $"The linked {expectedSection.ToLowerInvariant()} list could not be loaded: {exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(_sectionLoadCancellation, cancellation))
                _sectionLoadCancellation = null;
            cancellation.Dispose();
        }
    }

    private static string Humanise(string key) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' '));
    private static string Evidence(FieldProvenance p) => $"{p.Source} · {p.EvidenceStatus}";
    private static string Format(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "Not imported";
    private static string Format(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Not imported";
    private static string FormatObject<T>(T value) => value switch
    {
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal amount => amount.ToString("0.##", CultureInfo.InvariantCulture),
        bool flag => flag ? "Yes" : "No",
        null => "Not imported",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "Not imported"
    };
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
