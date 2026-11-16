using System.Windows.Controls;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Desktop.Views;

public partial class EntityBrowserView : UserControl
{
    public event EventHandler<EntitySearchResult>? ResultOpenRequested;

    public EntityBrowserView()
    {
        InitializeComponent();
    }

    private void ResultsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultsListBox.SelectedItem is EntitySearchResult result)
            ResultOpenRequested?.Invoke(this, result);
    }
}
