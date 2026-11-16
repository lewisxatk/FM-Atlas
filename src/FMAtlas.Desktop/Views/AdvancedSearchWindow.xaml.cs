using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FMAtlas.Domain.Search;
using FMAtlas.Desktop.ViewModels;

namespace FMAtlas.Desktop.Views;

public partial class AdvancedSearchWindow : Window
{
    private readonly AdvancedSearchWindowViewModel _viewModel;

    public AdvancedSearchWindow()
    {
        InitializeComponent();
        _viewModel = new AdvancedSearchWindowViewModel();
        DataContext = _viewModel;
        RefreshSelection();
    }

    private void EntityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            !Enum.TryParse<SearchEntityKind>(button.Tag?.ToString(), true, out var entity))
            return;

        _viewModel.SelectEntity(entity);
        RefreshSelection();
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            !Enum.TryParse<AdvancedSearchCategoryKind>(button.Tag?.ToString(), true, out var category))
            return;

        _viewModel.SelectCategory(category);
        RefreshSelection();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshSelection()
    {
        var showStaticGeneral = _viewModel.ShowStaticGeneral;
        GeneralPanel.Visibility = showStaticGeneral ? Visibility.Visible : Visibility.Collapsed;
        DynamicCategoryPanels.Visibility = showStaticGeneral ? Visibility.Collapsed : Visibility.Visible;

        SetSelected(PlayerEntityButton, _viewModel.SelectedEntity == SearchEntityKind.Player);
        SetSelected(CoachEntityButton, _viewModel.SelectedEntity == SearchEntityKind.Coach);
        SetSelected(ClubEntityButton, _viewModel.SelectedEntity == SearchEntityKind.Club);

        SetSelected(GeneralCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.General);
        SetSelected(ContractCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.Contract);
        SetSelected(TechnicalGoalkeepingCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.TechnicalGoalkeeping);
        SetSelected(MentalAttributesCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.MentalAttributes);
        SetSelected(PhysicalHiddenCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.PhysicalHidden);
        SetSelected(PreferredMovesCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.PreferredMoves);
        SetSelected(RelationsCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.Relations);
    }

    private void SetSelected(Button button, bool selected)
    {
        button.Background = selected
            ? new SolidColorBrush(Color.FromRgb(0x21, 0x17, 0x11))
            : Brushes.Transparent;
        button.BorderBrush = selected
            ? new SolidColorBrush(Color.FromRgb(0x4A, 0x2A, 0x1D))
            : Brushes.Transparent;
        button.Foreground = selected
            ? (Brush)FindResource("Atlas.Text")
            : (Brush)FindResource("Atlas.Muted");
    }
}
