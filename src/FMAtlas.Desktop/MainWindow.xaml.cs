using System.Windows;
using FMAtlas.Application.Abstractions;
using FMAtlas.Desktop.ViewModels;
using FMAtlas.Desktop.Views;

namespace FMAtlas.Desktop;

public partial class MainWindow : Window
{
    private readonly IAdvancedSearchProvider _advancedSearchProvider;
    private AdvancedSearchWindow? _advancedSearchWindow;

    public MainWindow(MainWindowViewModel viewModel, IAdvancedSearchProvider advancedSearchProvider)
    {
        InitializeComponent();
        DataContext = viewModel;
        _advancedSearchProvider = advancedSearchProvider ?? throw new ArgumentNullException(nameof(advancedSearchProvider));
    }

    private void AdvancedSearchButton_Click(object sender, RoutedEventArgs e)
    {
        // Advanced Search is a workspace, not a modal dialog: keep the main Atlas
        // window usable while the user builds and reviews a filter query.
        if (_advancedSearchWindow is { } openWindow)
        {
            if (openWindow.WindowState == WindowState.Minimized)
                openWindow.WindowState = WindowState.Normal;

            openWindow.Activate();
            return;
        }

        var searchWindow = new AdvancedSearchWindow(_advancedSearchProvider)
        {
            Owner = this
        };

        _advancedSearchWindow = searchWindow;
        searchWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_advancedSearchWindow, searchWindow))
                _advancedSearchWindow = null;
        };

        searchWindow.Show();
        searchWindow.Activate();
    }
}
