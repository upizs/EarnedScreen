using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using EarnedScreen.Core;

namespace EarnedScreen.App;

/// <summary>
/// Full-screen, un-closeable cool-down lock shown when the Guillotine drops. One window is created
/// per monitor so the user can't dodge it on another screen. The network is already re-blocked by the
/// service; this is the "anti-potato" nudge. Only the interactive window (on the primary monitor)
/// carries the checklist; the rest are plain covers.
/// </summary>
public partial class CoolDownWindow : Window
{
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private readonly List<CheckBox> _checkboxes = new();
    private readonly System.Drawing.Rectangle _bounds;
    private bool _completed;

    public bool IsInteractive { get; }

    /// <summary>Raised when the user finishes the interactive checklist.</summary>
    public event Action? Completed;

    public CoolDownWindow(Settings settings, bool interactive, System.Drawing.Rectangle bounds)
    {
        InitializeComponent();

        IsInteractive = interactive;
        _bounds = bounds;

        QuoteText.Text = "The wall is only as strong as your will.";

        if (!interactive)
        {
            // Cover-only window for secondary monitors: just the headline, no checklist/button.
            ChecklistHost.Visibility = Visibility.Collapsed;
            DoneButton.Visibility = Visibility.Collapsed;
            QuoteText.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var item in settings.CoolDownChecklist)
        {
            var cb = new CheckBox { Content = item, Style = (Style)FindResource("ChecklistCheckBox") };
            cb.Checked += (_, _) => UpdateDoneButton();
            cb.Unchecked += (_, _) => UpdateDoneButton();
            _checkboxes.Add(cb);
            ChecklistPanel.Children.Add(cb);
        }

        UpdateDoneButton();
    }

    // Position/size to the target monitor in physical pixels (DPI-proof via PerMonitorV2 manifest).
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_TOPMOST, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, SWP_SHOWWINDOW);
    }

    /// <summary>Permits this window to close (used to dismiss cover windows once the toll is paid).</summary>
    public void AllowClose() => _completed = true;

    private void UpdateDoneButton()
        => DoneButton.IsEnabled = _checkboxes.All(c => c.IsChecked == true);

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_checkboxes.All(c => c.IsChecked == true))
        {
            _completed = true;
            Completed?.Invoke();
        }
    }

    // Block Alt+F4 / programmatic close until allowed.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_completed) e.Cancel = true;
        base.OnClosing(e);
    }
}
