using System.Windows.Controls;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Desktop.Views;

public partial class ClubProfileView : UserControl
{
    public event EventHandler<EntitySearchResult>? SquadPlayerOpenRequested;
    public event EventHandler<EntitySearchResult>? ClubStaffOpenRequested;

    public ClubProfileView() => InitializeComponent();

    private void SquadPlayersListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SquadPlayersListBox.SelectedItem is EntitySearchResult result)
            SquadPlayerOpenRequested?.Invoke(this, result);
    }

    private void ClubStaffListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ClubStaffListBox.SelectedItem is EntitySearchResult result)
            ClubStaffOpenRequested?.Invoke(this, result);
    }
}
