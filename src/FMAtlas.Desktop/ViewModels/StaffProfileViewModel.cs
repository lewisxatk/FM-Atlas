using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FMAtlas.Domain.Data;
using FMAtlas.Domain.Staff;

namespace FMAtlas.Desktop.ViewModels;

public sealed class StaffProfileViewModel : INotifyPropertyChanged
{
    private string _selectedSection = "Information";
    private string _sectionDescription = string.Empty;
    private string _sectionEmptyText = string.Empty;

    public StaffProfileViewModel(StaffRecord staff)
    {
        Staff = staff ?? throw new ArgumentNullException(nameof(staff));
        Sections =
        [
            "Information", "Contract", "Career Plans", "Promises", "Swap Staff",
            "Training Rating", "Formations / Tendencies", "Relationships",
            "Knowledge", "Retire Date", "Languages"
        ];
        SelectSectionCommand = new ProfileSectionCommand(section =>
        {
            if (section is string name && Sections.Contains(name))
                SelectedSection = name;
        });
        UpdateRows();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private StaffRecord Staff { get; }
    public IReadOnlyList<string> Sections { get; }
    public ObservableCollection<ProfileValueRowViewModel> SectionRows { get; } = [];
    public ICommand SelectSectionCommand { get; }
    public string SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (_selectedSection == value) return;
            _selectedSection = value;
            OnPropertyChanged();
            UpdateRows();
        }
    }
    public string SectionDescription { get => _sectionDescription; private set { _sectionDescription = value; OnPropertyChanged(); } }
    public string SectionEmptyText { get => _sectionEmptyText; private set { _sectionEmptyText = value; OnPropertyChanged(); } }
    public bool HasSectionEmptyText => !string.IsNullOrWhiteSpace(SectionEmptyText);
    public string Name => Staff.FullName?.Value ?? "Unnamed staff record";
    public string Subtitle => $"{Staff.PrimaryRole?.Value ?? "Staff"} · {Staff.ClubName?.Value ?? "Club not imported"} · {Staff.Identity.ProviderKey}";
    public string AgeText => Format(Staff.Age?.Value);
    public string GenderText => Format(Staff.Gender?.Value);
    public string NationalityText => Format(Staff.Nationality?.Value);
    public string CaText => Format(Staff.CurrentAbility?.Value);
    public string PaText => Format(Staff.PotentialAbility?.Value);

    private void UpdateRows()
    {
        SectionRows.Clear();
        var rows = new List<ProfileValueRowViewModel>();
        void Add(string name, string? value, FieldProvenance? evidence = null)
        {
            if (!string.IsNullOrWhiteSpace(value) && value != "Not imported")
                rows.Add(new ProfileValueRowViewModel(name, value, evidence is null ? "Mapped value" : Evidence(evidence)));
        }
        void AddMap<T>(string prefix, IReadOnlyDictionary<string, SourcedValue<T>> map)
        {
            foreach (var pair in map.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                if (prefix.Length == 0 || pair.Key.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                    Add(Humanise(pair.Key), FormatObject(pair.Value.Value), pair.Value.Provenance);
        }

        switch (SelectedSection)
        {
            case "Information":
                SectionDescription = "Staff identity, role, reputation, CA/PA and supported coaching attributes.";
                Add("Name", Staff.FullName?.Value, Staff.FullName?.Provenance);
                Add("Unique ID", Staff.GameUid?.Value ?? Staff.Identity.SourceId);
                Add("Age", Format(Staff.Age?.Value), Staff.Age?.Provenance);
                Add("Date of birth", Format(Staff.DateOfBirth?.Value), Staff.DateOfBirth?.Provenance);
                Add("Gender", Staff.Gender?.Value, Staff.Gender?.Provenance);
                Add("Nationality", Staff.Nationality?.Value, Staff.Nationality?.Provenance);
                Add("Club", Staff.ClubName?.Value, Staff.ClubName?.Provenance);
                Add("Primary role", Staff.PrimaryRole?.Value, Staff.PrimaryRole?.Provenance);
                Add("Current Ability", Format(Staff.CurrentAbility?.Value), Staff.CurrentAbility?.Provenance);
                Add("Potential Ability", Format(Staff.PotentialAbility?.Value), Staff.PotentialAbility?.Provenance);
                AddMap("", Staff.Attributes);
                break;
            case "Contract":
                SectionDescription = "Club / national-team contract details recorded in this snapshot.";
                Add("Wage", Format(Staff.Wage?.Value), Staff.Wage?.Provenance);
                Add("Contract expiry", Format(Staff.ContractExpires?.Value), Staff.ContractExpires?.Provenance);
                AddMap("contract", Staff.SearchDateFields);
                AddMap("contract", Staff.SearchIntegerFields);
                AddMap("contract", Staff.SearchTextFields);
                break;
            case "Career Plans":
                SectionDescription = "Future job and career-plan information when supplied by the save.";
                AddMap("career", Staff.SearchTextFields);
                AddMap("future_job", Staff.SearchTextFields);
                AddMap("outlook", Staff.SearchTextFields);
                break;
            case "Promises":
                SectionDescription = "Outstanding staff promises and their current status.";
                AddMap("promise", Staff.SearchTextFields);
                AddMap("promise", Staff.SearchDateFields);
                break;
            case "Swap Staff":
                SectionDescription = "Staff swap operations require validated live editing and are not enabled.";
                break;
            case "Training Rating":
                SectionDescription = "Training ratings from the loaded career, not inferred from attributes.";
                AddMap("training", Staff.SearchIntegerFields);
                AddMap("training", Staff.SearchDecimalFields);
                break;
            case "Formations / Tendencies":
                SectionDescription = "Tactical tendencies and formation preferences supplied by the game.";
                AddMap("formation", Staff.SearchTextFields);
                AddMap("tendency", Staff.SearchTextFields);
                break;
            case "Relationships":
                SectionDescription = "Supported links between this staff member and other people, clubs or nations.";
                AddMap("relation", Staff.SearchTextFields);
                break;
            case "Knowledge":
                SectionDescription = "Knowledge and specialist coaching/medical ratings.";
                AddMap("", Staff.Knowledge);
                AddMap("knowledge", Staff.SearchIntegerFields);
                break;
            case "Retire Date":
                SectionDescription = "Scheduled retirement information when available.";
                AddMap("retire", Staff.SearchDateFields);
                AddMap("retire", Staff.SearchTextFields);
                break;
            case "Languages":
                SectionDescription = "Spoken languages and proficiency.";
                AddMap("language", Staff.SearchTextFields);
                AddMap("language", Staff.SearchIntegerFields);
                break;
        }

        foreach (var row in rows) SectionRows.Add(row);
        SectionEmptyText = rows.Count > 0 ? string.Empty : SelectedSection switch
        {
            "Promises" => "No promise records were imported.",
            "Swap Staff" => "Swap Staff is not enabled. No changes have been made.",
            "Training Rating" => "No separate training-rating record was imported.",
            "Formations / Tendencies" => "No formation or tendency data was imported.",
            "Relationships" => "No staff relationship records were imported.",
            "Career Plans" => "No career-plan values were imported.",
            "Retire Date" => "No retirement date was imported.",
            "Languages" => "Language records are not yet mapped for this staff record.",
            _ => "No values are available for this section in the current snapshot."
        };
        OnPropertyChanged(nameof(SectionRows));
        OnPropertyChanged(nameof(HasSectionEmptyText));
    }

    private static string Humanise(string key) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' '));
    private static string Evidence(FieldProvenance p) => $"{p.Source} · {p.EvidenceStatus}";
    private static string Format(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "Not imported";
    private static string Format(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Not imported";
    private static string Format(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "Not imported";
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
