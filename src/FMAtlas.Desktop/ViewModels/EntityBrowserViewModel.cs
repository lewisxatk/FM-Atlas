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
    private string _statusText = "Enter at least two characters to search this loaded dataset.";
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
        if (term.Length < 2)
        {
            StatusText = "Enter at least two characters to search.";
            Results.Clear();
            return;
        }

        IsBusy = true;
        StatusText = "Searching the active FM Atlas dataset…";
        try
        {
            var found = await _provider.QuickSearchAsync(term, [_kind], 200, includeWomenPlayers: _includeWomen());
            Results.Clear();
            foreach (var result in found)
                Results.Add(result);
            StatusText = found.Count == 0 ? $"No {Title.ToLowerInvariant()} matched “{term}”." : $"{found.Count:N0} {Title.ToLowerInvariant()} found. Double-click a record to open its profile.";
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
