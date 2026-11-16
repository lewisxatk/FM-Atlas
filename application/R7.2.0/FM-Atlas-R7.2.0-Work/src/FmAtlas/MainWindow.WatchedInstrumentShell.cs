using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Input;

namespace FmAtlas;

public partial class MainWindow
{
    private void BuildWatchedShell()
        {
            RootGrid.Children.Clear();
            RootGrid.Background = Brush("#07080A");
            _shell = new Grid();
            _navColumn = new ColumnDefinition { Width = new GridLength(_navCollapsed ? 68 : 224) };
            _shell.ColumnDefinitions.Add(_navColumn);
            _shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            RootGrid.Children.Add(_shell);
    
            var sidebar = BuildWatchedSidebar();
            Grid.SetColumn(sidebar, 0);
            _shell.Children.Add(sidebar);
    
            var main = new Grid { Margin = new Thickness(24, 15, 24, 18) };
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(main, 1);
            _shell.Children.Add(main);
    
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 14, 0) };
            _pageTitle = WatchedDisplayText("Dashboard", 25, true, Ink);
            _pageSubTitle = WatchedMonoText("CAREER INTELLIGENCE  /  SCOUTING  /  ANALYSIS", 10, false, Muted);
            titleStack.Children.Add(_pageTitle);
            titleStack.Children.Add(_pageSubTitle);
            headerGrid.Children.Add(titleStack);
    
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            actions.Children.Add(ToolbarIcon("\uE721", "Quick Search — find players, staff and clubs. Shortcut: Ctrl+K.", (_, _) => ShowGlobalSearch()));
            actions.Children.Add(ToolbarIcon("\uE71C", "Advanced Search — build filters across supported FM fields.", async (_, _) => await NavigateAsync("Advanced Search")));
            actions.Children.Add(ToolbarIcon("\uE8E5", "IMPORT SAVE COPY — inspect a local copy of an FM career save. The source is never modified.", async (_, _) => await ImportCareerSaveAsync()));
            _cancelImportButton = ToolbarIcon("\uE711", "Cancel the active live scan or save import safely.", (_, _) =>
            {
                if (_liveScanCancellation is not null)
                {
                    _liveScanCancelledByUser = true;
                    _liveScanCancellation.Cancel();
                }
                _saveImportCancellation?.Cancel();
                if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Cancellation requested — stopping safely at the next checkpoint.";
            });
            _cancelImportButton.IsEnabled = _saveImportCancellation is not null || _liveScanCancellation is not null;
            _cancelImportButton.Visibility = _saveImportCancellation is not null || _liveScanCancellation is not null ? Visibility.Visible : Visibility.Collapsed;
            actions.Children.Add(_cancelImportButton);
            actions.Children.Add(ToolbarIcon("\uE823", "Activity Centre — view progress, elapsed time and cancellation.", (_, _) => ShowActivityCenter()));
            actions.Children.Add(ToolbarIcon("\uE74E", "SAVE ATLAS DATA — save the FM Atlas local database and settings. This does not save the FM career.", async (_, _) => await SaveAtlasAsync()));
            actions.Children.Add(ToolbarIcon("\uE74E", "SAVE FM CAREER · IN PROGRESS — use Ctrl+S inside Football Manager after applying live edits.", (_, _) => ShowCareerSaveInProgress()));
            actions.Children.Add(ToolbarIcon("\uE8A7", "Open saved shortlists.", async (_, _) => await NavigateAsync("Shortlists")));
            actions.Children.Add(ToolbarIcon("\uE9D2", "Open performance analytics.", async (_, _) => await NavigateAsync("Analytics")));
            actions.Children.Add(ToolbarIcon("\uE80F", "Open your human-manager profile.", async (_, _) => await OpenMyManagerAsync()));
            actions.Children.Add(ToolbarIcon("\uE77B", "Open your current club profile.", async (_, _) => await OpenMyClubAsync()));
            _liveLoadButton = ToolbarIcon("\uE72C", "LOAD LIVE SAVE — connect to or refresh the currently loaded supported FM27 career.", async (_, _) => await LoadLiveCareerAsync());
            _liveLoadButton.IsEnabled = true;
            actions.Children.Add(_liveLoadButton);
            actions.Children.Add(ToolbarIcon("\uE9D9", "Export diagnostics and recent application logs.", (_, _) => ExportDiagnostics()));
            Grid.SetColumn(actions, 1);
            headerGrid.Children.Add(actions);
    
            var header = new Border
            {
                Background = Brush("#0C0E11"),
                BorderBrush = Brush("#303943"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(2, 0, 2, 8),
                Child = headerGrid
            };
            Grid.SetRow(header, 0);
            main.Children.Add(header);
    
            var careerGrid = new Grid();
            careerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            careerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var careerStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _connectionDot = new Ellipse { Width = 7, Height = 7, Fill = Brush("#D9A44D"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            careerStack.Children.Add(_connectionDot);
            var careerText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            careerText.Children.Add(WatchedMonoText("CAREER CONNECTION", 10, true, Teal));
            _connectionSummary = Txt("Waiting for your FM27 career", 12, true, Ink);
            careerText.Children.Add(_connectionSummary);
            _liveLoadStatus = WatchedMonoText("AUTOMATIC DETECTION  /  READ-ONLY INDEXING", 10, false, Muted);
            careerText.Children.Add(_liveLoadStatus);
            careerStack.Children.Add(careerText);
            careerGrid.Children.Add(careerStack);
    
            var progressStack = new StackPanel { Width = 230, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            progressStack.Children.Add(Txt("Open FM27 and load a career, or import a local save copy.", 10, false, Muted, wrap: true));
            _liveLoadProgress = new ProgressBar { Height = 3, Minimum = 0, Maximum = 100, Value = 0, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 0), Foreground = Brush("#3988C8"), Background = Brush("#303943") };
            progressStack.Children.Add(_liveLoadProgress);
            Grid.SetColumn(progressStack, 1);
            careerGrid.Children.Add(progressStack);
    
            var career = new Border
            {
                Background = Brush("#0C0E11"),
                BorderBrush = Brush("#303943"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(2, 8, 2, 8),
                Child = careerGrid
            };
            Grid.SetRow(career, 1);
            main.Children.Add(career);
    
            _pageHost = new ContentControl { Margin = new Thickness(0, 14, 0, 0), ClipToBounds = true };
            Grid.SetRow(_pageHost, 2);
            main.Children.Add(_pageHost);
        }
    
    private UIElement BuildWatchedSidebar()
        {
            var rail = new Border
            {
                Background = Brush("#0C0E11"),
                BorderBrush = Brush("#303943"),
                BorderThickness = new Thickness(0, 0, 1, 0)
            };
            var dock = new DockPanel { Margin = new Thickness(_navCollapsed ? 8 : 14, 14, _navCollapsed ? 8 : 14, 14) };
            rail.Child = dock;
    
            var brand = new Grid { Margin = new Thickness(2, 2, 2, 15) };
            brand.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            brand.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var logoLine = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logoImage = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/FmAtlas;component/Assets/FM-Atlas-27-Icon.png", UriKind.Absolute)), Stretch = Stretch.UniformToFill };
            var logo = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(ThemeRadius(0)), ClipToBounds = true, BorderBrush = Brush("#303943"), BorderThickness = new Thickness(1), Child = logoImage };
            logoLine.Children.Add(logo);
            var word = new StackPanel { Margin = new Thickness(10, 0, 0, 0), Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible };
            word.Children.Add(WatchedDisplayText("FM ATLAS", 17, true, Ink));
            word.Children.Add(WatchedMonoText("FOOTBALL INTELLIGENCE", 9, false, Muted));
            logoLine.Children.Add(word);
            Grid.SetColumn(logoLine, 0);
            brand.Children.Add(logoLine);
    
            var collapse = new Button
            {
                Content = _navCollapsed ? "›" : "‹",
                Width = 28, Height = 30, Padding = new Thickness(0),
                Background = Brush("#0C0E11"), Foreground = Ink,
                BorderBrush = Brush("#303943"), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, ToolTip = _navCollapsed ? "Expand navigation and show workspace names." : "Collapse navigation to a compact code rail.",
                Template = RoundedButtonTemplate(0)
            };
            collapse.Click += async (_, _) => await ToggleNavigationAsync();
            ToolTipService.SetInitialShowDelay(collapse, 350);
            Grid.SetColumn(collapse, 1);
            brand.Children.Add(collapse);
            DockPanel.SetDock(brand, Dock.Top);
            dock.Children.Add(brand);
    
            var navScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _navPanel = new StackPanel();
            navScroll.Content = _navPanel;
            dock.Children.Add(navScroll);
    
            AddNavGroup("HOME");
            AddNav("▦", "Dashboard");
            AddNavGroup("SCOUTING");
            AddNav("♟", "Players");
            AddNav("⌕", "Advanced Search");
            AddNav("⌕", "Recruitment");
            AddNav("◆", "Value Finder");
            AddNav("⌗", "Attribute Search");
            AddNav("∑", "Stats Search", inProgress: true);
            AddNav("▦", "Scouting Matrix", inProgress: true);
            AddNav("▤", "Shortlists");
            AddNavGroup("SQUAD & ANALYSIS");
            AddNav("▥", "Squad Architect");
            AddNav("◇", "Clubs");
            AddNav("♙", "Staff");
            AddNav("⇄", "Transfers");
            AddNav("▥", "Analytics");
            AddNav("⚑", "Tactical Lab");
            AddNav("◎", "Role Lab");
            AddNavGroup("EDITING");
            AddNav("✎", "Live Editor");
            AddNav("▦", "Mass Edit");
            AddNav("◷", "Edit History");
            AddNavGroup("DATA & TOOLS");
            AddNav("⌘", "Atlas Assistant");
            AddNav("▧", "Settings");
            AddNav("◉", "Tester & Logs");
            AddNav("?", "Help & Tutorial");
    
            var bottom = new Border
            {
                Background = Brush("#0C0E11"), BorderBrush = Brush("#303943"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(ThemeRadius(0)),
                Padding = new Thickness(10), Margin = new Thickness(0, 12, 0, 0),
                ToolTip = "FM connection status. Unknown game builds remain read-only."
            };
            var bottomStack = new StackPanel();
            if (!_navCollapsed)
            {
                bottomStack.Children.Add(WatchedMonoText("SESSION STATUS", 10, true, Teal));
                bottomStack.Children.Add(Txt("Automatic · read-only", 11, true, Ink));
                bottomStack.Children.Add(Txt("Automatic indexing is read-only. Live Apply is OFF until you explicitly enable it in Editor Studio.", 10, false, Muted, wrap: true));
            }
            else bottomStack.Children.Add(WatchedMonoText("●", 12, true, Teal));
            bottom.Child = bottomStack;
            DockPanel.SetDock(bottom, Dock.Bottom);
            dock.Children.Add(bottom);
            return rail;
        }


    private static double ThemeRadius(double _) => 0;

    private static TextBlock WatchedDisplayText(string text, double size, bool bold, Brush foreground, bool wrap = false)
        => new()
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = foreground,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

    private static TextBlock WatchedMonoText(string text, double size, bool bold, Brush foreground, bool wrap = false)
        => new()
        {
            Text = text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = foreground,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

}
