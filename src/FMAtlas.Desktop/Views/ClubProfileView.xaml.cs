using System.Windows.Controls;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Desktop.Views;

public partial class ClubProfileView : UserControl
{
    public event EventHandler<EntitySearchResult>? SquadPlayerOpenRequested;

    public ClubProfileView() => InitializeComponent();

    private void SquadPlayersListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SquadPlayersListBox.SelectedItem is EntitySearchResult result)
            SquadPlayerOpenRequested?.Invoke(this, result);
    }
}
