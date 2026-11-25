using System.IO;
using System.Windows;
using FMAtlas.Desktop.ViewModels;
using FMAtlas.Infrastructure.Connection;
using FMAtlas.Infrastructure.Import;
using FMAtlas.Infrastructure.Graphics;
using FMAtlas.Infrastructure.Persistence;

namespace FMAtlas.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var processProbe = new WindowsGameProcessProbe();
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FM Atlas");
        var databasePath = Path.Combine(dataDirectory, "atlas.db");
        var atlasDatabase = new AtlasDatabase(databasePath);
        // Create/migrate the local search database before Advanced Search opens.
        // The query provider is intentionally read-only and expects this schema to exist.
        try
        {
            atlasDatabase.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"FM Atlas could not initialise its local search database: {exception.Message}",
                "FM Atlas startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }
        var entitySearchProvider = new SqliteEntitySearchProvider(databasePath);
        var advancedSearchProvider = new SqliteAdvancedSearchProvider(databasePath);

        var snapshotStore = new SqliteEntityRecordSnapshotStore(databasePath);
        // Live-session providers are transient: a previous app session may have ended without
        // observing FM closing. Remove the known live import before exposing search at startup.
        try
        {
            ((FMAtlas.Application.Abstractions.IEntityRecordBatchSnapshotStore)snapshotStore)
                .DeleteProviderAsync(WindowsFm27LiveGameConnector.LegacyLiveProviderKey)
                .GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"FM Atlas could not clear a stale live-session index: {exception.Message}",
                "FM Atlas startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        var playerExportImporter = new FMViewExporterCsvImporter(snapshotStore);
        var liveGameConnector = new WindowsFm27LiveGameConnector(snapshotStore);

        // Index the standard FM27 user graphics folder when it exists. The indexer
        // only reads config.xml mappings; it never copies or changes graphics files.
        var graphicsLocator = new FootballManagerGraphicsLocator();
        Task? graphicsIndexTask = null;
        var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var graphicsFolder = Path.Combine(
            documentsFolder,
            "Sports Interactive",
            "Football Manager 27",
            "graphics");
        if (Directory.Exists(graphicsFolder))
            graphicsIndexTask = graphicsLocator.IndexAsync(graphicsFolder);

        var viewModel = new MainWindowViewModel(
            processProbe,
            atlasDatabase,
            entitySearchProvider,
            liveGameConnector,
            databasePath);
        var window = new MainWindow(
            viewModel,
            advancedSearchProvider,
            snapshotStore,
            playerExportImporter,
            entitySearchProvider,
            graphicsLocator,
            graphicsIndexTask);
        MainWindow = window;
        window.Show();
    }
}
