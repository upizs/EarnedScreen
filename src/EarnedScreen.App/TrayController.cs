using System.Windows;
using EarnedScreen.Core;
using WinForms = System.Windows.Forms;

namespace EarnedScreen.App;

/// <summary>
/// Owns the system-tray icon and the long-lived service connection. Lives for the whole app lifetime
/// so the Guillotine cool-down fires whether or not the gateway window is open. Shows the gateway
/// on demand from the tray.
/// </summary>
public sealed class TrayController : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly ServiceClient _client = new();
    private readonly CancellationTokenSource _cts = new();
    private MainWindow? _gateway;
    private bool _coolDownOpen;

    public TrayController()
    {
        _icon = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Shield,
            Text = "EarnedScreen",
            Visible = true,
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open EarnedScreen", null, (_, _) => ShowGateway());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => ShowGateway();

        // Keep listening for the Guillotine for the entire app lifetime.
        _client.SessionEnded += OnSessionEnded;
        _ = _client.ListenForEventsAsync(_cts.Token);
    }

    public void ShowGateway()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_gateway is null)
            {
                _gateway = new MainWindow();
                _gateway.Closed += (_, _) => _gateway = null;
            }

            _gateway.Show();
            if (_gateway.WindowState == WindowState.Minimized)
                _gateway.WindowState = WindowState.Normal;
            _gateway.Activate();
            _gateway.Topmost = true;
            _gateway.Topmost = false;
        });
    }

    private void OnSessionEnded()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_coolDownOpen) return;
            _coolDownOpen = true;

            var settings = new SettingsStore().Load();
            var screens = WinForms.Screen.AllScreens;
            var primary = WinForms.Screen.PrimaryScreen ?? screens[0];

            // One lock per monitor; only the primary carries the checklist, the rest are covers.
            var windows = new List<CoolDownWindow>();
            foreach (var screen in screens)
            {
                var interactive = screen.DeviceName == primary.DeviceName;
                windows.Add(new CoolDownWindow(settings, interactive, screen.Bounds));
            }

            var interactiveWindow = windows.FirstOrDefault(w => w.IsInteractive) ?? windows[0];
            interactiveWindow.Completed += () =>
            {
                foreach (var w in windows)
                {
                    w.AllowClose();
                    w.Close();
                }
                _coolDownOpen = false;
            };

            foreach (var w in windows) w.Show();
        });
    }

    private void ExitApp()
    {
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _icon.Visible = false;
        _icon.Dispose();
    }
}
