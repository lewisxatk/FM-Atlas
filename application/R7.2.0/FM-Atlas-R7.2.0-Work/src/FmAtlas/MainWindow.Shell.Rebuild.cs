using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private TextBox? _quickSearchBoxV9;

    /// <summary>
    /// V9 application shell. Existing workspaces and service methods are hosted
    /// inside this new shell; no save, search or edit data path is replaced here.
    /// </summary>
    private void BuildShellV9()
    {
        MonolithDesignSystem.SetAppearance(_themeMode);
        RootGrid.Children.Clear();

        var shell = new Grid
        {
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Canvas),
            SnapsToDevicePixels = true
        };
        shell.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(_navCollapsed ? 78 : 236)
        });
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        RootGrid.Children.Add(shell);

        var sidebar = BuildSidebar();
        Grid.SetColumn(sidebar, 0);
        shell.Children.Add(sidebar);

        var main = new Grid { Margin = new Thickness(18, 12, 18, 12) };
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(66) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        Grid.SetColumn(main, 1);
        shell.Children.Add(main);

        // Command/action strip: a persistent quick-search entry point and clear,
        // labelled shortcuts. Glass treatment is restricted to this functional layer.
        var toolbarGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) });
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var brand = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(MonolithDesignSystem.DisplayText("FM ATLAS", 19));
        brand.Children.Add(MonolithDesignSystem.MicroLabel("CAREER INTELLIGENCE", true));
        toolbarGrid.Children.Add(brand);

        var quickSearchBorder = new Border
        {
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Surface),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 0, 8, 0),
            Margin = new Thickness(8, 3, 12, 3),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var quickSearchGrid = new Grid();
        quickSearchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        quickSearchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        quickSearchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var searchGlyph = MonolithDesignSystem.DataText("⌕", 20, MonolithDesignSystem.Solid(MonolithDesignSystem.Signal));
        searchGlyph.VerticalAlignment = VerticalAlignment.Center;
        quickSearchGrid.Children.Add(searchGlyph);
        _quickSearchBoxV9 = new TextBox
        {
            FontFamily = MonolithDesignSystem.BodyFont,
            FontSize = 13,
            Foreground = MonolithDesignSystem.Solid(MonolithDesignSystem.Text),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(5, 8, 5, 8),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Quick Search · players, staff, clubs and IDs · Ctrl+K",
            MinWidth = 160
        };
        _quickSearchBoxV9.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ShowGlobalSearch(_quickSearchBoxV9.Text.Trim());
            }
        };
        System.Windows.Automation.AutomationProperties.SetName(_quickSearchBoxV9, "Quick Search players, staff and clubs");
        Grid.SetColumn(_quickSearchBoxV9, 1);
        quickSearchGrid.Children.Add(_quickSearchBoxV9);
        var shortcut = MonolithDesignSystem.DataText("CTRL K", 9, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        shortcut.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(shortcut, 2);
        quickSearchGrid.Children.Add(shortcut);
        quickSearchBorder.Child = quickSearchGrid;
        Grid.SetColumn(quickSearchBorder, 1);
        toolbarGrid.Children.Add(quickSearchBorder);

        var actions = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        actions.Children.Add(MonolithDesignSystem.ActionButton("ADVANCED", async (_, _) => await NavigateAsync("Advanced Search")));
        actions.Children.Add(ToolbarIcon("\\uE8E5", "Import a local copy of an FM career save. The reader is read-only.", async (_, _) => await ImportCareerSaveAsync()));
        actions.Children.Add(ToolbarIcon("\\uE823", "Activity Centre — current scan/import progress, elapsed time and cancellation.", (_, _) => ShowActivityCenter()));
        actions.Children.Add(ToolbarIcon("\\uE8A7", "Open your saved shortlists.", async (_, _) => await NavigateAsync("Shortlists")));
        _liveLoadButton = ToolbarIcon("\\uE72C", "Connect to or refresh the currently loaded supported FM27 career.", async (_, _) => await LoadLiveCareerAsync());
        _liveLoadButton.IsEnabled = true;
        actions.Children.Add(_liveLoadButton);
        actions.Children.Add(ToolbarIcon("\\uE713", "Current appearance: " + _themeMode + ". Click to cycle appearance modes.", async (_, _) => await CycleThemeAsync()));
        actions.Children.Add(ToolbarIcon("\\uE9D9", "Export diagnostics and recent application logs.", (_, _) => ExportDiagnostics()));

        _cancelImportButton = ToolbarIcon("\\uE711", "Cancel the active live scan or offline save import at a safe checkpoint.", (_, _) =>
        {
            if (_liveScanCancellation is not null)
            {
                _liveScanCancelledByUser = true;
                _liveScanCancellation.Cancel();
            }
            _saveImportCancellation?.Cancel();
            if (_liveLoadStatus is not null)
                _liveLoadStatus.Text = "Cancellation requested · stopping at the next safe checkpoint.";
        });
        _cancelImportButton.IsEnabled = _saveImportCancellation is not null || _liveScanCancellation is not null;
        _cancelImportButton.Visibility = _cancelImportButton.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        actions.Children.Add(_cancelImportButton);
        Grid.SetColumn(actions, 2);
        toolbarGrid.Children.Add(actions);

        var toolbarSurface = new Border
        {
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(232, 42, 45, 51), 0),
                    new GradientStop(Color.FromArgb(218, 20, 25, 31), 1)
                }, new Point(0, 0), new Point(0, 1)),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(9, 4, 9, 4),
            Child = toolbarGrid
        };
        Grid.SetRow(toolbarSurface, 0);
        main.Children.Add(toolbarSurface);

        var pageHeader = new Grid { Margin = new Thickness(4, 0, 4, 10) };
        pageHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pageHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var pageCopy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _pageTitle = MonolithDesignSystem.DisplayText("Dashboard", 25);
        _pageSubTitle = MonolithDesignSystem.BodyText(
            "Career overview · scouting · performance · squad planning",
            11, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        pageCopy.Children.Add(_pageTitle);
        pageCopy.Children.Add(_pageSubTitle);
        pageHeader.Children.Add(pageCopy);
        var context = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        context.Children.Add(MonolithDesignSystem.MicroLabel("FM27 WORKSPACE", true));
        context.Children.Add(MonolithDesignSystem.DataText("SAVE DATA  /  LIVE INDEX  /  ATLAS", 9,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        Grid.SetColumn(context, 1);
        pageHeader.Children.Add(context);
        Grid.SetRow(pageHeader, 1);
        main.Children.Add(pageHeader);

        _pageHost = new ContentControl
        {
            Margin = new Thickness(0, 0, 0, 10),
            ClipToBounds = true,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(_pageHost, 2);
        main.Children.Add(_pageHost);

        // Persistent footer status is compact but keeps source/connection state visible.
        var statusGrid = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var connection = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _connectionDot = new System.Windows.Shapes.Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = MonolithDesignSystem.Solid(MonolithDesignSystem.Caution),
            Margin = new Thickness(2, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        connection.Children.Add(_connectionDot);
        _connectionSummary = MonolithDesignSystem.BodyText(
            "Waiting for a loaded FM27 career or imported save snapshot.",
            10, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        connection.Children.Add(_connectionSummary);
        statusGrid.Children.Add(connection);

        var operation = new Grid { Margin = new Thickness(12, 0, 12, 0) };
        operation.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        operation.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
        _liveLoadStatus = MonolithDesignSystem.DataText("READY · READ-ONLY CAREER DETECTION", 9,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        _liveLoadStatus.VerticalAlignment = VerticalAlignment.Center;
        operation.Children.Add(_liveLoadStatus);
        _liveLoadProgress = new ProgressBar
        {
            Height = 4,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Visibility = Visibility.Collapsed,
            Foreground = MonolithDesignSystem.Solid(MonolithDesignSystem.Signal),
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(_liveLoadProgress, 1);
        operation.Children.Add(_liveLoadProgress);
        Grid.SetColumn(operation, 1);
        statusGrid.Children.Add(operation);

        var footerLabel = MonolithDesignSystem.DataText("FM ATLAS  /  LOCAL DATA", 9,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        footerLabel.VerticalAlignment = VerticalAlignment.Center;
        footerLabel.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(footerLabel, 2);
        statusGrid.Children.Add(footerLabel);

        var footer = new Border
        {
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Surface),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 10, 5),
            Child = statusGrid
        };
        Grid.SetRow(footer, 3);
        main.Children.Add(footer);
    }
}
