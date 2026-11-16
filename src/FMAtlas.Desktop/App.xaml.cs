using System.IO;
using System.Windows;
using FMAtlas.Desktop.ViewModels;
using FMAtlas.Infrastructure.Connection;
using FMAtlas.Infrastructure.Import;
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
        var playerExportImporter = new FMViewExporterCsvImporter(snapshotStore);

        var viewModel = new MainWindowViewModel(
            processProbe,
            atlasDatabase,
            entitySearchProvider,
            databasePath);
        var window = new MainWindow(viewModel, advancedSearchProvider, snapshotStore, playerExportImporter);
        MainWindow = window;
        window.Show();
    }
}
