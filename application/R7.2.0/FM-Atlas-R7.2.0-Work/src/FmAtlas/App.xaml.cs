using System.Windows;
using System.Windows.Threading;
using FmAtlas.Services;

namespace FmAtlas;

public partial class App : Application
{
    public App()
    {
        AtlasLog.Initialise();
        MonolithTheme.Apply(this);
        DispatcherUnhandledException += (_, e) =>
        {
            AtlasLog.Error("Unhandled WPF dispatcher exception.", e.Exception);
            // Preserve WPF's normal failure behaviour; the log is retained for the next launch.
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) AtlasLog.Error("Unhandled application-domain exception.", ex);
            else AtlasLog.Error("Unhandled application-domain exception with a non-Exception payload.");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AtlasLog.Error("Unobserved background task exception.", e.Exception);
            e.SetObserved();
        };
    }
}
