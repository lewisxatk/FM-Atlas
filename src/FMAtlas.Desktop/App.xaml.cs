using System.Windows;
using FMAtlas.Desktop.ViewModels;
using FMAtlas.Infrastructure.Connection;

namespace FMAtlas.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var processProbe = new WindowsGameProcessProbe();
        var viewModel = new MainWindowViewModel(processProbe);
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }
}
