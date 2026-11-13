using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
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
        checksTop.Children.Add(SoftButton("Open application log folder", (_, _) => OpenLogFolder()));
        DockPanel.SetDock(checksTop, Dock.Top); checksDock.Children.Add(checksTop);
        var checkList = new StackPanel();
        var checkScroll = new ScrollViewer { Content = checkList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        checksDock.Children.Add(checkScroll);
        Grid.SetColumn(checksCard, 0); body.Children.Add(checksCard);

        var reportCard = Card(new DockPanel(), 14, new Thickness(8, 0, 0, 0));
        var reportDock = (DockPanel)reportCard.Child;
        var reportTop = new StackPanel();
        reportTop.Children.Add(Txt("COPY & EXPORT", 9, true, Teal));
        reportTop.Children.Add(Txt("Support snapshot", 17, true, Ink));
        var sensitive = new CheckBox { Content = "Include personal details (player names, save filename and local database path)", IsChecked = false, Margin = new Thickness(0, 10, 0, 10), Foreground = Ink, FontSize = 10 };
        reportTop.Children.Add(sensitive);
        reportTop.Children.Add(PrimaryButton("Copy safe diagnostics to clipboard", async (_, _) =>
        {
            try { Clipboard.SetText(await CreateDiagnosticsReportAsync(sensitive.IsChecked == true)); checkStatus.Text = "Diagnostics copied. Paste them into your FM Atlas support chat."; }
            catch (Exception ex) { AtlasLog.Error("Copy diagnostics failed.", ex); MessageBox.Show(this, ex.Message, "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }));
        reportTop.Children.Add(SoftButton("Export support ZIP", async (_, _) => await ExportSupportPackAsync(sensitive.IsChecked == true)));
        reportTop.Children.Add(SoftButton("Copy recent application log", (_, _) =>
        {
            Clipboard.SetText(AtlasLog.ReadRecent()); checkStatus.Text = "Recent application log copied to clipboard.";
        }));
        DockPanel.SetDock(reportTop, Dock.Top); reportDock.Children.Add(reportTop);
        var logView = new TextBox { Text = AtlasLog.ReadRecent(12000), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 10, Background = Brush("#F4F8FB"), BorderBrush = Line, Padding = new Thickness(9) };
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
            await Add("Live edit write safety", () =>
            {
                string profileStatus;
                try { var pid = LiveMemoryEditService.FindSupportedProcessId(); profileStatus = $"A registered module profile matched process {pid}. "; }
                catch (Exception ex) { profileStatus = "No registered process profile matched (" + AtlasLog.RedactForSupport(ex.Message) + "). "; }
                return Task.FromResult((false, "BLOCKED BY BUILD CERTIFICATION GATE · " + profileStatus + LiveWriteCertification.BlockReason));
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

    private async Task<string> CreateDiagnosticsReportAsync(bool includePersonal, string? bundleId = null)
    {
        var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
        var tables = await RunDatabaseAsync(() => _database.GetFmsaveExtraTableCountsAsync());
        var process = GameProcessDetector.Detect();
        var bridge = await FmsaveBridgeService.IsAvailableAsync();
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas", "scouting.db");
        var dbSize = File.Exists(dbPath) ? new FileInfo(dbPath).Length : 0;
        var assembly = typeof(App).Assembly;
        var appVersion = assembly.GetName().Version?.ToString() ?? "unknown";
        var id = bundleId ?? Guid.NewGuid().ToString("N");
        var sb = new StringBuilder();
        sb.AppendLine("FM ATLAS — SUPPORT DIAGNOSTICS");
        sb.AppendLine("Report schema: 2");
        sb.AppendLine("Bundle ID: " + id);
        sb.AppendLine("Generated UTC: " + DateTime.UtcNow.ToString("O"));
        sb.AppendLine("App version: " + appVersion);
        sb.AppendLine("OS: " + Environment.OSVersion.VersionString + " · 64-bit OS: " + Environment.Is64BitOperatingSystem);
        sb.AppendLine("Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        sb.AppendLine("Process architecture: " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
        sb.AppendLine("Application base directory: " + (includePersonal ? AppContext.BaseDirectory : "[redacted]"));
        sb.AppendLine("Database: " + (includePersonal ? dbPath : "%LOCALAPPDATA%\\FmAtlas\\scouting.db") + $" · {dbSize:N0} bytes");
        sb.AppendLine("FM process check: " + AtlasLog.RedactForSupport(process.Detail));
        try
        {
            var profilePid = LiveMemoryEditService.FindSupportedProcessId();
            sb.AppendLine($"Store/build profile: exact registered module profile matched process {profilePid}; this is not field-level certification.");
        }
        catch (Exception ex)
        {
            sb.AppendLine("Store/build profile: no unique supported profile match · " + AtlasLog.RedactForSupport(ex.Message));
        }

        sb.AppendLine("Python/fmsave bridge: " + (bridge ? "available" : "not available"));
        sb.AppendLine();
        sb.AppendLine("DATA COUNTS");
        sb.AppendLine($"Live identities: players={metrics.LivePlayers} · clubs={metrics.LiveClubs} · staff={metrics.LiveStaff}");
        sb.AppendLine($"Latest save snapshot: players={metrics.SavePlayers} · clubs={metrics.SaveClubs} · staff={metrics.SaveStaff}");
        sb.AppendLine($"Facts: local player attributes={metrics.LocalAttributeFacts} · imported live facts={metrics.LiveFacts}");
        sb.AppendLine($"Latest save build: {metrics.SaveGame ?? "unknown game"} · {metrics.SaveBuild ?? "unknown build"} · registered layout={metrics.SaveBuildKnown?.ToString() ?? "unknown"} · date={metrics.SaveGameDate ?? "unknown"}");
        sb.AppendLine("Latest import time UTC: " + (metrics.LastSaveImportUtc ?? "none"));
        if (includePersonal) sb.AppendLine("Latest source save filename: " + (metrics.LastSaveFileName ?? "none"));
        sb.AppendLine("Raw save-validation payload: omitted from support bundle; use the local Save Inspector for the full validation detail.");
        sb.AppendLine();
        sb.AppendLine("RETAINED SAVE TABLES");
        foreach (var table in tables) sb.AppendLine($"{table.TableName}: {table.RowCount:N0}");
        sb.AppendLine();
        sb.AppendLine("EDITOR SAFETY");
        sb.AppendLine("Local Atlas database edits are separate from the running Football Manager process.");
        sb.AppendLine("Live memory Apply and Undo: BLOCKED by a build-wide certification gate.");
        sb.AppendLine(LiveWriteCertification.BlockReason);
        sb.AppendLine("A registered executable/module fingerprint does not prove a field offset, encoding or entity relationship.");
        if (includePersonal)
        {
            var history = await RunDatabaseAsync(() => _database.GetRecentEditHistoryAsync(30));
            sb.AppendLine();
            sb.AppendLine("RECENT EDIT HISTORY (PERSONAL DETAILS INCLUDED BY USER CHOICE)");
            foreach (var item in history)
                sb.AppendLine($"{item.ChangedUtc}\\t{item.PlayerName}\\t{item.FieldName}\\t{item.OldValue} -> {item.NewValue}\\t{item.ChangeSource}");
        }
        sb.AppendLine();
        sb.AppendLine("KNOWN LIMITS");
        sb.AppendLine("Live player facts and candidate ability/attribute values still require source and in-game verification. Save statistics depend on the installed fmsave layout and validation coverage. Game Pass graphics paths and every workspace's runtime behaviour have not all been accepted.");
        return AtlasLog.RedactForSupport(sb.ToString());
    }

    private async Task ExportSupportPackAsync(bool includePersonal)
    {
        var bundleId = Guid.NewGuid().ToString("N");
        try
        {
            var report = await CreateDiagnosticsReportAsync(includePersonal, bundleId);
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["support-report.txt"] = new UTF8Encoding(false).GetBytes(report),
                ["README.txt"] = new UTF8Encoding(false).GetBytes(
                    "FM Atlas support bundle\\r\\n" +
                    "This ZIP contains diagnostic text and redacted application logs only. It does not contain your career save, FM installation files, SQLite database or arbitrary personal files.\\r\\n" +
                    (includePersonal ? "You opted in to include player names, recent edit history, save filename and local database path. Review before sharing.\\r\\n" : "Personal names, save filename and local file paths were excluded by default.\\r\\n") +
                    "No files are uploaded automatically. You choose whether and how to share this ZIP.\\r\\n")
            };

            var logFiles = AtlasLog.GetLogFiles();
            var logIndex = 0;
            foreach (var logPath in logFiles)
            {
                var logName = logIndex++ == 0 ? "recent-application-log.txt" : $"application-logs/fm-atlas.{logIndex - 1}.log";
                var text = AtlasLog.ReadFileForSupport(logPath, 60000);
                if (string.IsNullOrWhiteSpace(text)) continue;
                files[logName] = new UTF8Encoding(false).GetBytes(text);
            }
            if (!files.ContainsKey("recent-application-log.txt"))
                files["recent-application-log.txt"] = new UTF8Encoding(false).GetBytes("No application log is available.");

            var manifestEntries = files.Select(pair => new
            {
                name = pair.Key,
                sizeBytes = pair.Value.LongLength,
                sha256 = Convert.ToHexString(SHA256.HashData(pair.Value))
            }).ToArray();
            var manifest = new
            {
                schema = 1,
                bundleId,
                generatedUtc = DateTime.UtcNow,
                appVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown",
                personalDetailsIncluded = includePersonal,
                automaticallyUploaded = false,
                excludedByDesign = new[] { "career saves", "SQLite database", "FM installation files", "arbitrary user files", "credentials and tokens" },
                files = manifestEntries
            };
            files["manifest.json"] = new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            var preview = new StringBuilder();
            preview.AppendLine("SUPPORT BUNDLE CONTENTS");
            preview.AppendLine("Bundle ID: " + bundleId);
            preview.AppendLine("Personal details: " + (includePersonal ? "included because you opted in" : "excluded"));
            preview.AppendLine();
            preview.AppendLine("Files to be exported:");
            foreach (var item in manifestEntries) preview.AppendLine($"• {item.name} · {item.sizeBytes:N0} bytes · SHA-256 {item.sha256}");
            preview.AppendLine("• manifest.json");
            preview.AppendLine();
            preview.AppendLine("Excluded: career save files, FM installation files, SQLite database, arbitrary user files, credentials and tokens.");
            preview.AppendLine("No automatic upload. The ZIP is saved locally and only shared if you choose.");
            preview.AppendLine();
            preview.AppendLine("DIAGNOSTIC REPORT PREVIEW");
            preview.AppendLine(report);

            var previewWindow = new Window
            {
                Title = "Review FM Atlas support bundle",
                Owner = this,
                Width = 820,
                Height = 620,
                MinWidth = 620,
                MinHeight = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brush("#101216"),
                Foreground = Ink
            };
            var layout = new Grid { Margin = new Thickness(16) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var title = Txt("Review before exporting", 18, true, Ink);
            layout.Children.Add(title);
            var previewText = new TextBox
            {
                Text = preview.ToString(),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 10,
                Background = Brush("#1B1F25"),
                Foreground = Ink,
                BorderBrush = Line,
                Padding = new Thickness(10),
                Margin = new Thickness(0, 12, 0, 12)
            };
            Grid.SetRow(previewText, 1);
            layout.Children.Add(previewText);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(SoftButton("Cancel", (_, _) => previewWindow.DialogResult = false));
            actions.Children.Add(PrimaryButton("Continue to save ZIP", (_, _) => previewWindow.DialogResult = true));
            Grid.SetRow(actions, 2);
            layout.Children.Add(actions);
            previewWindow.Content = layout;
            if (previewWindow.ShowDialog() != true) return;

            var dialog = new SaveFileDialog
            {
                Title = "Save reviewed FM Atlas support pack",
                FileName = "FM-Atlas-support-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip",
                Filter = "ZIP support pack (*.zip)|*.zip",
                AddExtension = true,
                DefaultExt = ".zip",
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != true) return;

            var tempPath = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
                    foreach (var pair in files)
                    {
                        var entry = archive.CreateEntry(pair.Key, CompressionLevel.Optimal);
                        await using var entryStream = entry.Open();
                        await entryStream.WriteAsync(pair.Value);
                    }
                }
                File.Move(tempPath, dialog.FileName, true);
            }
            catch
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                throw;
            }

            AtlasLog.Info("Support bundle exported locally. Bundle ID=" + bundleId);
            MessageBox.Show(this, "Support ZIP saved. No files were uploaded. The reviewed bundle contains only its listed report/log files; it does not include your career save or Atlas database. Review the ZIP once more before sharing.\\n\\nBundle ID: " + bundleId, "FM Atlas support pack", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AtlasLog.Error("Support ZIP export failed.", ex);
            MessageBox.Show(this, "Support export failed: " + ex.Message, "FM Atlas", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
