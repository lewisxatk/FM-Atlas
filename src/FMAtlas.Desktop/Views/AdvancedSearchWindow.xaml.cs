using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Search;
using FMAtlas.Desktop.ViewModels;

namespace FMAtlas.Desktop.Views;

public partial class AdvancedSearchWindow : Window
{
    private readonly AdvancedSearchWindowViewModel _viewModel;

    public AdvancedSearchWindow(IAdvancedSearchProvider searchProvider)
    {
        InitializeComponent();
        _viewModel = new AdvancedSearchWindowViewModel(searchProvider);
        DataContext = _viewModel;
        GeneralPanel.Visibility = Visibility.Collapsed;
        DynamicCategoryPanels.Visibility = Visibility.Visible;
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

    private void LogicComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.SelectedLogic = LogicComboBox.SelectedIndex == 1
            ? SearchFilterLogic.Any
            : SearchFilterLogic.All;
    }

    private void PositionLogicComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.SelectedPositionLogic = PositionLogicComboBox.SelectedIndex == 1
            ? SearchFilterLogic.Any
            : SearchFilterLogic.All;
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RunSearchAsync();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        LogicComboBox.SelectedIndex = 0;
        PositionLogicComboBox.SelectedIndex = 0;
        _viewModel.ClearFilters();
    }

    private void RefreshSelection()
    {
        SetSelected(PlayerEntityButton, _viewModel.SelectedEntity == SearchEntityKind.Player);
        SetSelected(CoachEntityButton, _viewModel.SelectedEntity == SearchEntityKind.Coach);
        SetSelected(ClubEntityButton, _viewModel.SelectedEntity == SearchEntityKind.Club);
        PositionLogicComboBox.IsEnabled = _viewModel.SelectedEntity == SearchEntityKind.Player;

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
