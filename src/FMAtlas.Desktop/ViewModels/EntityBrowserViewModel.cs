using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Entities;
using FMAtlas.Desktop.Commands;

namespace FMAtlas.Desktop.ViewModels;

public sealed class EntityBrowserViewModel : INotifyPropertyChanged
{
    private readonly IEntitySearchProvider _provider;
    private readonly EntityKind _kind;
    private readonly Func<bool> _includeWomen;
    private string _searchText = string.Empty;
    private string _statusText = "Loading indexed records…";
    private bool _isBusy;

    public EntityBrowserViewModel(IEntitySearchProvider provider, EntityKind kind, Func<bool> includeWomen)
    {
        _provider = provider;
        _kind = kind;
        _includeWomen = includeWomen;
        Title = kind switch { EntityKind.Player => "Players", EntityKind.Staff => "Staff & Coaches", EntityKind.Club => "Clubs", _ => kind.ToString() };
        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Title { get; }
    public string SearchText { get => _searchText; set { if (_searchText != value) { _searchText = value; OnPropertyChanged(); } } }
    public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
    public ObservableCollection<EntitySearchResult> Results { get; } = [];
    public ICommand SearchCommand { get; }

    private async Task SearchAsync()
    {
        var term = SearchText.Trim();
        if (term.Length == 1)
        {
            StatusText = "Enter at least two characters, or choose Browse All.";
            Results.Clear();
            return;
        }

        IsBusy = true;
        StatusText = term.Length == 0
            ? $"Loading all indexed {Title.ToLowerInvariant()}…"
            : "Searching the active FM Atlas dataset…";
        try
        {
            var found = await _provider.QuickSearchAsync(term, [_kind], 200, includeWomenPlayers: _includeWomen());
            Results.Clear();
            foreach (var result in found)
                Results.Add(result);
            StatusText = found.Count == 0
                ? term.Length == 0
                    ? $"No {Title.ToLowerInvariant()} are indexed yet. Load an FM27 career or import supported data, then choose Browse All."
                    : $"No {Title.ToLowerInvariant()} matched “{term}”."
                : term.Length == 0
                    ? $"Showing {found.Count:N0} indexed {Title.ToLowerInvariant()}. Double-click a record to open its profile."
                    : $"{found.Count:N0} {Title.ToLowerInvariant()} found. Double-click a record to open its profile.";
        }
        catch (Exception exception)
        {
            Results.Clear();
            StatusText = $"Search failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
