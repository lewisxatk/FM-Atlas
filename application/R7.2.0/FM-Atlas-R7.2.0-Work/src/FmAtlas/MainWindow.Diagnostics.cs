using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FmAtlas.Services;

namespace FmAtlas;

public partial class MainWindow
{
    private async Task<UIElement> TesterLogsPageAsync()
    {
        var root = new Grid();
        StackPanel checkList = null!;
        TextBox logView = null!;
        CheckBox sensitive = null!;
        string moduleReport = "";
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = Card(new StackPanel(), 15, new Thickness(0, 0, 0, 10));
        var hs = (StackPanel)header.Child;
        hs.Children.Add(Txt("DEVELOPER & SUPPORT", 9, true, Teal));
        hs.Children.Add(Txt("Tester & Logs", 22, true, Ink));
        hs.Children.Add(Txt("Run local health checks, copy a compact support log, or export a shareable support ZIP. The default report excludes player names, save names and personal file paths.", 10, false, Muted, wrap: true));
        Grid.SetRow(header, 0); root.Children.Add(header);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        var checksCard = Card(new DockPanel(), 14, new Thickness(0, 0, 8, 0));
        var checksDock = (DockPanel)checksCard.Child;
        var checksTop = new StackPanel();
        checksTop.Children.Add(Txt("SYSTEM CHECKS", 9, true, Teal));
        checksTop.Children.Add(Txt("Environment health", 17, true, Ink));
        var checkStatus = Txt("Checks have not been run in this session.", 10, false, Muted, wrap: true); checkStatus.Margin = new Thickness(0, 6, 0, 8);
        checksTop.Children.Add(checkStatus);
        checksTop.Children.Add(PrimaryButton("Run quick checks", async (_, _) => await RunChecksAsync()));
        checksTop.Children.Add(SoftButton("Run read-only FM module probe", async (_, _) =>
        {
            try
            {
                checkStatus.Text = "Inspecting FM process and game_plugin.dll read-only…";
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                moduleReport = await Task.Run(() => GameModuleDiagnostics.CreateSupportReport(sensitive.IsChecked == true));
                logView.Text = moduleReport;
                checkStatus.Text = "Read-only module probe completed. No game memory was changed. Use Copy diagnostics or Export support ZIP to share it.";
                AtlasLog.Info("Read-only FM module diagnostics completed.");
            }
            catch (Exception ex)
            {
                AtlasLog.Error("Read-only FM module diagnostics failed.", ex);
                checkStatus.Text = "Module probe failed: " + ex.GetType().Name + ". Export the support report and send it for investigation.";
                MessageBox.Show(this, ex.Message, "FM Atlas · Module diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { Mouse.OverrideCursor = null; }
        }));
        checksTop.Children.Add(SoftButton("Open application log folder", (_, _) => OpenLogFolder()));
        DockPanel.SetDock(checksTop, Dock.Top); checksDock.Children.Add(checksTop);
        checkList = new StackPanel();
        var checkScroll = new ScrollViewer { Content = checkList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        checksDock.Children.Add(checkScroll);
        Grid.SetColumn(checksCard, 0); body.Children.Add(checksCard);

        var reportCard = Card(new DockPanel(), 14, new Thickness(8, 0, 0, 0));
        var reportDock = (DockPanel)reportCard.Child;
        var reportTop = new StackPanel();
        reportTop.Children.Add(Txt("COPY & EXPORT", 9, true, Teal));
        reportTop.Children.Add(Txt("Support snapshot", 17, true, Ink));
        sensitive = new CheckBox { Content = "Include personal details (player names, save filename and local database path)", IsChecked = false, Margin = new Thickness(0, 10, 0, 10), Foreground = Ink, FontSize = 10 };
        reportTop.Children.Add(sensitive);
        reportTop.Children.Add(PrimaryButton("Copy safe diagnostics to clipboard", async (_, _) =>
        {
            try { Clipboard.SetText(await CreateDiagnosticsReportAsync(sensitive.IsChecked == true, moduleReport)); checkStatus.Text = "Diagnostics copied. Paste them into your FM Atlas support chat."; }
            catch (Exception ex) { AtlasLog.Error("Copy diagnostics failed.", ex); MessageBox.Show(this, ex.Message, "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }));
        reportTop.Children.Add(SoftButton("Export support ZIP", async (_, _) => await ExportSupportPackAsync(sensitive.IsChecked == true, moduleReport)));
        reportTop.Children.Add(SoftButton("Copy recent application log", (_, _) =>
        {
            Clipboard.SetText(AtlasLog.ReadRecent()); checkStatus.Text = "Recent application log copied to clipboard.";
        }));
        DockPanel.SetDock(reportTop, Dock.Top); reportDock.Children.Add(reportTop);
        logView = new TextBox { Text = AtlasLog.ReadRecent(12000), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 10, Background = Brush("#F4F8FB"), BorderBrush = Line, Padding = new Thickness(9) };
        DockPanel.SetDock(logView, Dock.Bottom); reportDock.Children.Add(logView);
        Grid.SetColumn(reportCard, 1); body.Children.Add(reportCard);
        Grid.SetRow(body, 1); root.Children.Add(body);

        async Task RunChecksAsync()
        {
            checkList.Children.Clear(); checkStatus.Text = "Running local checks…";
            async Task Add(string label, Func<Task<(bool Ok, string Detail)>> check)
            {
                try
                {
                    var result = await check();
                    AddCheckRow(checkList, label, result.Ok ? "PASS" : "INFO", result.Detail, result.Ok);
                }
                catch (Exception ex)
                {
                    AtlasLog.Error("Tester check failed: " + label, ex);
                    AddCheckRow(checkList, label, "FAIL", ex.GetType().Name + ": " + ex.Message, false);
                }
            }
            await Add("Atlas database", async () =>
            {
                var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
                return (true, $"Connected · {metrics.SavePlayers:N0} save players · {metrics.LivePlayers:N0} live identities · {metrics.LocalAttributeFacts:N0} attribute facts");
            });
            await Add("Local log storage", async () =>
            {
                Directory.CreateDirectory(AtlasLog.LogFolder);
                var probe = Path.Combine(AtlasLog.LogFolder, "write-test.tmp");
                await File.WriteAllTextAsync(probe, "FM Atlas log write test"); File.Delete(probe);
                return (true, "Writable · rolling application log enabled");
            });
            await Add("Python save bridge", async () =>
            {
                var available = await FmsaveBridgeService.IsAvailableAsync();
                return (available, available ? "Python Launcher and fmsave are detected" : "Not detected · run Setup-FMAtlas-Save-Bridge.ps1 to enable save imports");
            });
            await Add("Football Manager process", () => Task.FromResult((true, GameProcessDetector.Detect().Detail)));
            await Add("Imported career snapshot", async () =>
            {
                var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
                var ok = metrics.SavePlayers > 0;
                return (ok, ok ? $"{metrics.SavePlayers:N0} players · {metrics.SaveGame ?? "game unknown"} · {metrics.SaveBuild ?? "build unknown"}" : "No local save snapshot yet · import a COPY of a career save");
            });
            await Add("Store profile detection", () =>
            {
                try { var pid = LiveMemoryEditService.FindSupportedProcessId(); return Task.FromResult((true, $"Exact FM27 Windows module profile matched for process {pid}. This is a profile match, not a field-by-field write certification.")); }
                catch (Exception ex) { return Task.FromResult((false, ex.Message)); }
            });
            await Add("Legacy player preview path", () =>
            {
                var readOnly = !LiveMemoryEditService.LiveWritesEnabled;
                return Task.FromResult((readOnly,
                    readOnly
                        ? "READ-ONLY · this legacy player-only preview cannot Apply or Undo. Use the separately guarded Live Editor profile panel."
                        : "Unexpected legacy player writer state. Stop and report this build."));
            });
            await Add("Live editor session gate", () =>
            {
                var armed = LiveEntityMemoryEditService.LiveEditingEnabled;
                return Task.FromResult((true, armed
                    ? "ARMED · enabled for this Atlas session. Apply still requires fresh build, identity, career-session and read-back checks."
                    : "OFF · live writes are disarmed. Enable explicitly in Editor Studio only when testing an offline backup career."));
            });
            await Add("Live scalar field catalogue", () =>
            {
                var players = LiveEntityMemoryEditService.GetEditableFields(LiveEntityMemoryEditService.EntityKind.Player).Count;
                var staff = LiveEntityMemoryEditService.GetEditableFields(LiveEntityMemoryEditService.EntityKind.Staff).Count;
                var clubs = LiveEntityMemoryEditService.GetEditableFields(LiveEntityMemoryEditService.EntityKind.Club).Count;
                return Task.FromResult((players + staff + clubs > 0,
                    $"Whitelisted fields loaded · players={players:N0} · staff={staff:N0} · clubs={clubs:N0}. Catalogue presence does not mean every FM field is writable."));
            });
            checkStatus.Text = $"Completed {checkList.Children.Count:N0} checks · review INFO/FAIL rows before sending the support ZIP.";
            logView.Text = AtlasLog.ReadRecent(12000);
        }

        await RunChecksAsync();
        return root;
    }

    private static void AddCheckRow(Panel panel, string label, string state, string detail, bool success)
    {
        var border = new Border { BorderBrush = Brush("#D8E4ED"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 8, 0, 8) };
        var stack = new StackPanel();
        var row = new DockPanel();
        var name = Txt(label, 10, true, Ink); DockPanel.SetDock(name, Dock.Left); row.Children.Add(name);
        var result = Txt(state, 9, true, success ? Brush("#2F825D") : Brush("#A46E2B")); result.HorizontalAlignment = HorizontalAlignment.Right; row.Children.Add(result);
        stack.Children.Add(row); stack.Children.Add(Txt(detail, 9, false, Muted, wrap: true)); border.Child = stack; panel.Children.Add(border);
    }

    private void OpenLogFolder()
    {
        try { Directory.CreateDirectory(AtlasLog.LogFolder); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", AtlasLog.LogFolder) { UseShellExecute = true }); }
        catch (Exception ex) { AtlasLog.Error("Could not open application log folder.", ex); MessageBox.Show(this, ex.Message, "Open logs", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async Task<string> CreateDiagnosticsReportAsync(bool includePersonal, string? moduleReport = null)
    {
        var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
        var tables = await RunDatabaseAsync(() => _database.GetFmsaveExtraTableCountsAsync());
        var process = GameProcessDetector.Detect();
        var bridge = await FmsaveBridgeService.IsAvailableAsync();
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas", "scouting.db");
        var dbSize = File.Exists(dbPath) ? new FileInfo(dbPath).Length : 0;
        var sb = new StringBuilder();
        sb.AppendLine("FM ATLAS 27 — SUPPORT DIAGNOSTICS");
        sb.AppendLine("Report schema: 1 · Redacted by default");
        sb.AppendLine("Generated UTC: " + DateTime.UtcNow.ToString("O"));
        sb.AppendLine("App version: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " · FM Atlas V11");
        sb.AppendLine("OS: " + Environment.OSVersion.VersionString + " · 64-bit OS: " + Environment.Is64BitOperatingSystem);
        sb.AppendLine("Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        sb.AppendLine("Process architecture: " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
        sb.AppendLine("App base directory: " + (includePersonal ? AppContext.BaseDirectory : "[redacted]"));
        sb.AppendLine("Database file: " + (includePersonal ? dbPath : "%LOCALAPPDATA%\\FmAtlas\\scouting.db") + $" · {dbSize:N0} bytes");
        sb.AppendLine("FM process check: " + process.Detail);
        try
        {
            var profilePid = LiveMemoryEditService.FindSupportedProcessId();
            sb.AppendLine($"Store/build profile: exact profile matched for process {profilePid}; see Atlas logs for the profile ID/fingerprint. Match alone does not certify every field write.");
        }
        catch (Exception ex) { sb.AppendLine("Store/build profile: no unique supported profile match · " + ex.Message); }

        sb.AppendLine("Python/fmsave bridge: " + (bridge ? "available" : "not available"));
        sb.AppendLine();
        sb.AppendLine("DATA COUNTS");
        sb.AppendLine($"Live identities: players={metrics.LivePlayers} · clubs={metrics.LiveClubs} · staff={metrics.LiveStaff}");
        sb.AppendLine($"Latest save snapshot: players={metrics.SavePlayers} · clubs={metrics.SaveClubs} · staff={metrics.SaveStaff}");
        sb.AppendLine($"Facts: local player attributes={metrics.LocalAttributeFacts} · verified live facts={metrics.LiveFacts}");
        sb.AppendLine($"Latest save build: {metrics.SaveGame ?? "unknown game"} · {metrics.SaveBuild ?? "unknown build"} · known build={metrics.SaveBuildKnown?.ToString() ?? "unknown"} · date={metrics.SaveGameDate ?? "unknown"}");
        sb.AppendLine("Latest import time UTC: " + (metrics.LastSaveImportUtc ?? "none"));
        if (includePersonal) sb.AppendLine("Latest source save filename: " + (metrics.LastSaveFileName ?? "none"));
        sb.AppendLine("Latest validation JSON: " + (string.IsNullOrWhiteSpace(metrics.SaveValidationJson) ? "not supplied" : metrics.SaveValidationJson));
        sb.AppendLine();
        sb.AppendLine("RETAINED SAVE TABLES");
        foreach (var table in tables) sb.AppendLine($"{table.TableName}: {table.RowCount:N0}");
        sb.AppendLine();
        sb.AppendLine("EDITOR SAFETY");
        sb.AppendLine("FM Atlas V11: the player/staff/club scalar editor has a separate session-only opt-in. It starts OFF and resets OFF when Atlas closes. Apply/Undo re-check the armed state, exact build profile, career generation, entity identity and staged bytes; every completed write is read back. The legacy player-preview path remains read-only.");
        sb.AppendLine("Live editor session state: " + (LiveEntityMemoryEditService.LiveEditingEnabled ? "ARMED for this Atlas session" : "OFF"));
        sb.AppendLine("Mapped fields are a conservative whitelist. Unsupported pointer/list edits, relationship graphs, ownership/debt structures and unverified fields remain unavailable rather than guessed.");
        if (!string.IsNullOrWhiteSpace(moduleReport))
        {
            sb.AppendLine();
            sb.AppendLine("LIVE MODULE DIAGNOSTICS (READ-ONLY)");
            sb.AppendLine(moduleReport);
        }
        if (includePersonal)
        {
            var history = await RunDatabaseAsync(() => _database.GetRecentEditHistoryAsync(30));
            sb.AppendLine(); sb.AppendLine("RECENT EDIT HISTORY (PERSONAL DETAILS INCLUDED)");
            foreach (var item in history) sb.AppendLine($"{item.ChangedUtc}\t{item.PlayerName}\t{item.FieldName}\t{item.OldValue} -> {item.NewValue}\t{item.ChangeSource}");
        }
        sb.AppendLine(); sb.AppendLine("RECENT APPLICATION LOG");
        sb.AppendLine(AtlasLog.ReadRecent(30000));
        sb.AppendLine();
        sb.AppendLine("KNOWN LIMITS");
        sb.AppendLine("The live editor exposes only its explicit field catalogue for a matched FM27 build. Not every game attribute or database field is writable. New mappings require independent witnesses, bounded reads, validation and disposable-save acceptance. Some in-game displays update only after the game advances; save inside FM to persist confirmed changes.");
        return sb.ToString();
    }

    private async Task ExportSupportPackAsync(bool includePersonal, string? moduleReport = null)
    {
        var dlg = new SaveFileDialog { Title = "Export FM Atlas support pack", FileName = "FM-Atlas-27-support-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip", Filter = "ZIP support pack (*.zip)|*.zip" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var report = await CreateDiagnosticsReportAsync(includePersonal);
            await using var stream = new FileStream(dlg.FileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            var reportEntry = archive.CreateEntry("support-report.txt", CompressionLevel.Optimal);
            await using (var writer = new StreamWriter(reportEntry.Open(), new UTF8Encoding(false))) await writer.WriteAsync(report);
            if (!string.IsNullOrWhiteSpace(moduleReport))
            {
                var moduleEntry = archive.CreateEntry("live-module-diagnostics.txt", CompressionLevel.Optimal);
                await using var moduleWriter = new StreamWriter(moduleEntry.Open(), new UTF8Encoding(false));
                await moduleWriter.WriteAsync(moduleReport);
            }
            var logEntry = archive.CreateEntry("recent-application-log.txt", CompressionLevel.Optimal);
            await using (var writer = new StreamWriter(logEntry.Open(), new UTF8Encoding(false))) await writer.WriteAsync(AtlasLog.ReadRecent(60000));
            var readme = archive.CreateEntry("README.txt", CompressionLevel.Optimal);
            await using (var writer = new StreamWriter(readme.Open(), new UTF8Encoding(false)))
                await writer.WriteAsync("FM Atlas support bundle. Does not include the career save or the SQLite database. A live-module diagnostic is included only if you ran the read-only probe. Reports are redacted unless you opted into personal details in the export screen. Review files before sharing.\r\n");
            AtlasLog.Info("Support ZIP exported.");
            MessageBox.Show(this, "Support ZIP exported. It contains diagnostics and the recent application log, not your career save or Atlas database. Review it before sharing.", "FM Atlas support pack", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { AtlasLog.Error("Support ZIP export failed.", ex); MessageBox.Show(this, "Support export failed: " + ex.Message, "FM Atlas", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
