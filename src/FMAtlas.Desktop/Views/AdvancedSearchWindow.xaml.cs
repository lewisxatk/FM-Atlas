using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Search;
using FMAtlas.Domain.Entities;
using FMAtlas.Desktop.ViewModels;

namespace FMAtlas.Desktop.Views;

public partial class AdvancedSearchWindow : Window
{
    private readonly AdvancedSearchWindowViewModel _viewModel;

    public event Action<EntitySearchResult>? ResultOpenRequested;

    public AdvancedSearchWindow(IAdvancedSearchProvider searchProvider, Func<bool>? includeWomenPlayers = null)
    {
        InitializeComponent();
        _viewModel = new AdvancedSearchWindowViewModel(searchProvider, includeWomenPlayers);
        DataContext = _viewModel;
        GeneralPanel.Visibility = Visibility.Collapsed;
        DynamicCategoryPanels.Visibility = Visibility.Visible;
        RefreshSelection();
        Loaded += AdvancedSearchWindow_Loaded;
    }

    private async void AdvancedSearchWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshIndexedFieldsAsync();
    }

    private async void EntityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            !Enum.TryParse<SearchEntityKind>(button.Tag?.ToString(), true, out var entity))
            return;

        _viewModel.SelectEntity(entity);
        RefreshSelection();
        await _viewModel.RefreshIndexedFieldsAsync(entity);
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

    private void ResultsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultsListBox.SelectedItem is not AdvancedSearchResultViewModel selected)
            return;

        ResultOpenRequested?.Invoke(selected.Result);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        LogicComboBox.SelectedIndex = 0;
        PositionLogicComboBox.SelectedIndex = 0;
        _viewModel.ClearFilters();
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        var preset = _viewModel.CreatePreset(PresetNameTextBox.Text);
        if (preset is null)
            return;

        try
        {
            var directory = GetPresetDirectory();
            Directory.CreateDirectory(directory);

            var safeName = string.Concat(preset.Name.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "Advanced Search";

            var dialog = new SaveFileDialog
            {
                Title = "Save FM Atlas search criteria",
                InitialDirectory = directory,
                FileName = safeName + ".json",
                DefaultExt = ".json",
                AddExtension = true,
                Filter = "FM Atlas search preset (*.json)|*.json",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                _viewModel.SetStatus("Save preset cancelled.");
                return;
            }

            var json = JsonSerializer.Serialize(preset, PresetJsonOptions());
            File.WriteAllText(dialog.FileName, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            _viewModel.SetStatus($"Saved “{preset.Name}” to {dialog.FileName}.");
            PresetNameTextBox.Text = preset.Name;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _viewModel.SetStatus($"Could not save the search preset: {exception.Message}");
        }
    }

    private async void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = GetPresetDirectory();
            Directory.CreateDirectory(directory);

            var dialog = new OpenFileDialog
            {
                Title = "Load FM Atlas search criteria",
                InitialDirectory = directory,
                DefaultExt = ".json",
                CheckFileExists = true,
                Multiselect = false,
                Filter = "FM Atlas search preset (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
            {
                _viewModel.SetStatus("Load preset cancelled.");
                return;
            }

            var file = new FileInfo(dialog.FileName);
            if (file.Length > 1_000_000)
            {
                _viewModel.SetStatus("That preset is too large to load (maximum 1 MB).");
                return;
            }

            var json = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            var preset = JsonSerializer.Deserialize<AdvancedSearchPreset>(json, PresetJsonOptions());
            if (preset is null || string.IsNullOrWhiteSpace(preset.Name) || preset.Conditions is null)
            {
                _viewModel.SetStatus("The selected file is not a valid FM Atlas search preset.");
                return;
            }

            await _viewModel.RefreshIndexedFieldsAsync(preset.Entity);
            _viewModel.ApplyPreset(preset);
            LogicComboBox.SelectedIndex = preset.Logic == SearchFilterLogic.Any ? 1 : 0;
            PositionLogicComboBox.SelectedIndex = preset.PositionLogic == SearchFilterLogic.Any ? 1 : 0;
            PresetNameTextBox.Text = preset.Name;
            RefreshSelection();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            _viewModel.SetStatus($"Could not load the search preset: {exception.Message}");
        }
    }

    private static string GetPresetDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FM Atlas",
            "Search Presets");

    private static JsonSerializerOptions PresetJsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
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
        SetSelected(GoalkeepingCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.Goalkeeping);
        SetSelected(MentalAttributesCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.MentalAttributes);
        SetSelected(PhysicalHiddenCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.PhysicalHidden);
        SetSelected(PreferredMovesCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.PreferredMoves);
        SetSelected(RelationsCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.Relations);
        SetSelected(IndexedFieldsCategoryButton, _viewModel.SelectedCategory == AdvancedSearchCategoryKind.IndexedFields);
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
