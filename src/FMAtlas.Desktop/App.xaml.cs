using System.IO;
using System.Windows;
using FMAtlas.Desktop.ViewModels;
using FMAtlas.Infrastructure.Connection;
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
        var entitySearchProvider = new SqliteEntitySearchProvider(databasePath);
        var advancedSearchProvider = new SqliteAdvancedSearchProvider(databasePath);

        var snapshotStore = new SqliteEntityRecordSnapshotStore(databasePath);

        var viewModel = new MainWindowViewModel(
            processProbe,
            atlasDatabase,
            entitySearchProvider,
            databasePath);
        var window = new MainWindow(viewModel, advancedSearchProvider, snapshotStore);
        MainWindow = window;
        window.Show();
    }
}
