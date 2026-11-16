using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FMAtlas.Domain.Players;

namespace FMAtlas.Desktop.ViewModels;

public sealed class PlayerProfileViewModel : INotifyPropertyChanged
{
    private string _selectedSection = "Overview";

    public PlayerProfileViewModel(bool isGoalkeeper = false)
    {
        OpenPlayers = [];
        Sections =
        [
            "Overview", "Attributes", "Contract", "Positions", "Preferred Moves",
            "Season Stats", "History", "Personality", "Hidden Attributes", "Relations",
            "Languages", "Unavailability", "Transfer Proposals", "Retire Date",
            "Scout Information"
        ];

        AttributeGroups = new ObservableCollection<PlayerAttributeGroupViewModel>(
            PlayerAttributeCatalog.GetProfileGroups(isGoalkeeper)
                .Select(group => new PlayerAttributeGroupViewModel(
                    group.DisplayName,
                    group.Attributes.Select(attribute => new PlayerAttributeRowViewModel(
                        attribute.Key,
                        attribute.DisplayName,
                        attribute.IsHidden)).ToList())));

        SelectSectionCommand = new ProfileSectionCommand(section =>
        {
            if (section is string name && Sections.Contains(name))
            {
                SelectedSection = name;
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<OpenPlayerTabViewModel> OpenPlayers { get; }

    public IReadOnlyList<string> Sections { get; }

    public ObservableCollection<PlayerAttributeGroupViewModel> AttributeGroups { get; }

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

    public string PlayerTitle => "No player selected";
    public string PlayerSubtitle => "Load a career and open a player from Quick Search or Advanced Search.";
    public string DataState => "AWAITING LIVE PLAYER DATA";
    public bool HasPlayer => false;

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
    public PlayerAttributeRowViewModel(string key, string displayName, bool isHidden)
    {
        Key = key;
        DisplayName = displayName;
        IsHidden = isHidden;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public string ValueText => "—";
    public string EvidenceText => "Awaiting live data";
    public bool IsHidden { get; }
    public bool CanEdit => false;
}

/// <summary>Small synchronous command for selecting a profile sub-tab.</summary>
public sealed class ProfileSectionCommand : ICommand
{
    private readonly Action<object?> _execute;

    public ProfileSectionCommand(Action<object?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute(parameter);
}
