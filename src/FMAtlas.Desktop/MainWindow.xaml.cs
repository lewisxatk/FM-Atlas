using System.Windows;
using FMAtlas.Application.Abstractions;
using FMAtlas.Desktop.ViewModels;
using FMAtlas.Desktop.Views;

namespace FMAtlas.Desktop;

public partial class MainWindow : Window
{
    private readonly IAdvancedSearchProvider _advancedSearchProvider;

    public MainWindow(MainWindowViewModel viewModel, IAdvancedSearchProvider advancedSearchProvider)
    {
        InitializeComponent();
        DataContext = viewModel;
        _advancedSearchProvider = advancedSearchProvider ?? throw new ArgumentNullException(nameof(advancedSearchProvider));
    }

    private void AdvancedSearchButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AdvancedSearchWindow(_advancedSearchProvider)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }
}
