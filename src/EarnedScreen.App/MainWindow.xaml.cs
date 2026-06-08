using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EarnedScreen.Core;

namespace EarnedScreen.App;

public partial class MainWindow : Window
{
    private readonly ServiceClient _client = new();
    private readonly Settings _settings = new SettingsStore().Load();
    private readonly List<CheckBox> _checkboxes = new();
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly CancellationTokenSource _cts = new();
    private bool _coolDownOpen;

    public MainWindow()
    {
        InitializeComponent();

        BuildChecklist();

        _client.SessionEnded += OnSessionEnded;
        _ = _client.ListenForEventsAsync(_cts.Token);

        _statusTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _statusTimer.Start();

        Loaded += async (_, _) => await RefreshStatusAsync();
        Closed += (_, _) => _cts.Cancel();
    }

    private void BuildChecklist()
    {
        foreach (var item in _settings.GatewayChecklist)
        {
            var cb = new CheckBox { Content = item, Margin = new Thickness(0, 4, 0, 4), FontSize = 14 };
            cb.Checked += (_, _) => UpdateEarnButton();
            cb.Unchecked += (_, _) => UpdateEarnButton();
            _checkboxes.Add(cb);
            ChecklistPanel.Children.Add(cb);
        }
    }

    private bool AllChecked => _checkboxes.All(c => c.IsChecked == true);

    private async Task RefreshStatusAsync()
    {
        var status = await _client.GetStatusAsync(_cts.Token);
        if (status is null)
        {
            StatusText.Text = "⚠ Service not reachable. Is the EarnedScreen service running?";
            EarnButton.IsEnabled = false;
            return;
        }

        if (status.Status == BlockStatus.Unlocked)
        {
            var span = TimeSpan.FromSeconds(status.RemainingSeconds);
            StatusText.Text = $"🟢 Unlocked — {span:hh\\:mm\\:ss} remaining.";
        }
        else if (!status.SessionAvailableToday)
        {
            StatusText.Text = "🔒 Blocked — no sessions left today. Come back tomorrow.";
        }
        else
        {
            StatusText.Text = $"🔒 Blocked — one {status.SessionMinutes}-minute session available.";
        }

        _lastStatus = status;
        UpdateEarnButton();
    }

    private StatusResponse? _lastStatus;

    private void UpdateEarnButton()
    {
        var available = _lastStatus?.SessionAvailableToday == true
                        && _lastStatus?.Status == BlockStatus.Blocked;
        EarnButton.IsEnabled = available && AllChecked;
    }

    private async void EarnButton_Click(object sender, RoutedEventArgs e)
    {
        if (!AllChecked)
        {
            MessageText.Text = "Finish every item first. No shortcuts.";
            return;
        }

        EarnButton.IsEnabled = false;
        var result = await _client.RequestUnlockAsync(_cts.Token);
        MessageText.Text = result?.Message ?? "Could not reach the service.";

        if (result?.Status == BlockStatus.Unlocked)
            foreach (var cb in _checkboxes) cb.IsChecked = false;

        await RefreshStatusAsync();
    }

    private void OnSessionEnded()
    {
        Dispatcher.Invoke(() =>
        {
            if (_coolDownOpen) return;
            _coolDownOpen = true;

            var cooldown = new CoolDownWindow(_settings);
            cooldown.Closed += (_, _) => _coolDownOpen = false;
            cooldown.Show();
        });
    }
}
