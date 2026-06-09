using System.Threading;
using System.Windows;

namespace EarnedScreen.App;

/// <summary>
/// Tray-resident client. On startup it does NOT show a window — it sits in the system tray and keeps
/// a live listener for the Guillotine (so the cool-down lock fires even when no window is open).
/// The gateway window is shown on demand from the tray.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = "EarnedScreen.App.SingleInstance";
    private Mutex? _mutex;
    private TrayController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one tray instance (launch-at-login + a manual double-click shouldn't double up).
        _mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isNew);
        if (!isNew)
        {
            // Hard-exit the duplicate; Shutdown() during OnStartup can leave the process lingering.
            _mutex.Dispose();
            _mutex = null;
            Environment.Exit(0);
            return;
        }

        // The app lives in the tray; closing the gateway window must not exit the process.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _tray = new TrayController();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
