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

    /// <summary>
    /// Rebuilt navigation rail using the shared Monolith tokens. The route names
    /// intentionally match the existing navigation dispatcher so no workspace
    /// or data service is lost during the shell migration.
    /// </summary>
    private WrapPanel? _workspaceContextNavV9;

    private static string GetPrimaryNavigationRouteV9(string page) => page switch
    {
        "Dashboard" => "Dashboard",
        "Players" or "Advanced Search" or "Recruitment" or "Value Finder" or "Attribute Search"
            or "Stats Search" or "Scouting Matrix" or "Shortlists" or "Clubs" or "Staff" => "Players",
        "Squad Architect" or "Tactical Lab" or "Role Lab" or "Transfers" or "Analytics" => "Squad Architect",
        "Live Editor" or "Mass Edit" or "Edit History" => "Live Editor",
        _ => "Settings"
    };

    private UIElement BuildSidebarV9()
    {
        var rail = new Border
        {
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Surface),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(_navCollapsed ? 9 : 13, 14, _navCollapsed ? 9 : 13, 12)
        };
        var dock = new DockPanel();
        rail.Child = dock;

        var brand = new Grid { Margin = new Thickness(1, 2, 1, 22) };
        brand.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        brand.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var brandRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var mark = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(0),
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SignalSoft),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Signal),
            BorderThickness = new Thickness(1),
            Child = MonolithDesignSystem.DisplayText("FM", 13, MonolithDesignSystem.Solid(MonolithDesignSystem.Signal)),
            VerticalAlignment = VerticalAlignment.Center
        };
        ((TextBlock)mark.Child).HorizontalAlignment = HorizontalAlignment.Center;
        ((TextBlock)mark.Child).VerticalAlignment = VerticalAlignment.Center;
        brandRow.Children.Add(mark);
        if (!_navCollapsed)
        {
            var brandCopy = new StackPanel { Margin = new Thickness(10, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            brandCopy.Children.Add(MonolithDesignSystem.DisplayText("ATLAS", 16));
            brandCopy.Children.Add(MonolithDesignSystem.MicroLabel("FOOTBALL INTELLIGENCE", true));
            brandRow.Children.Add(brandCopy);
        }
        Grid.SetColumn(brandRow, 0);
        brand.Children.Add(brandRow);

        var collapse = new Button
        {
            Content = _navCollapsed ? "›" : "‹",
            Width = 29,
            Height = 32,
            Padding = new Thickness(0),
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceRaised),
            Foreground = MonolithDesignSystem.Solid(MonolithDesignSystem.Muted),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Focusable = true,
            ToolTip = _navCollapsed ? "Expand navigation" : "Collapse navigation",
            Template = RoundedButtonTemplate(0)
        };
        collapse.Click += async (_, _) => await ToggleNavigationAsync();
        System.Windows.Automation.AutomationProperties.SetName(collapse, collapse.ToolTip?.ToString() ?? "Toggle navigation");
        Grid.SetColumn(collapse, 1);
        brand.Children.Add(collapse);
        DockPanel.SetDock(brand, Dock.Top);
        dock.Children.Add(brand);

        _navPanel = new StackPanel();
        var navScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _navPanel
        };
        dock.Children.Add(navScroll);

        void Item(string glyph, string label, string route)
        {
            var selected = string.Equals(GetPrimaryNavigationRouteV9(_activePage), route, StringComparison.OrdinalIgnoreCase);
            var row = new Grid { MinHeight = 43 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var activeBar = new Border
            {
                Tag = "nav-active",
                Background = selected ? MonolithDesignSystem.Solid(MonolithDesignSystem.Signal) : Brushes.Transparent,
                Margin = new Thickness(0, 8, 0, 8)
            };
            row.Children.Add(activeBar);
            var icon = MonolithDesignSystem.DataText(glyph, 14,
                MonolithDesignSystem.Solid(selected ? MonolithDesignSystem.Signal : MonolithDesignSystem.Muted));
            icon.Tag = "nav-icon";
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(icon, 1);
            row.Children.Add(icon);
            if (!_navCollapsed)
            {
                var caption = MonolithDesignSystem.BodyText(label, 12,
                    MonolithDesignSystem.Solid(selected ? MonolithDesignSystem.Text : MonolithDesignSystem.Muted));
                caption.Tag = "nav-caption";
                caption.VerticalAlignment = VerticalAlignment.Center;
                caption.Margin = new Thickness(5, 0, 3, 0);
                Grid.SetColumn(caption, 2);
                row.Children.Add(caption);
            }
            var button = new Button
            {
                Tag = route,
                Content = row,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(4, 2, 5, 2),
                Margin = new Thickness(0, 3, 0, 3),
                Background = selected ? MonolithDesignSystem.Solid(MonolithDesignSystem.SignalSoft) : Brushes.Transparent,
                BorderBrush = selected ? MonolithDesignSystem.Solid(MonolithDesignSystem.Border) : Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Focusable = true,
                MinHeight = 45,
                ToolTip = _navCollapsed ? label + " · " + PageDescription(route) : PageDescription(route),
                Template = RoundedButtonTemplate(0)
            };
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            System.Windows.Automation.AutomationProperties.SetHelpText(button, PageDescription(route));
            button.MouseEnter += (_, _) =>
            {
                if (!string.Equals(GetPrimaryNavigationRouteV9(_activePage), route, StringComparison.OrdinalIgnoreCase))
                    button.Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceHover);
            };
            button.MouseLeave += (_, _) =>
            {
                if (!string.Equals(GetPrimaryNavigationRouteV9(_activePage), route, StringComparison.OrdinalIgnoreCase))
                    button.Background = Brushes.Transparent;
            };
            button.Click += async (_, _) => await NavigateAsync(route);
            _navPanel.Children.Add(button);
        }

        Item("⌂", "HOME", "Dashboard");
        Item("♟", "SCOUTING", "Players");
        Item("▥", "SQUAD & TACTICS", "Squad Architect");
        Item("✎", "EDITOR", "Live Editor");
        Item("⌘", "DATA & SETTINGS", "Settings");

        var connection = new Border
        {
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceRaised),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(_navCollapsed ? 5 : 10),
            Margin = new Thickness(0, 12, 0, 0),
            Child = _navCollapsed
                ? MonolithDesignSystem.DisplayText("●", 13, MonolithDesignSystem.Solid(MonolithDesignSystem.Caution))
                : new StackPanel()
        };
        if (!_navCollapsed && connection.Child is StackPanel connectionCopy)
        {
            connectionCopy.Children.Add(MonolithDesignSystem.MicroLabel("CAREER CONNECTION"));
            connectionCopy.Children.Add(MonolithDesignSystem.BodyText("Read-only detection", 11));
            connectionCopy.Children.Add(MonolithDesignSystem.BodyText(
                "Live editing unlocks only after field-level write checks pass.",
                10, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        }
        DockPanel.SetDock(connection, Dock.Bottom);
        dock.Children.Add(connection);
        return rail;
    }

    private void RefreshWorkspaceContextNavV9(string page)
    {
        if (_workspaceContextNavV9 is null) return;
        _workspaceContextNavV9.Children.Clear();
        var primary = GetPrimaryNavigationRouteV9(page);
        var tabs = primary switch
        {
            "Players" => new (string Label, string Route)[] {
                ("PLAYERS", "Players"), ("ADVANCED SEARCH", "Advanced Search"),
                ("RECRUITMENT", "Recruitment"), ("CLUBS", "Clubs"), ("STAFF", "Staff")
            },
            "Squad Architect" => new (string Label, string Route)[] {
                ("SQUAD", "Squad Architect"), ("TACTICS", "Tactical Lab"),
                ("ROLES", "Role Lab"), ("TRANSFERS", "Transfers"), ("ANALYTICS", "Analytics")
            },
            "Live Editor" => new (string Label, string Route)[] {
                ("LIVE EDITOR", "Live Editor"), ("MASS EDIT", "Mass Edit"), ("EDIT HISTORY", "Edit History")
            },
            "Settings" => new (string Label, string Route)[] {
                ("SETTINGS", "Settings"), ("DIAGNOSTICS", "Tester & Logs"),
                ("HELP", "Help & Tutorial"), ("ASSISTANT", "Atlas Assistant")
            },
            _ => new (string Label, string Route)[] { ("OVERVIEW", "Dashboard") }
        };

        foreach (var (label, route) in tabs)
        {
            var active = string.Equals(page, route, StringComparison.OrdinalIgnoreCase);
            var button = new Button
            {
                Content = label,
                Tag = route,
                FontFamily = MonolithDesignSystem.BodyFont,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = MonolithDesignSystem.Solid(active ? MonolithDesignSystem.Signal : MonolithDesignSystem.Muted),
                Background = MonolithDesignSystem.Solid(active ? MonolithDesignSystem.SignalSoft : MonolithDesignSystem.SurfaceRaised),
                BorderBrush = MonolithDesignSystem.Solid(active ? MonolithDesignSystem.Signal : MonolithDesignSystem.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(2, 0, 2, 0),
                MinHeight = 30,
                Cursor = Cursors.Hand,
                Focusable = true,
                Template = RoundedButtonTemplate(0),
                ToolTip = PageDescription(route)
            };
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.MouseEnter += (_, _) =>
            {
                if (!string.Equals(_activePage, route, StringComparison.OrdinalIgnoreCase))
                    button.Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceHover);
            };
            button.MouseLeave += (_, _) =>
            {
                if (!string.Equals(_activePage, route, StringComparison.OrdinalIgnoreCase))
                    button.Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceRaised);
            };
            button.Click += async (_, _) => await NavigateAsync(route);
            _workspaceContextNavV9.Children.Add(button);
        }

        if (primary == "Players")
        {
            var more = new Button
            {
                Content = "MORE ▾",
                FontFamily = MonolithDesignSystem.BodyFont,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = MonolithDesignSystem.Solid(MonolithDesignSystem.Muted),
                Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceRaised),
                BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(2, 0, 2, 0),
                MinHeight = 30,
                Cursor = Cursors.Hand,
                Focusable = true,
                Template = RoundedButtonTemplate(0),
                ToolTip = "More scouting tools"
            };
            var menu = new ContextMenu();
            foreach (var (label, route) in new (string Label, string Route)[] {
                ("Value Finder", "Value Finder"), ("Attribute Search", "Attribute Search"),
                ("Stats Search", "Stats Search"), ("Scouting Matrix", "Scouting Matrix"),
                ("Shortlists", "Shortlists")
            })
            {
                var item = new MenuItem { Header = label };
                item.Click += async (_, _) => await NavigateAsync(route);
                menu.Items.Add(item);
            }
            more.ContextMenu = menu;
            more.Click += (_, _) => { more.ContextMenu!.PlacementTarget = more; more.ContextMenu.IsOpen = true; };
            _workspaceContextNavV9.Children.Add(more);
        }
    }

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

        var sidebar = BuildSidebarV9();
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
            CornerRadius = new CornerRadius(0),
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
        var searchPlaceholder = MonolithDesignSystem.BodyText(
            "Search players, staff, clubs or UID…", 13,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        searchPlaceholder.Margin = new Thickness(7, 0, 0, 0);
        searchPlaceholder.IsHitTestVisible = false;
        void RefreshQuickSearchPlaceholder()
            => searchPlaceholder.Visibility = string.IsNullOrEmpty(_quickSearchBoxV9.Text)
                && !_quickSearchBoxV9.IsKeyboardFocused ? Visibility.Visible : Visibility.Collapsed;
        _quickSearchBoxV9.TextChanged += (_, _) => RefreshQuickSearchPlaceholder();
        _quickSearchBoxV9.GotKeyboardFocus += (_, _) => RefreshQuickSearchPlaceholder();
        _quickSearchBoxV9.LostKeyboardFocus += (_, _) => RefreshQuickSearchPlaceholder();
        Grid.SetColumn(searchPlaceholder, 1);
        quickSearchGrid.Children.Add(searchPlaceholder);
        RefreshQuickSearchPlaceholder();
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
        actions.Children.Add(ToolbarIcon("\uE8E5", "Import a local copy of an FM career save. The reader is read-only.", async (_, _) => await ImportCareerSaveAsync()));
        actions.Children.Add(ToolbarIcon("\uE823", "Activity Centre — current scan/import progress, elapsed time and cancellation.", (_, _) => ShowActivityCenter()));
        _liveLoadButton = ToolbarIcon("\uE72C", "Connect to or refresh the currently loaded supported FM27 career.", async (_, _) => await LoadLiveCareerAsync());
        _liveLoadButton.IsEnabled = true;
        actions.Children.Add(_liveLoadButton);
        actions.Children.Add(ToolbarIcon("\uE713", "Current appearance: " + _themeMode + ". Click to cycle appearance modes.", async (_, _) => await CycleThemeAsync()));

        _cancelImportButton = ToolbarIcon("\uE711", "Cancel the active live scan or offline save import at a safe checkpoint.", (_, _) =>
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
        Grid.SetColumn(actions, 2);
        toolbarGrid.Children.Add(actions);

        var toolbarSurface = new Border
        {
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.SurfaceRaised),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
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
        _workspaceContextNavV9 = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(_workspaceContextNavV9, 1);
        pageHeader.Children.Add(_workspaceContextNavV9);
        RefreshWorkspaceContextNavV9(_activePage);
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

        var footerRight = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _cancelImportButton.MinWidth = 36;
        _cancelImportButton.Height = 32;
        footerRight.Children.Add(_cancelImportButton);
        var footerLabel = MonolithDesignSystem.DataText("FM ATLAS  /  LOCAL DATA", 9,
            MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        footerLabel.VerticalAlignment = VerticalAlignment.Center;
        footerLabel.Margin = new Thickness(8, 0, 2, 0);
        footerRight.Children.Add(footerLabel);
        Grid.SetColumn(footerRight, 2);
        statusGrid.Children.Add(footerRight);

        var footer = new Border
        {
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Surface),
            BorderBrush = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(10, 5, 10, 5),
            Child = statusGrid
        };
        Grid.SetRow(footer, 3);
        main.Children.Add(footer);
    }
}
