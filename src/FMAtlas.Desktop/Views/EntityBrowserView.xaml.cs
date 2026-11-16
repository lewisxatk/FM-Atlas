using System.Windows.Controls;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Desktop.Views;

public partial class EntityBrowserView : UserControl
{
    public event EventHandler<EntitySearchResult>? ResultOpenRequested;

    public EntityBrowserView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is EntityBrowserViewModel viewModel &&
                viewModel.Results.Count == 0 &&
                string.IsNullOrWhiteSpace(viewModel.SearchText))
                viewModel.SearchCommand.Execute(null);
        };
    }

    private void ResultsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultsListBox.SelectedItem is EntitySearchResult result)
            ResultOpenRequested?.Invoke(this, result);
    }
}
