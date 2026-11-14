using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;
using Microsoft.Win32;
using FmAtlas.Data;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow : Window
{
    private static string _themeMode = LoadThemeMode();
    // Automatic read-only indexing is enabled only for exact supported FM27 profiles.
    // Scans are profile-bounded, cancellable, limited to two minutes and save-generation guarded.
    // Live Apply/Undo remains disabled until field mappings and rollback are independently certified.
    private static bool LiveMemoryImportEnabled => true;
    private static bool LiveMemoryEditingEnabled => false;
    private static Brush Ink => Brush("#E8EEF4");
    private static Brush Teal => Brush("#F08A42");
    private static Brush TealDark => Brush("#FFD0AA");
    private static Brush Muted => Brush("#9EADB9");
    private static Brush Line => Brush("#303943");
    private static Brush Canvas => Brush("#0D1117");
    private readonly ScoutDatabase _database = new();
    private readonly ObservableCollection<PlayerRecord> _players = new();
    private readonly ObservableCollection<AtlasClubRecord> _clubs = new();
    private readonly ObservableCollection<AtlasStaffRecord> _staff = new();
    private readonly ObservableCollection<ShortlistRecord> _shortlist = new();
    private Grid? _shell;
    private ColumnDefinition? _navColumn;
    private bool _navCollapsed;
    private StackPanel? _navPanel;
    private ContentControl? _pageHost;
    private TextBlock? _pageTitle;
    private TextBlock? _pageSubTitle;
    private TextBlock? _connectionSummary;
    private Ellipse? _connectionDot;
    private Button? _liveLoadButton;
    private Button? _cancelImportButton;
    private DispatcherTimer? _liveCareerTimer;
    private bool _liveCareerProbeBusy;
    private bool _liveImportBusy;
    private bool _liveScanCancelledByUser;
    private string? _activeLiveScanSessionKey;
    private CancellationTokenSource? _liveScanCancellation;
    private string? _lastAttemptedLiveSession;
    private string? _lastImportedLiveSessionKey;
    private DateTime _nextLiveRetryUtc = DateTime.MinValue;
    private LiveCareerProbe? _currentLiveProbe;
    private CancellationTokenSource? _saveImportCancellation;
    private string? _activeBackgroundOperation;
    private DateTime? _activeBackgroundOperationStartedUtc;
    private string? _lastBackgroundOperation;
    private DateTime? _lastBackgroundOperationCompletedUtc;
    private Window? _activityCenterWindow;
    private DispatcherTimer? _activityCenterTimer;
    private ProgressBar? _liveLoadProgress;
    private TextBlock? _liveLoadStatus;
    private string _activePage = "Dashboard";
    private PlayerRecord? _selectedPlayer;
    private DataGrid? _playerResultsGrid;
    private Grid? _playerWorkspaceGrid;
    private ColumnDefinition? _playerResultsColumn;
    private ColumnDefinition? _playerProfileColumn;
    private bool _profileExpanded;
    private Button? _profileExpandButton;
    private TabControl? _profileTabs;
    private StackPanel? _profilePanel;
    private Button? _previousPlayerPageButton;
    private Button? _nextPlayerPageButton;
    private TextBlock? _playerPageStatus;
    private int _playerOffset;
    private const int PlayerPageSize = 250;
    private bool _navigationBusy;
    private bool _editorEnabled; // Local edits and guarded live editing are OFF by default on every launch.
    private string? _queuedNavigation;
    private string? _pendingClubSourceId;
    private string? _pendingStaffSourceId;
    private string? _pendingClubSearchQuery;
    private string? _pendingStaffSearchQuery;
    private int _playerSearchRequestId;
    private readonly Dictionary<string, PlayerRecord> _openPlayerRecords = new(StringComparer.OrdinalIgnoreCase);
    private TextBlock? _playerCountText;
    private TextBox? _playerSearch;
    private ComboBox? _positionPicker;
    private TextBox? _playerMinCaInput, _playerMaxCaInput, _playerMinPaInput, _playerMaxPaInput;
    private ComboBox? _shortlistPicker;

    // Microsoft.Data.Sqlite's async ADO.NET methods may complete synchronously.
    // Run database work on a worker so long queries/imports never occupy the WPF dispatcher.
    private static Task<T> RunDatabaseAsync<T>(Func<Task<T>> operation) => Task.Run(operation);
    private static Task RunDatabaseAsync(Func<Task> operation) => Task.Run(operation);

    public MainWindow()
    {
        AtlasLog.Initialise();
        AtlasLog.Info("Main window construction started.");
        InitializeComponent();
        FontFamily = new FontFamily("Segoe UI");
        Loaded += WindowLoaded;
        Closed += (_, _) =>
        {
            // Do not leave background work running after the WPF shell exits.
            // Cancellation is cooperative: active importers roll back unpublished
            // snapshots and retain the last committed database state.
            _liveCareerTimer?.Stop();
            _liveScanCancellation?.Cancel();
            _saveImportCancellation?.Cancel();
            AtlasLog.Info("Main window closed; active live scan/save import cancellation requested.");
        };
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.K || e.Key == Key.F))
            {
                e.Handled = true;
                ShowGlobalSearch();
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.F)
            {
                e.Handled = true;
                ShowGlobalSearch();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W)
            {
                e.Handled = true;
                CloseCurrentProfileTab();
            }
        };
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunDatabaseAsync(() => _database.InitialiseAsync());
            BuildShell();
            RefreshConnectionStatus();
            await RefreshCareerStatusAsync();
            await NavigateAsync("Dashboard");
            StartLiveCareerMonitor();
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Application startup/database initialisation failed.", ex);
            MessageBox.Show(this, "FM Atlas could not initialise its local database. Your existing database has not been deleted.\n\n" + ex.Message,
                "FM Atlas database error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BuildShell()
    {
        RootGrid.Children.Clear();
        _shell = new Grid();
        _navColumn = new ColumnDefinition { Width = new GridLength(_navCollapsed ? 76 : 248) };
        _shell.ColumnDefinitions.Add(_navColumn);
        _shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _shell.Background = new LinearGradientBrush(
            Color.FromRgb(_themeMode == "Glass Light" ? (byte)239 : (byte)10, _themeMode == "Glass Light" ? (byte)244 : (byte)11, _themeMode == "Glass Light" ? (byte)250 : (byte)13),
            Color.FromRgb(_themeMode == "Glass Light" ? (byte)222 : (byte)27, _themeMode == "Glass Light" ? (byte)232 : (byte)28, _themeMode == "Glass Light" ? (byte)244 : (byte)31), 35);
        RootGrid.Children.Add(_shell);

        var sidebar = BuildSidebar();
        Grid.SetColumn(sidebar, 0);
        _shell.Children.Add(sidebar);

        var main = new Grid { Margin = new Thickness(20, 14, 20, 18) };
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(88) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(main, 1);
        _shell.Children.Add(main);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 12, 0) };
        _pageTitle = Txt("Dashboard", 23, true, Ink);
        _pageSubTitle = Txt("Career intelligence · scouting · analysis · squad planning", 10, false, Muted);
        titleStack.Children.Add(_pageTitle);
        titleStack.Children.Add(_pageSubTitle);
        headerGrid.Children.Add(titleStack);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 10, 0) };
        actions.Children.Add(ToolbarIcon("\uE721", "Global search — find players, staff and clubs. Shortcut: Ctrl+K.", (_, _) => ShowGlobalSearch()));
        actions.Children.Add(ToolbarIcon("\uE71C", "Open Advanced Search and choose player, attribute or performance-stat filters.", async (_, _) => await NavigateAsync("Advanced Search")));
        actions.Children.Add(ToolbarIcon("\uE8E5", "Import a local copy of an FM career save. The reader is read-only.", async (_, _) => await ImportCareerSaveAsync()));
        _cancelImportButton = ToolbarIcon("\uE711", "Cancel the current live-game scan or career-save import. An uncommitted snapshot is not published.", (_, _) =>
        {
            if (_liveScanCancellation is not null)
            {
                _liveScanCancelledByUser = true;
                _liveScanCancellation.Cancel();
            }
            _saveImportCancellation?.Cancel();
            if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Cancellation requested — safely stopping at the next scan/import checkpoint…";
        });
        _cancelImportButton.IsEnabled = _saveImportCancellation is not null || _liveScanCancellation is not null;
        _cancelImportButton.Visibility = _saveImportCancellation is not null || _liveScanCancellation is not null ? Visibility.Visible : Visibility.Collapsed;
        actions.Children.Add(_cancelImportButton);
        actions.Children.Add(ToolbarIcon("\uE823", "Activity Centre — view the active live scan or save import, elapsed time and cancellation controls.", (_, _) => ShowActivityCenter()));
        actions.Children.Add(ToolbarIcon("\uE74E", "Save your current FM Atlas workspace and local changes.", async (_, _) => await SaveAtlasAsync()));
        actions.Children.Add(ToolbarIcon("\uE8A7", "Open your saved shortlists.", async (_, _) => await NavigateAsync("Shortlists")));
        actions.Children.Add(ToolbarIcon("\uE9D2", "Open recruitment and performance analytics.", async (_, _) => await NavigateAsync("Analytics")));
        actions.Children.Add(ToolbarIcon("\uE80F", "Open your human-manager profile in the Staff directory.", async (_, _) => await OpenMyManagerAsync()));
        actions.Children.Add(ToolbarIcon("\uE77B", "Open your current club in the Club directory.", async (_, _) => await OpenMyClubAsync()));
        _liveLoadButton = ToolbarIcon("\uE72C", "Connect to or refresh the currently loaded FM27 career. Atlas also detects save changes automatically.", async (_, _) => await LoadLiveCareerAsync());
        _liveLoadButton.IsEnabled = true;
        actions.Children.Add(_liveLoadButton);
        actions.Children.Add(ToolbarIcon("\uE713", "Current appearance: " + _themeMode + ". Click to cycle Monolith Glass, Glass Dark, Glass Light and Studio.", async (_, _) => await CycleThemeAsync()));
        actions.Children.Add(ToolbarIcon("\uE9D9", "Export diagnostics and recent application logs.", (_, _) => ExportDiagnostics()));
        Grid.SetColumn(actions, 1);
        headerGrid.Children.Add(actions);

        var header = new Border
        {
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(_themeMode == "Glass Light"
                        ? Color.FromArgb(240, 255, 255, 255)
                        : Color.FromArgb(222, 38, 40, 45), 0),
                    new GradientStop(_themeMode == "Glass Light"
                        ? Color.FromArgb(220, 233, 241, 249)
                        : Color.FromArgb(188, 20, 22, 27), 1)
                },
                new Point(0, 0), new Point(0, 1)),
            BorderBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(172, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(68, 244, 155, 100), 1)
                },
                new Point(0, 0), new Point(0, 1)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(3),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 0, 0),
                BlurRadius = 20,
                ShadowDepth = 3,
                Opacity = _themeMode == "Glass Light" ? 0.08 : 0.22
            },
            Child = headerGrid
        };
        header.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(header, 0);
        main.Children.Add(header);

        var career = Card(new Grid(), 16);
        careerGridForBuild(career);
        void careerGridForBuild(Border careerBorder)
        {
            var careerGrid = (Grid)careerBorder.Child;
            careerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            careerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var careerStack = new StackPanel();
            careerStack.Children.Add(Txt("MONOLITH GLASS · LIVE CAREER", 9, true, Teal));
            careerStack.Children.Add(Txt("Waiting for your FM27 career", 15, true, Ink));
            _connectionSummary = Txt("Open Football Manager 27 and load a career, or import a local save copy.", 10, false, Muted);
            careerStack.Children.Add(_connectionSummary);
            _liveLoadStatus = Txt("Automatic career detection and bounded read-only indexing active", 9, false, Muted);
            _liveLoadStatus.Margin = new Thickness(0, 5, 0, 0);
            careerStack.Children.Add(_liveLoadStatus);
            _liveLoadProgress = new ProgressBar { Height = 4, Minimum = 0, Maximum = 100, Value = 0, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 5, 0, 0), Foreground = Brush("#3988C8"), Background = Brush("#DCE8F2") };
            careerStack.Children.Add(_liveLoadProgress);
            careerGrid.Children.Add(careerStack);
            var state = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _connectionDot = new Ellipse { Width = 9, Height = 9, Fill = Brush("#D9A44D"), Margin = new Thickness(0, 0, 7, 0) };
            state.Children.Add(_connectionDot);
            state.Children.Add(Txt("Awaiting verified save", 10, true, Muted));
            Grid.SetColumn(state, 1);
            careerGrid.Children.Add(state);
        }
        Grid.SetRow(career, 1);
        main.Children.Add(career);

        _pageHost = new ContentControl { Margin = new Thickness(0, 14, 0, 0), ClipToBounds = true };
        Grid.SetRow(_pageHost, 2);
        main.Children.Add(_pageHost);
    }

    private Button ToolbarIcon(string glyph, string description, RoutedEventHandler click)
    {
        var icon = Txt(glyph, 17, true, Ink);
        icon.FontFamily = new FontFamily("Segoe MDL2 Assets");
        var button = new Button
        {
            Content = icon,
            Width = 42,
            Height = 42,
            Padding = new Thickness(0),
            Margin = new Thickness(3, 0, 3, 0),
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(142, 125, 76, 49), 0),
                    new GradientStop(Color.FromArgb(76, 57, 39, 30), 1)
                },
                new Point(0, 0), new Point(0, 1)),
            Foreground = Ink,
            BorderBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(170, 255, 197, 155), 0),
                    new GradientStop(Color.FromArgb(78, 220, 125, 74), 1)
                },
                new Point(0, 0), new Point(0, 1)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = description,
            Template = RoundedButtonTemplate(13)
        };
        ToolTipService.SetInitialShowDelay(button, 350);
        ToolTipService.SetShowDuration(button, 30000);
        ToolTipService.SetShowOnDisabled(button, true);
        System.Windows.Automation.AutomationProperties.SetName(button, description);
        System.Windows.Automation.AutomationProperties.SetHelpText(button, description);
        button.Click += click;
        return button;
    }

    private static void ApplyGlassControlTheme(DependencyObject root, HashSet<DependencyObject>? visited = null)
    {
        visited ??= new HashSet<DependencyObject>();
        if (!visited.Add(root)) return;

        switch (root)
        {
            case TextBox textBox:
                textBox.Background = Brush("#171F28");
                textBox.Foreground = Ink;
                var textBoxRestingBorder = Brush("#303943");
                textBox.BorderBrush = textBoxRestingBorder;
                textBox.BorderThickness = new Thickness(1);
                textBox.CaretBrush = Teal;
                textBox.SelectionBrush = Brush("#483022");
                textBox.Padding = new Thickness(9, 6, 9, 6);
                textBox.SnapsToDevicePixels = true;
                // A clear keyboard-focus ring makes the dark glass controls usable
                // without relying on hover or colour-only selection cues.
                textBox.GotKeyboardFocus += (_, _) => textBox.BorderBrush = Teal;
                textBox.LostKeyboardFocus += (_, _) => textBox.BorderBrush = textBoxRestingBorder;
                break;

            case ComboBox comboBox:
                comboBox.Background = Brush("#171F28");
                comboBox.Foreground = Ink;
                var comboRestingBorder = Brush("#303943");
                comboBox.BorderBrush = comboRestingBorder;
                comboBox.BorderThickness = new Thickness(1);
                comboBox.Padding = new Thickness(8, 4, 8, 4);
                comboBox.SnapsToDevicePixels = true;
                comboBox.GotKeyboardFocus += (_, _) => comboBox.BorderBrush = Teal;
                comboBox.LostKeyboardFocus += (_, _) => comboBox.BorderBrush = comboRestingBorder;
                var comboItemStyle = new Style(typeof(ComboBoxItem));
                comboItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#171F28")));
                comboItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
                comboItemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 6, 9, 6)));
                var comboHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                comboHover.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#483022")));
                comboItemStyle.Triggers.Add(comboHover);
                var comboSelected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
                comboSelected.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#483022")));
                comboSelected.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
                comboItemStyle.Triggers.Add(comboSelected);
                comboBox.ItemContainerStyle = comboItemStyle;
                break;

            case TabControl tabs:
                tabs.Background = Brush("#171F28");
                tabs.Foreground = Ink;
                tabs.BorderBrush = Brush("#303943");
                tabs.BorderThickness = new Thickness(1);
                var tabItemStyle = new Style(typeof(TabItem));
                tabItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#202A35")));
                tabItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Muted));
                tabItemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 7, 12, 7)));
                tabItemStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(2, 0, 2, 0)));
                var tabSelected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
                tabSelected.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#483022")));
                tabSelected.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
                tabSelected.Setters.Add(new Setter(Control.BorderBrushProperty, Teal));
                tabSelected.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 2)));
                tabItemStyle.Triggers.Add(tabSelected);
                tabs.ItemContainerStyle = tabItemStyle;
                break;

            case CheckBox checkBox:
                checkBox.Foreground = Ink;
                checkBox.Focusable = true;
                break;

            case RadioButton radioButton:
                radioButton.Foreground = Ink;
                radioButton.Focusable = true;
                break;

            case ProgressBar progressBar:
                progressBar.Foreground = Teal;
                progressBar.Background = Brush("#303943");
                break;
        }

        // Walk both logical children and already-realised visual children. This covers
        // ordinary workspace controls and controls created by templates without styling
        // the contents of a popup that has not been opened.
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            ApplyGlassControlTheme(child, visited);
        if (root is Visual visual)
        {
            var count = VisualTreeHelper.GetChildrenCount(visual);
            for (var childIndex = 0; childIndex < count; childIndex++)
                ApplyGlassControlTheme(VisualTreeHelper.GetChild(visual, childIndex), visited);
        }
    }

    private void BeginBackgroundOperation(string name)
    {
        _activeBackgroundOperation = name;
        _activeBackgroundOperationStartedUtc = DateTime.UtcNow;
        AtlasLog.Info("Background operation started: " + name + ".");
    }

    private void EndBackgroundOperation()
    {
        if (_activeBackgroundOperation is null) return;
        _lastBackgroundOperation = _activeBackgroundOperation + " · " + (_liveLoadStatus?.Text ?? "Finished");
        _lastBackgroundOperationCompletedUtc = DateTime.UtcNow;
        AtlasLog.Info("Background operation finished: " + _activeBackgroundOperation + ".");
        _activeBackgroundOperation = null;
        _activeBackgroundOperationStartedUtc = null;
    }

    private void ShowActivityCenter()
    {
        if (_activityCenterWindow is { IsVisible: true })
        {
            _activityCenterWindow.Activate();
            return;
        }

        var content = new StackPanel();
        content.Children.Add(Txt("FM ATLAS · ACTIVITY", 9, true, Teal));
        content.Children.Add(Txt("Activity Centre", 22, true, Ink));
        content.Children.Add(Txt("Long-running career scans and save imports appear here. Atlas runs one scan/import at a time; normal search and page navigation remain independent.", 10, false, Muted, wrap: true));
        var activeName = Txt(_activeBackgroundOperation ?? "No active background task", 12, true, Ink);
        activeName.Margin = new Thickness(0, 12, 0, 4);
        content.Children.Add(activeName);
        var activeStatus = Txt(_liveLoadStatus?.Text ?? "FM Atlas is ready.", 10, false, Muted, wrap: true);
        activeStatus.Margin = new Thickness(0, 0, 0, 8);
        content.Children.Add(activeStatus);
        var elapsed = Txt("Elapsed · —", 9, true, Teal);
        content.Children.Add(elapsed);
        var progress = new ProgressBar { Height = 5, Margin = new Thickness(0, 10, 0, 10), IsIndeterminate = _activeBackgroundOperation is not null, Visibility = _activeBackgroundOperation is null ? Visibility.Collapsed : Visibility.Visible };
        content.Children.Add(progress);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = SoftButton("Cancel active task", (_, _) =>
        {
            _liveScanCancellation?.Cancel();
            _saveImportCancellation?.Cancel();
            activeStatus.Text = "Cancellation requested. Atlas will stop at a safe checkpoint and retain the last committed snapshot.";
        });
        cancel.IsEnabled = _liveScanCancellation is not null || _saveImportCancellation is not null;
        actions.Children.Add(cancel);
        actions.Children.Add(PrimaryButton("Close", (_, _) => _activityCenterWindow?.Close()));
        content.Children.Add(actions);
        if (_lastBackgroundOperation is not null)
        {
            var previous = Txt("Previous task · " + _lastBackgroundOperation, 9, false, Muted, wrap: true);
            previous.Margin = new Thickness(0, 12, 0, 0);
            content.Children.Add(previous);
        }

        var window = new Window
        {
            Title = "FM Atlas · Activity Centre",
            Owner = this,
            Width = 440,
            Height = 330,
            MinWidth = 400,
            MinHeight = 300,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush("#0D1117"),
            Foreground = Ink,
            Content = Card(content, 18, new Thickness(14))
        };
        _activityCenterWindow = window;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _activityCenterTimer = timer;
        timer.Tick += (_, _) =>
        {
            var active = _activeBackgroundOperation is not null;
            activeName.Text = _activeBackgroundOperation ?? "No active background task";
            activeStatus.Text = active ? (_liveLoadStatus?.Text ?? "Working…") : "No scan or save import is currently running.";
            elapsed.Text = active && _activeBackgroundOperationStartedUtc.HasValue
                ? "Elapsed · " + (DateTime.UtcNow - _activeBackgroundOperationStartedUtc.Value).ToString(@"hh\:mm\:ss")
                : _lastBackgroundOperationCompletedUtc.HasValue
                    ? "Last task finished · " + _lastBackgroundOperationCompletedUtc.Value.ToLocalTime().ToString("HH:mm:ss")
                    : "Elapsed · —";
            progress.IsIndeterminate = active;
            progress.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            cancel.IsEnabled = _liveScanCancellation is not null || _saveImportCancellation is not null;
        };
        window.Closed += (_, _) =>
        {
            timer.Stop();
            if (ReferenceEquals(_activityCenterTimer, timer)) _activityCenterTimer = null;
            if (ReferenceEquals(_activityCenterWindow, window)) _activityCenterWindow = null;
        };
        timer.Start();
        window.Show();
    }

    private async Task CycleThemeAsync()
    {
        _themeMode = _themeMode switch
        {
            "Monolith Glass" => "Glass Dark",
            "Glass Dark" => "Glass Light",
            "Glass Light" => "Studio",
            _ => "Monolith Glass"
        };
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FMAtlas");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "theme.txt"), _themeMode);
        }
        catch (Exception ex) { AtlasLog.Warning("Could not persist Atlas appearance preference: " + ex.Message); }
        var page = _activePage;
        BuildShell();
        await NavigateAsync(page);
    }

    private async Task ToggleNavigationAsync()
    {
        _navCollapsed = !_navCollapsed;
        var page = _activePage;
        BuildShell();
        await NavigateAsync(page);
    }

    private void ShowLiveScanPaused()
    {
        MessageBox.Show(this,
            "The current FM27 session is not ready for indexing. Load a supported career and keep Atlas open; it will connect automatically. The read-only scan is limited to two minutes and can be cancelled from the toolbar.",
            "FM Atlas · Career not ready", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private UIElement BuildSidebar()
    {
        var rail = new Border
        {
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(_themeMode == "Glass Light"
                        ? Color.FromArgb(238, 250, 253, 255)
                        : _themeMode == "Monolith Glass"
                            ? Color.FromArgb(232, 29, 31, 36)
                            : Color.FromArgb(224, 20, 31, 46), 0),
                    new GradientStop(_themeMode == "Glass Light"
                        ? Color.FromArgb(224, 228, 237, 246)
                        : _themeMode == "Monolith Glass"
                            ? Color.FromArgb(210, 17, 19, 23)
                            : Color.FromArgb(202, 12, 21, 34), 1)
                },
                new Point(0, 0), new Point(1, 1)),
            BorderBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(150, 255, 255, 255), 0),
                    new GradientStop(_themeMode == "Glass Light"
                        ? Color.FromArgb(105, 128, 158, 188)
                        : Color.FromArgb(92, 244, 155, 100), 1)
                },
                new Point(0, 0), new Point(0, 1)),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 0, 0),
                BlurRadius = 28,
                ShadowDepth = 0,
                Opacity = _themeMode == "Glass Light" ? 0.07 : 0.20
            }
        };
        var dock = new DockPanel { Margin = new Thickness(_navCollapsed ? 9 : 14, 15, _navCollapsed ? 9 : 14, 14) };
        rail.Child = dock;

        var brand = new Grid { Margin = new Thickness(2, 2, 2, 18) };
        brand.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        brand.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var logoLine = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var logoImage = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/FmAtlas;component/Assets/FM-Atlas-27-Icon.png", UriKind.Absolute)), Stretch = Stretch.UniformToFill };
        var logo = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(13), ClipToBounds = true, BorderBrush = Brush("#9DC8EF"), BorderThickness = new Thickness(1), Child = logoImage };
        logoLine.Children.Add(logo);
        var word = new StackPanel { Margin = new Thickness(10, 2, 0, 0), Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible };
        word.Children.Add(Txt("FM ATLAS", 16, true, Ink));
        word.Children.Add(Txt(_themeMode == "Monolith Glass" ? "MONOLITH GLASS" : "V8 · ATLAS LIQUID", 8, true, Muted));
        logoLine.Children.Add(word);
        Grid.SetColumn(logoLine, 0);
        brand.Children.Add(logoLine);

        var collapse = new Button
        {
            Content = _navCollapsed ? "›" : "‹",
            Width = 30,
            Height = 34,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(80, 150, 185, 225)),
            Foreground = Ink,
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 180, 210, 240)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = _navCollapsed ? "Expand side navigation and show workspace names." : "Collapse side navigation to an icon rail.",
            Template = RoundedButtonTemplate(10)
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
        AddNav("∑", "Stats Search");
        AddNav("▦", "Scouting Matrix");
        AddNav("◈", "Scouting Analytics");
        AddNav("↗", "Player Development");
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
        AddNav("▤", "World Editor");
        AddNav("▦", "Mass Edit");
        AddNav("◷", "Edit History");
        AddNavGroup("DATA & TOOLS");
        AddNav("⌘", "Atlas Assistant");
        AddNav("▧", "Settings");
        AddNav("◉", "Tester & Logs");
        AddNav("?", "Help & Tutorial");

        var bottom = new Border
        {
            Background = _themeMode == "Glass Light"
                ? new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
                : _themeMode == "Monolith Glass"
                    ? new SolidColorBrush(Color.FromArgb(210, 27, 29, 33))
                    : new SolidColorBrush(Color.FromArgb(110, 95, 125, 165)),
            BorderBrush = _themeMode == "Monolith Glass"
                ? new SolidColorBrush(Color.FromArgb(90, 226, 132, 80))
                : new SolidColorBrush(Color.FromArgb(85, 175, 205, 235)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(11),
            Margin = new Thickness(0, 12, 0, 0),
            ToolTip = "FM connection is optional. Offline save import remains the primary safe route."
        };
        var bottomStack = new StackPanel();
        if (!_navCollapsed)
        {
            bottomStack.Children.Add(Txt("GAME CONNECTION", 9, true, Teal));
            var stateRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 3) };
            stateRow.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = Brush("#D9A44D"), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            stateRow.Children.Add(Txt("Automatic · read-only", 10, true, Ink));
            bottomStack.Children.Add(stateRow);
            bottomStack.Children.Add(Txt("FM27 career detection and cancellable live indexing are active. Live editing remains disabled.", 8, false, Muted, wrap: true));
        }
        else
        {
            bottomStack.Children.Add(Txt("●", 14, true, Teal));
        }
        bottom.Child = bottomStack;
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);
        return rail;
    }

    private void AddNavGroup(string label)
    {
        if (_navPanel is null || _navCollapsed) return;
        _navPanel.Children.Add(Txt(label, 9, true, Teal) is var t ? Configure(t, new Thickness(7, 16, 0, 5)) : new TextBlock());
    }

    private static TextBlock Configure(TextBlock t, Thickness margin) { t.Margin = margin; return t; }

    private void AddNav(string icon, string label)
    {
        if (_navPanel is null) return;
        var glyph = Txt(icon, 15, true, TealDark);
        glyph.Width = 22;
        glyph.TextAlignment = TextAlignment.Center;
        var caption = Txt(_navCollapsed ? "" : label, 11, true, Ink);
        caption.Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(glyph);
        row.Children.Add(caption);
        var button = new Button
        {
            Tag = label,
            Content = row,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(_navCollapsed ? 11 : 12, 10, 10, 10),
            Margin = new Thickness(0, 2, 0, 2),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = PageDescription(label),
            MinHeight = 42
        };
        ToolTipService.SetInitialShowDelay(button, 350);
        ToolTipService.SetShowDuration(button, 30000);
        ToolTipService.SetShowOnDisabled(button, true);
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        System.Windows.Automation.AutomationProperties.SetHelpText(button, PageDescription(label));
        button.Template = RoundedButtonTemplate(13);
        button.Click += async (_, _) => await NavigateAsync(label);
        _navPanel.Children.Add(button);
    }

    private async Task NavigateAsync(string page)
    {
        if (_pageHost is null) return;
        if (_navigationBusy)
        {
            _queuedNavigation = page;
            return;
        }

        _navigationBusy = true;
        // Keep navigation responsive; a later click replaces the queued destination.
        try
        {
            _activePage = page;
            if (_pageTitle is not null) _pageTitle.Text = page;
            if (_pageSubTitle is not null) _pageSubTitle.Text = PageDescription(page);
            foreach (var button in _navPanel?.Children.OfType<Button>() ?? Enumerable.Empty<Button>())
            {
                var active = string.Equals(button.Tag?.ToString(), page, StringComparison.OrdinalIgnoreCase);
                button.Background = active ? Brush("#294765") : Brushes.Transparent;
                if (button.Content is StackPanel row)
                    foreach (var tb in row.Children.OfType<TextBlock>()) tb.Foreground = active ? Brush("#A9D6FF") : (tb.Text.Trim() == page ? Ink : Muted);
            }

            _pageHost.Content = NavigationLoadingPane(page);
            await Dispatcher.Yield(DispatcherPriority.Background);
            UIElement content = page switch
            {
                "Dashboard" => await DashboardPageAsync(),
                "Advanced Search" => await AdvancedSearchWorkspaceAsync(),
                "Players" => await PlayerWorkspaceAsync(false),
                "Recruitment" => await PlayerWorkspaceAsync(true),
                "Clubs" => await ClubsPageAsync(),
                "Staff" => await StaffPageAsync(),
                "Squad Architect" => await SquadArchitectPageAsync(),
                "Transfers" => await TransfersPageAsync(),
                "Tactical Lab" => await TacticalLabPageAsync(),
                "Player Development" => await PlayerDevelopmentPageAsync(),
                "Role Lab" => await RoleLabPageAsync(),
                "Shortlists" => await ShortlistsPageAsync(),
                "Live Editor" => await LiveEditorPageAsync(),
                "World Editor" => await WorldEditorPageAsync(),
                "Edit History" => await EditHistoryPageAsync(),
                "Atlas Assistant" => AssistantPage(),
                "Settings" => await SettingsPageAsync(),
                "Help & Tutorial" => TutorialPage(),
                "Analytics" => await AnalyticsPageAsync(),
                "Stats Search" => await StatsSearchPageAsync(),
                "Scouting Matrix" => await StatsSearchPageAsync(),
                "Scouting Analytics" => await ScoutingEnginePageAsync(),
                "Attribute Search" => await AttributeSearchPageAsync(),
                "Value Finder" => await ValueFinderPageAsync(),
                "Mass Edit" => await MassEditPageAsync(),
                "Tester & Logs" => await TesterLogsPageAsync(),
                _ => DashboardPageFallback()
            };
            _pageHost.Content = content;
            try { ApplyGlassControlTheme(content); }
            catch (Exception themeEx) { AtlasLog.Warning("Workspace glass-control styling was skipped: " + themeEx.Message); }
            if (SystemParameters.ClientAreaAnimation)
            {
                _pageHost.Opacity = 0;
                _pageHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                }, HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                _pageHost.BeginAnimation(UIElement.OpacityProperty, null);
                _pageHost.Opacity = 1;
            }
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Workspace navigation failed: " + page, ex);
            var panel = new StackPanel { Margin = new Thickness(26) };
            panel.Children.Add(Txt("WORKSPACE COULD NOT OPEN", 10, true, Brush("#A54E55")));
            panel.Children.Add(Txt(page, 24, true, Ink));
            panel.Children.Add(Txt(ex.Message, 12, false, Muted, wrap: true));
            panel.Children.Add(SoftButton("Return to Dashboard", async (_, _) => await NavigateAsync("Dashboard")));
            _pageHost.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
        finally
        {
            _navigationBusy = false;
            var queued = _queuedNavigation;
            _queuedNavigation = null;
            if (!string.IsNullOrWhiteSpace(queued) && !string.Equals(queued, page, StringComparison.OrdinalIgnoreCase))
                await NavigateAsync(queued);
        }
    }

    private static UIElement NavigationLoadingPane(string page)
    {
        var panel = new StackPanel { Margin = new Thickness(28), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Txt("OPENING WORKSPACE", 10, true, Brush("#C6F36B")));
        panel.Children.Add(Txt(page, 24, true, Brush("#E8EEF4")));
        panel.Children.Add(Txt("Loading this workspace. Navigation stays available; your latest destination will open next.", 12, false, Brush("#9EADB9"), wrap: true));
        return panel;
    }

    private void ShowGlobalSearch(string? initialQuery = null)
    {
        var search = new GlobalSearchWindow(_database, this, hit => _ = OpenGlobalSearchHitAsync(hit), initialQuery);
        search.ShowDialog();
    }

    private async Task OpenGlobalSearchHitAsync(GlobalSearchHit hit)
    {
        switch (hit.Record)
        {
            case PlayerRecord player:
                await NavigateAsync("Players");
                _selectedPlayer = player;
                try { await ShowPlayerProfileAsync(player); }
                catch (Exception ex)
                {
                    ClearProfile("The player record could not be opened: " + ex.Message);
                }
                break;
            case AtlasClubRecord club:
                _pendingClubSourceId = club.SourceId;
                _pendingClubSearchQuery = club.Name;
                await NavigateAsync("Clubs");
                break;
            case AtlasStaffRecord staff:
                _pendingStaffSourceId = staff.SourceId;
                _pendingStaffSearchQuery = staff.Name;
                await NavigateAsync("Staff");
                break;
        }
    }

    private static string PageDescription(string page) => page switch
    {
        "Dashboard" => "Your career at a glance · data coverage · import health",
        "Advanced Search" => "Choose player identity, attributes/personality or performance-stat criteria",
        "Players" => "Player intelligence · name/position/CA/PA search · full profile",
        "Recruitment" => "Build an explainable shortlist from your imported career data",
        "Squad Architect" => "Inspect club squads, age distribution and positional coverage",
        "Clubs" => "Club directory · squad links · available save data",
        "Staff" => "Staff directory · coaching and staff fields where supported by the source",
        "Analytics" => "Season and match performance · metrics actually present in your save",
        "Stats Search" => "Multi-condition statistical discovery · grouped source metrics · per-90 sample controls",
        "Scouting Matrix" => "Reusable multi-metric scouting profiles · source-backed performance search · minutes and per-90 controls",
        "Scouting Analytics" => "StatsBomb event data · Wyscout CSV · player comparisons · percentiles · recruitment discovery",
        "Attribute Search" => "Build precise player profiles with up to 60 simultaneous 1–20 ratings",
        "Value Finder" => "Spot players whose ability and potential rank ahead of their market-value percentile",
        "Mass Edit" => "Bulk local database edits · preview and audit before applying",
        "Transfers" => "Transfer candidate discovery from available player and contract data",
        "Tactical Lab" => "Tactical and role analysis · only where the source provides enough verified inputs",
        "Player Development" => "CA/PA headroom screening · source-backed player snapshots, not predicted growth",
        "Role Lab" => "Transparent IP/OOP role-fit estimates from imported player attributes",
        "Tester & Logs" => "System checks · diagnostic export · application and build logs",
        "Shortlists" => "Manage your local recruitment lists",
        "Live Editor" => "Editing boundary · live game writing is not enabled in this build",
        "World Editor" => "Explore every indexed FM save directory · edit source-backed scalar fields locally with audit and undo",
        "Edit History" => "Local FM Atlas data changes and audit trail",
        "Atlas Assistant" => "Search your local career snapshot using natural-language-style filters",
        "Settings" => "Data sources · save bridge · local storage · diagnostics",
        "Help & Tutorial" => "Quick start · player search · profiles · save imports · troubleshooting",
        _ => "FM Atlas 27"
    };

    private async Task<UIElement> DashboardPageAsync()
    {
        var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var heroGrid = new Grid();
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var heroLeft = new StackPanel();
        heroLeft.Children.Add(Txt("FM ATLAS · MONOLITH GLASS", 9, true, Teal));
        heroLeft.Children.Add(Txt("Your Football Manager intelligence workspace.", 25, true, Ink));
        heroLeft.Children.Add(Txt("Load an FM27 career and Atlas automatically indexes supported player, staff, club and manager identity data. The live scan is read-only, cancellable and limited to two minutes; offline save import adds historical and match data.", 11, false, Muted, wrap: true));
        heroGrid.Children.Add(heroLeft);
        var heroActions = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Width = 185 };
        heroActions.Children.Add(WhiteButton("Import career save", async (_, _) => await ImportCareerSaveAsync()));
        var liveButton = new Button { Content = "Connect / Refresh career", Margin = new Thickness(3), Padding = new Thickness(12, 9, 12, 9), Foreground = Brush("#0D1117"), Background = Brush("#F08A42"), BorderBrush = Brush("#C86D34"), Template = RoundedButtonTemplate(12), FontWeight = FontWeights.SemiBold };
        liveButton.ToolTip = "Connect to the current supported FM27 career or refresh the live index.";
        liveButton.Click += async (_, _) => await LoadLiveCareerAsync();
        heroActions.Children.Add(liveButton);
        Grid.SetColumn(heroActions, 1);
        heroGrid.Children.Add(heroActions);
        root.Children.Add(Card(heroGrid, 23, new Thickness(0, 0, 0, 14)));

        var cards = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 14) };
        cards.SizeChanged += (_, e) => { cards.Columns = e.NewSize.Width >= 1280 ? 4 : e.NewSize.Width >= 720 ? 2 : 1; };
        cards.Children.Add(MetricCard("PLAYER RECORDS", (metrics.SavePlayers + metrics.LivePlayers).ToString("N0"), metrics.SavePlayers > 0 ? $"{metrics.SavePlayers:N0} save snapshot · {metrics.LivePlayers:N0} live identities (sources may overlap)" : "Live identities only · not full attribute profiles", "♟"));
        cards.Children.Add(MetricCard("CLUB RECORDS", (metrics.SaveClubs + metrics.LiveClubs).ToString("N0"), metrics.SaveClubs > 0 ? $"{metrics.SaveClubs:N0} save snapshot · {metrics.LiveClubs:N0} live identities (sources may overlap)" : "Live club identities only · details depend on save import", "◇"));
        var activeStaffCount = IsCurrentLiveCareer(metrics) ? metrics.LiveStaff : metrics.SaveStaff;
        cards.Children.Add(MetricCard("STAFF", activeStaffCount.ToString("N0"), IsCurrentLiveCareer(metrics) ? "Live FM27 staff identities" : metrics.SaveStaff > 0 ? "Save snapshot records" : "Not yet populated", "♙"));
        cards.Children.Add(MetricCard("ATTRIBUTE FACTS", metrics.LocalAttributeFacts.ToString("N0"), "Imported local values", "▤"));
        Grid.SetRow(cards, 1); root.Children.Add(cards);

        var lower = new Grid(); lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) }); lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        var coverage = Card(new StackPanel(), 17); var coverageStack = (StackPanel)coverage.Child;
        coverageStack.Children.Add(Txt("DATA COVERAGE", 9, true, Teal)); coverageStack.Children.Add(Txt("What Atlas currently knows", 19, true, Ink));
        AddCoverageRow(coverageStack, "Live player identities", metrics.LivePlayers, "FM27 in-memory read-only importer");
        AddCoverageRow(coverageStack, "Save snapshot players", metrics.SavePlayers, metrics.SavePlayers > 0 ? "FMSave local parser" : "Import a local career save copy");
        AddCoverageRow(coverageStack, "Save snapshot staff", metrics.SaveStaff, "Staff identity rows; attribute coverage depends on the parser");
        AddCoverageRow(coverageStack, "Imported attribute values", metrics.LocalAttributeFacts, "Only imported facts; no generated ratings");
        coverageStack.Children.Add(Txt("Live CA/PA and attribute reads remain build-specific. Candidate memory offsets are not promoted to verified fields automatically.", 10, false, Muted, wrap: true));
        Grid.SetColumn(coverage, 0); lower.Children.Add(coverage);

        var status = Card(new StackPanel(), 17); var statusStack = (StackPanel)status.Child;
        statusStack.Children.Add(Txt(IsCurrentLiveCareer(metrics) ? "ACTIVE FM27 CAREER" : "CAREER SNAPSHOT", 9, true, Teal));
        statusStack.Children.Add(Txt(IsCurrentLiveCareer(metrics) ? "Automatically connected live career" : "Latest offline data import", 19, true, Ink));
        statusStack.Children.Add(Txt(IsCurrentLiveCareer(metrics) ? $"FM27 live · {metrics.LivePlayers:N0} players · {metrics.LiveStaff:N0} staff" : metrics.LastSaveFileName ?? "No save snapshot imported yet", 12, true, Ink, wrap: true));
        if (!string.IsNullOrWhiteSpace(metrics.CurrentManager) && IsCurrentLiveCareer(metrics))
            statusStack.Children.Add(Txt($"Human manager: {metrics.CurrentManager}{(string.IsNullOrWhiteSpace(metrics.CurrentManagerClub) ? "" : " · " + metrics.CurrentManagerClub)}{(string.IsNullOrWhiteSpace(metrics.CurrentManagerNation) ? "" : " · " + metrics.CurrentManagerNation)}", 10, true, Teal, wrap: true));
        else if (IsCurrentLiveCareer(metrics))
            statusStack.Children.Add(Txt("Human manager not uniquely identified in this live snapshot.", 10, true, Brush("#D18A3A"), wrap: true));
        statusStack.Children.Add(Txt(metrics.LastSaveImportUtc is null ? "—" : "Imported " + FormatDate(metrics.LastSaveImportUtc), 10, false, Muted));
        if (!string.IsNullOrWhiteSpace(metrics.SaveBuild)) statusStack.Children.Add(Txt($"{metrics.SaveGame ?? "Football Manager"} · {metrics.SaveBuild} · In-game date {metrics.SaveGameDate ?? "unknown"}", 10, true, Teal, wrap: true));
        if (metrics.SaveBuildKnown == false) statusStack.Children.Add(Txt("UNVERIFIED BUILD LAYOUT — inspect reader report before relying on fields.", 9, true, Brush("#B45B2F"), wrap: true));
        if (string.Equals(metrics.SaveGame, "FM27", StringComparison.OrdinalIgnoreCase)) statusStack.Children.Add(Txt("FM27 field meanings remain unconfirmed by the parser. Compare important values with the game.", 9, false, Muted, wrap: true));
        statusStack.Children.Add(new Border { Height = 1, Background = Brush("#D9E5EF"), Margin = new Thickness(0, 12, 0, 10) });
        statusStack.Children.Add(Txt("Live connector", 11, true, Ink)); statusStack.Children.Add(Txt(metrics.LastLiveImportUtc is null ? "No successful live import recorded" : "Last completed " + FormatDate(metrics.LastLiveImportUtc), 10, false, Muted));
        statusStack.Children.Add(Txt("The local database at %LOCALAPPDATA%\\FmAtlas\\scouting.db is retained. Offline save snapshots remain versioned; a new live career refresh replaces only the active LIVE index and keeps offline records, shortlists and projects.", 10, false, Muted, wrap: true));
        Grid.SetColumn(status, 1); lower.Children.Add(status);
        Grid.SetRow(lower, 2); root.Children.Add(lower);
        return root;
    }

    private async Task<UIElement> PlayerWorkspaceAsync(bool recruitmentMode)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _playerWorkspaceGrid = root;
        _playerResultsColumn = new ColumnDefinition { Width = new GridLength(_profileExpanded ? 0 : .82, GridUnitType.Star) };
        _playerProfileColumn = new ColumnDefinition { Width = new GridLength(_profileExpanded ? 1 : 1.18, GridUnitType.Star) };
        root.ColumnDefinitions.Add(_playerResultsColumn);
        root.ColumnDefinitions.Add(_playerProfileColumn);

        var searchCard = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 10));
        var searchStack = (StackPanel)searchCard.Child;
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headline = new StackPanel();
        headline.Children.Add(Txt(recruitmentMode ? "RECRUITMENT WORKSPACE" : "PLAYER DATABASE", 9, true, Teal));
        _playerCountText = Txt("Search the career database", 18, true, Ink);
        headline.Children.Add(_playerCountText);
        headline.Children.Add(Txt("Search results are paged and virtualized. The 40K identity count is not a count of fully decoded player profiles or attribute sets.", 10, false, Muted, wrap: true));
        top.Children.Add(headline);
        var headButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        headButtons.Children.Add(SoftButton("⌕ Advanced Search", (_, _) => ShowGlobalSearch()));
        headButtons.Children.Add(SoftButton("Import report", async (_, _) => await ImportIntelligenceReportAsync()));
        headButtons.Children.Add(PrimaryButton("＋ Shortlist", async (_, _) => await AddSelectedToShortlistAsync()));
        Grid.SetColumn(headButtons, 1); top.Children.Add(headButtons);
        searchStack.Children.Add(top);

        var searchRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _playerSearch = Input("Search player, club, nation or UID");
        _playerSearch.Height = 40; _playerSearch.FontSize = 12; _playerSearch.Margin = new Thickness(0, 2, 8, 4);
        _playerSearch.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; _playerOffset = 0; await RefreshPlayerResultsAsync(); } };
        Grid.SetColumn(_playerSearch, 0); searchRow.Children.Add(_playerSearch);
        var positionOptions = new[] { "Any position", "ATTACKERS", "MIDFIELDERS", "DEFENDERS", "GOALKEEPERS" }
            .Concat(PositionSuitabilityService.PositionCodes)
            .Append("ST") // Preserve the legacy centre-forward filter label; current FM27 uses STC.
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _positionPicker = new ComboBox { ItemsSource = positionOptions, SelectedIndex = 0, Margin = new Thickness(0, 2, 8, 4), Padding = new Thickness(9), MinHeight = 40, FontSize = 11 };
        Grid.SetColumn(_positionPicker, 1); searchRow.Children.Add(_positionPicker);
        var searchButton = PrimaryButton("Search", async (_, _) => { _playerOffset = 0; await RefreshPlayerResultsAsync(); });
        searchButton.MinWidth = 88; searchButton.Height = 40; Grid.SetColumn(searchButton, 2); searchRow.Children.Add(searchButton);
        searchStack.Children.Add(searchRow);

        var positionRibbon = new WrapPanel { Margin = new Thickness(0, 7, 0, 1) };
        foreach (var category in new[] { "All", "Attackers", "Midfielders", "Defenders", "Goalkeepers" })
        {
            var chip = new Button { Content = category.ToUpperInvariant(), Tag = category, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 7, 0), Background = category == "All" ? Brush("#2D75B6") : Brush("#EAF1F8"), Foreground = category == "All" ? Brushes.White : Brush("#486477"), BorderBrush = Brushes.Transparent, FontWeight = FontWeights.SemiBold, FontSize = 9, Template = RoundedButtonTemplate(18), Cursor = Cursors.Hand };
            chip.Click += async (_, _) =>
            {
                _positionPicker!.SelectedItem = category switch { "Attackers" => "ATTACKERS", "Midfielders" => "MIDFIELDERS", "Defenders" => "DEFENDERS", "Goalkeepers" => "GOALKEEPERS", _ => "Any position" };
                foreach (var btn in positionRibbon.Children.OfType<Button>()) { var active = btn == chip; btn.Background = active ? Brush("#2D75B6") : Brush("#EAF1F8"); btn.Foreground = active ? Brush("#0D1117") : Brush("#9EADB9"); }
                _playerOffset = 0;
                await RefreshPlayerResultsAsync();
            };
            positionRibbon.Children.Add(chip);
        }
        searchStack.Children.Add(positionRibbon);

        // Quick FMRTE-style ability bounds. They feed the same SQL filters for both
        // offline save rows and the live FM27 identity index; negative PA ranges are valid.
        var abilityFilters = new WrapPanel { Margin = new Thickness(0, 8, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        _playerMinCaInput = Input("Min CA"); _playerMinCaInput.Width = 78; _playerMinCaInput.MaxLength = 3;
        _playerMaxCaInput = Input("Max CA"); _playerMaxCaInput.Width = 78; _playerMaxCaInput.MaxLength = 3;
        _playerMinPaInput = Input("Min PA"); _playerMinPaInput.Width = 78; _playerMinPaInput.MaxLength = 3;
        _playerMaxPaInput = Input("Max PA"); _playerMaxPaInput.Width = 78; _playerMaxPaInput.MaxLength = 3;
        abilityFilters.Children.Add(FilterControl("CA MIN · 1–200", _playerMinCaInput, 88));
        abilityFilters.Children.Add(FilterControl("CA MAX · 1–200", _playerMaxCaInput, 88));
        abilityFilters.Children.Add(FilterControl("PA MIN · -10–200", _playerMinPaInput, 88));
        abilityFilters.Children.Add(FilterControl("PA MAX · -10–200", _playerMaxPaInput, 88));
        abilityFilters.Children.Add(SoftButton("Clear CA/PA", (_, _) =>
        {
            _playerMinCaInput.Text = ""; _playerMaxCaInput.Text = "";
            _playerMinPaInput.Text = ""; _playerMaxPaInput.Text = "";
            _playerOffset = 0;
            _ = RefreshPlayerResultsAsync();
        }));
        searchStack.Children.Add(abilityFilters);

        Grid.SetRow(searchCard, 0); Grid.SetColumnSpan(searchCard, 2); root.Children.Add(searchCard);

        var listCard = Card(new DockPanel(), 16, new Thickness(0, 0, 9, 0));
        var listDock = (DockPanel)listCard.Child;
        var listHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        listHeader.Children.Add(Txt("PLAYER RESULTS", 9, true, Teal));
        listHeader.Children.Add(Txt("Select a row to open its profile tab · double-click to focus", 10, false, Muted));
        listHeader.Children.Add(Txt("SOURCE / CONFIDENCE is shown on each row; profile-matched live CA/PA and fields remain provisional until checked against the FM UI.", 9, false, Brush("#A06B24"), wrap: true));
        DockPanel.SetDock(listHeader, Dock.Top); listDock.Children.Add(listHeader);

        _playerResultsGrid = CreateGrid();
        _playerResultsGrid.FontSize = 11;
        _playerResultsGrid.RowHeight = 40;
        _playerResultsGrid.EnableRowVirtualization = true;
        _playerResultsGrid.EnableColumnVirtualization = true;
        VirtualizingPanel.SetIsVirtualizing(_playerResultsGrid, true);
        VirtualizingPanel.SetVirtualizationMode(_playerResultsGrid, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(_playerResultsGrid, true);
        _playerResultsGrid.Columns.Add(Column("PLAYER", "Name", 1.35));
        _playerResultsGrid.Columns.Add(Column("CLUB", "Club", 1.15));
        _playerResultsGrid.Columns.Add(Column("AGE", "Age", .45));
        _playerResultsGrid.Columns.Add(Column("POS", "Position", .5));
        _playerResultsGrid.Columns.Add(Column("LEAGUE", "League", .82));
        _playerResultsGrid.Columns.Add(Column("CA", "DisplayAbility", .38));
        _playerResultsGrid.Columns.Add(Column("PA", "DisplayPotential", .38));
        _playerResultsGrid.Columns.Add(Column("NATION", "Nation", .7));
        _playerResultsGrid.Columns.Add(Column("HOME-GROWN · CLUB", "DisplayHomeGrownClub", .72));
        _playerResultsGrid.Columns.Add(Column("HOME-GROWN · NATION", "DisplayHomeGrownNation", .72));
        _playerResultsGrid.Columns.Add(Column("SOURCE / CONFIDENCE", "DataOrigin", .95));
        _playerResultsGrid.ItemsSource = _players;
        _playerResultsGrid.SelectionChanged += async (_, _) =>
        {
            if (_playerResultsGrid.SelectedItem is PlayerRecord player)
            {
                _selectedPlayer = player;
                await ShowPlayerProfileAsync(player);
            }
        };
        listDock.Children.Add(_playerResultsGrid);

        var pageBar = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        _playerPageStatus = Txt("Search to begin", 9, false, Muted);
        DockPanel.SetDock(_playerPageStatus, Dock.Left); pageBar.Children.Add(_playerPageStatus);
        var pageButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _previousPlayerPageButton = SoftButton("← Previous", async (_, _) => { _playerOffset = Math.Max(0, _playerOffset - PlayerPageSize); await RefreshPlayerResultsAsync(); });
        _nextPlayerPageButton = SoftButton("Next →", async (_, _) => { _playerOffset += PlayerPageSize; await RefreshPlayerResultsAsync(); });
        _previousPlayerPageButton.IsEnabled = false; _nextPlayerPageButton.IsEnabled = false;
        pageButtons.Children.Add(_previousPlayerPageButton); pageButtons.Children.Add(_nextPlayerPageButton);
        DockPanel.SetDock(pageButtons, Dock.Right); pageBar.Children.Add(pageButtons);
        DockPanel.SetDock(pageBar, Dock.Bottom); listDock.Children.Add(pageBar);
        Grid.SetRow(listCard, 2); Grid.SetColumn(listCard, 0); root.Children.Add(listCard);

        var profileCard = Card(new TabControl { FontSize = 11, Background = Brush("#171F28") }, 16, new Thickness(0, 0, 0, 0));
        _profileTabs = (TabControl)profileCard.Child;
        _profileTabs.SelectionChanged += (_, _) =>
        {
            if (_profileTabs.SelectedItem is TabItem selected && selected.Content is ScrollViewer scroller && scroller.Content is StackPanel selectedPanel)
                _profilePanel = selectedPanel;
        };
        AddWelcomeProfileTab();
        Grid.SetRow(profileCard, 2); Grid.SetColumn(profileCard, 1); root.Children.Add(profileCard);

        // Restore previously opened profiles after the workspace is displayed. Do not make
        // navigation wait for a chain of profile queries when the user switches workspaces.
        var profilesToRestore = _openPlayerRecords.Values.ToList();
        if (_profileExpanded) ToggleProfileExpanded(true);
        if (profilesToRestore.Count > 0)
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => _ = RestoreOpenPlayerProfilesSafelyAsync(profilesToRestore)));

        await Task.CompletedTask;
        return root;
    }

    private async Task RestoreOpenPlayerProfilesSafelyAsync(IReadOnlyList<PlayerRecord> profiles)
    {
        try
        {
            await RestoreOpenPlayerProfilesAsync(profiles);
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Deferred player-profile restoration failed.", ex);
        }
    }

    private async Task RestoreOpenPlayerProfilesAsync(IReadOnlyList<PlayerRecord> profiles)
    {
        foreach (var profile in profiles)
        {
            if (_activePage is not ("Players" or "Recruitment") || _profileTabs is null)
                return;
            try { await ShowPlayerProfileAsync(profile); }
            catch (Exception ex) { AtlasLog.Warning("Deferred profile restore skipped for " + profile.Name + ": " + ex.Message); }
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
    }

    private void AddWelcomeProfileTab()
    {
        if (_profileTabs is null) return;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(Txt("PLAYER CENTRE", 9, true, Teal));
        panel.Children.Add(Txt("Open a player profile", 23, true, Ink));
        panel.Children.Add(Txt("Use Global Search (Ctrl+F) or the Players search field to find a player. Each opened record gets its own closable tab; use Ctrl+W to close the selected profile. Your open profiles are kept while you move between workspaces.", 11, false, Muted, wrap: true));
        panel.Children.Add(Txt("The full profile follows the familiar Football Manager order: Technical, Set Pieces, Mental, Physical, Goalkeeping and Hidden. Atlas displays imported values only; it does not fabricate ratings.", 10, false, Muted, wrap: true));
        var tab = new TabItem { Header = "Welcome", Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Tag = "__welcome" };
        _profileTabs.Items.Add(tab); _profileTabs.SelectedItem = tab; _profilePanel = panel;
    }

    private static FrameworkElement FilterControl(string label, FrameworkElement control, double width)
    {
        var panel = new StackPanel { Width = width, Margin = new Thickness(0, 0, 8, 0) };
        panel.Children.Add(Txt(label, 8, true, Muted)); panel.Children.Add(control); return panel;
    }

    private static bool TryParseOptionalInt(string? raw, int minimum, int maximum, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed < minimum || parsed > maximum) return false;
        value = parsed;
        return true;
    }

    private async Task ImportIntelligenceReportAsync()
    {
        var open = new OpenFileDialog { Title = "Import a verified FM Atlas player intelligence report", Filter = "CSV reports (*.csv)|*.csv|All files (*.*)|*.*", CheckFileExists = true };
        if (open.ShowDialog(this) != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            AtlasLog.Info("Starting player intelligence CSV import.");
            var result = await RunDatabaseAsync(() => _database.ImportFullIntelligenceReportAsync(open.FileName));
            AtlasLog.Info($"Player intelligence CSV import completed: matched={result.MatchedPlayers}, facts={result.FactsImported}.");
            MessageBox.Show(this, $"Intelligence report imported.\n\nMatched players: {result.MatchedPlayers:N0}\nFacts imported: {result.FactsImported:N0}\n\nOnly fields present in the report were imported.", "FM Atlas · Intelligence report", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshPlayerResultsAsync();
        }
        catch (Exception ex) { MessageBox.Show(this, "The intelligence report was not imported.\n\n" + ex.Message, "FM Atlas · Report import", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { Mouse.OverrideCursor = null; }
    }

    private async Task RefreshPlayerResultsAsync()
    {
        if (_playerResultsGrid is null || _playerCountText is null) return;
        var term = _playerSearch?.Text.Trim() ?? string.Empty;
        var pos = _positionPicker?.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(pos) || pos == "Any position") pos = null;
        if (!TryParseOptionalInt(_playerMinCaInput?.Text, 1, 200, out var minCa)
            || !TryParseOptionalInt(_playerMaxCaInput?.Text, 1, 200, out var maxCa)
            || !TryParseOptionalInt(_playerMinPaInput?.Text, -10, 200, out var minPa)
            || !TryParseOptionalInt(_playerMaxPaInput?.Text, -10, 200, out var maxPa))
        {
            _playerPageStatus!.Text = "CA must be 1–200; PA must be -10–200. Clear invalid values and search again.";
            return;
        }
        if ((minCa.HasValue && maxCa.HasValue && minCa > maxCa)
            || (minPa.HasValue && maxPa.HasValue && minPa > maxPa))
        {
            _playerPageStatus!.Text = "Each minimum must be less than or equal to its maximum.";
            return;
        }
        var hasAbilityFilters = minCa.HasValue || maxCa.HasValue || minPa.HasValue || maxPa.HasValue;
        var hasPositionFilter = pos is not null;
        if (term.Length < 2 && !hasAbilityFilters && !hasPositionFilter)
        {
            ++_playerSearchRequestId; // invalidate any search still completing in the background
            Mouse.OverrideCursor = null;
            _players.Clear();
            _playerCountText.Text = "Search the career database";
            _playerPageStatus!.Text = "Enter two characters or choose a position / CA / PA filter · the full player database is not loaded into the screen";
            _previousPlayerPageButton!.IsEnabled = false;
            _nextPlayerPageButton!.IsEnabled = false;
            ClearProfile("Choose a player from search results. Your open profile tabs will remain available.");
            return;
        }

        var requestId = ++_playerSearchRequestId;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (_playerPageStatus is not null) _playerPageStatus.Text = "Searching…";
            // Query enough from each source to construct one correctly ordered merged page.
            // Applying the same OFFSET independently to both sources creates skipped/duplicated
            // records whenever save and live rows overlap.
            var mergedFetchLimit = Math.Clamp(_playerOffset + PlayerPageSize + 1, PlayerPageSize + 1, 100000);
            var localTask = RunDatabaseAsync(() => _database.SearchAsync(term, null, null, pos, null, null, null,
                minCa: minCa, maxCa: maxCa, minPa: minPa, maxPa: maxPa, limit: mergedFetchLimit, offset: 0));
            var liveTask = RunDatabaseAsync(() => _database.SearchLivePlayersAsync(term, mergedFetchLimit,
                ageAsOf: DateTime.Today, offset: 0, minCa: minCa, maxCa: maxCa, minPa: minPa, maxPa: maxPa, positionFilter: pos));
            var localCountTask = RunDatabaseAsync(() => _database.CountLocalPlayersAsync(term, minCa, maxCa, minPa, maxPa, pos));
            var liveCountTask = RunDatabaseAsync(() => _database.CountLivePlayersAsync(term, minCa, maxCa, minPa, maxPa, pos));
            await Task.WhenAll(localTask, liveTask, localCountTask, liveCountTask);

            var local = await localTask;
            var live = await liveTask;
            // Merge/deduplicate/sort on a worker too: deep pages can contain tens of thousands
            // of candidates even though only one page of rows is ultimately displayed.
            var (results, mergedHasMore) = await Task.Run(() =>
            {
                static string IdentityKey(PlayerRecord item) =>
                    item.Name.Trim().ToUpperInvariant() + "|" + (item.Club ?? string.Empty).Trim().ToUpperInvariant() + "|" + (item.Age?.ToString() ?? "?");
                static int Completeness(PlayerRecord item) =>
                    (item.Age.HasValue ? 1 : 0) + (item.Nation is not null ? 1 : 0) +
                    (item.League is not null ? 1 : 0) + (item.Position is not null ? 1 : 0) +
                    (item.CurrentAbility.HasValue ? 1 : 0) + (item.PotentialAbility.HasValue ? 1 : 0) +
                    (item.Value.HasValue ? 1 : 0) + (item.PreferredFoot is not null ? 1 : 0) +
                    (item.LeftFoot.HasValue ? 1 : 0) + (item.RightFoot.HasValue ? 1 : 0) +
                    (item.HomeGrownClub.HasValue ? 1 : 0) + (item.HomeGrownNation.HasValue ? 1 : 0);
                // Prefer the richest source row when the same player appears in both indexes.
                var merged = local.Concat(live)
                    .GroupBy(IdentityKey, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.OrderByDescending(Completeness).First())
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Club, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var hasMore = merged.Count > _playerOffset + PlayerPageSize;
                var page = merged.Skip(_playerOffset).Take(PlayerPageSize).ToList();
                return (page, hasMore);
            });
            var countLocal = await localCountTask;
            var countLive = await liveCountTask;
            if (requestId != _playerSearchRequestId) return;
            // The counts describe source rows, not unique entities; they are displayed as such.
            var total = countLocal + countLive;

            _players.Clear();
            foreach (var item in results) _players.Add(item);
            var filterSummary = string.Join(" · ", new[]
            {
                pos is null ? null : $"Position {pos}",
                minCa.HasValue || maxCa.HasValue ? $"CA {minCa?.ToString() ?? "any"}–{maxCa?.ToString() ?? "any"}" : null,
                minPa.HasValue || maxPa.HasValue ? $"PA {minPa?.ToString() ?? "any"}–{maxPa?.ToString() ?? "any"}" : null
            }.Where(x => x is not null));
            _playerCountText.Text = $"{total:N0} matching source records · {countLive:N0} live identities · {countLocal:N0} save/local records";
            _playerPageStatus!.Text = results.Count == 0
                ? "No matches · adjust the search or filters"
                : $"Showing {_playerOffset + 1:N0}–{_playerOffset + results.Count:N0} · {countLive:N0} live · {countLocal:N0} save/local{(filterSummary.Length == 0 ? "" : " · " + filterSummary)}";
            _previousPlayerPageButton!.IsEnabled = _playerOffset > 0;
            _nextPlayerPageButton!.IsEnabled = results.Count == PlayerPageSize && mergedHasMore;

            if (results.Count == 0) ClearProfile("No records matched. Check spelling, try a shorter term, or use Advanced Search.");
            else if (_playerResultsGrid.SelectedItem is null) _playerResultsGrid.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            if (requestId == _playerSearchRequestId) _playerPageStatus!.Text = "Search failed: " + ex.Message;
        }
        finally { if (requestId == _playerSearchRequestId) Mouse.OverrideCursor = null; }
    }

    private void RenderPlayerCards()
    {
        // Compatibility shim retained for existing callers. DataGrid owns its virtualized containers.
        if (_playerResultsGrid is not null) _playerResultsGrid.ItemsSource = _players;
    }



    private async Task<UIElement> BuildLiveEntityEditorAsync(
        LiveEntityMemoryEditService.EntityKind entityType, string sourceId, string displayName)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 16, 0, 10) };
        panel.Children.Add(Txt("LIVE FM27 EDITOR · " + entityType.ToString().ToUpperInvariant(), 9, true, Teal));
        panel.Children.Add(Txt(
            "These values are read from the currently loaded FM27 career. Apply writes the selected, verified fields into the running game and reads each value back. Use a separate offline career copy for first testing, then save inside Football Manager to commit the changes to the career file.",
            9, false, Muted, wrap: true));

        if (!_editorEnabled)
        {
            panel.Children.Add(Txt("Enable the Editor at the top of this profile to edit the live career. Atlas-local overrides remain separate.", 9, false, Muted, wrap: true));
            return panel;
        }
        if (!sourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase)
            || !uint.TryParse(sourceId["LIVE:".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid))
        {
            panel.Children.Add(Txt("This record is not linked to a live FM27 UID. Live writing is available only for the current loaded career, not an offline import.", 9, false, Muted, wrap: true));
            return panel;
        }

        var rows = new ObservableCollection<LiveEntityEditView>();
        var status = Txt("Reading the current FM27 values…", 9, false, Muted, wrap: true);
        panel.Children.Add(status);
        try
        {
            var processId = await Task.Run(() => LiveEntityMemoryEditService.FindSupportedProcessId());
            var catalogue = LiveEntityMemoryEditService.GetEditableFields(entityType);
            var current = await Task.Run(() => LiveEntityMemoryEditService.ReadCurrentValues(
                processId, entityType, uid, displayName, catalogue.Select(x => x.Key).ToList()));
            var descriptors = catalogue.Where(x => current.ContainsKey(x.Key)).ToList();
            foreach (var field in descriptors.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var value = current[field.Key];
                rows.Add(new LiveEntityEditView
                {
                    FieldKey = field.Key,
                    Label = field.Label,
                    CurrentValue = value,
                    TargetValue = value.ToString(CultureInfo.InvariantCulture)
                });
            }

            if (rows.Count == 0)
            {
                status.Text = "No editable live fields could be resolved for this record. It may lack a contract, full finance record, or a verified stadium object.";
                return panel;
            }

            status.Text = rows.Count.ToString("N0", CultureInfo.InvariantCulture)
                + " live fields resolved · optional records omitted where FM does not provide them.";
            var grid = CreateGrid();
            grid.IsReadOnly = false;
            grid.CanUserAddRows = false;
            grid.MinHeight = 180;
            grid.MaxHeight = 520;
            var fieldColumn = Column("FM27 FIELD", nameof(LiveEntityEditView.Label), 1.3);
            fieldColumn.IsReadOnly = true;
            grid.Columns.Add(fieldColumn);
            var currentColumn = Column("CURRENT", nameof(LiveEntityEditView.CurrentValue), .55);
            currentColumn.IsReadOnly = true;
            grid.Columns.Add(currentColumn);
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "TARGET",
                Binding = new Binding(nameof(LiveEntityEditView.TargetValue))
                    { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(.7, DataGridLengthUnitType.Star)
            });
            grid.ItemsSource = rows;
            panel.Children.Add(grid);

            LiveEntityMemoryEditService.BatchPlan? stagedPlan = null;
            LiveEntityMemoryEditService.BatchReceipt? appliedReceipt = null;
            Button? applyButton = null;
            Button? undoButton = null;
            var stageButton = PrimaryButton("Stage live changes", async (_, _) =>
            {
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
                grid.CommitEdit(DataGridEditingUnit.Row, true);
                var changes = new List<LiveEntityMemoryEditService.TargetValue>();
                foreach (var row in rows)
                {
                    if (!int.TryParse(row.TargetValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var target))
                    {
                        MessageBox.Show(this, "Enter a whole number for " + row.FieldKey + ".", "FM27 live editor", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (target != row.CurrentValue)
                        changes.Add(new LiveEntityMemoryEditService.TargetValue(row.FieldKey, target));
                }
                if (changes.Count == 0)
                {
                    MessageBox.Show(this, "There are no changed target values to stage.", "FM27 live editor", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    stagedPlan = await Task.Run(() => LiveEntityMemoryEditService.StageBatch(
                        processId, entityType, uid, displayName, changes));
                    status.Text = "STAGED · " + stagedPlan.Values.Count.ToString("N0", CultureInfo.InvariantCulture)
                        + " field(s) · " + stagedPlan.ProfileId + " · no memory written";
                    applyButton!.IsEnabled = true;
                    undoButton!.IsEnabled = false;
                    var preview = string.Join(Environment.NewLine, stagedPlan.Values.Take(30).Select(x =>
                        x.FieldKey + ": " + x.CurrentValue.ToString(CultureInfo.InvariantCulture)
                        + " → " + x.TargetValue.ToString(CultureInfo.InvariantCulture)));
                    if (stagedPlan.Values.Count > 30)
                        preview += Environment.NewLine + "… and " + (stagedPlan.Values.Count - 30).ToString(CultureInfo.InvariantCulture) + " more fields.";
                    MessageBox.Show(this,
                        "Live edit staged for " + displayName + "." + Environment.NewLine + Environment.NewLine
                        + "Build: " + stagedPlan.ProfileId + Environment.NewLine
                        + "Fields: " + stagedPlan.Values.Count.ToString(CultureInfo.InvariantCulture)
                        + Environment.NewLine + Environment.NewLine + preview
                        + Environment.NewLine + Environment.NewLine
                        + "No FM values have been changed yet.",
                        "FM Atlas · Live edit staged", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    stagedPlan = null;
                    applyButton!.IsEnabled = false;
                    status.Text = "STAGE BLOCKED · " + ex.Message;
                    AtlasLog.Warning("Live entity edit staging was blocked: " + ex.Message);
                    MessageBox.Show(this, ex.Message, "FM Atlas · Stage blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                finally { Mouse.OverrideCursor = null; }
            });

            applyButton = PrimaryButton("Apply to live FM career", async (_, _) =>
            {
                if (stagedPlan is null) return;
                var answer = MessageBox.Show(this,
                    "This writes directly into the currently loaded Football Manager career in memory." + Environment.NewLine
                    + Environment.NewLine + "Make sure this is an offline career and you have a separate backup copy. "
                    + "Changes become part of the career file when you save inside Football Manager." + Environment.NewLine
                    + Environment.NewLine + "Apply " + stagedPlan.Values.Count.ToString(CultureInfo.InvariantCulture)
                    + " field(s) to " + displayName + "?",
                    "Confirm live FM27 edit", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) return;
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    appliedReceipt = await Task.Run(() => LiveEntityMemoryEditService.ApplyBatch(stagedPlan));
                    foreach (var row in rows)
                    {
                        var item = stagedPlan.Values.FirstOrDefault(x => x.FieldKey.Equals(row.FieldKey, StringComparison.OrdinalIgnoreCase));
                        if (item is null) continue;
                        row.CurrentValue = item.TargetValue;
                        row.TargetValue = item.TargetValue.ToString(CultureInfo.InvariantCulture);
                    }
                    status.Text = "LIVE WRITE VERIFIED · " + appliedReceipt.Plan.Values.Count.ToString(CultureInfo.InvariantCulture)
                        + " field(s) written and read back from FM memory. Save the career inside FM to persist to disk.";
                    stagedPlan = null;
                    applyButton!.IsEnabled = false;
                    undoButton!.IsEnabled = true;
                    AtlasLog.Info("Live FM27 write verified for " + entityType + " " + uid + " · fields=" + appliedReceipt.Plan.Values.Count);
                    MessageBox.Show(this,
                        "FM27 accepted " + appliedReceipt.Plan.Values.Count.ToString(CultureInfo.InvariantCulture)
                        + " change(s) for " + displayName + ". Every field passed read-back verification." + Environment.NewLine
                        + Environment.NewLine + "Save the career in Football Manager to persist the changes to the save file. Undo remains available in this profile while the same career session is loaded.",
                        "FM Atlas · Live changes applied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    status.Text = "APPLY BLOCKED / FAILED · " + ex.Message;
                    AtlasLog.Error("Live entity Apply failed for " + entityType + " " + uid, ex);
                    MessageBox.Show(this, ex.Message, "FM Atlas · Live Apply failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally { Mouse.OverrideCursor = null; }
            });
            applyButton.IsEnabled = false;
            undoButton = SoftButton("Undo last live batch", async (_, _) =>
            {
                if (appliedReceipt is null) return;
                var answer = MessageBox.Show(this,
                    "Restore the original values in Football Manager for the last applied batch? Undo is allowed only while every field still contains the value Atlas wrote and the same career remains loaded.",
                    "Confirm live Undo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) return;
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    await Task.Run(() => LiveEntityMemoryEditService.UndoBatch(appliedReceipt));
                    foreach (var row in rows)
                    {
                        var item = appliedReceipt.Plan.Values.FirstOrDefault(x => x.FieldKey.Equals(row.FieldKey, StringComparison.OrdinalIgnoreCase));
                        if (item is null) continue;
                        row.CurrentValue = item.CurrentValue;
                        row.TargetValue = item.CurrentValue.ToString(CultureInfo.InvariantCulture);
                    }
                    status.Text = "LIVE UNDO VERIFIED · original values restored in the same FM career session.";
                    AtlasLog.Info("Live FM27 undo verified for " + entityType + " " + uid);
                    appliedReceipt = null;
                    undoButton!.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    status.Text = "UNDO BLOCKED / FAILED · " + ex.Message;
                    AtlasLog.Error("Live entity Undo failed for " + entityType + " " + uid, ex);
                    MessageBox.Show(this, ex.Message, "FM Atlas · Live Undo failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally { Mouse.OverrideCursor = null; }
            });
            undoButton.IsEnabled = false;
            var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            buttons.Children.Add(stageButton);
            buttons.Children.Add(applyButton);
            buttons.Children.Add(undoButton);
            panel.Children.Add(buttons);
        }
        catch (Exception ex)
        {
            status.Text = "LIVE FIELDS UNAVAILABLE · " + ex.Message;
            AtlasLog.Warning("Live entity editor could not load " + entityType + " " + uid + ": " + ex.Message);
        }
        return panel;
    }

    private sealed class LiveEntityEditView
    {
        public string FieldKey { get; set; } = "";
        public string Label { get; set; } = "";
        public int CurrentValue { get; set; }
        public string TargetValue { get; set; } = "";
    }

    private async Task<UIElement> BuildLocalEntityFactEditorAsync(
        string entityType, string sourceId, string displayName, IReadOnlyList<AtlasFactRecord> sourceFacts)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 14, 0, 10) };
        panel.Children.Add(Txt("LOCAL FIELD EDITOR · " + entityType.ToUpperInvariant(), 9, true, Teal));
        panel.Children.Add(Txt(
            "Every scalar field supplied by the current FM save/live import is exposed below. The source value is preserved separately; saving creates an Atlas-only override and an audit entry. It does not write to Football Manager.",
            9, false, Muted, wrap: true));

        var imported = sourceFacts
            .Where(x => !string.IsNullOrWhiteSpace(x.Group) && !string.IsNullOrWhiteSpace(x.Field))
            .GroupBy(x => x.Group.Trim() + "\u001f" + x.Field.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Field, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (imported.Count == 0)
        {
            panel.Children.Add(Txt("No source-backed scalar fields were imported for this record. Atlas will not invent editable fields; import a richer save or connect to a supported live career.", 9, false, Muted, wrap: true));
            return panel;
        }

        var existing = await RunDatabaseAsync(() => _database.GetLocalEntityOverridesAsync(entityType, sourceId));
        var overrideByKey = existing.ToDictionary(
            x => x.Group + "\u001f" + x.Field, x => x, StringComparer.OrdinalIgnoreCase);
        var rows = new ObservableCollection<EditableEntityFactView>();
        foreach (var fact in imported)
        {
            var original = fact.Value ?? string.Empty;
            var key = fact.Group.Trim() + "\u001f" + fact.Field.Trim();
            overrideByKey.TryGetValue(key, out var prior);
            rows.Add(new EditableEntityFactView
            {
                Group = fact.Group, Field = fact.Field,
                OriginalValue = prior?.OriginalValue ?? original,
                InitialValue = prior?.OverrideValue ?? original,
                Value = prior?.OverrideValue ?? original,
                SourceLabel = prior?.SourceLabel ?? fact.Group
            });
        }

        var grid = CreateGrid();
        grid.IsReadOnly = false;
        grid.CanUserAddRows = false;
        grid.MinHeight = 220;
        grid.MaxHeight = 480;
        var groupColumn = Column("SOURCE GROUP", nameof(EditableEntityFactView.Group), 1.0); groupColumn.IsReadOnly = true; grid.Columns.Add(groupColumn);
        var fieldColumn = Column("FIELD", nameof(EditableEntityFactView.Field), 1.2); fieldColumn.IsReadOnly = true; grid.Columns.Add(fieldColumn);
        var sourceColumn = Column("IMPORTED VALUE", nameof(EditableEntityFactView.OriginalValue), 1.0); sourceColumn.IsReadOnly = true; grid.Columns.Add(sourceColumn);
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "LOCAL OVERRIDE",
            Binding = new Binding(nameof(EditableEntityFactView.Value)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(1.25, DataGridLengthUnitType.Star), MinWidth = 140
        });
        grid.ItemsSource = rows;
        panel.Children.Add(Txt($"{rows.Count:N0} source-backed fields · {existing.Count:N0} existing local overrides", 8, true, Muted));
        panel.Children.Add(grid);
        panel.Children.Add(PrimaryButton("Save changed local fields", async (_, _) =>
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            var changed = rows.Where(x => !string.Equals(x.Value, x.InitialValue, StringComparison.Ordinal)).ToList();
            if (changed.Count == 0)
            {
                MessageBox.Show(this, "No local field changes to save.", "FM Atlas local editor", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                await RunDatabaseAsync(() => _database.SaveLocalEntityOverridesAsync(
                    entityType, sourceId, displayName,
                    changed.Select(x => new AtlasLocalEntityEdit(x.Group, x.Field, x.OriginalValue, x.Value, x.SourceLabel)).ToList()));
                foreach (var item in changed) item.InitialValue = item.Value;
                AtlasLog.Info($"Saved {changed.Count:N0} local {entityType} overrides for {displayName}.");
                MessageBox.Show(this,
                    $"Saved {changed.Count:N0} local {entityType} field overrides for {displayName}. The original imported facts are preserved and Football Manager was not changed.",
                    "Atlas local overrides saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AtlasLog.Error($"Local {entityType} editor save failed for {displayName}.", ex);
                MessageBox.Show(this, ex.Message, "FM Atlas local editor", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }));
        return panel;
    }

    private sealed class EditableEntityFactView
    {
        public string Group { get; set; } = "";
        public string Field { get; set; } = "";
        public string OriginalValue { get; set; } = "";
        public string InitialValue { get; set; } = "";
        public string Value { get; set; } = "";
        public string SourceLabel { get; set; } = "";
    }

    private async Task ShowPlayerProfileAsync(PlayerRecord player)
    {
        if (_profileTabs is null) return;
        var tab = _profileTabs.Items.OfType<TabItem>().FirstOrDefault(x => string.Equals(x.Tag?.ToString(), player.SourceId, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
        {
            foreach (var welcome in _profileTabs.Items.OfType<TabItem>().Where(x => Equals(x.Tag, "__welcome")).ToList()) _profileTabs.Items.Remove(welcome);
            var panel = new StackPanel { Margin = new Thickness(16) };
            var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            tab = new TabItem { Tag = player.SourceId, Content = scroll };
            var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            header.Children.Add(new TextBlock { Text = player.Name, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush("#183B56"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 7, 0) });
            var close = new Button { Content = "×", Width = 20, Height = 20, Padding = new Thickness(0), Margin = new Thickness(0, 0, 1, 0), Background = Brush("#EAF1F5"), Foreground = Brush("#526B7D"), BorderBrush = Brushes.Transparent, FontSize = 13, Cursor = Cursors.Hand, ToolTip = "Close player tab" };
            close.Click += (_, _) =>
            {
                _openPlayerRecords.Remove(player.SourceId);
                _profileTabs.Items.Remove(tab);
                if (_profileTabs.Items.Count == 0) AddWelcomeProfileTab();
            };
            header.Children.Add(close);
            tab.Header = header;
            _profileTabs.Items.Add(tab);
            _openPlayerRecords[player.SourceId] = player;
        }
        else _openPlayerRecords[player.SourceId] = player;
        _profileTabs.SelectedItem = tab;
        var profilePanel = (StackPanel)((ScrollViewer)tab.Content).Content;
        _profilePanel = profilePanel;
        if (profilePanel.Children.Count > 0) return;
        profilePanel.Children.Clear();

        var attributesTask = RunDatabaseAsync(() => _database.GetPlayerAttributesAsync(player.SourceId));
        var generalTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "general"));
        var positionTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "positions"));
        var reputationTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "reputation"));
        var stateTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "state"));
        var contractTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "contract"));
        var traitsTask = RunDatabaseAsync(() => _database.GetFmsavePlayerFactsAsync(player.SourceId, "traits"));
        var performanceTask = RunDatabaseAsync(() => _database.GetPlayerPerformanceFactsAsync(player.SourceId));
        var developmentHistoryTask = RunDatabaseAsync(() => _database.GetPlayerDevelopmentHistoryAsync(player.SourceId));
        Task<List<(string FieldKey, double? NumericValue, string? TextValue, string ValidationStatus)>>? liveFactsTask = null;
        if (player.SourceId.StartsWith("LIVE:", StringComparison.Ordinal) && long.TryParse(player.SourceId[5..], out var liveUid))
            liveFactsTask = RunDatabaseAsync(() => _database.GetLivePlayerFactsAsync(liveUid));
        await Task.WhenAll(new Task[] { attributesTask, generalTask, positionTask, reputationTask, stateTask, contractTask, traitsTask, performanceTask, developmentHistoryTask }.Concat(liveFactsTask is null ? Array.Empty<Task>() : new Task[] { liveFactsTask }));

        var attributes = await attributesTask;
        var generalFacts = await generalTask;
        var positionFacts = await positionTask;
        var reputationFacts = await reputationTask;
        var stateFacts = await stateTask;
        var contractFacts = await contractTask;
        var traits = await traitsTask;
        var performanceFacts = await performanceTask;
        var developmentHistory = await developmentHistoryTask;
        var liveFacts = liveFactsTask is null ? new List<(string FieldKey, double? NumericValue, string? TextValue, string ValidationStatus)>() : await liveFactsTask;
        foreach (var fact in liveFacts.Where(f => f.NumericValue.HasValue && f.ValidationStatus.Contains("Verified", StringComparison.OrdinalIgnoreCase) && (f.FieldKey.Contains("Attribute", StringComparison.OrdinalIgnoreCase) || f.FieldKey.StartsWith("Ability.", StringComparison.OrdinalIgnoreCase))))
            attributes[fact.FieldKey] = fact.NumericValue!.Value;

        profilePanel.Children.Add(Txt("PLAYER PROFILE", 9, true, Teal));
        var hero = new Border { Background = new LinearGradientBrush(Color.FromRgb(29, 39, 49), Color.FromRgb(18, 25, 33), 25), BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(18), Margin = new Thickness(0, 8, 0, 13) };
        var heroGrid = new Grid();
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var graphicsUid = player.SourceId.StartsWith("FMSAVE:", StringComparison.OrdinalIgnoreCase) ? player.SourceId[7..]
            : player.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase) ? player.SourceId[5..] : null;
        var facePath = await Task.Run(() => GraphicsResolver.Shared.ResolveFace(graphicsUid));
        var avatarBorder = new Border { Width = 68, Height = 68, CornerRadius = new CornerRadius(14), Background = Brush("#202A35"), BorderBrush = Brush("#485562"), BorderThickness = new Thickness(1), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        if (!string.IsNullOrWhiteSpace(facePath) && File.Exists(facePath))
        {
            try
            {
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 160; bitmap.UriSource = new Uri(facePath, UriKind.Absolute); bitmap.EndInit(); bitmap.Freeze();
                avatarBorder.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
            }
            catch { avatarBorder.Child = Txt(Initials(player.Name), 22, true, Brush("#C6F36B")); }
        }
        else avatarBorder.Child = new TextBlock { Text = Initials(player.Name), FontSize = 21, FontWeight = FontWeights.Bold, Foreground = Brush("#C6F36B"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        heroGrid.Children.Add(avatarBorder);
        var heroStack = new StackPanel();
        heroStack.Children.Add(Txt(player.Name, 25, true, Ink));
        heroStack.Children.Add(Txt(player.Club ?? "Club unavailable", 12, true, Brush("#B9C5D0")));
        heroStack.Children.Add(Txt($"{player.Age?.ToString() ?? "—"} years  ·  {player.Nation ?? "Nationality unavailable"}  ·  {player.Position ?? "Position unavailable"}", 11, false, Brush("#B9C5D0"), wrap: true));
        heroStack.Children.Add(Txt($"{player.League ?? "League unavailable"}  ·  {player.DataOrigin}", 9, false, Brush("#9EADB9"), wrap: true));
        Grid.SetColumn(heroStack, 1); heroGrid.Children.Add(heroStack);
        var actions = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(WhiteButton("＋ Add to shortlist", async (_, _) => { _selectedPlayer = player; await AddSelectedToShortlistAsync(); }));
        actions.Children.Add(SoftButton("Find similar players", async (_, _) =>
        {
            try
            {
                var candidates = await RunDatabaseAsync(() => _database.FindSimilarPlayersAsync(
                    player.SourceId, limit: 100, minimumCommonAttributes: 4, minimumAnchorCoverage: 0.35));
                new SimilarPlayersWindow(_database, player, candidates) { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Similar Player Discovery from profile failed.", ex);
                MessageBox.Show(this, "Similar Player Discovery failed: " + ex.Message, "FM Atlas", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }));
        actions.Children.Add(SoftButton("Compare open players", async (_, _) =>
        {
            var selected = new[] { player }.Concat(_openPlayerRecords.Values
                    .Where(x => !x.SourceId.Equals(player.SourceId, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First()).Take(4).ToList();
            if (selected.Count < 2)
            {
                MessageBox.Show(this, "Open at least one other player profile first. Atlas compares up to four players at a time.",
                    "FM Atlas · Player Comparison", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            await PlayerComparisonWindow.ShowAsync(this, _database, selected);
        }));
        _profileExpandButton = SoftButton(_profileExpanded ? "Show search results" : "Expand full profile", (_, _) => ToggleProfileExpanded(!_profileExpanded));
        actions.Children.Add(_profileExpandButton);
        actions.Children.Add(SoftButton("Open in Editor Studio", async (_, _) => { _selectedPlayer = player; await NavigateAsync("Live Editor"); }));
        actions.Children.Add(SoftButton(_editorEnabled ? "Profile Editor · ON" : "Enable Profile Editor", async (_, _) =>
        {
            _editorEnabled = !_editorEnabled;
            _selectedPlayer = player;
            profilePanel.Children.Clear();
            await ShowPlayerProfileAsync(player);
        }));
        Grid.SetColumn(actions, 2); heroGrid.Children.Add(actions); hero.Child = heroGrid; profilePanel.Children.Add(hero);

        var ability = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };
        var isLiveRecord = player.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase);
        ability.Children.Add(ProfileMetric("CURRENT ABILITY", player.DisplayAbility, player.CurrentAbility.HasValue ? (isLiveRecord ? "Live memory · provisional until UI witness" : "Imported save / local source") : "Not imported"));
        ability.Children.Add(ProfileMetric("POTENTIAL", player.DisplayPotential, player.PotentialAbility.HasValue ? (isLiveRecord ? "Live memory · provisional until UI witness" : "Imported save / local source") : "Not imported"));
        ability.Children.Add(ProfileMetric("VALUE", player.DisplayValue, "Stored source units"));
        ability.Children.Add(ProfileMetric("AGE", player.Age?.ToString() ?? "—", player.DateOfBirth?.ToString("dd MMM yyyy") ?? "Date of birth unavailable"));
        profilePanel.Children.Add(ability);

        var detailCard = new Border { Background = Brush("#171F28"), BorderBrush = Brush("#303943"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(13), Margin = new Thickness(0, 0, 0, 12) };
        var detailGrid = new UniformGrid { Columns = 2 };
        AddDetailRow(detailGrid, "Nationality", player.Nation ?? "Not decoded");
        AddDetailRow(detailGrid, "League", player.League ?? "Not decoded");
        AddDetailRow(detailGrid, "Natural positions", player.Position ?? "Not imported");
        AddDetailRow(detailGrid, "Preferred foot", player.PreferredFoot ?? "Not imported");
        AddDetailRow(detailGrid, "Left foot", player.LeftFoot?.ToString() ?? "—");
        AddDetailRow(detailGrid, "Right foot", player.RightFoot?.ToString() ?? "—");
        AddDetailRow(detailGrid, "Home-grown at club", player.HomeGrownClub.HasValue ? (player.HomeGrownClub.Value ? "Yes" : "No") : "Unknown");
        AddDetailRow(detailGrid, "Home-grown nation", player.HomeGrownNation.HasValue ? (player.HomeGrownNation.Value ? "Yes" : "No") : "Unknown");
        detailCard.Child = detailGrid;
        profilePanel.Children.Add(detailCard);

        var profileTabs = new TabControl
        {
            Margin = new Thickness(0, 0, 0, 12),
            MinHeight = 500
        };
        StackPanel AddProfileTab(string title)
        {
            var content = new StackPanel { Margin = new Thickness(12, 10, 12, 14) };
            profileTabs.Items.Add(new TabItem { Header = title, Content = content });
            return content;
        }

        var overviewTab = AddProfileTab("Overview");
        var attributesTab = AddProfileTab("Attributes");
        var positionsTab = AddProfileTab("Positions & Roles");
        if (player.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase))
        {
            attributesTab.Children.Add(Txt(
                "LIVE FM27 · 52 player attribute fields and 8 personality fields are shown where the current supported layout supplies them. These are provisional reads, not certified field values. Hidden attributes in particular still need independent in-game UI witnesses.",
                9, false, Brush("#D89A60"), wrap: true));
            positionsTab.Children.Add(Txt(
                "LIVE FM27 POSITION RATINGS · values are taken from the current process layout and remain provisional until compared with the player's Positions screen in Football Manager.",
                9, false, Brush("#D89A60"), wrap: true));
        }
        var contractTab = AddProfileTab("Contract");
        var performanceTab = AddProfileTab("Performance");
        var developmentTab = AddProfileTab("Development");
        var historyTab = AddProfileTab("History");
        var evidenceTab = AddProfileTab("Source & Evidence");
        var profileEditorTab = AddProfileTab("Editor");
        profilePanel.Children.Add(profileTabs);

        AddProfileFactsSection(overviewTab, "PERSONAL & GENERAL INFORMATION", generalFacts, 30);
        AddProfileFactsSection(overviewTab, "CONDITION & STATUS", stateFacts, 30);
        AddProfileFactsSection(overviewTab, "PLAYER TRAITS", traits, 24);
        overviewTab.Children.Add(Txt(
            "Profile values are source-backed. A dash means the imported source did not provide the field; Atlas does not substitute zero or infer a rating.",
            9, false, Muted, wrap: true));

        AddAttributeRadar(attributesTab, attributes);
        AddOrderedAttributeSections(attributesTab, attributes);

        // Profile-wide local editor: every source-backed FM27 player attribute is editable
        // from the profile itself. Writes remain local until the live writer has passed
        // independent field-witness and disposable-save acceptance.
        profileEditorTab.Children.Add(Txt("PROFILE EDITOR", 9, true, Teal));
        profileEditorTab.Children.Add(Txt(
            "Edit the values shown on this player profile. Save writes to the FM Atlas database and edit history; it does not change the running Football Manager career.",
            9, false, Muted, wrap: true));
        if (!_editorEnabled)
        {
            profileEditorTab.Children.Add(Txt("Enable Profile Editor from the player header to unlock the editable fields.", 10, false, Muted, wrap: true));
        }
        else
        {
            var abilityEditor = new WrapPanel { Margin = new Thickness(0, 8, 0, 10) };
            var profileCaInput = Input("Current Ability"); profileCaInput.Width = 115; profileCaInput.Text = player.CurrentAbility?.ToString(CultureInfo.InvariantCulture) ?? "";
            var profilePaInput = Input("Potential Ability"); profilePaInput.Width = 115; profilePaInput.Text = player.PotentialAbility?.ToString(CultureInfo.InvariantCulture) ?? "";
            abilityEditor.Children.Add(FieldLabel("CA")); abilityEditor.Children.Add(profileCaInput);
            abilityEditor.Children.Add(FieldLabel("PA")); abilityEditor.Children.Add(profilePaInput);
            abilityEditor.Children.Add(PrimaryButton("Save CA / PA locally", async (_, _) =>
            {
                if (!int.TryParse(profileCaInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ca) ||
                    !int.TryParse(profilePaInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pa) ||
                    ca is < 0 or > 200 || pa is < 0 or > 200)
                {
                    MessageBox.Show(this, "Enter CA and PA as whole numbers from 0 to 200.", "Profile editor");
                    return;
                }
                try
                {
                    await RunDatabaseAsync(() => _database.SaveLocalAbilitiesAsync(player.SourceId, player.Name, ca, pa));
                    MessageBox.Show(this, "CA and PA saved to the FM Atlas database only. Football Manager itself has not been changed.", "Local edit saved", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshPlayerResultsAsync();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Profile editor", MessageBoxButton.OK, MessageBoxImage.Warning); }
            }));
            profileEditorTab.Children.Add(abilityEditor);

            var editableRows = new ObservableCollection<EditableAttributeView>();
            foreach (var field in PlayerAttributeSchema.Fields)
            {
                var current = attributes.FirstOrDefault(x => x.Key.Equals(field.Key, StringComparison.OrdinalIgnoreCase));
                if (current.Key is null) continue;
                editableRows.Add(new EditableAttributeView
                {
                    AttributeName = field.Key,
                    Category = field.Category,
                    OriginalValue = current.Value,
                    Value = current.Value
                });
            }
            if (player.LeftFoot.HasValue)
                editableRows.Add(new EditableAttributeView { AttributeName = "left_foot", Category = "Foot", OriginalValue = player.LeftFoot.Value, Value = player.LeftFoot.Value });
            if (player.RightFoot.HasValue)
                editableRows.Add(new EditableAttributeView { AttributeName = "right_foot", Category = "Foot", OriginalValue = player.RightFoot.Value, Value = player.RightFoot.Value });
            foreach (var fact in positionFacts)
            {
                if (!double.TryParse(fact.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var positionValue) ||
                    positionValue is < 1 or > 20) continue;
                var key = fact.Field;
                if (editableRows.Any(x => x.AttributeName.Equals(key, StringComparison.OrdinalIgnoreCase))) continue;
                editableRows.Add(new EditableAttributeView
                {
                    AttributeName = key,
                    Category = "Positions",
                    OriginalValue = positionValue,
                    Value = positionValue
                });
            }
            foreach (var fact in liveFacts.Where(x => x.NumericValue.HasValue &&
                (x.FieldKey.StartsWith("personality.", StringComparison.OrdinalIgnoreCase) ||
                 x.FieldKey.StartsWith("positions.", StringComparison.OrdinalIgnoreCase))))
            {
                var key = fact.FieldKey[(fact.FieldKey.IndexOf('.') + 1)..];
                if (editableRows.Any(x => x.AttributeName.Equals(key, StringComparison.OrdinalIgnoreCase))) continue;
                var category = fact.FieldKey.StartsWith("personality.", StringComparison.OrdinalIgnoreCase) ? "Personality" : "Positions";
                editableRows.Add(new EditableAttributeView
                {
                    AttributeName = key,
                    Category = category,
                    OriginalValue = fact.NumericValue!.Value,
                    Value = fact.NumericValue.Value
                });
            }

            var profileEditorGrid = CreateGrid();
            profileEditorGrid.IsReadOnly = false;
            profileEditorGrid.CanUserAddRows = false;
            var profileNameColumn = Column("FIELD", "AttributeName", 1.25); profileNameColumn.IsReadOnly = true; profileEditorGrid.Columns.Add(profileNameColumn);
            var profileGroupColumn = Column("GROUP", "Category", .8); profileGroupColumn.IsReadOnly = true; profileEditorGrid.Columns.Add(profileGroupColumn);
            profileEditorGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "VALUE / 20",
                Binding = new Binding(nameof(EditableAttributeView.Value)) { StringFormat = "0.#", UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(.65, DataGridLengthUnitType.Star)
            });
            profileEditorGrid.ItemsSource = editableRows;
            profileEditorGrid.MinHeight = 260;
            profileEditorTab.Children.Add(profileEditorGrid);
            profileEditorTab.Children.Add(PrimaryButton("Save all changed profile attributes locally", async (_, _) =>
            {
                profileEditorGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                profileEditorGrid.CommitEdit(DataGridEditingUnit.Row, true);
                var changed = editableRows.Where(x => Math.Abs(x.Value - x.OriginalValue) > 0.0001).ToList();
                if (changed.Count == 0)
                {
                    MessageBox.Show(this, "No changes to save.", "Profile editor");
                    return;
                }
                if (changed.Any(x => x.Value < 1 || x.Value > 20 || !double.IsFinite(x.Value)))
                {
                    MessageBox.Show(this, "Attributes, personality and position ratings must be between 1 and 20.", "Profile editor");
                    return;
                }
                try
                {
                    foreach (var item in changed)
                        await RunDatabaseAsync(() => _database.SavePlayerAttributeAsync(
                            player.SourceId, player.Name, item.AttributeName, item.Category, item.Value,
                            "FM Atlas profile editor · local database only"));
                    foreach (var item in changed) item.OriginalValue = item.Value;
                    AtlasLog.Info($"Profile editor saved {changed.Count:N0} local fields for {player.Name}.");
                    MessageBox.Show(this, $"Saved {changed.Count:N0} fields to the FM Atlas database and edit history. No live Football Manager values were written.", "Local edits saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    AtlasLog.Error("Profile-wide local edit failed.", ex);
                    MessageBox.Show(this, ex.Message, "Profile editor", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }));
            if (editableRows.Count == 0)
                profileEditorTab.Children.Add(Txt("No source-backed editable attribute values were imported for this player. Import a supported FM27 save or connect to a supported live career first.", 9, false, Muted, wrap: true));
            var allPlayerFacts = generalFacts.Concat(positionFacts).Concat(reputationFacts)
                .Concat(stateFacts).Concat(contractFacts).Concat(traits)
                .Concat(liveFacts.Select(x => new AtlasFactRecord(
                    "Live FM27 · " + (x.ValidationStatus.Contains("Verified", StringComparison.OrdinalIgnoreCase) ? "verified" : "provisional"),
                    x.FieldKey, x.NumericValue?.ToString("0.########", CultureInfo.InvariantCulture) ?? x.TextValue)))
                .ToList();
            profileEditorTab.Children.Add(await BuildLocalEntityFactEditorAsync(
                "player", player.SourceId, player.Name, allPlayerFacts));
            if (_editorEnabled && player.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase))
                profileEditorTab.Children.Add(await BuildLiveEntityEditorAsync(
                    LiveEntityMemoryEditService.EntityKind.Player, player.SourceId, player.Name));
        }
        if (attributes.Count == 0)
            attributesTab.Children.Add(Txt("No player attribute rows were supplied by the selected source.", 10, false, Muted, wrap: true));

        AddPositionSuitabilityMap(positionsTab, positionFacts, liveFacts);
        var positionDisplayFacts = positionFacts.Concat(liveFacts
            .Where(f => f.FieldKey.StartsWith("positions.", StringComparison.OrdinalIgnoreCase) && f.NumericValue.HasValue)
            .Select(f => new AtlasFactRecord(
                "Live FM27 · " + (f.ValidationStatus.Contains("Verified", StringComparison.OrdinalIgnoreCase) ? "verified" : "provisional"),
                f.FieldKey, f.NumericValue!.Value.ToString("0.#", CultureInfo.InvariantCulture))))
            .ToList();
        AddProfileFactsSection(positionsTab, "POSITION SUITABILITY · SOURCE VALUES", positionDisplayFacts, 36);
        positionsTab.Children.Add(Txt(
            "Role Fit scores are Atlas estimates based on available 1–20 attributes, not Football Manager's hidden role rating. Open Role Lab for the selected role profiles and coverage explanation.",
            9, false, Muted, wrap: true));
        positionsTab.Children.Add(Txt("BEST-SUPPORTED ROLE MATCHES", 9, true, Teal)
            is var roleHeading ? Configure(roleHeading, new Thickness(0, 16, 0, 5)) : new TextBlock());
        positionsTab.Children.Add(Txt(
            "Calculated from this player's imported 1–20 footballing attributes using the same weighted Role Fit engine as Role Lab. Each row includes coverage; these are Atlas estimates, not FM star ratings.",
            9, false, Muted, wrap: true));
        var profileRoleMatches = RoleFitService.RankProfilesForPlayer(
            attributes, RoleFitProfileStore.LoadAll(), minimumCoverage: 0.5, limit: 6);
        if (profileRoleMatches.Count == 0)
            positionsTab.Children.Add(Txt("No role profile has enough source-backed attributes to calculate a reliable estimate for this player.", 9, false, Muted, wrap: true));
        else
        {
            foreach (var match in profileRoleMatches)
            {
                var score = match.Score;
                var roleCard = Card(new StackPanel(), 10, new Thickness(0, 5, 0, 5));
                var roleContent = (StackPanel)roleCard.Child;
                roleContent.Children.Add(Txt((match.Profile.IsCustom ? "CUSTOM · " : "") + match.Profile.Name, 11, true, Ink));
                roleContent.Children.Add(Txt(
                    $"{match.Profile.Phase} · {match.Profile.PositionGroup} · {score.Score:0.00}/20 · {score.Coverage:P0} coverage · {score.AttributesAvailable}/{score.AttributesExpected} attributes",
                    9, false, Muted, wrap: true));
                roleContent.Children.Add(Txt(
                    string.Join(" · ", score.Contributions.Take(3).Select(x => $"{x.Attribute} {x.Value:0.#}")),
                    9, false, Muted, wrap: true));
                positionsTab.Children.Add(roleCard);
            }
        }

        AddProfileFactsSection(contractTab, "CONTRACT & REGISTRATION", contractFacts, 30);
        if (contractFacts.Count == 0)
            contractTab.Children.Add(Txt("No contract or registration facts were retained for this player in the selected snapshot.", 9, false, Muted, wrap: true));

        if (performanceFacts.Count == 0)
            performanceTab.Children.Add(Txt(
                "PERFORMANCE DATA · No player-season, career-history or match-stat rows for this player were supplied by the current live index. To add historical and match statistics, import a read-only snapshot of a COPY of the career save or add an FM View Exporter CSV. The live player/CA/PA index does not require that manual import.",
                9, false, Muted, wrap: true));
        else
        {
            performanceTab.Children.Add(Txt($"PERFORMANCE INTELLIGENCE · {performanceFacts.Count:N0} numeric source facts retained",
                9, true, Teal) is var performanceHeading ? Configure(performanceHeading, new Thickness(0, 0, 0, 6)) : new TextBlock());
            foreach (var group in performanceFacts.GroupBy(x => x.Group).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                performanceTab.Children.Add(Txt(group.Key.ToUpperInvariant(), 8, true, Teal) is var statsHeading
                    ? Configure(statsHeading, new Thickness(0, 9, 0, 4)) : new TextBlock());
                var statsGrid = new UniformGrid { Columns = 2 };
                foreach (var fact in group) AddDetailRow(statsGrid, fact.Field, fact.Value ?? "—");
                performanceTab.Children.Add(statsGrid);
            }
        }

        AddDevelopmentHistorySection(developmentTab, developmentHistory);

        if (developmentHistory.Count == 0)
            historyTab.Children.Add(Txt("No dated player snapshots are available for this record. History appears only when the imported save contains a valid in-game date and comparable CA, PA or value evidence.",
                9, false, Muted, wrap: true));
        else
        {
            historyTab.Children.Add(Txt(
                $"{developmentHistory.Count:N0} dated source snapshot(s) · original game dates and import/source evidence are retained.",
                9, false, Muted, wrap: true));
            foreach (var snapshot in developmentHistory.OrderByDescending(x => x.GameDate).ThenByDescending(x => x.SnapshotId))
            {
                var card = Card(new StackPanel(), 12, new Thickness(0, 7, 0, 7));
                var cardContent = (StackPanel)card.Child;
                cardContent.Children.Add(Txt(snapshot.GameDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), 12, true, Ink));
                AddDetailRow(cardContent, "Snapshot ID", snapshot.SnapshotId.ToString(CultureInfo.InvariantCulture));
                AddDetailRow(cardContent, "Imported", snapshot.ImportedUtc);
                AddDetailRow(cardContent, "Source file", snapshot.SourceFileName);
                AddDetailRow(cardContent, "CA", snapshot.CurrentAbility?.ToString("0.#", CultureInfo.InvariantCulture) ?? "—");
                AddDetailRow(cardContent, "PA", snapshot.PotentialAbility?.ToString("0.#", CultureInfo.InvariantCulture) ?? "—");
                AddDetailRow(cardContent, "Value · source units", snapshot.Value?.ToString("N0", CultureInfo.InvariantCulture) ?? "—");
                historyTab.Children.Add(card);
            }
        }

        if (player.SourceId.StartsWith("FMSAVE:", StringComparison.Ordinal))
            evidenceTab.Children.Add(Txt(
                "DATA QUALITY NOTE · CA/PA, attributes and several FM27 save fields are parser-derived and not independently verified for every game build. Values are shown only when the imported snapshot actually contains them; confirm critical edits in Football Manager.",
                10, false, Brush("#8B622C"), wrap: true));
        var liveStatus = player.SourceId.StartsWith("LIVE:", StringComparison.Ordinal)
            ? (liveFacts.Count == 0
                ? "LIVE DATA COVERAGE · No live fact rows are stored for this player yet. Refresh the live read after loading a career."
                : $"LIVE DATA COVERAGE · {liveFacts.Count:N0} profile facts stored. CA, PA, 52 player attributes and position ratings are provisional layout reads until checked against the Football Manager UI; unverified facts are labelled and are not treated as independent proof.")
            : "";
        if (!string.IsNullOrWhiteSpace(liveStatus))
            evidenceTab.Children.Add(Txt(liveStatus, 10, true, Brush("#84612B"), wrap: true));

        AddProfileFactsSection(evidenceTab, "REPUTATION · FIELD MEANINGS UNCONFIRMED", reputationFacts, 30);
        evidenceTab.Children.Add(Txt("DATA PROVENANCE", 9, true, Teal) is var sourceTitle
            ? Configure(sourceTitle, new Thickness(0, 18, 0, 5)) : new TextBlock());
        evidenceTab.Children.Add(Txt(
            $"Record: {player.SourceId}\nOrigin: {player.DataOrigin}\nGenerated-player status: {player.RegenStatus} · {player.RegenConfidence}",
            9, false, Muted, wrap: true));
        if (liveFacts.Count > 0)
        {
            evidenceTab.Children.Add(Txt("LIVE FIELD EVIDENCE · VALUE AND VALIDATION STATUS", 9, true, Teal)
                is var liveTitle ? Configure(liveTitle, new Thickness(0, 15, 0, 5)) : new TextBlock());
            var liveGrid = CreateGrid();
            liveGrid.MinHeight = 180;
            liveGrid.MaxHeight = 340;
            liveGrid.Columns.Add(Column("FIELD", "Field", 1.2));
            liveGrid.Columns.Add(Column("NUMERIC", "NumericValue", .7));
            liveGrid.Columns.Add(Column("TEXT", "TextValue", 1.0));
            liveGrid.Columns.Add(Column("VALIDATION", "Validation", 1.2));
            liveGrid.ItemsSource = liveFacts
                .OrderBy(x => x.FieldKey, StringComparer.OrdinalIgnoreCase)
                .Select(x => new { Field = x.FieldKey, x.NumericValue, x.TextValue, Validation = x.ValidationStatus })
                .ToList();
            evidenceTab.Children.Add(liveGrid);
        }

    }

    private static Border ProfileMetric(string label, string value, string note)
    {
        var panel = new StackPanel();
        panel.Children.Add(Txt(label, 8, true, Brush("#9EADB9")));
        panel.Children.Add(Txt(value, 22, true, Brush("#E8EEF4")));
        panel.Children.Add(Txt(note, 8, false, Brush("#9EADB9"), wrap: true));
        return new Border { Background = Brush("#171F28"), BorderBrush = Brush("#303943"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(11), Margin = new Thickness(0, 0, 8, 0), Child = panel };
    }


    private static void AddAttributeRadar(Panel panel, IReadOnlyDictionary<string, double> values)
    {
        var categories = new[] { "Technical", "Set Pieces", "Mental", "Physical", "Goalkeeping" };
        string Normalize(string value)
        {
            var last = value.Split(new[] { '·', '.', ':' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? value;
            return Regex.Replace(last, "[^A-Za-z0-9]", "").ToLowerInvariant();
        }

        var lookup = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            var key = Normalize(pair.Key);
            if (key.Length == 0 || !double.IsFinite(pair.Value) || pair.Value is < 1 or > 20) continue;
            if (!lookup.ContainsKey(key)) lookup[key] = pair.Value;
        }

        var averages = new List<(string Category, double Value, int Count)>();
        foreach (var category in categories)
        {
            var available = PlayerAttributeSchema.Fields
                .Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .Select(x => lookup.TryGetValue(Normalize(x.Label), out var rating) ? (double?)rating : null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();
            if (available.Count > 0) averages.Add((category, available.Average(), available.Count));
        }
        if (averages.Count < 3)
        {
            panel.Children.Add(Txt("ATTRIBUTE RADAR · At least three attribute groups need imported 1–20 source values. Missing categories are not plotted as zero.",
                9, false, Muted, wrap: true) is var note ? Configure(note, new Thickness(0, 10, 0, 3)) : new TextBlock());
            return;
        }

        var host = new Border
        {
            Background = Brush("#171F28"),
            BorderBrush = Brush("#303943"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 12, 0, 10)
        };
        var layout = new StackPanel();
        layout.Children.Add(Txt("ATTRIBUTE RADAR · IMPORTED AVERAGES", 9, true, Teal));
        layout.Children.Add(Txt("Average of available source-backed 1–20 ratings per attribute group. This is descriptive, not the game's role formula; missing fields are excluded.",
            9, false, Muted, wrap: true));
        var canvas = new Canvas
        {
            Width = 570,
            Height = 330,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = Brush("#141C25"),
            Margin = new Thickness(0, 8, 0, 0)
        };
        const double centerX = 285, centerY = 158, radius = 108;
        var gridBrush = Brush("#344656");
        var axisBrush = Brush("#536B7E");
        for (var ring = 1; ring <= 5; ring++)
        {
            var polygon = new Polygon
            {
                Stroke = gridBrush,
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                Points = new PointCollection(Enumerable.Range(0, averages.Count).Select(index =>
                {
                    var angle = -Math.PI / 2 + index * (2 * Math.PI / averages.Count);
                    var r = radius * ring / 5d;
                    return new Point(centerX + Math.Cos(angle) * r, centerY + Math.Sin(angle) * r);
                }))
            };
            canvas.Children.Add(polygon);
        }

        var scorePoints = new PointCollection();
        for (var i = 0; i < averages.Count; i++)
        {
            var angle = -Math.PI / 2 + i * (2 * Math.PI / averages.Count);
            var end = new Point(centerX + Math.Cos(angle) * radius, centerY + Math.Sin(angle) * radius);
            canvas.Children.Add(new Line { X1 = centerX, Y1 = centerY, X2 = end.X, Y2 = end.Y, Stroke = axisBrush, StrokeThickness = 1 });
            var value = averages[i].Value;
            var pointRadius = radius * value / 20d;
            scorePoints.Add(new Point(centerX + Math.Cos(angle) * pointRadius, centerY + Math.Sin(angle) * pointRadius));
            var label = new TextBlock
            {
                Text = $"{averages[i].Category}\n{value:0.0}/20 · {averages[i].Count} attrs",
                Foreground = Ink,
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                Width = 104
            };
            System.Windows.Controls.Canvas.SetLeft(label, centerX + Math.Cos(angle) * (radius + 48) - 52);
            System.Windows.Controls.Canvas.SetTop(label, centerY + Math.Sin(angle) * (radius + 45) - 17);
            canvas.Children.Add(label);
        }
        canvas.Children.Add(new Polygon
        {
            Points = scorePoints,
            Stroke = Brush("#75B9E7"),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(68, 76, 157, 207))
        });
        foreach (var point in scorePoints)
        {
            canvas.Children.Add(new Ellipse
            {
                Width = 7, Height = 7,
                Fill = Brush("#A9D7F7"),
                Stroke = Brush("#E8EEF4"),
                StrokeThickness = 1
            });
            var dot = (Ellipse)canvas.Children[^1];
            System.Windows.Controls.Canvas.SetLeft(dot, point.X - 3.5);
            System.Windows.Controls.Canvas.SetTop(dot, point.Y - 3.5);
        }
        layout.Children.Add(canvas);
        host.Child = layout;
        panel.Children.Add(host);
    }


    private static void AddDevelopmentHistorySection(Panel panel, IReadOnlyList<PlayerDevelopmentSnapshot> history)
    {
        var snapshots = history
            .GroupBy(x => x.GameDate.Date)
            .Select(group => group.OrderByDescending(x => x.SnapshotId).First())
            .OrderBy(x => x.GameDate)
            .ToList();
        panel.Children.Add(Txt("DEVELOPMENT HISTORY · DATED SAVE SNAPSHOTS", 9, true, Teal) is var heading
            ? Configure(heading, new Thickness(0, 18, 0, 5)) : new TextBlock());
        if (snapshots.Count == 0)
        {
            panel.Children.Add(Txt("No dated ability/value snapshots were supplied for this player. Import more career snapshots with in-game dates to build a history. No development trend is inferred.",
                9, false, Muted, wrap: true));
            return;
        }

        var note = Txt(
            "Imported CA/PA/value observations only · parser-derived and build-dependent · no projections or interpolation. Each point retains its source save and import timestamp.",
            9, false, Muted, wrap: true);
        panel.Children.Add(note);
        if (snapshots.Count < 2)
        {
            var only = snapshots[0];
            AddDetailRow(panel, "Only dated snapshot", only.GameDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
            AddDetailRow(panel, "Current ability", only.CurrentAbility?.ToString("0.#", CultureInfo.InvariantCulture) ?? "—");
            AddDetailRow(panel, "Potential ability", only.PotentialAbility?.ToString("0.#", CultureInfo.InvariantCulture) ?? "—");
            AddDetailRow(panel, "Source save", only.SourceFileName);
            panel.Children.Add(Txt("A trend needs at least two distinct in-game dates. Import another career snapshot after advancing the save to compare development.",
                9, false, Muted, wrap: true));
            return;
        }

        const double left = 58, top = 22, width = 610, height = 205;
        var chart = new Canvas
        {
            Width = 700, Height = 275,
            Background = Brush("#141C25"),
            Margin = new Thickness(0, 8, 0, 9),
            ClipToBounds = true
        };
        var abilityValues = snapshots.SelectMany(x => new[] { x.CurrentAbility, x.PotentialAbility })
            .Where(x => x.HasValue && double.IsFinite(x.Value)).Select(x => x!.Value).ToList();
        if (abilityValues.Count == 0)
        {
            panel.Children.Add(Txt("Dated snapshots were found, but none contain a finite CA or PA value to plot.", 9, false, Muted));
            return;
        }
        var minValue = Math.Floor(abilityValues.Min() / 10d) * 10d;
        var maxValue = Math.Ceiling(abilityValues.Max() / 10d) * 10d;
        if (maxValue <= minValue) { minValue -= 5; maxValue += 5; }
        var range = maxValue - minValue;
        var dateSpan = (snapshots[^1].GameDate - snapshots[0].GameDate).TotalDays;
        if (dateSpan <= 0) dateSpan = 1;

        for (var tick = 0; tick <= 4; tick++)
        {
            var y = top + height - height * tick / 4d;
            var value = minValue + range * tick / 4d;
            chart.Children.Add(new Line
            {
                X1 = left, Y1 = y, X2 = left + width, Y2 = y,
                Stroke = Brush("#344656"), StrokeThickness = 1
            });
            var label = Txt(value.ToString("0", CultureInfo.InvariantCulture), 8, false, Muted);
            System.Windows.Controls.Canvas.SetLeft(label, 8);
            System.Windows.Controls.Canvas.SetTop(label, y - 8);
            chart.Children.Add(label);
        }

        void AddSeries(string label, Func<PlayerDevelopmentSnapshot, double?> selector, Brush stroke, double yOffset)
        {
            var points = new PointCollection();
            var entries = new List<(PlayerDevelopmentSnapshot Snapshot, Point Point)>();
            foreach (var snapshot in snapshots)
            {
                var value = selector(snapshot);
                if (!value.HasValue || !double.IsFinite(value.Value)) continue;
                var x = left + width * (snapshot.GameDate - snapshots[0].GameDate).TotalDays / dateSpan;
                var y = top + height - height * Math.Clamp((value.Value - minValue) / range, 0, 1);
                var point = new Point(x, y);
                points.Add(point);
                entries.Add((snapshot, point));
            }
            if (points.Count >= 2)
                chart.Children.Add(new Polyline { Points = points, Stroke = stroke, StrokeThickness = 2 });
            foreach (var entry in entries)
            {
                var dot = new Ellipse
                {
                    Width = 8, Height = 8, Fill = stroke,
                    Stroke = Brush("#E8EEF4"), StrokeThickness = .8,
                    ToolTip = $"{label}: {selector(entry.Snapshot):0.#}\nGame date: {entry.Snapshot.GameDate:dd MMM yyyy}\nSource: {entry.Snapshot.SourceFileName}\nImported: {entry.Snapshot.ImportedUtc}"
                };
                System.Windows.Controls.Canvas.SetLeft(dot, entry.Point.X - 4);
                System.Windows.Controls.Canvas.SetTop(dot, entry.Point.Y - 4);
                chart.Children.Add(dot);
            }
            var legend = Txt(label, 9, true, stroke);
            System.Windows.Controls.Canvas.SetLeft(legend, left + yOffset);
            System.Windows.Controls.Canvas.SetTop(legend, 245);
            chart.Children.Add(legend);
        }

        AddSeries("CA", x => x.CurrentAbility, Brush("#77BCE8"), 0);
        AddSeries("PA", x => x.PotentialAbility, Brush("#E6C477"), 48);
        var firstDate = Txt(snapshots[0].GameDate.ToString("dd MMM yy", CultureInfo.InvariantCulture), 8, false, Muted);
        var lastDate = Txt(snapshots[^1].GameDate.ToString("dd MMM yy", CultureInfo.InvariantCulture), 8, false, Muted);
        System.Windows.Controls.Canvas.SetLeft(firstDate, left);
        System.Windows.Controls.Canvas.SetTop(firstDate, top + height + 9);
        System.Windows.Controls.Canvas.SetLeft(lastDate, left + width - 60);
        System.Windows.Controls.Canvas.SetTop(lastDate, top + height + 9);
        chart.Children.Add(firstDate);
        chart.Children.Add(lastDate);
        panel.Children.Add(chart);

        panel.Children.Add(Txt($"{snapshots.Count:N0} distinct dated snapshots · dates and values are retained as imported. Duplicate exports on the same in-game date are collapsed to the latest snapshot for the visual trend.",
            9, false, Muted, wrap: true));
    }

    private static void AddOrderedAttributeSections(Panel panel, Dictionary<string, double> values)
    {
        string Normalize(string value)
        {
            var last = value.Split(new[] { '·', '.', ':' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? value;
            return Regex.Replace(last, "[^A-Za-z0-9]", "").ToLowerInvariant();
        }
        var lookup = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in values)
        {
            var key = Normalize(item.Key);
            if (!lookup.ContainsKey(key)) lookup[key] = item.Value;
        }
        if (values.Count == 0)
        {
            panel.Children.Add(new Border
            {
                Background = Brush("#30291D"), BorderBrush = Brush("#66563A"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 10),
                Child = Txt("NO ATTRIBUTE VALUES IMPORTED · The sections below show the Football Manager attribute catalogue, but this source has not supplied ratings for the player. Blank values are not zero and are never guessed.", 10, true, Brush("#E6C477"), wrap: true)
            });
        }
        var fields = PlayerAttributeSchema.Fields;
        foreach (var group in fields.GroupBy(x => x.Category))
        {
            var section = new StackPanel { Margin = new Thickness(0, 13, 0, 2) };
            section.Children.Add(Txt(group.Key.ToUpperInvariant(), 10, true, Teal));
            var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 5, 0, 0) };
            foreach (var field in group)
            {
                var hasValue = lookup.TryGetValue(Normalize(field.Label), out var value);
                grid.Children.Add(AttributeCell(field.Label, hasValue ? value.ToString("0.#") : "—", hasValue ? value : (double?)null));
            }
            section.Children.Add(grid);
            if (group.Key.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                section.Children.Add(Txt("Hidden attributes are shown only when present in imported facts. Candidate memory offsets are not treated as proof of a decoded value.", 9, false, Muted, wrap: true));
            panel.Children.Add(section);
        }
        var known = new HashSet<string>(fields.Select(x => Normalize(x.Label)), StringComparer.OrdinalIgnoreCase);
        var extras = values.Where(x => !known.Contains(Normalize(x.Key)) && !x.Key.StartsWith("Ability.", StringComparison.OrdinalIgnoreCase)).Take(80).ToList();
        if (extras.Count > 0)
        {
            panel.Children.Add(Txt("OTHER IMPORTED ATTRIBUTE FACTS", 9, true, Teal) is var title ? Configure(title, new Thickness(0, 15, 0, 5)) : new TextBlock());
            var extraGrid = new UniformGrid { Columns = 2 };
            foreach (var item in extras) extraGrid.Children.Add(AttributeCell(item.Key, item.Value.ToString("0.#"), item.Value));
            panel.Children.Add(extraGrid);
        }
    }

    private static Border AttributeCell(string label, string value, double? numeric)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 7, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = Txt(label, 10, false, Brush("#C4CED8"));
        row.Children.Add(name);
        var valueBrush = !numeric.HasValue ? Brush("#9EADB9") : numeric.Value >= 15 ? Brush("#8BCFA6") : numeric.Value >= 11 ? Brush("#E6C477") : Brush("#F08C96");
        var pill = new Border { Background = !numeric.HasValue ? Brush("#202A35") : numeric.Value >= 15 ? Brush("#20342C") : numeric.Value >= 11 ? Brush("#30291D") : Brush("#35252A"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 3, 8, 3), MinWidth = 34, Child = Txt(value, 10, true, valueBrush) };
        Grid.SetColumn(pill, 1); row.Children.Add(pill);
        return new Border { BorderBrush = Brush("#303943"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(3, 5, 3, 5), Child = row };
    }

    private static void AddProfileFactsSection(Panel panel, string title, List<AtlasFactRecord> facts, int limit)
    {
        if (facts.Count == 0) return;
        panel.Children.Add(Txt(title, 9, true, Teal) is var heading ? Configure(heading, new Thickness(0, 16, 0, 5)) : new TextBlock());
        var grid = new UniformGrid { Columns = 2 };
        foreach (var fact in facts) AddDetailRow(grid, fact.Field.Replace('_', ' ').Replace('.', ' '), fact.Value);
        panel.Children.Add(grid);
    }

    private void ToggleProfileExpanded(bool? expanded = null)
    {
        _profileExpanded = expanded ?? !_profileExpanded;
        if (_playerResultsColumn is null || _playerProfileColumn is null) return;
        _playerResultsColumn.Width = new GridLength(_profileExpanded ? 0 : .82, GridUnitType.Star);
        _playerProfileColumn.Width = new GridLength(_profileExpanded ? 1 : 1.18, GridUnitType.Star);
        if (_profileExpandButton is not null) _profileExpandButton.Content = _profileExpanded ? "Show search results" : "Expand full profile";
    }

    private void CloseCurrentProfileTab()
    {
        if (_profileTabs?.SelectedItem is not TabItem tab || Equals(tab.Tag, "__welcome")) return;
        var sourceId = tab.Tag?.ToString();
        if (!string.IsNullOrWhiteSpace(sourceId)) _openPlayerRecords.Remove(sourceId);
        _profileTabs.Items.Remove(tab);
        if (_profileTabs.Items.Count == 0) AddWelcomeProfileTab();
    }

    private void ClearProfile(string message)
    {
        if (_profileTabs is null) return;
        if (_profileTabs.Items.Count == 0) { AddWelcomeProfileTab(); return; }
        if (_profileTabs.SelectedItem is not TabItem tab || tab.Tag?.ToString() != "__welcome")
        {
            var welcome = _profileTabs.Items.OfType<TabItem>().FirstOrDefault(x => Equals(x.Tag, "__welcome"));
            if (welcome is null) { AddWelcomeProfileTab(); welcome = _profileTabs.Items.OfType<TabItem>().FirstOrDefault(x => Equals(x.Tag, "__welcome")); }
            if (welcome is not null) _profileTabs.SelectedItem = welcome;
        }
        _profilePanel = _profileTabs.SelectedItem is TabItem selected && selected.Content is ScrollViewer viewer ? (StackPanel)viewer.Content : null;
        if (_profilePanel is null) return;
        _profilePanel.Children.Clear();
        _profilePanel.Children.Add(Txt("PLAYER CENTRE", 9, true, Teal));
        _profilePanel.Children.Add(Txt("Open a player profile", 21, true, Ink));
        _profilePanel.Children.Add(Txt(message, 10, false, Muted, wrap: true));
    }

    private async Task<UIElement> ClubsPageAsync()
    {
        const int pageSize = 200;
        var clubOffset = 0;
        var clubRequestVersion = 0;
        var clubProfileVersion = 0;
        Button previous = null!;
        Button next = null!;
        StackPanel stack = null!;
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.08, GridUnitType.Star) });
        var left = Card(new DockPanel(), 15, new Thickness(0, 0, 10, 0));
        var dock = (DockPanel)left.Child;
        var top = new StackPanel();
        top.Children.Add(Txt("CLUB INTELLIGENCE", 9, true, Teal));
        top.Children.Add(Txt("Club directory", 20, true, Ink));
        top.Children.Add(Txt("Search the complete club index. Only one page is loaded into the grid at a time.", 10, false, Muted, wrap: true));
        var search = Input("Search club name or ID");
        search.Margin = new Thickness(0, 8, 0, 5);
        if (!string.IsNullOrWhiteSpace(_pendingClubSearchQuery)) { search.Text = _pendingClubSearchQuery; _pendingClubSearchQuery = null; }
        top.Children.Add(search);
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);

        var dg = CreateGrid();
        dg.Columns.Add(Column("CLUB", "Name", 1.8));
        dg.Columns.Add(Column("NATION", "Nation", .8));
        dg.Columns.Add(Column("PLAYERS", "PlayerCount", .6));
        dg.Columns.Add(Column("SOURCE", "DataSource", 1.1));
        dock.Children.Add(dg);
        var footer = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var listStatus = Txt("Loading club index…", 9, false, Muted);
        DockPanel.SetDock(listStatus, Dock.Left); footer.Children.Add(listStatus);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        previous = SoftButton("← Previous", async (_, _) => { clubOffset = Math.Max(0, clubOffset - pageSize); await Refresh(); });
        next = SoftButton("Next →", async (_, _) => { clubOffset += pageSize; await Refresh(); });
        previous.IsEnabled = false; next.IsEnabled = false;
        buttons.Children.Add(previous); buttons.Children.Add(next);
        DockPanel.SetDock(buttons, Dock.Right); footer.Children.Add(buttons);
        DockPanel.SetDock(footer, Dock.Bottom); dock.Children.Add(footer);
        Grid.SetColumn(left, 0); root.Children.Add(left);

        var info = Card(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = new StackPanel() }, 15);
        stack = (StackPanel)((ScrollViewer)info.Child).Content;
        stack.Children.Add(Txt("CLUB PROFILE", 9, true, Teal));
        stack.Children.Add(Txt("Select a club", 20, true, Ink));
        stack.Children.Add(Txt("Club identity, squad links, staff, finances, facilities and stadium information appear when the imported save or live reader actually supplies them. Historical values are labelled as such.", 10, false, Muted, wrap: true));
        Grid.SetColumn(info, 1); root.Children.Add(info);

        async Task ShowClubSummaryAsync(AtlasClubRecord club)
        {
            var requestVersion = ++clubProfileVersion;
            var isLive = club.DataSource.Contains("live", StringComparison.OrdinalIgnoreCase);
            var (facts, squadRows, clubStaff, squadWages) = await Task.Run(async () =>
            {
                var loadedFacts = isLive
                    ? await _database.GetLiveClubFactsAsync(club.SourceId).ConfigureAwait(false)
                    : await _database.GetFmsaveClubFactsAsync(club.SourceId).ConfigureAwait(false);
                var loadedSquad = await _database.GetPlayersForClubAsync(club.SourceId, isLive, 5000).ConfigureAwait(false);
                var loadedStaff = isLive
                    ? new List<AtlasStaffRecord>()
                    : (await _database.SearchFmsaveStaffAsync(club.Name).ConfigureAwait(false))
                        .Where(x => string.Equals(x.Club?.Trim(), club.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                        .Take(100).ToList();
                var loadedWages = isLive
                    ? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    : await _database.GetLatestPlayerWageFactsAsync(
                        loadedSquad.Select(x => x.SourceId).ToList()).ConfigureAwait(false);
                return (loadedFacts, loadedSquad, loadedStaff, loadedWages);
            });
            if (requestVersion != clubProfileVersion) return;

            stack.Children.Clear();
            stack.Children.Add(Txt("CLUB PROFILE", 9, true, Teal));
            var clubUid = club.SourceId.StartsWith("FMSAVE:", StringComparison.OrdinalIgnoreCase) || club.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase)
                ? club.SourceId[(club.SourceId.IndexOf(':') + 1)..] : null;
            var clubLogoPath = await Task.Run(() => GraphicsResolver.Shared.ResolveClubLogo(clubUid));
            var clubHero = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 0, 8) };
            var clubLogo = new Border { Width = 58, Height = 58, Background = Brush("#202A35"), BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Margin = new Thickness(0, 0, 12, 0) };
            if (!string.IsNullOrWhiteSpace(clubLogoPath) && File.Exists(clubLogoPath))
            {
                try { var logo = new BitmapImage(); logo.BeginInit(); logo.CacheOption = BitmapCacheOption.OnLoad; logo.DecodePixelWidth = 128; logo.UriSource = new Uri(clubLogoPath, UriKind.Absolute); logo.EndInit(); logo.Freeze(); clubLogo.Child = new Image { Source = logo, Stretch = Stretch.Uniform }; }
                catch { clubLogo.Child = Txt("CLUB", 9, true, Teal); }
            }
            else clubLogo.Child = Txt("CLUB", 9, true, Teal);
            clubHero.Children.Add(clubLogo); clubHero.Children.Add(Txt(club.Name, 23, true, Ink, wrap: true));
            stack.Children.Add(clubHero);
            stack.Children.Add(SoftButton(_editorEnabled ? "Local club editor · ON" : "Enable local club editor", async (_, _) =>
            {
                _editorEnabled = !_editorEnabled;
                await ShowClubSummaryAsync(club);
            }));
            AddDetailRow(stack, "Nation", club.Nation ?? "Not decoded");
            AddDetailRow(stack, "Linked players", club.PlayerCount.ToString("N0"));
            AddDetailRow(stack, "Source", club.DataSource);
            AddDetailRow(stack, "Club record ID", club.SourceId);
            stack.Children.Add(Txt("CLUB DATA", 9, true, Teal) is var heading ? Configure(heading, new Thickness(0, 17, 0, 6)) : new TextBlock());

            if (facts.Count == 0)
                stack.Children.Add(Txt("No additional finance, facility, stadium or affiliation facts were decoded for this club. A club identity alone does not contain those details.", 10, false, Muted, wrap: true));
            foreach (var group in facts.GroupBy(x => x.Group))
            {
                stack.Children.Add(Txt(group.Key.Replace('_', ' ').ToUpperInvariant(), 8, true, Teal));
                foreach (var fact in group.Take(80)) AddDetailRow(stack, fact.Field.Replace('_', ' ').Replace('.', ' '), fact.Value);
            }
            if (_editorEnabled)
                stack.Children.Add(await BuildLocalEntityFactEditorAsync("club", club.SourceId, club.Name, facts));
            else
                stack.Children.Add(Txt("Enable the local club editor above to create auditable overrides for every imported club, finance, facility and stadium fact. These changes remain inside Atlas.", 9, false, Muted, wrap: true));
            if (isLive)
                stack.Children.Add(await BuildLiveEntityEditorAsync(
                    LiveEntityMemoryEditService.EntityKind.Club, club.SourceId, club.Name));

            stack.Children.Add(Txt("CLUB STAFF", 9, true, Teal) is var staffHeading ? Configure(staffHeading, new Thickness(0, 17, 0, 6)) : new TextBlock());
            if (isLive)
                stack.Children.Add(Txt("Live staff-to-club relationships are not validated by the current reader, so no staff are inferred here.", 9, false, Muted, wrap: true));
            else if (clubStaff.Count == 0)
                stack.Children.Add(Txt("No staff records with an exact matching club name were supplied by this save snapshot.", 9, false, Muted, wrap: true));
            else
            {
                AddDetailRow(stack, "Exact club-name matches", clubStaff.Count.ToString("N0"));
                var staffGrid = CreateGrid();
                staffGrid.Columns.Add(Column("NAME", "Name", 1.2));
                staffGrid.Columns.Add(Column("ROLE", "Job", 1));
                staffGrid.Columns.Add(Column("AGE", "Age", .4));
                staffGrid.ItemsSource = clubStaff;
                staffGrid.Height = Math.Min(260, Math.Max(100, clubStaff.Count * 32));
                stack.Children.Add(staffGrid);
            }

            stack.Children.Add(Txt("CLUB SQUAD", 9, true, Teal) is var squadTitle ? Configure(squadTitle, new Thickness(0, 18, 0, 6)) : new TextBlock());
            if (squadRows.Count == 0)
                stack.Children.Add(Txt("No player-to-club links were imported for this club. The identity reader can find club names without necessarily decoding the complete squad relationship for every record.", 10, false, Muted, wrap: true));
            else
            {
                var squadAnalysis = ClubSquadAnalysisService.Analyse(squadRows, squadWages);
                var squadMetrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 5, 0, 8) };
                squadMetrics.Children.Add(MetricCard("AVG AGE", squadAnalysis.AverageAge?.ToString("0.0") ?? "—", $"{squadAnalysis.AgeKnownCount}/{squadAnalysis.PlayerCount} known", "◷"));
                squadMetrics.Children.Add(MetricCard("AVG CA", squadAnalysis.AverageCurrentAbility?.ToString("0.0") ?? "—", $"{squadAnalysis.CurrentAbilityKnownCount}/{squadAnalysis.PlayerCount} known", "↗"));
                squadMetrics.Children.Add(MetricCard("AVG PA", squadAnalysis.AveragePotentialAbility?.ToString("0.0") ?? "—", $"{squadAnalysis.PotentialAbilityKnownCount}/{squadAnalysis.PlayerCount} known", "⌁"));
                squadMetrics.Children.Add(MetricCard("TOTAL VALUE", squadAnalysis.TotalValue?.ToString("N0", CultureInfo.InvariantCulture) ?? "—", $"{squadAnalysis.ValueKnownCount}/{squadAnalysis.PlayerCount} · source units", "◈"));
                stack.Children.Add(squadMetrics);
                stack.Children.Add(Txt("AGE PROFILE", 8, true, Teal));
                foreach (var band in squadAnalysis.AgeBands)
                    AddDetailRow(stack, band.Label, band.Count.ToString("N0", CultureInfo.InvariantCulture));
                stack.Children.Add(Txt("PRIMARY POSITION COVERAGE", 8, true, Teal));
                foreach (var position in squadAnalysis.PositionCounts.Take(12))
                    AddDetailRow(stack, position.Position, position.Count.ToString("N0", CultureInfo.InvariantCulture));
                AddDetailRow(stack, "Wages recorded", squadAnalysis.WageKnownCount.ToString("N0", CultureInfo.InvariantCulture) + "/" + squadAnalysis.PlayerCount.ToString("N0", CultureInfo.InvariantCulture));
                AddDetailRow(stack, "Wage total / mean", squadAnalysis.TotalWage.HasValue
                    ? $"{squadAnalysis.TotalWage.Value.ToString("N0", CultureInfo.InvariantCulture)} / {squadAnalysis.AverageWage?.ToString("N0", CultureInfo.InvariantCulture) ?? "—"} source units"
                    : "Unavailable");
                AddDetailRow(stack, "Home-grown at club", $"{squadAnalysis.HomeGrownClubYes} yes · {squadAnalysis.HomeGrownClubNo} no · {squadAnalysis.HomeGrownClubUnknown} unknown");
                AddDetailRow(stack, "Home-grown in nation", $"{squadAnalysis.HomeGrownNationYes} yes · {squadAnalysis.HomeGrownNationNo} no · {squadAnalysis.HomeGrownNationUnknown} unknown");
                AddDetailRow(stack, "Other data coverage", $"Age {squadAnalysis.AgeKnownCount}/{squadAnalysis.PlayerCount} · CA {squadAnalysis.CurrentAbilityKnownCount}/{squadAnalysis.PlayerCount} · PA {squadAnalysis.PotentialAbilityKnownCount}/{squadAnalysis.PlayerCount} · value {squadAnalysis.ValueKnownCount}/{squadAnalysis.PlayerCount}");
                stack.Children.Add(Txt("Player values and wages retain imported source units. Currency and wage pay period are not inferred from a numeric field alone.", 9, false, Muted, wrap: true));
                AddDetailRow(stack, "Roster rows loaded", squadRows.Count.ToString("N0"));
                var squadGrid = CreateGrid(); squadGrid.RowHeight = 34;
                squadGrid.Columns.Add(Column("PLAYER", "Name", 1.3));
                squadGrid.Columns.Add(Column("AGE", "Age", .4));
                squadGrid.Columns.Add(Column("POSITION", "Position", .7));
                squadGrid.Columns.Add(Column("CA", "DisplayAbility", .4));
                squadGrid.Columns.Add(Column("PA", "DisplayPotential", .4));
                squadGrid.ItemsSource = squadRows;
                squadGrid.Height = Math.Min(430, Math.Max(145, Math.Min(squadRows.Count, 12) * 35));
                squadGrid.MouseDoubleClick += async (_, _) =>
                {
                    if (squadGrid.SelectedItem is PlayerRecord selectedPlayer)
                    {
                        _selectedPlayer = selectedPlayer;
                        await NavigateAsync("Players");
                        await ShowPlayerProfileAsync(selectedPlayer);
                    }
                };
                stack.Children.Add(squadGrid);
            }
            stack.Children.Add(Txt("Finance rows may be historical snapshots. Confirm reporting dates and money units before relying on them.", 9, false, Muted, wrap: true));
        }

        dg.SelectionChanged += async (_, _) =>
        {
            if (dg.SelectedItem is not AtlasClubRecord club) return;
            try { await ShowClubSummaryAsync(club); }
            catch (Exception ex)
            {
                stack.Children.Clear();
                stack.Children.Add(Txt("CLUB PROFILE COULD NOT LOAD", 10, true, Brush("#A54E55")));
                stack.Children.Add(Txt(ex.Message, 10, false, Muted, wrap: true));
            }
        };

        async Task Refresh()
        {
            var requestVersion = ++clubRequestVersion;
            var query = search.Text.Trim();
            listStatus.Text = "Loading clubs…";
            previous.IsEnabled = next.IsEnabled = false;
            try
            {
                var (rows, count) = await Task.Run(async () =>
                {
                    var page = await _database.SearchClubDirectoryAsync(query, pageSize, clubOffset).ConfigureAwait(false);
                    var total = await _database.CountClubDirectoryAsync(query).ConfigureAwait(false);
                    return (page, total);
                });
                if (requestVersion != clubRequestVersion) return;
                dg.ItemsSource = rows;
                listStatus.Text = $"{rows.Count:N0} shown · {count:N0} source records";
                previous.IsEnabled = clubOffset > 0;
                next.IsEnabled = clubOffset + rows.Count < count && rows.Count == pageSize;
                if (!string.IsNullOrWhiteSpace(_pendingClubSourceId))
                {
                    var target = rows.FirstOrDefault(x => x.SourceId == _pendingClubSourceId);
                    if (target is not null) { dg.SelectedItem = target; dg.ScrollIntoView(target); }
                    _pendingClubSourceId = null;
                }
                else if (rows.Count == 0) { stack.Children.Clear(); stack.Children.Add(Txt("No clubs found", 20, true, Ink)); }
                else if (dg.SelectedItem is null)
                {
                    stack.Children.Clear();
                    stack.Children.Add(Txt("CLUB PROFILE", 9, true, Teal));
                    stack.Children.Add(Txt("Select a club", 20, true, Ink));
                    stack.Children.Add(Txt("Choose a row to load its club facts and linked squad. FM Atlas does not fetch thousands of squad rows until you open a club.", 10, false, Muted, wrap: true));
                }
            }
            catch (Exception ex)
            {
                if (requestVersion != clubRequestVersion) return;
                listStatus.Text = "Club search failed: " + ex.Message;
                stack.Children.Clear();
                stack.Children.Add(Txt("CLUB DIRECTORY COULD NOT LOAD", 10, true, Brush("#A54E55")));
                stack.Children.Add(Txt(ex.Message, 10, false, Muted, wrap: true));
            }
        }

        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        debounce.Tick += async (_, _) => { debounce.Stop(); clubOffset = 0; await Refresh(); };
        search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
        await Refresh();
        return root;
    }

    private async Task<UIElement> StaffPageAsync()
    {
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = Card(new StackPanel(), 14, new Thickness(0, 0, 0, 10)); var s = (StackPanel)header.Child; s.Children.Add(Txt("STAFF INTELLIGENCE", 9, true, Teal)); s.Children.Add(Txt("Staff directory", 20, true, Ink));
        s.Children.Add(Txt("Search staff and your human-manager record from the currently loaded FM27 career or an offline save snapshot. Live CA/PA, role, club and nation are shown where the running build exposes them; unsupported fields remain blank.", 10, false, Muted, wrap: true));
        Grid.SetRow(header, 0); root.Children.Add(header);
        var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.85, GridUnitType.Star) });
        var panel = Card(new DockPanel(), 14, new Thickness(0, 0, 9, 0)); var dock = (DockPanel)panel.Child; var search = Input("Search staff, club or ID");
        if (!string.IsNullOrWhiteSpace(_pendingStaffSearchQuery)) { search.Text = _pendingStaffSearchQuery; _pendingStaffSearchQuery = null; }
        DockPanel.SetDock(search, Dock.Top); dock.Children.Add(search);
        var staffStatus = Txt("Enter at least two characters to search the imported staff index.", 9, false, Muted, wrap: true); DockPanel.SetDock(staffStatus, Dock.Bottom); dock.Children.Add(staffStatus);
        var dg = CreateGrid(); dg.Columns.Add(Column("NAME", "Name", 1.3)); dg.Columns.Add(Column("ROLE", "Job", .9)); dg.Columns.Add(Column("CLUB", "Club", 1)); dg.Columns.Add(Column("AGE", "Age", .4)); dg.Columns.Add(Column("CA", "CurrentAbility", .35)); dg.Columns.Add(Column("PA", "PotentialAbility", .35)); dg.Columns.Add(Column("NATION", "Nation", .8)); dg.Columns.Add(Column("SOURCE / CONFIDENCE", "ValidationStatus", .9)); dg.ItemsSource = _staff; dock.Children.Add(dg); Grid.SetColumn(panel, 0); body.Children.Add(panel);
        var profile = Card(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel() }, 14); var profileStack = (StackPanel)((ScrollViewer)profile.Child).Content;
        profileStack.Children.Add(Txt("STAFF PROFILE", 9, true, Teal)); profileStack.Children.Add(Txt("Select a staff member", 19, true, Ink)); profileStack.Children.Add(Txt("Staff profiles are limited to fields actually present in the imported save.", 10, false, Muted, wrap: true));
        Grid.SetColumn(profile, 1); body.Children.Add(profile); Grid.SetRow(body, 1); root.Children.Add(body);
        var staffProfileVersion = 0;
        async Task ShowStaffAsync(AtlasStaffRecord person)
        {
            var requestVersion = ++staffProfileVersion;
            var facts = await RunDatabaseAsync(() => _database.GetFmsaveStaffFactsAsync(person.SourceId));
            if (requestVersion != staffProfileVersion) return;
            profileStack.Children.Clear(); profileStack.Children.Add(Txt("STAFF PROFILE", 9, true, Teal)); profileStack.Children.Add(Txt(person.Name, 22, true, Ink));
            profileStack.Children.Add(SoftButton(_editorEnabled ? "Local staff editor · ON" : "Enable local staff editor", async (_, _) =>
            {
                _editorEnabled = !_editorEnabled;
                await ShowStaffAsync(person);
            }));
            AddDetailRow(profileStack, "Role", person.Job); AddDetailRow(profileStack, "Club", person.Club); AddDetailRow(profileStack, "Age", person.Age?.ToString()); AddDetailRow(profileStack, "Nation", person.Nation); AddDetailRow(profileStack, "Current ability", person.CurrentAbility?.ToString() ?? "—"); AddDetailRow(profileStack, "Potential ability", person.PotentialAbility?.ToString() ?? "—"); AddDetailRow(profileStack, "Human manager", person.IsHumanManager ? "Yes" : "No / not identified"); AddDetailRow(profileStack, "Source / confidence", person.ValidationStatus); AddDetailRow(profileStack, "Record ID", person.SourceId);
            var preferenceFacts = facts.Where(x => x.Field.StartsWith("preference.", StringComparison.OrdinalIgnoreCase)).ToList();
            if (preferenceFacts.Count > 0)
            {
                profileStack.Children.Add(Txt("PREFERRED JOB MATCH · PROVISIONAL", 9, true, Teal) is var preferenceHeader ? Configure(preferenceHeader, new Thickness(0, 17, 0, 6)) : new TextBlock());
                var topJob = preferenceFacts.FirstOrDefault(x => x.Field.Equals("preference.top_job", StringComparison.OrdinalIgnoreCase));
                var preferenceSummary = preferenceFacts.FirstOrDefault(x => x.Field.Equals("preference.summary", StringComparison.OrdinalIgnoreCase));
                var highest = preferenceFacts.FirstOrDefault(x => x.Field.Equals("preference.highest_rating", StringComparison.OrdinalIgnoreCase));
                AddDetailRow(profileStack, "Preferred job", topJob?.Value ?? preferenceSummary?.Value ?? "No clear preferred job recorded");
                if (highest?.Value is not null) AddDetailRow(profileStack, "Highest preference rating", highest.Value + " / 20");
                foreach (var fact in preferenceFacts
                    .Where(x => !x.Field.Equals("preference.top_job", StringComparison.OrdinalIgnoreCase)
                        && !x.Field.Equals("preference.summary", StringComparison.OrdinalIgnoreCase)
                        && !x.Field.Equals("preference.highest_rating", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => double.TryParse(x.Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var score) ? score : -1)
                    .Take(15))
                    AddDetailRow(profileStack, fact.Field["preference.".Length..].Replace('_', ' '), fact.Value + " / 20");
                profileStack.Children.Add(Txt("FM27 stores broad role groups; analyst preference combines recruitment and performance analysis, and one group remains unresolved. Scores are not a guarantee of job suitability.", 9, false, Muted, wrap: true));
            }
            var coachingFacts = facts.Where(x => x.Field.StartsWith("staff_attributes.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Field, StringComparer.OrdinalIgnoreCase).ToList();
            if (coachingFacts.Count > 0)
            {
                profileStack.Children.Add(Txt("COACHING ATTRIBUTES · PROVISIONAL", 9, true, Teal) is var coachingHeader ? Configure(coachingHeader, new Thickness(0, 17, 0, 6)) : new TextBlock());
                foreach (var fact in coachingFacts)
                    AddDetailRow(profileStack, fact.Field["staff_attributes.".Length..].Replace('_', ' '), fact.Value is null ? "—" : fact.Value + " / 20");
            }

            var personalityFacts = facts.Where(x => x.Field.StartsWith("staff_personality.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Field, StringComparer.OrdinalIgnoreCase).ToList();
            if (personalityFacts.Count > 0)
            {
                profileStack.Children.Add(Txt("PERSONALITY · PROVISIONAL", 9, true, Teal) is var personalityHeader ? Configure(personalityHeader, new Thickness(0, 17, 0, 6)) : new TextBlock());
                foreach (var fact in personalityFacts)
                    AddDetailRow(profileStack, fact.Field["staff_personality.".Length..].Replace('_', ' '), fact.Value is null ? "—" : fact.Value + " / 20");
            }

            var contractFacts = facts.Where(x => x.Field.StartsWith("contract.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Field, StringComparer.OrdinalIgnoreCase).ToList();
            if (contractFacts.Count > 0)
            {
                profileStack.Children.Add(Txt("CONTRACT DETAILS · SOURCE VALUES", 9, true, Teal) is var contractHeader ? Configure(contractHeader, new Thickness(0, 17, 0, 6)) : new TextBlock());
                foreach (var fact in contractFacts)
                {
                    var field = fact.Field["contract.".Length..].Replace('_', ' ');
                    var value = fact.Field.Equals("contract.wage_per_week_gbp", StringComparison.OrdinalIgnoreCase)
                        && decimal.TryParse(fact.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var wage)
                            ? "£" + wage.ToString("N0", CultureInfo.InvariantCulture) + " / week"
                            : fact.Value;
                    AddDetailRow(profileStack, field, value);
                }
            }

            var remainingFacts = facts.Where(x =>
                !x.Field.StartsWith("preference.", StringComparison.OrdinalIgnoreCase)
                && !x.Field.StartsWith("staff_attributes.", StringComparison.OrdinalIgnoreCase)
                && !x.Field.StartsWith("staff_personality.", StringComparison.OrdinalIgnoreCase)
                && !x.Field.StartsWith("contract.", StringComparison.OrdinalIgnoreCase)).ToList();
            profileStack.Children.Add(Txt("IMPORTED STAFF FACTS", 9, true, Teal) is var h ? Configure(h, new Thickness(0, 17, 0, 6)) : new TextBlock());
            if (remainingFacts.Count == 0 && coachingFacts.Count == 0 && personalityFacts.Count == 0 && contractFacts.Count == 0 && preferenceFacts.Count == 0)
                profileStack.Children.Add(Txt("No additional staff facts were imported.", 10, false, Muted));
            foreach (var group in remainingFacts.GroupBy(x => x.Group))
            {
                profileStack.Children.Add(Txt(group.Key.ToUpperInvariant(), 8, true, Teal));
                foreach (var fact in group.Take(50)) AddDetailRow(profileStack, fact.Field.Replace('_', ' ').Replace('.', ' '), fact.Value);
            }
            if (_editorEnabled)
                profileStack.Children.Add(await BuildLocalEntityFactEditorAsync("staff", person.SourceId, person.Name, facts));
            else
                profileStack.Children.Add(Txt("Enable the local staff editor above to create auditable overrides for every imported staff fact. These changes remain inside Atlas.", 9, false, Muted, wrap: true));
            if (person.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase))
                profileStack.Children.Add(await BuildLiveEntityEditorAsync(
                    LiveEntityMemoryEditService.EntityKind.Staff, person.SourceId, person.Name));
        }

        dg.SelectionChanged += async (_, _) =>
        {
            if (dg.SelectedItem is not AtlasStaffRecord person) return;
            try { await ShowStaffAsync(person); }
            catch (Exception ex)
            {
                profileStack.Children.Clear();
                profileStack.Children.Add(Txt("STAFF PROFILE COULD NOT LOAD", 10, true, Brush("#A54E55")));
                profileStack.Children.Add(Txt(ex.Message, 10, false, Muted, wrap: true));
            }
        };
        var staffRequestVersion = 0;
        async Task Refresh()
        {
            var requestVersion = ++staffRequestVersion;
            var query = search.Text.Trim();
            if (query.Length < 2 && string.IsNullOrWhiteSpace(_pendingStaffSourceId))
            {
                _staff.Clear(); dg.ItemsSource = _staff;
                staffStatus.Text = "Type two or more characters. Staff records are not bulk-loaded on first open.";
                profileStack.Children.Clear(); profileStack.Children.Add(Txt("STAFF PROFILE", 9, true, Teal)); profileStack.Children.Add(Txt("Search for a staff member", 19, true, Ink));
                profileStack.Children.Add(Txt("Live FM27 staff identities and CA/PA appear here after Atlas connects to a loaded career. Historical and extended facts appear when supplied by an offline save snapshot.", 10, false, Muted, wrap: true));
                return;
            }
            staffStatus.Text = "Searching staff…";
            try
            {
                var rows = await RunDatabaseAsync(() => _database.SearchFmsaveStaffAsync(query));
                if (requestVersion != staffRequestVersion) return;
                _staff.Clear(); foreach (var r in rows) _staff.Add(r); dg.ItemsSource = _staff;
                staffStatus.Text = rows.Count == 0 ? "No staff records found in the imported sources. Import a supported career save." : $"{rows.Count:N0} staff records · bounded search results";
                if (!string.IsNullOrWhiteSpace(_pendingStaffSourceId)) { var target = rows.FirstOrDefault(x => x.SourceId == _pendingStaffSourceId); if (target is not null) { dg.SelectedItem = target; dg.ScrollIntoView(target); } _pendingStaffSourceId = null; }
            }
            catch (Exception ex)
            {
                if (requestVersion != staffRequestVersion) return;
                staffStatus.Text = "Staff search failed: " + ex.Message;
                profileStack.Children.Clear();
                profileStack.Children.Add(Txt("STAFF DIRECTORY COULD NOT LOAD", 10, true, Brush("#A54E55")));
                profileStack.Children.Add(Txt(ex.Message, 10, false, Muted, wrap: true));
            }
        }
        var staffSearchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        staffSearchDebounce.Tick += async (_, _) => { staffSearchDebounce.Stop(); await Refresh(); };
        search.TextChanged += (_, _) => { staffSearchDebounce.Stop(); staffSearchDebounce.Start(); };
        await Refresh();
        return root;
    }

    private async Task<UIElement> SquadArchitectPageAsync()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var (allPlayers, sourceLabel) = await Task.Run(async () =>
        {
            // Merge both sources: a partial save snapshot must not hide the much larger
            // live identity index (the earlier R5 implementation did exactly that).
            const int sourceLimit = 50000;
            var localRowsTask = _database.SearchAsync("", null, null, null, null, null, null, limit: sourceLimit);
            var liveRowsTask = _database.SearchLivePlayersAsync("", sourceLimit);
            await Task.WhenAll(localRowsTask, liveRowsTask).ConfigureAwait(false);
            var localRows = await localRowsTask.ConfigureAwait(false);
            var liveRows = await liveRowsTask.ConfigureAwait(false);
            static string Identity(PlayerRecord p) => $"{p.Name.Trim().ToUpperInvariant()}|{(p.Club ?? string.Empty).Trim().ToUpperInvariant()}|{p.Age?.ToString() ?? "?"}";
            static int Completeness(PlayerRecord p) =>
                (p.Age.HasValue ? 1 : 0) + (p.Nation is not null ? 1 : 0) + (p.Position is not null ? 1 : 0) +
                (p.CurrentAbility.HasValue ? 1 : 0) + (p.PotentialAbility.HasValue ? 1 : 0) + (p.Value.HasValue ? 1 : 0) +
                (p.League is not null ? 1 : 0) + (p.PreferredFoot is not null ? 1 : 0);
            var merged = localRows.Concat(liveRows)
                .GroupBy(Identity, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(Completeness).First())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var label = $"Combined sources · {liveRows.Count:N0} live identities + {localRows.Count:N0} save/local records";
            return (merged, label);
        });

        // Load source evidence once; the view never synthesizes ratings or wage units.
        var positionRatingsBySource = await RunDatabaseAsync(() => _database.GetImportedPositionSuitabilityAsync());
        var wageBySource = await RunDatabaseAsync(() => _database.GetLatestPlayerWageFactsAsync());
        var canonicalPositions = PositionSuitabilityService.MapSourceRatings(Array.Empty<KeyValuePair<string, double?>>());

        var header = Card(new Grid(), 15, new Thickness(0, 0, 0, 10));
        var headerGrid = (Grid)header.Child;
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(Txt("SQUAD ARCHITECT", 9, true, Teal));
        title.Children.Add(Txt("Squad structure & succession", 21, true, Ink));
        title.Children.Add(Txt("Inspect the imported club squad, age profile and positional cover. Team slot IDs are retained, but senior/reserve/youth labels are not guessed when the source does not expose a validated mapping.", 10, false, Muted, wrap: true));
        headerGrid.Children.Add(title);
        var filter = new StackPanel { Width = 245, VerticalAlignment = VerticalAlignment.Center };
        filter.Children.Add(Txt("CLUB", 8, true, Teal));
        var clubPicker = new ComboBox { Padding = new Thickness(9), Margin = new Thickness(0, 4, 0, 0), MinHeight = 38, IsTextSearchEnabled = true, MaxDropDownHeight = 480 };
        VirtualizingPanel.SetIsVirtualizing(clubPicker, true);
        VirtualizingPanel.SetVirtualizationMode(clubPicker, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(clubPicker, true);
        var clubs = allPlayers.Select(x => x.Club).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        clubPicker.ItemsSource = new[] { "All clubs" }.Concat(clubs).ToList(); clubPicker.SelectedIndex = 0;
        filter.Children.Add(clubPicker); Grid.SetColumn(filter, 1); headerGrid.Children.Add(filter);
        root.Children.Add(header);

        var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(metrics, 1); root.Children.Add(metrics);
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.8, GridUnitType.Star) });
        var rosterCard = Card(new DockPanel(), 15, new Thickness(0, 0, 8, 0));
        var rosterDock = (DockPanel)rosterCard.Child;
        var rosterTitle = new StackPanel(); rosterTitle.Children.Add(Txt("SQUAD ROSTER", 9, true, Teal)); rosterTitle.Children.Add(Txt("Current registered players", 17, true, Ink));
        DockPanel.SetDock(rosterTitle, Dock.Top); rosterDock.Children.Add(rosterTitle);
        var roster = CreateGrid();
        roster.Columns.Add(Column("PLAYER", "Name", 1.12)); roster.Columns.Add(Column("POSITION", "Position", .68)); roster.Columns.Add(Column("LEAGUE", "League", .82)); roster.Columns.Add(Column("AGE", "Age", .4)); roster.Columns.Add(Column("CA", "DisplayAbility", .38)); roster.Columns.Add(Column("PA", "DisplayPotential", .38)); roster.Columns.Add(Column("HOME-GROWN", "DisplayHomeGrownClub", .62));
        rosterDock.Children.Add(roster); Grid.SetColumn(rosterCard, 0); Grid.SetRow(rosterCard, 0); body.Children.Add(rosterCard);

        var insightCard = Card(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel() }, 15);
        var insight = (StackPanel)((ScrollViewer)insightCard.Child).Content;
        Grid.SetColumn(insightCard, 1); Grid.SetRow(insightCard, 0); body.Children.Add(insightCard);
        root.Children.Add(body); Grid.SetRow(body, 2);

        async Task RefreshSquad()
        {
            var selectedClub = clubPicker.SelectedItem?.ToString();
            // Filtering, sorting, grouping and aggregate calculations can touch 40K rows.
            // Do all of that away from the dispatcher; only the compact summary is rendered here.
            var snapshot = await Task.Run(() =>
            {
                var squad = string.IsNullOrWhiteSpace(selectedClub) || selectedClub == "All clubs"
                    ? allPlayers
                    : allPlayers.Where(x => string.Equals(x.Club, selectedClub, StringComparison.OrdinalIgnoreCase)).ToList();
                var ordered = squad.OrderBy(x => x.Position).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var ages = squad.Where(x => x.Age.HasValue).Select(x => (double)x.Age!.Value).ToList();
                var cas = squad.Where(x => x.CurrentAbility.HasValue).Select(x => (double)x.CurrentAbility!.Value).ToList();
                var pas = squad.Where(x => x.PotentialAbility.HasValue).Select(x => (double)x.PotentialAbility!.Value).ToList();
                var positions = squad.GroupBy(x => string.IsNullOrWhiteSpace(x.Position) ? "Unspecified" : x.Position!)
                    .Select(g => (Position: g.Key, Count: g.Count())).OrderByDescending(x => x.Count).ThenBy(x => x.Position).ToList();
                var suitability = canonicalPositions.Select(slot =>
                {
                    var ratings = squad
                        .Where(player => positionRatingsBySource.ContainsKey(player.SourceId))
                        .SelectMany(player => positionRatingsBySource[player.SourceId])
                        .Where(entry => entry.Code.Equals(slot.Code, StringComparison.OrdinalIgnoreCase) && entry.Rating.HasValue)
                        .Select(entry => entry.Rating!.Value)
                        .ToList();
                    return (slot.Code, slot.Label, RatedPlayers: ratings.Count,
                        CompetentPlus: ratings.Count(value => value >= 10),
                        Average: ratings.Count == 0 ? (double?)null : ratings.Average());
                }).ToList();
                var wageRows = squad
                    .Where(player => wageBySource.ContainsKey(player.SourceId))
                    .Select(player => wageBySource[player.SourceId])
                    .Where(value => double.IsFinite(value) && value > 0)
                    .ToList();
                var homeGrownClubYes = squad.Count(player => player.HomeGrownClub == true);
                var homeGrownClubNo = squad.Count(player => player.HomeGrownClub == false);
                var homeGrownNationYes = squad.Count(player => player.HomeGrownNation == true);
                var homeGrownNationNo = squad.Count(player => player.HomeGrownNation == false);
                var bands = new[]
                {
                    (Label: "U21", Min: 0, Max: 20), (Label: "21–24", Min: 21, Max: 24),
                    (Label: "25–29", Min: 25, Max: 29), (Label: "30+", Min: 30, Max: 100)
                }.Select(b => (b.Label, Count: squad.Count(x => x.Age.HasValue && x.Age.Value >= b.Min && x.Age.Value <= b.Max))).ToList();
                return (Squad: ordered, AverageAge: ages.Count == 0 ? (double?)null : ages.Average(),
                    AverageCa: cas.Count == 0 ? (double?)null : cas.Average(), AveragePa: pas.Count == 0 ? (double?)null : pas.Average(),
                    Positions: positions, Suitability: suitability, AgeBands: bands,
                    WageCount: wageRows.Count, WageTotal: wageRows.Count == 0 ? (double?)null : wageRows.Sum(),
                    WageAverage: wageRows.Count == 0 ? (double?)null : wageRows.Average(),
                    HomeGrownClubYes: homeGrownClubYes, HomeGrownClubNo: homeGrownClubNo,
                    HomeGrownNationYes: homeGrownNationYes, HomeGrownNationNo: homeGrownNationNo);
            });
            roster.ItemsSource = snapshot.Squad;
            metrics.Children.Clear();
            metrics.Children.Add(MetricCard("PLAYERS", snapshot.Squad.Count.ToString("N0"), sourceLabel, "♟"));
            metrics.Children.Add(MetricCard("AVERAGE AGE", snapshot.AverageAge?.ToString("0.0") ?? "—", "Where available", "◷"));
            metrics.Children.Add(MetricCard("AVERAGE CA", snapshot.AverageCa?.ToString("0.0") ?? "—", "Parser values · unconfirmed", "↗"));
            metrics.Children.Add(MetricCard("AVERAGE PA", snapshot.AveragePa?.ToString("0.0") ?? "—", "Parser values · unconfirmed", "⌁"));
            insight.Children.Clear();
            insight.Children.Add(Txt("POSITIONAL COVER", 9, true, Teal)); insight.Children.Add(Txt("Imported primary-position counts", 18, true, Ink));
            foreach (var position in snapshot.Positions) AddDetailRow(insight, position.Position, position.Count.ToString("N0") + " primary-position labels");
            insight.Children.Add(Txt("SOURCE-BACKED POSITION SUITABILITY", 9, true, Teal) is var suitabilityTitle ? Configure(suitabilityTitle, new Thickness(0, 16, 0, 5)) : new TextBlock());
            insight.Children.Add(Txt("Imported explicit 1–20 position ratings only. Competent+ is 10/20 or higher; missing ratings are left unrated.", 9, false, Muted, wrap: true));
            foreach (var position in snapshot.Suitability)
            {
                var value = position.RatedPlayers == 0
                    ? "No imported ratings"
                    : $"{position.RatedPlayers:N0} rated · {position.CompetentPlus:N0} competent+ · Avg {position.Average:0.0}/20";
                AddDetailRow(insight, $"{position.Code} · {position.Label}", value);
            }
            insight.Children.Add(Txt("CONTRACT & HOME-GROWN DATA", 9, true, Teal) is var contractTitle ? Configure(contractTitle, new Thickness(0, 16, 0, 5)) : new TextBlock());
            AddDetailRow(insight, "Recorded wage field", snapshot.WageCount == 0 ? "Unreported" : $"{snapshot.WageCount:N0} records · source units");
            AddDetailRow(insight, "Wage total / mean", snapshot.WageTotal.HasValue ? $"{snapshot.WageTotal.Value:N0} / {snapshot.WageAverage!.Value:N0} source units" : "Unavailable");
            AddDetailRow(insight, "Home-grown at club", $"{snapshot.HomeGrownClubYes:N0} yes · {snapshot.HomeGrownClubNo:N0} no · {Math.Max(0, snapshot.Squad.Count - snapshot.HomeGrownClubYes - snapshot.HomeGrownClubNo):N0} unknown");
            AddDetailRow(insight, "Home-grown in nation", $"{snapshot.HomeGrownNationYes:N0} yes · {snapshot.HomeGrownNationNo:N0} no · {Math.Max(0, snapshot.Squad.Count - snapshot.HomeGrownNationYes - snapshot.HomeGrownNationNo):N0} unknown");
            insight.Children.Add(Txt("AGE PROFILE", 9, true, Teal) is var ageTitle ? Configure(ageTitle, new Thickness(0, 16, 0, 5)) : new TextBlock());
            foreach (var band in snapshot.AgeBands) AddDetailRow(insight, band.Label, band.Count.ToString("N0"));
            insight.Children.Add(Txt("SOURCE & LIMITS", 9, true, Teal) is var limits ? Configure(limits, new Thickness(0, 16, 0, 5)) : new TextBlock());
            insight.Children.Add(Txt("This view merges the live identity index and save/local player rows. Live-only records can lack positions, abilities, contracts and other fields. Team slot labels (Senior, B-team, Reserve or U21) are not guessed unless the source mapping is validated.", 9, false, Muted, wrap: true));
            if (snapshot.Squad.Count == 0) insight.Children.Add(Txt("No players are linked to this club in the current imported sources.", 10, false, Muted));
        }
        clubPicker.SelectionChanged += async (_, _) => await RefreshSquad();
        await RefreshSquad();
        return root;
    }

    private async Task<UIElement> TransfersPageAsync()
    {
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var head = Card(new StackPanel(), 14); var stack = (StackPanel)head.Child; stack.Children.Add(Txt("TRANSFER INTELLIGENCE", 9, true, Teal)); stack.Children.Add(Txt("Player market", 20, true, Ink));
        stack.Children.Add(Txt("This view ranks imported players by available value and ability. Transfer-list flags, asking prices and contract expiry should only be shown once imported from the save and mapped to the player; this build does not infer availability.", 10, false, Muted, wrap: true));
        root.Children.Add(head);
        var body = Card(new DockPanel(), 14); var grid = CreateGrid(); grid.Columns.Add(Column("PLAYER", "Name", 1.4)); grid.Columns.Add(Column("CLUB", "Club", 1.2)); grid.Columns.Add(Column("AGE", "Age", .4)); grid.Columns.Add(Column("CA", "DisplayAbility", .4)); grid.Columns.Add(Column("PA", "DisplayPotential", .4)); grid.Columns.Add(Column("VALUE", "DisplayValue", .8)); body.Child = grid; Grid.SetRow(body, 1); root.Children.Add(body);
        var ranked = await Task.Run(async () =>
        {
            var players = await _database.SearchAsync("", null, null, null, null, null, null, limit: 5000).ConfigureAwait(false);
            if (players.Count == 0) players = await _database.SearchLivePlayersAsync("", 5000).ConfigureAwait(false);
            return players.OrderByDescending(p => p.Value ?? 0).Take(1000).ToList();
        });
        grid.ItemsSource = ranked;
        return root;
    }

    private async Task<UIElement> ShortlistsPageAsync()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });

        var left = Card(new DockPanel(), 14);
        var dock = (DockPanel)left.Child;
        var top = new StackPanel();
        top.Children.Add(Txt("RECRUITMENT", 9, true, Teal));
        top.Children.Add(Txt("Candidate pipeline", 20, true, Ink));
        top.Children.Add(Txt("Track each target from identification through scouting, contact, offer and signing. Stages and notes are stored locally in your Atlas database.", 10, false, Muted, wrap: true));
        var controls = new WrapPanel { Margin = new Thickness(0, 5, 0, 5) };
        _shortlistPicker = new ComboBox { Width = 210, Margin = new Thickness(0, 3, 7, 7), Padding = new Thickness(8) };
        controls.Children.Add(FilterControl("SHORTLIST", _shortlistPicker, 220));
        controls.Children.Add(SoftButton("＋ New", async (_, _) => await CreateNewShortlistAsync()));
        var stageFilter = new ComboBox { Width = 155, Height = 34, Margin = new Thickness(0, 3, 7, 7), FontSize = 10 };
        stageFilter.Items.Add("All stages");
        foreach (var stage in ShortlistRecord.PipelineStages) stageFilter.Items.Add(stage);
        stageFilter.SelectedIndex = 0;
        var priorityFilter = new ComboBox { Width = 145, Height = 34, Margin = new Thickness(0, 3, 7, 7), FontSize = 10 };
        priorityFilter.Items.Add("All priorities");
        foreach (var priority in ShortlistRecord.PipelinePriorities) priorityFilter.Items.Add(priority);
        priorityFilter.SelectedIndex = 0;
        controls.Children.Add(FilterControl("STAGE FILTER", stageFilter, 165));
        controls.Children.Add(FilterControl("PRIORITY FILTER", priorityFilter, 155));
        top.Children.Add(controls);
        var pipelineSummary = Txt("Loading recruitment pipeline…", 9, false, Muted, wrap: true);
        pipelineSummary.Margin = new Thickness(0, 2, 0, 9);
        top.Children.Add(pipelineSummary);
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);

        TextBlock selectedPlayerLabel = null!;
        ComboBox stagePicker = null!;
        ComboBox priorityPicker = null!;
        TextBox targetFeeBox = null!;
        TextBox targetWageBox = null!;
        TextBox nextActionBox = null!;
        DatePicker reviewDatePicker = null!;
        TextBox notesEditor = null!;
        TextBlock detailsStatus = null!;
        Button saveDetails = null!;

        var dg = CreateGrid();
        dg.SelectionMode = DataGridSelectionMode.Single;
        dg.Columns.Add(Column("PLAYER", "PlayerName", 1.2));
        dg.Columns.Add(Column("CLUB", "DisplayClub", 1));
        dg.Columns.Add(Column("POSITION", "DisplayPosition", .65));
        dg.Columns.Add(Column("PRIORITY", "Priority", .65));
        dg.Columns.Add(Column("STAGE", "Stage", .9));
        dg.Columns.Add(Column("TARGET FEE · SOURCE UNITS", "TargetFee", .9));
        dg.Columns.Add(Column("TARGET WAGE · SOURCE UNITS", "TargetWage", .9));
        dg.Columns.Add(Column("NEXT ACTION", "NextAction", 1.1));
        dg.Columns.Add(Column("REVIEW", "ReviewDate", .75));
        dg.SelectionChanged += (_, _) =>
        {
            if (dg.SelectedItem is ShortlistRecord selected)
            {
                stagePicker.SelectedItem = ShortlistRecord.PipelineStages.FirstOrDefault(x =>
                    x.Equals(selected.Stage, StringComparison.OrdinalIgnoreCase)) ?? ShortlistRecord.DefaultStage;
                priorityPicker.SelectedItem = ShortlistRecord.PipelinePriorities.FirstOrDefault(x =>
                    x.Equals(selected.Priority, StringComparison.OrdinalIgnoreCase)) ?? ShortlistRecord.DefaultPriority;
                targetFeeBox.Text = selected.TargetFee?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "";
                targetWageBox.Text = selected.TargetWage?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "";
                nextActionBox.Text = selected.NextAction ?? "";
                reviewDatePicker.SelectedDate = DateTime.TryParseExact(selected.ReviewDate, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var reviewDate)
                    ? reviewDate : null;
                notesEditor.Text = selected.Notes ?? "";
                selectedPlayerLabel.Text = selected.PlayerName + (string.IsNullOrWhiteSpace(selected.DisplayClub) ? "" : " · " + selected.DisplayClub);
                detailsStatus.Text = "Edit this candidate's pipeline stage or private recruitment notes.";
                saveDetails.IsEnabled = true;
            }
            else
            {
                selectedPlayerLabel.Text = "Select a candidate";
                stagePicker.SelectedItem = ShortlistRecord.DefaultStage;
                priorityPicker.SelectedItem = ShortlistRecord.DefaultPriority;
                targetFeeBox.Text = "";
                targetWageBox.Text = "";
                nextActionBox.Text = "";
                reviewDatePicker.SelectedDate = null;
                notesEditor.Text = "";
                detailsStatus.Text = "Select a player from the shortlist to edit their stage, priority, target and follow-up details.";
                saveDetails.IsEnabled = false;
            }
        };

        var allPipelineRows = new List<ShortlistRecord>();
        async Task RefreshPipelineRowsAsync()
        {
            if (_shortlistPicker?.SelectedItem is not string selectedList) return;
            allPipelineRows = await RunDatabaseAsync(() => _database.GetShortlistAsync(selectedList));
            var selectedStage = stageFilter.SelectedItem?.ToString();
            var selectedPriority = priorityFilter.SelectedItem?.ToString();
            var visibleRows = allPipelineRows.Where(row =>
                (string.IsNullOrWhiteSpace(selectedStage) || selectedStage == "All stages" ||
                 row.Stage.Equals(selectedStage, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(selectedPriority) || selectedPriority == "All priorities" ||
                 row.Priority.Equals(selectedPriority, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            _shortlist.Clear();
            foreach (var row in visibleRows) _shortlist.Add(row);
            dg.ItemsSource = _shortlist;

            var today = DateOnly.FromDateTime(DateTime.Today);
            var reviewsDue = allPipelineRows.Count(row =>
                DateOnly.TryParseExact(row.ReviewDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date) && date <= today);
            var highPriority = allPipelineRows.Count(row => row.Priority.Equals("High", StringComparison.OrdinalIgnoreCase));
            var feeTotal = allPipelineRows.Where(row => row.TargetFee.HasValue).Sum(row => row.TargetFee!.Value);
            pipelineSummary.Text =
                $"{allPipelineRows.Count:N0} candidate(s) · {highPriority:N0} high priority · {reviewsDue:N0} review(s) due · " +
                $"specified target fees total {feeTotal:N0} source units · showing {visibleRows.Count:N0}";
        }

        stageFilter.SelectionChanged += async (_, _) =>
        {
            dg.SelectedItem = null;
            await RefreshPipelineRowsAsync();
        };
        priorityFilter.SelectionChanged += async (_, _) =>
        {
            dg.SelectedItem = null;
            await RefreshPipelineRowsAsync();
        };

        dg.MouseDoubleClick += async (_, _) =>
        {
            if (dg.SelectedItem is not ShortlistRecord selected) return;
            var player = new PlayerRecord { SourceId = selected.SourceId, Name = selected.PlayerName, Club = selected.DisplayClub, Position = selected.DisplayPosition };
            _selectedPlayer = player;
            await NavigateAsync("Players");
            try { await ShowPlayerProfileAsync(player); }
            catch (Exception ex) { ClearProfile("Could not open this shortlist player: " + ex.Message); }
        };
        dock.Children.Add(dg);

        var side = new StackPanel();
        var right = Card(new ScrollViewer
        {
            Content = side,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        }, 14);
        side.Children.Add(Txt("RECRUITMENT WORKFLOW", 9, true, Teal));
        side.Children.Add(Txt("Candidate details", 19, true, Ink));
        selectedPlayerLabel = Txt("Select a candidate", 11, true, Muted, wrap: true);
        selectedPlayerLabel.Margin = new Thickness(0, 4, 0, 12);
        side.Children.Add(selectedPlayerLabel);

        stagePicker = new ComboBox
        {
            ItemsSource = ShortlistRecord.PipelineStages,
            SelectedItem = ShortlistRecord.DefaultStage,
            Height = 36,
            Margin = new Thickness(0, 3, 0, 10),
            FontSize = 11
        };
        side.Children.Add(FilterControl("PIPELINE STAGE", stagePicker, 330));
        priorityPicker = new ComboBox
        {
            ItemsSource = ShortlistRecord.PipelinePriorities,
            SelectedItem = ShortlistRecord.DefaultPriority,
            Height = 36,
            Margin = new Thickness(0, 3, 0, 10),
            FontSize = 11
        };
        side.Children.Add(FilterControl("RECRUITMENT PRIORITY", priorityPicker, 330));

        targetFeeBox = Input("Target fee · source units");
        targetFeeBox.Width = 200;
        side.Children.Add(FilterControl("TARGET TRANSFER FEE · SOURCE UNITS", targetFeeBox, 330));
        targetWageBox = Input("Target wage · source units");
        targetWageBox.Width = 200;
        side.Children.Add(FilterControl("TARGET WAGE · SOURCE UNITS", targetWageBox, 330));
        nextActionBox = Input("Next recruitment action");
        nextActionBox.Width = 320;
        side.Children.Add(FilterControl("NEXT ACTION · MAX 300 CHARACTERS", nextActionBox, 340));
        reviewDatePicker = new DatePicker
        {
            SelectedDateFormat = DatePickerFormat.Short,
            Height = 36,
            Margin = new Thickness(0, 3, 0, 10)
        };
        side.Children.Add(FilterControl("NEXT REVIEW DATE", reviewDatePicker, 330));

        notesEditor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 140,
            MaxHeight = 220,
            MaxLength = 2000,
            Padding = new Thickness(9),
            Margin = new Thickness(0, 4, 0, 10),
            Background = Brush("#171F28"),
            Foreground = Ink,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            ToolTip = "Private scouting notes for this shortlist candidate. Maximum 2,000 characters."
        };
        side.Children.Add(FilterControl("SCOUTING NOTES", notesEditor, 340));
        detailsStatus = Txt("Select a player from the shortlist to edit their stage and notes.", 9, false, Muted, wrap: true);
        detailsStatus.Margin = new Thickness(0, 5, 0, 6);
        side.Children.Add(detailsStatus);
        double? ReadOptionalSourceAmount(TextBox box, string label)
        {
            if (string.IsNullOrWhiteSpace(box.Text)) return null;
            if (!double.TryParse(box.Text.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount)
                || !double.IsFinite(amount) || amount < 0 || amount > 1_000_000_000_000d)
                throw new FormatException($"{label} must be a finite non-negative amount in source units.");
            return amount;
        }
        saveDetails = PrimaryButton("Save candidate details", async (_, _) =>
        {
            if (dg.SelectedItem is not ShortlistRecord selected || _shortlistPicker?.SelectedItem is not string name) return;
            try
            {
                await RunDatabaseAsync(() => _database.UpdateShortlistDetailsAsync(
                    selected.SourceId, name, stagePicker.SelectedItem?.ToString() ?? ShortlistRecord.DefaultStage,
                    notesEditor.Text, priorityPicker.SelectedItem?.ToString() ?? ShortlistRecord.DefaultPriority,
                    ReadOptionalSourceAmount(targetFeeBox, "Target fee"),
                    ReadOptionalSourceAmount(targetWageBox, "Target wage"),
                    nextActionBox.Text, reviewDatePicker.SelectedDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
                await RefreshPipelineRowsAsync();
                detailsStatus.Text = $"Saved {selected.PlayerName} · {stagePicker.SelectedItem} · {priorityPicker.SelectedItem}.";
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Shortlist candidate details could not be saved.", ex);
                detailsStatus.Text = "Could not save candidate details: " + ex.Message;
            }
        });
        saveDetails.IsEnabled = false;
        side.Children.Add(saveDetails);
        side.Children.Add(SoftButton("Remove selected", async (_, _) =>
        {
            if (dg.SelectedItem is ShortlistRecord item && _shortlistPicker.SelectedItem is string name)
            {
                await RunDatabaseAsync(() => _database.RemoveFromShortlistAsync(item.SourceId, name));
                await RefreshPipelineRowsAsync();
            }
        }));
        side.Children.Add(SoftButton("Export shortlist CSV", (_, _) => ExportShortlistCsv()));

        Grid.SetColumn(left, 0);
        root.Children.Add(left);
        Grid.SetColumn(right, 1);
        root.Children.Add(right);

        var names = await RunDatabaseAsync(() => _database.GetShortlistNamesAsync());
        _shortlistPicker.ItemsSource = names;
        _shortlistPicker.SelectedIndex = 0;
        _shortlistPicker.SelectionChanged += async (_, _) =>
        {
            dg.SelectedItem = null;
            await RefreshPipelineRowsAsync();
        };
        await RefreshPipelineRowsAsync();
        return root;
    }


    private async Task<UIElement> WorldEditorPageAsync()
    {
        var root = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var directory = Card(new DockPanel(), 14, new Thickness(0, 0, 10, 0));
        var directoryDock = (DockPanel)directory.Child;
        var controls = new StackPanel();
        controls.Children.Add(Txt("WORLD EDITOR", 9, true, Teal));
        controls.Children.Add(Txt("FM save directories", 20, true, Ink));
        controls.Children.Add(Txt("This workspace indexes every extra directory exported by the read-only FM save bridge. Select a table and record to inspect its actual imported fields. No values are invented.", 9, false, Muted, wrap: true));

        var tables = await RunDatabaseAsync(() => _database.GetFmsaveExtraTableNamesAsync());
        var tablePicker = new ComboBox
        {
            ItemsSource = tables,
            DisplayMemberPath = nameof(FmsaveExtraTableSummary.TableName),
            Height = 36,
            Margin = new Thickness(0, 10, 0, 6),
            IsEditable = false
        };
        controls.Children.Add(FieldLabel("SOURCE TABLE"));
        controls.Children.Add(tablePicker);
        var search = Input("Search names, IDs or imported values");
        search.Margin = new Thickness(0, 6, 0, 6);
        controls.Children.Add(search);
        var tableStatus = Txt($"{tables.Count:N0} imported directories available", 8, true, Muted);
        controls.Children.Add(tableStatus);
        DockPanel.SetDock(controls, Dock.Top);
        directoryDock.Children.Add(controls);

        var rowsGrid = CreateGrid();
        rowsGrid.IsReadOnly = true;
        rowsGrid.SelectionMode = DataGridSelectionMode.Single;
        rowsGrid.Columns.Add(Column("RECORD", nameof(FmsaveExtraRowRecord.Label), 1.1));
        rowsGrid.Columns.Add(Column("ROW", nameof(FmsaveExtraRowRecord.RowIndex), .45));
        rowsGrid.ItemsSource = new ObservableCollection<FmsaveExtraRowRecord>();
        directoryDock.Children.Add(rowsGrid);
        Grid.SetColumn(directory, 0);
        root.Children.Add(directory);

        var detail = Card(new DockPanel(), 14);
        var detailDock = (DockPanel)detail.Child;
        var detailHeader = new StackPanel();
        detailHeader.Children.Add(Txt("ENTITY EDITOR", 9, true, Teal));
        detailHeader.Children.Add(Txt("Choose a record", 22, true, Ink));
        detailHeader.Children.Add(Txt("The editor exposes all scalar properties in the selected raw save record, including nested objects and indexed array elements. Pointer/list operations are not silently converted into numeric writes.", 9, false, Muted, wrap: true));
        var toggle = _editorEnabled
            ? SoftButton("Disable local editor", async (_, _) => { _editorEnabled = false; await NavigateAsync("World Editor"); })
            : PrimaryButton("Enable local editor", async (_, _) => { _editorEnabled = true; await NavigateAsync("World Editor"); });
        detailHeader.Children.Add(toggle);
        var editorMessage = Txt("Select a row on the left. Enable the local editor to change its imported values and save an auditable Atlas override.", 10, false, Muted, wrap: true);
        detailHeader.Children.Add(editorMessage);
        DockPanel.SetDock(detailHeader, Dock.Top);
        detailDock.Children.Add(detailHeader);
        var detailScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var detailStack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        detailScroll.Content = detailStack;
        detailDock.Children.Add(detailScroll);
        Grid.SetColumn(detail, 1);
        root.Children.Add(detail);

        async Task RefreshWorldRowsAsync()
        {
            if (tablePicker.SelectedItem is not FmsaveExtraTableSummary selectedTable) return;
            try
            {
                var results = await RunDatabaseAsync(() => _database.SearchFmsaveExtraRowsAsync(selectedTable.TableName, search.Text, 1_000));
                rowsGrid.ItemsSource = new ObservableCollection<FmsaveExtraRowRecord>(results);
                tableStatus.Text = $"{selectedTable.Count:N0} rows in source · showing {results.Count:N0}";
                detailStack.Children.Clear();
                editorMessage.Text = "Select a record from the refreshed list.";
            }
            catch (Exception ex)
            {
                tableStatus.Text = "Directory search failed · " + ex.Message;
                AtlasLog.Error("World Editor directory search failed.", ex);
            }
        }
        // Declare the event handler only after all captured controls have been initialised.
        var searchButton = PrimaryButton("Search selected directory", async (_, _) => await RefreshWorldRowsAsync());
        controls.Children.Add(searchButton);

        rowsGrid.SelectionChanged += async (_, _) =>
        {
            if (rowsGrid.SelectedItem is not FmsaveExtraRowRecord selectedRow) return;
            try
            {
                var facts = await RunDatabaseAsync(() => _database.GetFmsaveExtraRowFactsAsync(
                    selectedRow.SnapshotId, selectedRow.TableName, selectedRow.RowIndex));
                detailStack.Children.Clear();
                detailStack.Children.Add(Txt(selectedRow.TableName.ToUpperInvariant(), 9, true, Teal));
                detailStack.Children.Add(Txt(selectedRow.Label, 19, true, Ink, wrap: true));
                AddDetailRow(detailStack, "Source row", selectedRow.RowIndex.ToString(CultureInfo.InvariantCulture));
                AddDetailRow(detailStack, "Source ID", selectedRow.SourceId);
                AddDetailRow(detailStack, "Imported scalar fields", facts.Count.ToString("N0", CultureInfo.InvariantCulture));
                if (_editorEnabled)
                {
                    detailStack.Children.Add(await BuildLocalEntityFactEditorAsync(
                        selectedRow.TableName.ToLowerInvariant(), selectedRow.SourceId, selectedRow.Label, facts));
                    editorMessage.Text = "Local editor enabled · changes are stored separately from the source save snapshot and can be undone in Edit History.";
                }
                else
                {
                    detailStack.Children.Add(Txt("Enable the local editor above to change every imported scalar value in this record. The original save snapshot remains untouched.", 10, false, Muted, wrap: true));
                    detailStack.Children.Add(AddFactsReadOnlyPanel(facts));
                }
            }
            catch (Exception ex)
            {
                detailStack.Children.Clear();
                detailStack.Children.Add(Txt("RECORD COULD NOT BE READ", 10, true, Brush("#A54E55")));
                detailStack.Children.Add(Txt(ex.Message, 10, false, Muted, wrap: true));
            }
        };

        tablePicker.SelectionChanged += async (_, _) => await RefreshWorldRowsAsync();
        if (tables.Count == 0)
        {
            tablePicker.IsEnabled = false;
            searchButton.IsEnabled = false;
            tableStatus.Text = "No extra save tables have been imported yet.";
            detailStack.Children.Add(Txt("Import a supported FM27 career save through Settings. The World Editor will then list the fields the save bridge actually returns.", 10, false, Muted, wrap: true));
        }
        else
        {
            tablePicker.SelectedIndex = 0;
            await RefreshWorldRowsAsync();
        }

        return root;
    }

    private static UIElement AddFactsReadOnlyPanel(IReadOnlyList<AtlasFactRecord> facts)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        if (facts.Count == 0)
        {
            panel.Children.Add(Txt("No scalar fields were present in this record.", 9, false, Muted));
            return panel;
        }
        foreach (var group in facts.GroupBy(x => x.Group).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            panel.Children.Add(Txt(group.Key.ToUpperInvariant(), 8, true, Teal));
            foreach (var fact in group.Take(100))
                AddDetailRow(panel, fact.Field, fact.Value ?? "—");
        }
        return panel;
    }

    private async Task<UIElement> LiveEditorPageAsync()
    {
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = Card(new StackPanel(), 16, new Thickness(0, 0, 0, 10)); var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("EDITOR WORKSPACE", 9, true, Teal)); hs.Children.Add(Txt("Editor Studio", 23, true, Ink));
        hs.Children.Add(Txt(_editorEnabled ? "Atlas Editor · ON for this session" : "Atlas Editor · OFF by default", 11, true, _editorEnabled ? Brush("#2D8A63") : Brush("#94652A")));
        hs.Children.Add(_editorEnabled
            ? SoftButton("Turn Editor OFF", async (_, _) => { _editorEnabled = false; AtlasLog.Info("FM Atlas editor disabled by user."); await NavigateAsync("Live Editor"); })
            : PrimaryButton("Enable Editor", async (_, _) => { _editorEnabled = true; AtlasLog.Info("FM Atlas editor enabled by user for this session; live writes remain individually gated."); await NavigateAsync("Live Editor"); }));
        var alert = new Border { Background = Brush("#342C1C"), BorderBrush = Brush("#66502C"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 6) };
        alert.Child = Txt("Atlas local edits are separate from Football Manager. In this V8 build, Editor Studio saves changes to the FM Atlas database only. Live Apply/Undo is hard-disabled; no game-memory writes are exposed. Live editing will remain blocked until exact-build identity, field mappings, in-game witnesses, read-back, rollback and Windows acceptance are independently verified.", 10, true, Brush("#E6C477"), wrap: true);
        hs.Children.Add(alert);
        if (!_editorEnabled)
        {
            hs.Children.Add(Txt("The editor is locked. Enable it above to access local Atlas database edits only. The switch resets to OFF when FM Atlas closes; Football Manager itself is never changed.", 11, false, Muted, wrap: true));
            Grid.SetRow(header, 0); root.Children.Add(header); return root;
        }
        if (_selectedPlayer is null)
        {
            hs.Children.Add(Txt("Choose a player in Players before opening the local editor.", 11, false, Muted));
            hs.Children.Add(SoftButton("Open Players", async (_, _) => await NavigateAsync("Players")));
            Grid.SetRow(header, 0); root.Children.Add(header); return root;
        }
        hs.Children.Add(Txt($"Selected player · {_selectedPlayer.Name} · {_selectedPlayer.Club ?? "Club unavailable"}", 11, true, Ink));
        Grid.SetRow(header, 0); root.Children.Add(header);

        var editor = Card(new DockPanel(), 16); var dock = (DockPanel)editor.Child;
        var controls = new StackPanel(); controls.Children.Add(Txt("ABILITY VALUES", 9, true, Teal));
        var abilityRow = new WrapPanel { Margin = new Thickness(0, 7, 0, 9) };
        var caInput = Input("Current Ability"); caInput.Width = 110; caInput.Text = _selectedPlayer.CurrentAbility?.ToString() ?? "";
        var paInput = Input("Potential Ability"); paInput.Width = 110; paInput.Text = _selectedPlayer.PotentialAbility?.ToString() ?? "";
        abilityRow.Children.Add(FieldLabel("CA")); abilityRow.Children.Add(caInput); abilityRow.Children.Add(FieldLabel("PA")); abilityRow.Children.Add(paInput);
        abilityRow.Children.Add(PrimaryButton("Save CA / PA locally", async (_, _) =>
        {
            if (!int.TryParse(caInput.Text, out var ca) || !int.TryParse(paInput.Text, out var pa) || ca < 0 || ca > 200 || pa < 0 || pa > 200)
            { MessageBox.Show(this, "Enter both CA and PA as whole numbers from 0 to 200.", "Local editor"); return; }
            try
            {
                await RunDatabaseAsync(() => _database.SaveLocalAbilitiesAsync(_selectedPlayer.SourceId, _selectedPlayer.Name, ca, pa));
                AtlasLog.Info("Local CA/PA edit saved for the selected player.");
                await RefreshPlayerResultsAsync();
                _selectedPlayer = _players.FirstOrDefault(x => x.SourceId == _selectedPlayer.SourceId) ?? _selectedPlayer;
                await ShowPlayerProfileAsync(_selectedPlayer);
                MessageBox.Show(this, "CA and PA saved to the FM Atlas database only. The Football Manager career has not been changed.", "Local edit saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { AtlasLog.Error("Local CA/PA edit failed.", ex); MessageBox.Show(this, ex.Message, "Local editor", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }));
        if (_selectedPlayer.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase))
        {
            var liveAbilityApply = SoftButton("Live CA / PA Apply disabled in V8", (_, _) => { });
            liveAbilityApply.IsEnabled = false;
            liveAbilityApply.ToolTip = "Live memory writes are hard-disabled. Save local changes to the FM Atlas database only.";
            abilityRow.Children.Add(liveAbilityApply);
        }
        controls.Children.Add(abilityRow);
        controls.Children.Add(Txt("PLAYER ATTRIBUTES", 9, true, Teal));
        controls.Children.Add(Txt("Edit imported values. Saving here changes the local FM Atlas database only; it does not change the running Football Manager career.", 9, false, Muted));
        DockPanel.SetDock(controls, Dock.Top); dock.Children.Add(controls);

        var rows = await RunDatabaseAsync(() => _database.GetPlayerAttributeRowsAsync(_selectedPlayer.SourceId));
        var viewRows = new ObservableCollection<EditableAttributeView>(rows.Select(x => new EditableAttributeView { AttributeName=x.Name, Category=x.Category, OriginalValue=x.Value, Value=x.Value }));
        var grid = CreateGrid(); grid.IsReadOnly = false; grid.CanUserAddRows = false;
        var attributeNameColumn = Column("ATTRIBUTE", "AttributeName", 1.4); attributeNameColumn.IsReadOnly = true; grid.Columns.Add(attributeNameColumn);
        var categoryColumn = Column("GROUP", "Category", 1.1); categoryColumn.IsReadOnly = true; grid.Columns.Add(categoryColumn);
        grid.Columns.Add(new DataGridTextColumn { Header = "VALUE / 20", Binding = new Binding(nameof(EditableAttributeView.Value)) { StringFormat = "0.#", UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(.6, DataGridLengthUnitType.Star) });
        grid.ItemsSource = viewRows; dock.Children.Add(grid);
        var saveAttributes = PrimaryButton("Save changed attributes locally", async (_, _) =>
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
            var changed = viewRows.Where(x => Math.Abs(x.Value - x.OriginalValue) > 0.0001).ToList();
            if (changed.Count == 0) { MessageBox.Show(this, "No attribute changes to save.", "Local editor"); return; }
            if (changed.Any(x => x.Value < 1 || x.Value > 20)) { MessageBox.Show(this, "Player attributes must be between 1 and 20.", "Local editor"); return; }
            try
            {
                foreach (var item in changed) await RunDatabaseAsync(() => _database.SavePlayerAttributeAsync(_selectedPlayer.SourceId, _selectedPlayer.Name, item.AttributeName, item.Category, item.Value));
                AtlasLog.Info($"Local player attribute edit completed: fields={changed.Count}.");
                foreach (var item in changed) item.OriginalValue = item.Value;
                await ShowPlayerProfileAsync(_selectedPlayer);
                MessageBox.Show(this, $"Saved {changed.Count:N0} local attribute changes. No Football Manager values were written.", "Local edits saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { AtlasLog.Error("Local player attribute edit failed.", ex); MessageBox.Show(this, ex.Message, "Local editor", MessageBoxButton.OK, MessageBoxImage.Warning); }
        });
        saveAttributes.Margin = new Thickness(0, 9, 0, 0); DockPanel.SetDock(saveAttributes, Dock.Bottom); dock.Children.Add(saveAttributes);
        if (_selectedPlayer.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(_selectedPlayer.SourceId["LIVE:".Length..], out var livePlayerUid))
        {
            var liveFacts = await RunDatabaseAsync(() => _database.GetLivePlayerFactsAsync(livePlayerUid));
            var stageRows = new ObservableCollection<LiveFieldStageView>();
            foreach (var fact in liveFacts)
            {
                if (!fact.NumericValue.HasValue || !double.IsFinite(fact.NumericValue.Value)
                    || Math.Abs(fact.NumericValue.Value - Math.Round(fact.NumericValue.Value)) > 0.0001
                    || !LiveMemoryEditService.SupportsPlayerFieldPreview(fact.FieldKey))
                    continue;
                var currentValue = checked((int)Math.Round(fact.NumericValue.Value));
                stageRows.Add(new LiveFieldStageView
                {
                    FieldKey = fact.FieldKey,
                    CurrentValue = currentValue,
                    TargetValue = currentValue.ToString(CultureInfo.InvariantCulture)
                });
            }

            var stagePanel = new StackPanel { Margin = new Thickness(0, 12, 0, 10) };
            stagePanel.Children.Add(Txt("LIVE FIELD PREVIEW · READ ONLY", 9, true, Teal));
            stagePanel.Children.Add(Txt(
                "The FM27 field catalogue currently supports staged previews for player attributes, positions, personality, foot ratings, CA/PA, height, condition, match sharpness, jadedness, morale, reputation, asking price and international statistics. This reads the current player again, checks the build and save session, and prepares a change plan. Apply/Undo remains hard-disabled.",
                9, false, Muted, wrap: true));
            var stageGrid = CreateGrid();
            stageGrid.IsReadOnly = false;
            stageGrid.CanUserAddRows = false;
            stageGrid.MinHeight = 150;
            stageGrid.MaxHeight = 260;
            var stageFieldColumn = Column("FM FIELD KEY", nameof(LiveFieldStageView.FieldKey), 1.5); stageFieldColumn.IsReadOnly = true; stageGrid.Columns.Add(stageFieldColumn);
            var stageCurrentColumn = Column("CURRENT", nameof(LiveFieldStageView.CurrentValue), .6); stageCurrentColumn.IsReadOnly = true; stageGrid.Columns.Add(stageCurrentColumn);
            stageGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "TARGET",
                Binding = new Binding(nameof(LiveFieldStageView.TargetValue)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(.7, DataGridLengthUnitType.Star)
            });
            stageGrid.ItemsSource = stageRows;
            stagePanel.Children.Add(Txt($"{stageRows.Count:N0} live values have a supported, range-checked preview mapping.", 8, true, Muted));
            stagePanel.Children.Add(stageGrid);
            var stageStatus = Txt("No live preview staged.", 9, false, Muted, wrap: true);
            stagePanel.Children.Add(PrimaryButton("Stage read-only live preview", async (_, _) =>
            {
                stageGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                stageGrid.CommitEdit(DataGridEditingUnit.Row, true);
                var changes = new List<LiveMemoryEditService.Change>();
                foreach (var row in stageRows)
                {
                    if (!int.TryParse(row.TargetValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var target))
                    {
                        MessageBox.Show(this, $"Enter a whole-number target for {row.FieldKey}.", "Live preview", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (target == row.CurrentValue) continue;
                    changes.Add(new LiveMemoryEditService.Change(row.FieldKey, row.CurrentValue, target));
                }
                if (changes.Count == 0)
                {
                    MessageBox.Show(this, "Change at least one target value before staging a preview.", "Live preview", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    var plan = await Task.Run(() =>
                    {
                        var processId = LiveMemoryEditService.FindSupportedProcessId();
                        return LiveMemoryEditService.StageBatch(processId, checked((uint)livePlayerUid), _selectedPlayer.Name, changes);
                    });
                    stageStatus.Text = $"PREVIEW READY · {plan.Changes.Count:N0} field(s) · {plan.ProfileId} · no writes made";
                    var preview = string.Join("\n", plan.Changes.Take(24).Select(x =>
                        $"{x.FieldKey}: {x.ExpectedCurrentValue:N0} → {x.TargetValue:N0}  (offset 0x{x.Offset:X})"));
                    if (plan.Changes.Count > 24) preview += $"\n…and {plan.Changes.Count - 24:N0} more field(s).";
                    MessageBox.Show(this,
                        $"A read-only live preview was staged for {_selectedPlayer.Name}.\n\nBuild: {plan.ProfileId}\nFields: {plan.Changes.Count:N0}\n\n{preview}\n\nNo memory was written. Apply and Undo remain disabled.",
                        "FM Atlas · Preview only", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    stageStatus.Text = "PREVIEW BLOCKED · " + ex.Message;
                    AtlasLog.Warning("Live player-field preview was blocked: " + ex.Message);
                    MessageBox.Show(this, ex.Message, "FM Atlas · Live preview blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                finally { Mouse.OverrideCursor = null; }
            }));
            stagePanel.Children.Add(stageStatus);
            var disabledApply = SoftButton("Apply live changes · disabled pending acceptance", (_, _) => { });
            disabledApply.IsEnabled = false;
            disabledApply.ToolTip = "Live writes remain hard-disabled until the exact field witnesses, disposable-save checks and rollback acceptance are complete.";
            stagePanel.Children.Add(disabledApply);
            DockPanel.SetDock(stagePanel, Dock.Bottom); dock.Children.Add(stagePanel);
        }
        if (_selectedPlayer.SourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase))
        {
            var liveAttributeApply = SoftButton("Live attribute Apply disabled in V10", (_, _) => { });
            liveAttributeApply.IsEnabled = false;
            liveAttributeApply.ToolTip = "Live memory writes are hard-disabled. Save local edits or prepare a read-only preview only.";
            liveAttributeApply.Margin = new Thickness(0, 8, 0, 0);
            DockPanel.SetDock(liveAttributeApply, Dock.Bottom);
            dock.Children.Add(liveAttributeApply);
        }
        Grid.SetRow(editor, 1); root.Children.Add(editor); return root;
    }

    private sealed class LiveFieldStageView
    {
        public string FieldKey { get; set; } = "";
        public int CurrentValue { get; set; }
        public string TargetValue { get; set; } = "";
    }

    private sealed class EditableAttributeView
    {
        public string AttributeName { get; set; } = "";
        public string Category { get; set; } = "";
        public double OriginalValue { get; set; }
        public double Value { get; set; }
    }

    private async Task<UIElement> EditHistoryPageAsync()
    {
        var panel = Card(new DockPanel(), 15); var dock = (DockPanel)panel.Child;
        var top = new StackPanel();
        top.Children.Add(Txt("AUDIT TRAIL", 9, true, Teal));
        top.Children.Add(Txt("Edit history & local undo", 20, true, Ink));
        top.Children.Add(Txt("Local Atlas edits can be undone in the local database. Live FM history entries are retained for audit purposes, but live Undo is disabled in V8.", 10, false, Muted, wrap: true));
        var dg = CreateGrid(); dg.SelectionMode = DataGridSelectionMode.Single;
        dg.Columns.Add(Column("ID", "Id", .35)); dg.Columns.Add(Column("PLAYER", "PlayerName", 1.1)); dg.Columns.Add(Column("FIELD", "FieldName", 1)); dg.Columns.Add(Column("OLD", "OldValue", .6)); dg.Columns.Add(Column("NEW", "NewValue", .6)); dg.Columns.Add(Column("CHANGED", "ChangedUtc", 1)); dg.Columns.Add(Column("SOURCE", "ChangeSource", 1));
        dg.ItemsSource = await RunDatabaseAsync(() => _database.GetRecentEditHistoryAsync());
        top.Children.Add(SoftButton("Undo selected local change", async (_, _) =>
        {
            if (dg.SelectedItem is not AtlasHistoryRecord selected)
            {
                MessageBox.Show(this, "Select an edit history row first.", "Undo edit");
                return;
            }

            var liveChange = selected.ChangeSource.Contains("FM27 live memory", StringComparison.OrdinalIgnoreCase);
            if (liveChange)
            {
                MessageBox.Show(this,
                    "This is a historical live-game entry. Live Apply and Undo are disabled in V8; the record is retained for audit only. No Football Manager process will be written to.",
                    "FM Atlas · Live undo disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirmLocal = MessageBox.Show(this,
                $"Undo local change #{selected.Id} for {selected.PlayerName}?\\n\\nThis changes the FM Atlas database only, not the live game or source career save.",
                "Confirm local undo", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmLocal != MessageBoxResult.Yes) return;

            try
            {
                await RunDatabaseAsync(() => _database.UndoLocalEditAsync(selected.Id));
                AtlasLog.Info($"Local edit history item undone: id={selected.Id}.");
                dg.ItemsSource = await RunDatabaseAsync(() => _database.GetRecentEditHistoryAsync());
                await RefreshPlayerResultsAsync();
                if (_selectedPlayer is not null) await ShowPlayerProfileAsync(_selectedPlayer);
            }
            catch (Exception ex)
            {
                AtlasLog.Error($"Local edit undo failed for history item {selected.Id}.", ex);
                MessageBox.Show(this, ex.Message, "Undo failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }));
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top); dock.Children.Add(dg); return panel;
    }

    private UIElement AssistantPage()
    {
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) }); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = Card(new StackPanel(), 16); var s = (StackPanel)left.Child; s.Children.Add(Txt("ATLAS ASSISTANT", 9, true, Teal)); s.Children.Add(Txt("Ask your career", 22, true, Ink));
        s.Children.Add(Txt("The current assistant composes a local, explainable database search. It does not invent player facts or call an external AI service.", 10, false, Muted, wrap: true));
        var prompt = new TextBox { Height = 120, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 10, 0, 8), Padding = new Thickness(10), Text = "Find players under 23 with high finishing" };
        s.Children.Add(prompt); var run = PrimaryButton("Search career database", async (_, _) => await RunAssistantSearchAsync(prompt.Text)); s.Children.Add(run);
        s.Children.Add(SoftButton("Open advanced Global Search", (_, _) => ShowGlobalSearch()));
        var right = Card(new StackPanel(), 16); var resultStack = (StackPanel)right.Child; resultStack.Children.Add(Txt("SEARCH RESULTS", 9, true, Teal)); resultStack.Children.Add(Txt("Your results will appear here", 19, true, Ink)); resultStack.Children.Add(Txt("Supported filters depend on the fields imported in your save snapshot. Unsupported parts of a request are identified rather than silently ignored.", 10, false, Muted, wrap: true));
        Grid.SetColumn(left, 0); root.Children.Add(left); Grid.SetColumn(right, 1); root.Children.Add(right);
        _assistantResultStack = resultStack;
        return root;
    }
    private StackPanel? _assistantResultStack;

    private async Task RunAssistantSearchAsync(string query)
    {
        if (_assistantResultStack is null) return;
        var lower = query.ToLowerInvariant();
        var age = Regex.Match(lower, @"(?:under|below|younger than)\s+(\d+)");
        var maxAge = age.Success && int.TryParse(age.Groups[1].Value, out var a) ? a - 1 : (int?)null;
        var attrNames = await RunDatabaseAsync(() => _database.GetAttributeNamesAsync());
        var matchedAttribute = attrNames.FirstOrDefault(x => lower.Contains(x.Replace('_', ' ').ToLowerInvariant()));
        double? minimum = Regex.Match(lower, @"(?:at least|minimum|min)\s+(\d+)") is Match n && n.Success && double.TryParse(n.Groups[1].Value, out var val) ? val : null;
        var position = new[] { "striker", "centre back", "center back", "winger", "midfielder", "goalkeeper" }.FirstOrDefault(x => lower.Contains(x));
        var positionCode = position switch { "striker" => "ST", "centre back" or "center back" => "DC", "winger" => "AMR", "midfielder" => "MC", "goalkeeper" => "GK", _ => null };
        var results = await RunDatabaseAsync(() => _database.SearchAsync("", null, maxAge, positionCode, null, null, null, null, matchedAttribute, minimum, null));
        if (results.Count == 0) results = await RunDatabaseAsync(() => _database.SearchLivePlayersAsync("", 5000, matchedAttribute, minimum, null, null, maxAge));
        _assistantResultStack.Children.Clear(); _assistantResultStack.Children.Add(Txt("SEARCH RESULTS", 9, true, Teal));
        _assistantResultStack.Children.Add(Txt($"{results.Count:N0} matching records", 20, true, Ink));
        AddDetailRow(_assistantResultStack, "Age filter", maxAge.HasValue ? $"Under {maxAge + 1}" : "Not applied");
        AddDetailRow(_assistantResultStack, "Attribute", matchedAttribute ?? "Not applied");
        AddDetailRow(_assistantResultStack, "Minimum rating", minimum?.ToString() ?? "Not applied");
        AddDetailRow(_assistantResultStack, "Position", positionCode ?? "Not applied");
        _assistantResultStack.Children.Add(Txt("This is a rule-based query composer, not a generative football analyst. Footedness, tactical system, budget, transfer availability and replacement context are not applied unless their fields are present and mapped in the database.", 10, false, Muted, wrap: true));
        var dg = CreateGrid(); dg.Height = 420; dg.Columns.Add(Column("PLAYER", "Name", 1.2)); dg.Columns.Add(Column("AGE", "Age", .4)); dg.Columns.Add(Column("CLUB", "Club", 1)); dg.Columns.Add(Column("CA", "DisplayAbility", .4)); dg.Columns.Add(Column("PA", "DisplayPotential", .4)); dg.ItemsSource = results.Take(100).ToList(); _assistantResultStack.Children.Add(dg);
    }

    private async Task<UIElement> SettingsPageAsync()
    {
        var root = new StackPanel();
        var bridge = Card(new StackPanel(), 16); var b = (StackPanel)bridge.Child; b.Children.Add(Txt("SAVE READER", 9, true, Teal)); b.Children.Add(Txt("FM27 save snapshot bridge", 20, true, Ink));
        var available = await FmsaveBridgeService.IsAvailableAsync();
        b.Children.Add(Txt(available ? "fmsave is available under Python 3.12." : "fmsave is not currently available. Install Python 3.12+ and the local fmsave package to enable save-file imports.", 11, false, available ? Brush("#237C62") : Muted, wrap: true));
        b.Children.Add(SoftButton("Run save bridge setup", (_, _) => RunSaveBridgeSetup()));
        b.Children.Add(Txt("Current published fmsave documentation lists FM27 build 27.0.0+2486695 as a registered layout. Other FM27 builds may borrow layouts where section schemas match. A registered build does not certify field meanings: FM27 fields remain unconfirmed. Each import records a reader validation report.", 10, false, Muted, wrap: true));
        if (!string.IsNullOrWhiteSpace((await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync())).SaveValidationJson)) b.Children.Add(Txt("A validation report is stored with the latest snapshot. Reader failures and coverage should be reviewed before relying on the corresponding data.", 10, false, Teal, wrap: true));
        root.Children.Add(bridge);
        var db = Card(new StackPanel(), 16); var d = (StackPanel)db.Child; d.Children.Add(Txt("LOCAL DATA", 9, true, Teal)); d.Children.Add(Txt("Database & safety", 20, true, Ink));
        d.Children.Add(Txt("Database: %LOCALAPPDATA%\\FmAtlas\\scouting.db", 10, true, Ink)); d.Children.Add(Txt("FM Atlas 27 keeps your existing database. Save imports are stored as versioned snapshots; player attributes are normalized into the local attribute table. The live reader is read-only during discovery. Local Editor Studio changes are stored only in the Atlas database. Live FM Apply/Undo is disabled throughout this V8 build; no game-memory writes are exposed.", 10, false, Muted, wrap: true));
        d.Children.Add(SoftButton("Open FM Atlas data folder", (_, _) => { var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas"); Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true }); })); root.Children.Add(db);
        var capability = Card(new StackPanel(), 16);
        var capabilityStack = (StackPanel)capability.Child;
        capabilityStack.Children.Add(Txt("CAPABILITY & DATA CONFIDENCE", 9, true, Teal));
        capabilityStack.Children.Add(Txt("What Atlas can safely do today", 20, true, Ink));
        capabilityStack.Children.Add(Txt("READ-ONLY · Live FM27 process/build/session detection and bounded player, staff and club indexing. Offline save inspection uses the local fmsave reader and does not modify the source save.", 10, false, Muted, wrap: true));
        capabilityStack.Children.Add(Txt("PROVISIONAL · Live CA/PA, player attributes, positions, personality, staff coaching/preferred-job ratings and manager relationships. Compare these values with the in-game FM27 UI before relying on them.", 10, false, Muted, wrap: true));
        capabilityStack.Children.Add(Txt("LOCAL ONLY · Editor Studio saves draft CA/PA and attribute changes to the FM Atlas database. These changes do not alter Football Manager.", 10, false, Muted, wrap: true));
        capabilityStack.Children.Add(Txt("BLOCKED · Live Apply/Undo and direct writes to Football Manager memory are disabled in V8. This is a hard safety gate, not a selectable preference.", 10, true, Brush("#D97736"), wrap: true));
        root.Children.Add(capability);

        var design = Card(new StackPanel(), 16); var ds = (StackPanel)design.Child; ds.Children.Add(Txt("OFFICIAL BRANDING", 9, true, Teal));
        ds.Children.Add(new Image { Source = new BitmapImage(new Uri("pack://application:,,,/FmAtlas;component/Assets/FM-Atlas-27-Logo.png", UriKind.Absolute)), Height = 150, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 12) });
        ds.Children.Add(Txt("Monolith Glass · FM Atlas 27", 20, true, Ink)); ds.Children.Add(Txt("Graphite surfaces, warm off-white typography, amber-orange accents, translucent raised cards, restrained shadows and precise editorial layouts.", 10, false, Muted, wrap: true)); root.Children.Add(design);
        var graphics = Card(new StackPanel(), 16); var gs = (StackPanel)graphics.Child;
        gs.Children.Add(Txt("INSTALLED GRAPHICS", 9, true, Teal));
        gs.Children.Add(Txt("Reuse the faces, club logos, competition badges and flags already installed in your Football Manager 27 graphics folder. Atlas reads config.xml mappings and does not bundle or duplicate graphics packs.", 10, false, Muted, wrap: true));
        var graphicsScan = await Task.Run(() => GraphicsResolver.Shared.Scan());
        gs.Children.Add(Txt($"Root: {graphicsScan.GraphicsRoot ?? "Not found"}", 9, true, Ink, wrap: true));
        gs.Children.Add(Txt($"Faces {graphicsScan.FaceMappings:N0} · Clubs {graphicsScan.ClubMappings:N0} · Competitions {graphicsScan.CompetitionMappings:N0} · Flags {graphicsScan.FlagMappings:N0} · Configs {graphicsScan.ConfigFiles:N0}", 9, false, Muted, wrap: true));
        gs.Children.Add(Txt(graphicsScan.Status, 9, false, Muted, wrap: true));
        gs.Children.Add(SoftButton("Choose a graphics config.xml", async (_, _) =>
        {
            var open = new OpenFileDialog { Title = "Choose a config.xml inside Football Manager 27\\graphics", Filter = "Graphics config (config.xml)|config.xml", FileName = "config.xml", CheckFileExists = true };
            if (open.ShowDialog(this) != true) return;
            try
            {
                GraphicsResolver.Shared.SetRootFromConfigFile(open.FileName);
                await Task.Run(() => GraphicsResolver.Shared.Scan(true));
                await NavigateAsync("Settings");
            }
            catch (Exception ex) { AtlasLog.Error("Graphics root selection failed.", ex); MessageBox.Show(this, ex.Message, "Graphics folder not set", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }));
        gs.Children.Add(SoftButton("Rescan installed graphics", async (_, _) => { await Task.Run(() => GraphicsResolver.Shared.Scan(true)); await NavigateAsync("Settings"); }));
        root.Children.Add(graphics);
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
    }

    private UIElement TutorialPage()
    {
        var root = new StackPanel { Margin = new Thickness(4) };
        var hero = Card(new StackPanel(), 18, new Thickness(0, 0, 0, 12));
        var h = (StackPanel)hero.Child;
        h.Children.Add(Txt("FM ATLAS 27 · QUICK START", 10, true, Teal));
        h.Children.Add(Txt("Your career, organised.", 25, true, Ink));
        h.Children.Add(Txt("A practical guide to FM Atlas 27. Load your FM27 career and Atlas detects the supported process, build and save generation automatically. The read-only player/staff/club index runs with a two-minute safety limit and a Cancel button; live Apply/Undo remains disabled.", 11, false, Muted, wrap: true));
        h.Children.Add(SoftButton("Open Global Search  ·  Ctrl + F", (_, _) => ShowGlobalSearch()));
        root.Children.Add(hero);

        void Section(string title, params string[] steps)
        {
            var card = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 11));
            var stack = (StackPanel)card.Child;
            stack.Children.Add(Txt(title, 15, true, Ink));
            for (var i = 0; i < steps.Length; i++)
                stack.Children.Add(Txt($"{i + 1}.  {steps[i]}", 11, false, Brush("#3D586E"), wrap: true) is var line ? Configure(line, new Thickness(0, 5, 0, 5)) : new TextBlock());
            root.Children.Add(card);
        }

        Section("1 · Detect a career", "Start Football Manager 27 and load your career. Keep FM Atlas open; it automatically indexes the supported live career without asking you to choose a save file.", "Atlas checks the game's save-load generation before and after reading. The scan is profile-bounded, has a two-minute safety limit and can be cancelled from the top toolbar. Loading a different career cancels the current read and starts a fresh snapshot for the new session.", "Use the optional offline read-only save bridge only when you need extended history, match/season statistics, contracts or other fields not included in the live identity scan.");
        Section("2 · Find people", "Use Global Search, Ctrl + K, Advanced Search or the Players and Staff directories.", "Search imported player and staff records by name, club, nation or UID. In Players, set minimum/maximum CA and PA directly, including signed negative PA ranges, or search by position without a name. CA/PA, attributes and position ratings appear where supplied by the selected source.", "Open a result to inspect facts supplied by the imported save snapshot and existing local database. A missing field remains blank rather than being guessed.", "The player finder is paged and virtualised so large FM databases stay responsive.");
        Section("3 · Read a player profile", "The profile displays attributes in Technical, Set Pieces, Mental, Physical, Goalkeeping and Hidden groups.", "A dash means the source did not supply a value. It is not zero and is never guessed.", "CA/PA and other parsed FM27 fields can be build-dependent and are marked as source-derived. Cross-check values in the game before making decisions.");
        Section("4 · Clubs and staff", "Use the My Club shortcut to open the current human manager's club, or open Clubs to search other teams.", "Open Staff to search live staff identities, including the human manager, CA/PA, club, nation, age and role candidates. Open your manager's profile from the top-toolbar shortcut.", "Finances, facilities, stadium and contract values are shown only when retained by a source. Historical money fields must not be treated as current balances without checking their date and unit.");
        Section("5 · Atlas Assistant", "Use the Assistant for straightforward local queries such as players under a specified age or with an attribute threshold.", "The current assistant is a rule-based search composer, not an external generative AI model. It reports only filters it actually applied.", "For more detailed criteria, use Global Search. Keep the Assistant available as a quick way into the database.");
        Section("6 · Your data and safety", "The local database is stored at %LOCALAPPDATA%\\FmAtlas\\scouting.db.", "Live scans are read-only, limited to two minutes and cancellable from the toolbar. A save switch cancels the read and prevents mixed-session records. A successful new live snapshot replaces only the active LIVE player/staff/club facts in one SQLite transaction; offline snapshots, local/CSV records, shortlists and recruitment projects are preserved.", "Live Apply/Undo remains disabled until every writable field and rollback path is independently certified. The first PC test should monitor Football Manager responsiveness and compare provisional CA/PA/attribute values against the in-game UI. Back up the FmAtlas data folder before major changes or moving the application to another PC.");
        Section("7 · Analytics and Mass Edit", "Analytics discovers numeric season/match metrics retained by the latest imported save snapshot. Choose a metric, set minimum/maximum values and search.", "If xG, xA or tackles per 90 are not in the imported source, they will not appear. Atlas does not calculate or invent missing match statistics.", "Mass Edit changes filtered local Atlas player records only. Every changed player is recorded separately in Edit History. Save Atlas checkpoints the local database; it does not save the FM career.");
        Section("8 · Diagnostics", "Use Diagnostics in the top toolbar to export a support report. Check it before sharing because it may include player names, file paths and recent edit history.");
        Section("Troubleshooting", "If Atlas cannot connect, confirm FM27 has finished loading a career and that the game build is supported. Close duplicate FM instances if more than one is open.", "If a save changes during indexing, Atlas cancels the old session read and starts the new session after the old worker exits. A two-minute timeout or toolbar Cancel retains the previous snapshot.", "Live CA/PA and attribute mappings remain provisional until compared against the FM UI. Use the optional offline save reader when you need historical or match-stat facts.", "After updating, run Build-FM-Atlas-27.bat. The build window must report 0 errors before launching the new executable.");
        root.Children.Add(SoftButton("Open FM Atlas data folder", (_, _) => { var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas"); Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true }); }));
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
    }

    private void StartLiveCareerMonitor()
    {
        if (_liveCareerTimer is not null) return;
        // Exact-profile/session probe every three seconds. It remains active while the
        // profile-bounded background importer runs so save switches can cancel it promptly.
        _liveCareerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _liveCareerTimer.Tick += async (_, _) =>
        {
            if (_liveCareerProbeBusy) return;
            _liveCareerProbeBusy = true;
            try
            {
                var probe = await Task.Run(LiveCareerMonitor.Probe);
                _currentLiveProbe = probe;
                if (_liveImportBusy)
                {
                    // Keep checking the generation while the worker scans. Cancel promptly
                    // if FM switches save, exits or changes to an unsupported build.
                    if (!probe.IsCareerLoaded || !string.Equals(probe.SessionKey, _activeLiveScanSessionKey, StringComparison.Ordinal))
                        _liveScanCancellation?.Cancel();
                    return;
                }
                if (probe.IsCareerLoaded)
                {
                    if (_connectionSummary is not null) _connectionSummary.Text = probe.Status;
                    if (_connectionDot is not null) _connectionDot.Fill = Brush("#2BA578");
                    if (LiveMemoryImportEnabled)
                    {
                        var isNewSession = !string.Equals(_lastAttemptedLiveSession, probe.SessionKey, StringComparison.Ordinal);
                        var retryDue = DateTime.UtcNow >= _nextLiveRetryUtc;
                        if (isNewSession || retryDue)
                        {
                            // Let this monitor tick finish while the read-only worker runs.
                            // Later ticks continue checking save generation and can cancel after a switch.
                            _ = LoadLiveCareerAsync(probe, automatic: true);
                            return;
                        }
                    }
                    else
                    {
                        // Detect process/build/save-generation changes without scanning the
                        // large player/staff/club arrays. Avoid retrying the paused import every tick.
                        _lastAttemptedLiveSession = probe.SessionKey;
                        _nextLiveRetryUtc = DateTime.MaxValue;
                        if (_liveLoadStatus is not null)
                            _liveLoadStatus.Text = "Career detected · starting bounded player/staff/club index…";
                    }
                }
                else if (probe.IsGameRunning)
                {
                    if (_connectionSummary is not null) _connectionSummary.Text = probe.Status;
                    if (_connectionDot is not null) _connectionDot.Fill = Brush(probe.IsSupportedBuild ? "#D8A34A" : "#C7797B");
                }
                else
                {
                    // A closed game must never leave the previous career's manager displayed as current.
                    _lastAttemptedLiveSession = null;
                    _nextLiveRetryUtc = DateTime.MinValue;
                    if (_connectionSummary is not null &&
                        (_connectionSummary.Text.StartsWith("FM27", StringComparison.OrdinalIgnoreCase)
                         || _connectionSummary.Text.StartsWith("Live FM27", StringComparison.OrdinalIgnoreCase)
                         || _connectionSummary.Text.StartsWith("Waiting for Football Manager", StringComparison.OrdinalIgnoreCase)))
                        _connectionSummary.Text = "FM27 is not running · your local Atlas database remains available.";
                    if (_connectionDot is not null) _connectionDot.Fill = Brush("#C7797B");
                }
            }
            catch (Exception ex)
            {
                AtlasLog.Warning("The automatic FM27 career check failed: " + ex.Message);
                _nextLiveRetryUtc = DateTime.UtcNow.AddSeconds(30);
            }
            finally { _liveCareerProbeBusy = false; }
        };
        _liveCareerTimer.Start();
    }

    private bool IsCurrentLiveCareer(WorkspaceMetrics metrics)
        => LiveMemoryImportEnabled
           && _currentLiveProbe is { IsCareerLoaded: true } probe
           && probe.MatchesSession(_lastImportedLiveSessionKey)
           && metrics.LiveSaveLoadCounter.HasValue
           && metrics.LiveSaveLoadCounter.Value == checked((long)probe.SaveLoadCounter);

    private async Task OpenMyManagerAsync()
    {
        _currentLiveProbe = await Task.Run(LiveCareerMonitor.Probe);
        var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
        if (!IsCurrentLiveCareer(metrics))
        {
            MessageBox.Show(this, "The current live career has not finished indexing yet. Keep FM27 open while the cancellable scan completes, or use Connect / Refresh to retry. The manager shortcut is enabled once this career's staff record is available.", "Manager not available", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(metrics.CurrentManager))
        {
            MessageBox.Show(this, "Atlas has not uniquely identified the human manager in the connected career yet. Search the Staff directory or reconnect after the save finishes loading.", "Manager not available", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _pendingStaffSearchQuery = metrics.CurrentManager;
        await NavigateAsync("Staff");
    }

    private async Task OpenMyClubAsync()
    {
        _currentLiveProbe = await Task.Run(LiveCareerMonitor.Probe);
        var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
        if (!IsCurrentLiveCareer(metrics))
        {
            MessageBox.Show(this, "The current live career has not finished indexing yet. Keep FM27 open while the cancellable scan completes, or use Connect / Refresh to retry. The My Club shortcut is enabled once the manager's current club is available.", "Club not available", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(metrics.CurrentManagerClub))
        {
            MessageBox.Show(this, "Atlas has not identified the human manager's club yet. Check that the live staff scan has completed.", "Club not available", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _pendingClubSearchQuery = metrics.CurrentManagerClub;
        await NavigateAsync("Clubs");
    }

    private async Task LoadLiveCareerAsync(LiveCareerProbe? expectedSession = null, bool automatic = false)
    {
        if (!LiveMemoryImportEnabled)
        {
            if (!automatic) ShowLiveScanPaused();
            return;
        }
        if (_liveImportBusy || _saveImportCancellation is not null) return;
        var session = expectedSession ?? await Task.Run(LiveCareerMonitor.Probe);
        if (!session.IsCareerLoaded)
        {
            if (_connectionSummary is not null) _connectionSummary.Text = session.Status;
            if (!automatic)
                MessageBox.Show(this, session.Status + "\n\nLoad a supported FM27 career and Atlas will connect automatically.", "FM Atlas · Connect career", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _liveImportBusy = true;
        BeginBackgroundOperation("FM27 live-career scan");
        _liveScanCancelledByUser = false;
        _activeLiveScanSessionKey = session.SessionKey;
        _lastAttemptedLiveSession = session.SessionKey;
        _nextLiveRetryUtc = DateTime.UtcNow.AddMinutes(5);
        using var scanCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        _liveScanCancellation = scanCancellation;
        if (_liveLoadButton is not null) _liveLoadButton.IsEnabled = false;
        if (_cancelImportButton is not null)
        {
            _cancelImportButton.IsEnabled = true;
            _cancelImportButton.Visibility = Visibility.Visible;
        }
        if (_liveLoadProgress is not null) { _liveLoadProgress.Visibility = Visibility.Visible; _liveLoadProgress.IsIndeterminate = true; }
        if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Connecting to the loaded FM27 career · checking save generation…";
        if (_connectionSummary is not null) _connectionSummary.Text = session.Status;
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            var scanProgress = new Progress<string>(message =>
            {
                if (_liveLoadStatus is not null) _liveLoadStatus.Text = message;
            });
            if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Reading players, staff, clubs and your manager… (2-minute safety limit; Cancel available)";
            AtlasLog.Info($"Starting bounded read-only FM27 live scan: pid={session.ProcessId}; profile={session.ProfileId}; saveLoadCounter={session.SaveLoadCounter}; maximumDuration=00:02:00.");
            var payload = await Task.Run(() => LiveDataImporter.Extract(session, scanCancellation.Token, scanProgress, TimeSpan.FromMinutes(2)));
            scanCancellation.Token.ThrowIfCancellationRequested();
            scanCancellation.Token.ThrowIfCancellationRequested();
            var beforeCommitProbe = await Task.Run(LiveCareerMonitor.Probe);
            if (!beforeCommitProbe.MatchesSession(session.SessionKey))
                throw new OperationCanceledException("FM27 changed career before Atlas could publish the live snapshot.", scanCancellation.Token);
            if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Saving live career snapshot and manager context to Atlas…";
            var result = await RunDatabaseAsync(() => _database.ImportLiveSnapshotAsync(payload, scanCancellation.Token));
            scanCancellation.Token.ThrowIfCancellationRequested();
            var afterCommitProbe = await Task.Run(LiveCareerMonitor.Probe);
            _currentLiveProbe = afterCommitProbe;
            if (!afterCommitProbe.MatchesSession(session.SessionKey))
            {
                // A career may change in the narrow gap after SQLite commit. Do not present
                // the just-committed old session as the active career; the next monitor tick
                // immediately starts the new generation and replaces the LIVE snapshot.
                _lastAttemptedLiveSession = null;
                _nextLiveRetryUtc = DateTime.MinValue;
                if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Career changed after indexing. Refreshing the new FM27 session…";
                AtlasLog.Warning("The FM27 career changed during snapshot publication; the monitor will immediately index the new session.");
                return;
            }
            _lastImportedLiveSessionKey = session.SessionKey;
            AtlasLog.Info($"Live career import completed: players={result.PlayersImported:N0}/{result.PlayersSeen:N0}; staff={result.StaffImported:N0}/{result.StaffSeen:N0}; clubs={result.ClubsImported:N0}; player facts={result.AttributeRowsImported:N0}; session={session.SessionKey}.");
            _nextLiveRetryUtc = DateTime.MaxValue;
            await RefreshCareerStatusAsync();
            if (_liveLoadStatus is not null)
                _liveLoadStatus.Text = $"Connected · {result.PlayersImported:N0} players · {result.StaffImported:N0} staff · {result.ClubsImported:N0} clubs";
            if (!automatic)
            {
                var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
                var managerLine = string.IsNullOrWhiteSpace(metrics.CurrentManager)
                    ? "Human manager: not uniquely identified"
                    : $"Your manager: {metrics.CurrentManager}\nClub: {metrics.CurrentManagerClub ?? "unavailable"} · Nation: {metrics.CurrentManagerNation ?? "unavailable"}";
                MessageBox.Show(this,
                    $"FM27 career connected successfully.\n\n{managerLine}\n\nPlayers: {result.PlayersImported:N0}\nStaff: {result.StaffImported:N0}\nClubs: {result.ClubsImported:N0}\nPlayer attribute facts: {result.AttributeRowsImported:N0}\n\nThe scan is read-only. Live CA/PA and attribute mappings are still provisional until compared with the FM UI. Live editing remains disabled.",
                    "FM Atlas · Career connected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            if (!automatic && string.Equals(_activePage, "Dashboard", StringComparison.OrdinalIgnoreCase))
                await NavigateAsync("Dashboard");
        }
        catch (OperationCanceledException)
        {
            var sessionChanged = _currentLiveProbe is null
                || !string.Equals(_currentLiveProbe.SessionKey, session.SessionKey, StringComparison.Ordinal);
            if (_liveScanCancelledByUser)
            {
                _lastAttemptedLiveSession = session.SessionKey;
                _nextLiveRetryUtc = DateTime.MaxValue;
                if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Live scan cancelled. Your previous snapshot remains intact; use Connect / Refresh to try again.";
            }
            else if (sessionChanged)
            {
                _lastAttemptedLiveSession = null;
                _nextLiveRetryUtc = DateTime.MinValue;
                if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Career changed during scan. Discarded the partial read; connecting to the newly loaded career.";
            }
            else
            {
                _nextLiveRetryUtc = DateTime.UtcNow.AddMinutes(5);
                if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Live scan reached its 2-minute safety limit. Previous snapshot retained; use Connect / Refresh to retry.";
            }
            AtlasLog.Warning("The live career scan was cancelled or reached its safety deadline. No partial snapshot was committed.");
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Live career import failed; no partial snapshot was committed. Atlas will retry after a five-minute cooldown.", ex);
            _nextLiveRetryUtc = DateTime.UtcNow.AddMinutes(5);
            if (_liveLoadStatus is not null) _liveLoadStatus.Text = "Connection stopped safely · retry scheduled · " + ex.Message;
            if (_connectionSummary is not null) _connectionSummary.Text = "FM27 connection stopped safely · " + ex.Message;
            if (!automatic)
                MessageBox.Show(this, "No live snapshot was committed. Atlas will keep your existing local data.\n\n" + ex.Message,
                    "FM Atlas · Connection stopped safely", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _liveImportBusy = false;
            _activeLiveScanSessionKey = null;
            if (ReferenceEquals(_liveScanCancellation, scanCancellation)) _liveScanCancellation = null;
            if (_saveImportCancellation is null && _cancelImportButton is not null)
            {
                _cancelImportButton.IsEnabled = false;
                _cancelImportButton.Visibility = Visibility.Collapsed;
            }
            if (_liveLoadButton is not null) _liveLoadButton.IsEnabled = true;
            if (_liveLoadProgress is not null) { _liveLoadProgress.IsIndeterminate = false; _liveLoadProgress.Visibility = Visibility.Collapsed; }
            EndBackgroundOperation();
        }
    }

    private async Task ImportCareerSaveAsync()
    {
        if (_saveImportCancellation is not null || _liveImportBusy) return;
        var open = new OpenFileDialog { Title = "Select a COPY of your Football Manager career save", Filter = "Football Manager saves (*.fm;*.fmf;*.dat)|*.fm;*.fmf;*.dat|All files (*.*)|*.*", CheckFileExists = true };
        if (open.ShowDialog(this) != true) return;
        if (!await FmsaveBridgeService.IsAvailableAsync())
        {
            var answer = MessageBox.Show(this, "The local save reader is missing or out of date. FM Atlas requires fmsave 0.12.4 or newer. Run Setup-FMAtlas-Save-Bridge.ps1 from the extracted FM Atlas folder, then restart the app.\n\nWould you like to open the setup script's folder?", "Save bridge not installed", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer == MessageBoxResult.Yes) Process.Start(new ProcessStartInfo("explorer.exe", AppContext.BaseDirectory) { UseShellExecute = true });
            return;
        }
        var confirmation = MessageBox.Show(this, "For safety, select a COPY of your career save, not your only save file. The parser is read-only.\n\nProceed with a local, offline snapshot?", "Import career snapshot", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes) return;

        using var cancellation = new CancellationTokenSource();
        var importTimer = Stopwatch.StartNew();
        _saveImportCancellation = cancellation;
        BeginBackgroundOperation("Offline FM career save import");
        if (_cancelImportButton is not null)
        {
            _cancelImportButton.Visibility = Visibility.Visible;
            _cancelImportButton.IsEnabled = true;
        }
        if (_liveLoadProgress is not null)
        {
            _liveLoadProgress.Visibility = Visibility.Visible;
            _liveLoadProgress.IsIndeterminate = true;
            _liveLoadProgress.Value = 0;
        }
        void SetImportStatus(string message)
        {
            if (_liveLoadStatus is not null) _liveLoadStatus.Text = message;
        }
        var progress = new Progress<string>(message =>
        {
            SetImportStatus(message);
            AtlasLog.Info("Save import: " + message);
        });
        string? temporarySnapshotPath = null;

        try
        {
            SetImportStatus("Starting read-only save inspection…");
            AtlasLog.Info("Starting read-only FM career save export/import.");
            var result = await FmsaveBridgeService.ExportAsync(open.FileName, cancellation.Token, progress);
            temporarySnapshotPath = result.OutputPath;
            cancellation.Token.ThrowIfCancellationRequested();
            SetImportStatus("Save inspection complete. Streaming table rows into the Atlas snapshot…");
            var imported = await RunDatabaseAsync(() => _database.ImportFmsaveSnapshotAsync(result.OutputPath, cancellation.Token, progress));
            cancellation.Token.ThrowIfCancellationRequested();
            AtlasLog.Info($"Career snapshot import completed: players={imported.PlayersImported}, staff={imported.StaffImported}, clubs={imported.ClubsImported}, extraRows={imported.ExtraRowsImported}, warnings={imported.Warnings.Count}.");
            var warnings = imported.Warnings.Count == 0 ? "No adapter warnings were reported." : string.Join("\n", imported.Warnings.Take(8));
            importTimer.Stop();
            SetImportStatus($"Career snapshot imported in {importTimer.Elapsed:hh\\:mm\\:ss}.");
            MessageBox.Show(this, $"Career save snapshot imported.\n\nPlayers: {imported.PlayersImported:N0}\nStaff: {imported.StaffImported:N0}\nClubs: {imported.ClubsImported:N0}\nAdditional table rows retained: {imported.ExtraRowsImported:N0}\n\n{warnings}\n\nThe source save was read-only; no game process or save was modified.", "FM Atlas · Save snapshot complete", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshConnectionStatus();
            await RefreshCareerStatusAsync();
            await NavigateAsync("Dashboard");
        }
        catch (OperationCanceledException)
        {
            AtlasLog.Info("FM career save import cancelled; an uncommitted snapshot transaction was discarded.");
            importTimer.Stop();
            SetImportStatus($"Import cancelled after {importTimer.Elapsed:hh\\:mm\\:ss}. Existing published snapshots remain available.");
            MessageBox.Show(this, "The import was cancelled safely. Any uncommitted snapshot transaction was rolled back; your previous imported career snapshot is retained.", "FM Atlas · Import cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AtlasLog.Error("FM career save import failed.", ex);
            importTimer.Stop();
            SetImportStatus($"Import failed after {importTimer.Elapsed:hh\\:mm\\:ss}. The previous published snapshot remains available.");
            MessageBox.Show(this, "The save snapshot was not imported.\n\n" + ex.Message, "FM Atlas · Save import error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            importTimer.Stop();
            if (!string.IsNullOrWhiteSpace(temporarySnapshotPath))
            {
                try
                {
                    if (Directory.Exists(temporarySnapshotPath)) Directory.Delete(temporarySnapshotPath, recursive: true);
                    else if (File.Exists(temporarySnapshotPath)) File.Delete(temporarySnapshotPath);
                }
                catch (Exception ex) { AtlasLog.Warning("Could not remove temporary save snapshot bundle: " + ex.Message); }
            }
            if (_liveLoadProgress is not null)
            {
                _liveLoadProgress.IsIndeterminate = false;
                _liveLoadProgress.Visibility = Visibility.Collapsed;
            }
            if (_cancelImportButton is not null)
            {
                _cancelImportButton.IsEnabled = false;
                _cancelImportButton.Visibility = Visibility.Collapsed;
            }
            _saveImportCancellation = null;
            EndBackgroundOperation();
        }
    }

    private void RunSaveBridgeSetup()
    {
        var script = System.IO.Path.Combine(AppContext.BaseDirectory, "Setup-FMAtlas-Save-Bridge.ps1");
        if (!File.Exists(script)) { MessageBox.Show(this, "Setup script was not found next to the application. Extract the complete FM Atlas release folder first."); return; }
        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"") { UseShellExecute = true });
    }

    private void RefreshConnectionStatus()
    {
        var status = GameProcessDetector.Detect();
        if (_connectionDot is not null) _connectionDot.Fill = Brush(status.MemoryReadVerified ? "#2BA578" : status.IsRunning ? "#D8A34A" : "#C7797B");
        if (_connectionSummary is not null) _connectionSummary.Text = status.Detail + (status.MemoryReadVerified ? " · waiting for FM27 save-generation verification" : "");
    }


    private async Task RefreshCareerStatusAsync()
    {
        if (_connectionSummary is null) return;
        var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
        if (IsCurrentLiveCareer(metrics))
        {
            _connectionSummary.Text = !string.IsNullOrWhiteSpace(metrics.CurrentManager)
                ? $"Live FM27 career · {metrics.CurrentManager} · {metrics.CurrentManagerClub ?? "club unavailable"} · {metrics.CurrentManagerNation ?? "nation unavailable"} · player/staff field mappings remain provisional."
                : $"Live FM27 career connected · {metrics.LivePlayers:N0} players / {metrics.LiveStaff:N0} staff · human manager not uniquely identified.";
            if (_connectionDot is not null) _connectionDot.Fill = Brush("#2BA578");
            return;
        }
        if (!string.IsNullOrWhiteSpace(metrics.SaveBuild))
        {
            var buildStatus = metrics.SaveBuildKnown == true ? "registered parser layout" : metrics.SaveBuildKnown == false ? "unregistered parser layout" : "parser layout status unknown";
            _connectionSummary.Text = $"Local career snapshot loaded · {metrics.SaveGame ?? "Football Manager"} {metrics.SaveBuild} · {metrics.SaveGameDate ?? "date unknown"} · {buildStatus}.";
            return;
        }
        if (metrics.LivePlayers > 0)
            _connectionSummary.Text = $"Cached FM27 live index · {metrics.LivePlayers:N0} player identities · waiting for a currently loaded career.";
    }

    private async Task AddSelectedToShortlistAsync()
    {
        if (_selectedPlayer is null) { MessageBox.Show(this, "Select a player first.", "Shortlist"); return; }
        var names = await RunDatabaseAsync(() => _database.GetShortlistNamesAsync());
        var name = names.FirstOrDefault() ?? "My Shortlist";
        if (_shortlistPicker?.SelectedItem is string selected) name = selected;
        await RunDatabaseAsync(() => _database.AddToShortlistAsync(_selectedPlayer, name));
        MessageBox.Show(this, $"{_selectedPlayer.Name} added to {name}.", "Shortlist updated", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task CreateNewShortlistAsync()
    {
        var dialog = new Window { Title = "New shortlist", Owner = this, Width = 390, Height = 190, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush("#171F28") };
        var panel = new StackPanel { Margin = new Thickness(18) }; panel.Children.Add(Txt("Shortlist name", 16, true, Ink)); var input = Input("e.g. Summer 2027 targets"); panel.Children.Add(input);
        var save = PrimaryButton("Create shortlist", (_, _) => dialog.DialogResult = true); panel.Children.Add(save); dialog.Content = panel;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(input.Text))
        {
            await RunDatabaseAsync(() => _database.CreateShortlistAsync(input.Text.Trim())); if (_shortlistPicker is not null) { _shortlistPicker.ItemsSource = await RunDatabaseAsync(() => _database.GetShortlistNamesAsync()); _shortlistPicker.SelectedItem = input.Text.Trim(); }
        }
    }

    private async Task RefreshShortlistRowsAsync(DataGrid grid)
    {
        if (_shortlistPicker?.SelectedItem is not string name) return;
        var rows = await RunDatabaseAsync(() => _database.GetShortlistAsync(name)); _shortlist.Clear(); foreach (var r in rows) _shortlist.Add(r); grid.ItemsSource = _shortlist;
    }

    private async void ExportShortlistCsv()
    {
        if (_shortlistPicker?.SelectedItem is not string name) return;
        var rows = await RunDatabaseAsync(() => _database.GetShortlistAsync(name));
        var save = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"{SanitizeFileName(name)}.csv" };
        if (save.ShowDialog(this) != true) return;
        static string Escape(string? value)
        {
            var text = value ?? "";
            var trimmed = text.TrimStart();
            if (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@') text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        var lines = new List<string>
        {
            "Player,Club,Position,Stage,Priority,TargetFeeSourceUnits,TargetWageSourceUnits,NextAction,ReviewDate,SourceId,Notes"
        };
        lines.AddRange(rows.Select(x => string.Join(",", new[]
        {
            Escape(x.PlayerName), Escape(x.DisplayClub), Escape(x.DisplayPosition),
            Escape(x.Stage), Escape(x.Priority),
            x.TargetFee?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "",
            x.TargetWage?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "",
            Escape(x.NextAction), Escape(x.ReviewDate), Escape(x.SourceId), Escape(x.Notes)
        })));
        await File.WriteAllLinesAsync(save.FileName, lines, new System.Text.UTF8Encoding(true));
        MessageBox.Show(this, $"Exported {rows.Count:N0} shortlist entries.", "Export complete");
    }

    private UIElement DashboardPageFallback() => Card(Txt("No page is available.", 15, true, Ink), 20);

    private static Border MetricCard(string title, string value, string subtitle, string symbol)
    {
        var stack = new StackPanel(); var top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(Txt(title, 9, true, Teal)); var icon = Txt(symbol, 16, true, TealDark); Grid.SetColumn(icon, 1); top.Children.Add(icon);
        stack.Children.Add(top); stack.Children.Add(Txt(value, 25, true, Ink)); stack.Children.Add(Txt(subtitle, 9, false, Muted));
        return Card(stack, 14, new Thickness(0, 0, 10, 0));
    }

    private static void AddCoverageRow(Panel panel, string label, int count, string description)
    {
        var row = new Grid { Margin = new Thickness(0, 8, 0, 8) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var s = new StackPanel(); s.Children.Add(Txt(label, 11, true, Ink)); s.Children.Add(Txt(description, 9, false, Muted)); row.Children.Add(s); var v = Txt(count.ToString("N0"), 17, true, TealDark); Grid.SetColumn(v, 1); row.Children.Add(v); panel.Children.Add(row);
    }

    private static void AddAbilityTile(Grid grid, int column, string label, string value)
    {
        var border = new Border { Background = Brush("#EAF2F9"), CornerRadius = new CornerRadius(11), Padding = new Thickness(10), Margin = new Thickness(0, 7, 6, 7) };
        var stack = new StackPanel(); stack.Children.Add(Txt(label, 8, true, Muted)); stack.Children.Add(Txt(value, 20, true, Ink)); border.Child = stack; Grid.SetColumn(border, column); grid.Children.Add(border);
    }

    private static void AddDetailRow(Panel panel, string label, string? value)
    {
        var row = new Grid { Margin = new Thickness(0, 4, 0, 4) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(Txt(label, 9, false, Muted)); var val = Txt(string.IsNullOrWhiteSpace(value) ? "—" : value, 10, true, Ink, wrap: true); Grid.SetColumn(val, 1); row.Children.Add(val); panel.Children.Add(row);
    }

    private static Border AttributeBar(string name, double value)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.Children.Add(Txt(name.Replace('_', ' ').Replace('.', ' '), 10, true, Ink)); var val = Txt(value.ToString("0.#"), 11, true, Ink); Grid.SetColumn(val, 1); row.Children.Add(val); stack.Children.Add(row);
        var track = new Border { Background = Brush("#D9E7F1"), Height = 6, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 5, 0, 0), Child = new Border { Background = Brush(value >= 15 ? "#277BB9" : value >= 11 ? "#E5A65D" : "#D77B7E"), HorizontalAlignment = HorizontalAlignment.Left, Width = Math.Clamp(value, 0, 20) * 12.0, CornerRadius = new CornerRadius(4) } };
        stack.Children.Add(track); return new Border { Child = stack };
    }

    private static DataGrid CreateGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserSortColumns = true,
            HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = Brush("#303943"), Background = Brush("#171F28"), RowBackground = Brush("#171F28"),
            AlternatingRowBackground = Brush("#202A35"), BorderBrush = Brush("#303943"), BorderThickness = new Thickness(1),
            RowHeight = 40, FontSize = 11, Foreground = Ink, SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow, EnableRowVirtualization = true,
            EnableColumnVirtualization = true, FontFamily = new FontFamily("Segoe UI")
        };

        // Shared glass-compatible table treatment keeps dense scouting data crisp.
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        rowStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#171F28")));
        rowStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#303943")));
        rowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        rowStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(5, 0, 5, 0)));
        var rowHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        rowHover.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#202A35")));
        rowStyle.Triggers.Add(rowHover);
        var rowSelected = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        rowSelected.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#483022")));
        rowSelected.Setters.Add(new Setter(Control.BorderBrushProperty, Teal));
        rowStyle.Triggers.Add(rowSelected);
        grid.RowStyle = rowStyle;

        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#202A35")));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#303943")));
        headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 7, 9, 7)));
        headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        grid.ColumnHeaderStyle = headerStyle;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        cellStyle.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3, 0, 3, 0)));
        var cellSelected = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        cellSelected.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#483022")));
        cellSelected.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        cellStyle.Triggers.Add(cellSelected);
        grid.CellStyle = cellStyle;

        VirtualizingPanel.SetIsVirtualizing(grid, true);
        VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(grid, true);
        ScrollViewer.SetIsDeferredScrollingEnabled(grid, true);
        grid.ColumnHeaderHeight = 38;
        grid.Margin = new Thickness(0, 2, 0, 0);
        return grid;
    }

    private static DataGridTextColumn Column(string header, string property, double weight)
        => new() { Header = header, Binding = new Binding(property), Width = new DataGridLength(weight, DataGridLengthUnitType.Star), ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center), new Setter(TextBlock.MarginProperty, new Thickness(7, 0, 5, 0)), new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis) } } };

    private static Border Card(UIElement child, double radius, Thickness? margin = null)
    {
        // Keep the original direct-child contract: many established workspaces populate
        // Card(...).Child after construction. A nested visual host here breaks those
        // callers at runtime. The material is therefore rendered by the Border itself.
        var light = _themeMode == "Glass Light";
        var studio = _themeMode == "Studio";
        var monolith = _themeMode == "Monolith Glass";
        var top = light
            ? Color.FromArgb(246, 255, 255, 255)
            : studio
                ? Color.FromArgb(238, 23, 26, 32)
                : monolith
                    ? Color.FromArgb(236, 47, 48, 53)
                    : Color.FromArgb(222, 26, 35, 46);
        var bottom = light
            ? Color.FromArgb(232, 237, 244, 250)
            : studio
                ? Color.FromArgb(228, 13, 16, 21)
                : monolith
                    ? Color.FromArgb(224, 23, 24, 28)
                    : Color.FromArgb(204, 15, 24, 35);
        var edgeTop = light
            ? Color.FromArgb(210, 255, 255, 255)
            : monolith
                ? Color.FromArgb(188, 242, 140, 69)
                : Color.FromArgb(158, 244, 155, 100);
        var edgeBottom = light
            ? Color.FromArgb(132, 151, 178, 205)
            : monolith
                ? Color.FromArgb(88, 242, 140, 69)
                : Color.FromArgb(58, 244, 155, 100);

        return new Border
        {
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(top, 0),
                    new GradientStop(bottom, 1)
                },
                new Point(0, 0),
                new Point(0, 1)),
            BorderBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(edgeTop, 0),
                    new GradientStop(Color.FromArgb(light ? (byte)120 : monolith ? (byte)64 : (byte)50, 255, 255, 255), 0.42),
                    new GradientStop(edgeBottom, 1)
                },
                new Point(0, 0),
                new Point(0, 1)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            Padding = new Thickness(15),
            Margin = margin ?? new Thickness(0),
            Effect = new DropShadowEffect
            {
                Color = light ? Color.FromRgb(111, 126, 145) : Color.FromRgb(0, 0, 0),
                BlurRadius = monolith ? 26 : 22,
                ShadowDepth = monolith ? 5 : 4,
                Opacity = light ? 0.10 : monolith ? 0.30 : 0.24
            },
            SnapsToDevicePixels = true,
            Child = child
        };
    }

    private static TextBlock Txt(string text, double size, bool bold, Brush foreground, bool wrap = false)
        => new() { Text = text, FontSize = Math.Max(10, size), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = foreground, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Segoe UI") };

    private static TextBox Input(string placeholder)
        => new() { Text = "", Tag = placeholder, ToolTip = placeholder, Height = 35, Padding = new Thickness(9, 7, 9, 7), Margin = new Thickness(0, 3, 0, 8), Background = Brush("#FBFDFF"), Foreground = Brush("#1A3851"), BorderBrush = Brush("#D4E1EC"), BorderThickness = new Thickness(1), FontSize = 12 };

    private static TextBlock FieldLabel(string label) => Txt(label, 9, true, Muted);

    private static Button SoftButton(string content, RoutedEventHandler click)
    {
        var button = new Button { Content = content, Padding = new Thickness(13, 8, 13, 8), Margin = new Thickness(3), Background = Brush("#EFF5FA"), Foreground = Brush("#365F83"), BorderBrush = Brush("#D4E1EC"), BorderThickness = new Thickness(1), FontSize = 11, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, Template = RoundedButtonTemplate(11) };
        button.Click += click; return button;
    }

    private static Button PrimaryButton(string content, RoutedEventHandler click)
    {
        var button = new Button { Content = content, Padding = new Thickness(15, 9, 15, 9), Margin = new Thickness(3), Background = Brush("#2D75B6"), Foreground = Brush("#0D1117"), BorderBrush = Brush("#C6F36B"), FontSize = 11, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, Template = RoundedButtonTemplate(11) };
        button.Click += click; return button;
    }

    private static Button WhiteButton(string content, RoutedEventHandler click)
    {
        var button = new Button { Content = content, Padding = new Thickness(13, 9, 13, 9), Margin = new Thickness(3), Background = Brush("#F6FCFA"), Foreground = Brush("#205F91"), BorderBrush = Brush("#D3E9E4"), FontSize = 11, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, Template = RoundedButtonTemplate(11) };
        button.Click += click; return button;
    }

    private static ControlTemplate RoundedButtonTemplate(double radius)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "AtlasButtonSurface";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#483022"), "AtlasButtonSurface"));
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("#F7A46A"), "AtlasButtonSurface"));
        template.Triggers.Add(hover);

        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.78, "AtlasButtonSurface"));
        template.Triggers.Add(pressed);

        var focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focused.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("#F7A46A"), "AtlasButtonSurface"));
        focused.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "AtlasButtonSurface"));
        template.Triggers.Add(focused);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.42, "AtlasButtonSurface"));
        template.Triggers.Add(disabled);
        return template;
    }

    private static string LoadThemeMode()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FMAtlas", "theme.txt");
            var value = File.Exists(path) ? File.ReadAllText(path).Trim() : "Monolith Glass";
            return value is "Monolith Glass" or "Glass Light" or "Studio" or "Glass Dark" ? value : "Monolith Glass";
        }
        catch { return "Monolith Glass"; }
    }

    private static Brush Brush(string hex)
    {
        var key = hex.ToUpperInvariant();
        string mapped;
        if (_themeMode == "Glass Light")
        {
            mapped = key switch
            {
                "#E8EEF4" => "#172B40",
                "#C6F36B" or "#D5E5C3" or "#A9D6FF" => "#D97736",
                "#294765" => "#FCE9DC",
                "#9EADB9" => "#657A8F",
                "#303943" or "#D7E3EA" or "#D4E1EC" or "#DDE7ED" or "#D5E0E7" or "#D8E4ED" or "#D9E5EF" or "#E1E9EF" or "#C9D9E8" => "#D1DDE8",
                "#FFFFFF" or "#FBFDFF" or "#F9FDFC" or "#F8FCFA" or "#F7FBFA" or "#F6FCFA" or "#F4F8FC" or "#F4F8FB" or "#F5F8FC" or "#EFF5FA" or "#EAF1F8" or "#EAF2F9" or "#E9EFF3" or "#E8EEF2" or "#E6EDF2" or "#E1F3EA" or "#D3E9E4" or "#DBF2EF" => "#FFFFFF",
                "#0D1117" or "#111820" => "#EEF3F8",
                "#171F28" or "#202A35" or "#1C242E" or "#151B22" => "#E5EDF5",
                "#35424E" => "#CAD7E4",
                "#2D75B6" or "#087F91" or "#3988C8" or "#205F91" or "#277BB9" => "#D97736",
                "#F08C96" => "#C34D68",
                "#8BCFA6" => "#34845C",
                "#E6C477" => "#A87822",
                _ => key
            };
        }
        else if (_themeMode == "Monolith Glass")
        {
            mapped = key switch
            {
                "#E8EEF4" or "#E8F0FA" or "#C4CED8" => "#F1F0ED",
                "#C6F36B" or "#D5E5C3" or "#A9D6FF" or "#79BDEB" => "#F28C45",
                "#294765" => "#33251E",
                "#9EADB9" => "#9A9CA2",
                "#303943" or "#D7E3EA" or "#D4E1EC" or "#DDE7ED" or "#D5E0E7" or "#D8E4ED" or "#D9E5EF" or "#E1E9EF" or "#C9D9E8" => "#34363B",
                "#FFFFFF" or "#FBFDFF" or "#F9FDFC" or "#F8FCFA" or "#F7FBFA" or "#F6FCFA" or "#F4F8FC" or "#F4F8FB" or "#F5F8FC" or "#EFF5FA" or "#EAF1F8" or "#EAF2F9" or "#E9EFF3" or "#E8EEF2" or "#E6EDF2" or "#E1F3EA" or "#D3E9E4" or "#DBF2EF" => "#1B1D21",
                "#0D1117" or "#111820" => "#0C0D10",
                "#171F28" or "#202A35" or "#1C242E" or "#151B22" => "#17191D",
                "#35424E" => "#303238",
                "#2D75B6" or "#087F91" or "#3988C8" or "#205F91" or "#277BB9" or "#F29A5C" or "#D97736" => "#F28C45",
                "#182F45" or "#183B56" or "#1A3851" or "#173B55" or "#25435E" or "#334F65" or "#365F83" or "#3D586E" or "#486477" or "#344656" => "#F1F0ED",
                "#2A669C" or "#356C98" or "#536B7E" or "#75B9E7" or "#77BCE8" or "#9DC8EF" or "#A9D7F7" => "#F7A46A",
                "#63778A" or "#647A8E" or "#6F8293" or "#8293A1" or "#526B7D" or "#536C7F" => "#A3A5AA",
                "#D9E7F1" or "#DCE5ED" or "#EAF1F5" or "#E5EDF5" or "#EEF4F8" or "#EEF4F9" or "#F1F6FC" => "#272A2F",
                "#EBD5A7" or "#FFF4DE" or "#FFF5E4" => "#46351F",
                "#F8E8E9" => "#3B2528",
                "#F08C96" => "#F08C96",
                "#8BCFA6" => "#72C698",
                "#E6C477" => "#E6BE70",
                _ => key
            };
        }
        else if (_themeMode == "Studio")
        {
            mapped = key switch
            {
                "#E8EEF4" => "#E8F0FA",
                "#C6F36B" or "#D5E5C3" => "#F29A5C",
                "#9EADB9" => "#91A4BA",
                "#303943" or "#D7E3EA" or "#D4E1EC" or "#DDE7ED" or "#D5E0E7" or "#D8E4ED" or "#D9E5EF" or "#E1E9EF" or "#C9D9E8" => "#2B3A50",
                "#FFFFFF" or "#FBFDFF" or "#F9FDFC" or "#F8FCFA" or "#F7FBFA" or "#F6FCFA" or "#F4F8FC" or "#F4F8FB" or "#F5F8FC" or "#EFF5FA" or "#EAF1F8" or "#EAF2F9" or "#E9EFF3" or "#E8EEF2" or "#E6EDF2" or "#E1F3EA" or "#D3E9E4" or "#DBF2EF" => "#111D2C",
                "#0D1117" => "#070E18",
                "#111820" or "#171F28" => "#0C1726",
                "#202A35" or "#1C242E" => "#18263A",
                "#35424E" => "#26364D",
                "#2D75B6" or "#087F91" or "#3988C8" or "#205F91" or "#277BB9" => "#F29A5C",
                "#F08C96" => "#F18EAA",
                "#8BCFA6" => "#70C99C",
                "#E6C477" => "#E5BD72",
                _ => key
            };
        }
        else
        {
            mapped = key switch
            {
                "#182F45" or "#183B56" or "#1A3851" or "#173B55" or "#25435E" or "#334F65" or "#365F83" or "#3D586E" or "#486477" => "#E8EEF4",
                "#C6F36B" => "#F08A42",
                "#D5E5C3" => "#FFD0AA",
                "#087F91" or "#2D75B6" or "#2D8A63" or "#3988C8" or "#205F91" or "#237C62" or "#2A669C" or "#356C98" or "#277BB9" or "#16845D" or "#4BA6A8" => "#F08A42",
                "#63778A" or "#647A8E" or "#6F8293" or "#8293A1" or "#526B7D" or "#536C7F" => "#9EADB9",
                "#D7E3EA" or "#D4E1EC" or "#DDE7ED" or "#D5E0E7" or "#D8E4ED" or "#D9E5EF" or "#E1E9EF" or "#C9D9E8" => "#303943",
                "#FFFFFF" or "#FBFDFF" or "#F9FDFC" or "#F8FCFA" or "#F7FBFA" or "#F6FCFA" or "#F4F8FC" or "#F4F8FB" or "#F5F8FC" or "#EFF5FA" or "#EAF1F8" or "#EAF2F9" or "#E9EFF3" or "#E8EEF2" or "#E6EDF2" or "#E1F3EA" or "#D3E9E4" or "#DBF2EF" => "#171F28",
                "#EEF4F8" or "#EEF4F9" or "#EAF1F5" => "#0D1117",
                "#151B22" => "#111820",
                "#1C242E" or "#EFF5FA" or "#EAF2F9" => "#202A35",
                "#D9E7F1" or "#DCE5ED" or "#DCE8F2" or "#B7E5E0" => "#35424E",
                "#F8E8E9" or "#EBD5A7" or "#EED8AA" or "#FFF4DE" or "#FFF5E4" or "#F8F0DF" => "#30291D",
                "#A54E55" or "#B44B52" or "#B94A48" or "#D77B7E" => "#F08C96",
                "#176B46" or "#2F825D" or "#37875A" or "#4F9B70" => "#8BCFA6",
                "#D9A44D" or "#78591F" or "#84612B" or "#8B622C" or "#94652A" or "#A06B24" or "#A46E2B" or "#A56D22" or "#C47B32" or "#D18A3A" => "#E6C477",
                "#B45B2F" => "#E6A06E",
                _ => key
            };
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(mapped));
    }
    private static string Initials(string name)
    {
        var bits = name.Split(' ', StringSplitOptions.RemoveEmptyEntries); return bits.Length switch { 0 => "?", 1 => bits[0][..Math.Min(2, bits[0].Length)].ToUpperInvariant(), _ => (bits[0][0].ToString() + bits[^1][0]).ToUpperInvariant() };
    }
    private static string FormatDate(string value) => DateTime.TryParse(value, out var dt) ? dt.ToLocalTime().ToString("dd MMM yyyy · HH:mm") : value;
    private static string SanitizeFileName(string value) { foreach (var c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_'); return value; }
}
