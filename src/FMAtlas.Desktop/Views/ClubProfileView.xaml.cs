using System.Windows.Controls;
using FMAtlas.Desktop.ViewModels;
using FMAtlas.Domain.Entities;

namespace FMAtlas.Desktop.Views;

public partial class ClubProfileView : UserControl
{
    public event EventHandler<EntitySearchResult>? SquadPlayerOpenRequested;
    public event EventHandler<EntitySearchResult>? ClubStaffOpenRequested;

    public ClubProfileView() => InitializeComponent();

    private void SquadPlayersListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SquadPlayersListBox.SelectedItem is ClubProfileLinkViewModel link)
            SquadPlayerOpenRequested?.Invoke(this, link.Result);
    }

    private void ClubStaffListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ClubStaffListBox.SelectedItem is ClubProfileLinkViewModel link)
            ClubStaffOpenRequested?.Invoke(this, link.Result);
    }
}
