using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EarnedScreen.Core;

namespace EarnedScreen.App;

public partial class MainWindow : Window
{
    private static readonly string[] Quotes =
    {
        "Discipline is choosing what you want most over what you want now.",
        "The wall is only as strong as your will. Make both strong.",
        "Earn it. Then enjoy it without guilt.",
        "Small reps today. A different person in a year.",
        "Motivation gets you started. Systems keep you going.",
        "You don't rise to your goals — you fall to your systems.",
        "Do the hard thing first. The screen can wait.",
        "Future you is watching. Make them proud.",
    };

    private readonly ServiceClient _client = new();
    private readonly Settings _settings = new SettingsStore().Load();
    private readonly NotionTasksClient _notion = new();
    private readonly List<CheckBox> _checkboxes = new();
    private readonly Dictionary<CheckBox, string> _notionPageIds = new();
    private string? _notionDoneOptionId;
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly CancellationTokenSource _cts = new();

    public MainWindow()
    {
        InitializeComponent();

        QuoteText.Text = Quotes[Random.Shared.Next(Quotes.Length)];

        BuildChecklist();

        // Status polling only runs while this window is open; stopped on close to stay idle-cheap.
        _statusTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _statusTimer.Start();

        Loaded += async (_, _) =>
        {
            await RefreshStatusAsync();
            await LoadNotionTasksAsync();
        };
        Closed += (_, _) =>
        {
            _statusTimer.Stop();
            _cts.Cancel();
            _notion.Dispose();
        };
    }

    private void BuildChecklist()
    {
        foreach (var item in _settings.GatewayChecklist)
            ChecklistPanel.Children.Add(NewItem(item));

        UpdateEarnButton();
    }

    private CheckBox NewItem(string text)
    {
        var cb = new CheckBox { Content = text, Style = (Style)FindResource("ChecklistCheckBox") };
        cb.Checked += (_, _) => UpdateEarnButton();
        cb.Unchecked += (_, _) => UpdateEarnButton();
        _checkboxes.Add(cb);
        return cb;
    }

    private bool AllChecked => _checkboxes.Count > 0 && _checkboxes.All(c => c.IsChecked == true);

    private async Task LoadNotionTasksAsync()
    {
        if (!_settings.Notion.Enabled)
        {
            ShowNotionNote("Notion tasklist not found.");
            return;
        }

        ShowNotionNote("Loading tasks from Notion…");
        var result = await _notion.GetTodayOpenTasksAsync(_settings.Notion, _cts.Token);

        if (!result.Available)
        {
            ShowNotionNote("Notion tasklist not found.");
            return;
        }

        _notionDoneOptionId = result.DoneOptionId;

        if (result.Tasks.Count == 0)
        {
            ShowNotionNote("No Notion tasks due today. 🎉", Brush("SuccessBrush"));
            return;
        }

        NotionNote.Visibility = Visibility.Collapsed;
        NotionHeader.Visibility = Visibility.Visible;

        foreach (var task in result.Tasks)
        {
            var cb = NewItem(task.Title);
            _notionPageIds[cb] = task.PageId;
            NotionPanel.Children.Add(cb);
        }

        UpdateEarnButton();
    }

    private void ShowNotionNote(string text, Brush? color = null)
    {
        NotionNote.Text = text;
        NotionNote.Foreground = color ?? Brush("WarnBrush");
        NotionNote.Visibility = Visibility.Visible;
    }

    private Brush Brush(string key) => (Brush)FindResource(key);

    private async Task RefreshStatusAsync()
    {
        var status = await _client.GetStatusAsync(_cts.Token);
        if (status is null)
        {
            StatusText.Foreground = Brush("DangerBrush");
            StatusText.Text = "⚠ Service not reachable. Is the EarnedScreen service running?";
            _lastStatus = null;
            UpdateEarnButton();
            return;
        }

        if (status.Status == BlockStatus.Unlocked)
        {
            var span = TimeSpan.FromSeconds(status.RemainingSeconds);
            StatusText.Foreground = Brush("SuccessBrush");
            StatusText.Text = $"😎 Unlocked — {span:hh\\:mm\\:ss} remaining.";
        }
        else if (!status.SessionAvailableToday)
        {
            StatusText.Foreground = Brush("DangerBrush");
            StatusText.Text = "🔒 Blocked — no sessions left today. Come back tomorrow.";
        }
        else
        {
            StatusText.Foreground = Brush("DangerBrush");
            StatusText.Text = $"💪 Blocked — one {status.SessionMinutes}-minute session to earn.";
        }

        DnsStatusText.Text = status.DnsFilterActive
            ? $"🛡 Family-safe DNS: ON ({status.DnsFilterName})"
            : "";

        _lastStatus = status;
        UpdateEarnButton();
    }

    private StatusResponse? _lastStatus;

    private void UpdateEarnButton()
    {
        // Only let the user work the toll when a session is actually earnable today.
        var canEarn = _lastStatus?.SessionAvailableToday == true
                      && _lastStatus?.Status == BlockStatus.Blocked;

        foreach (var cb in _checkboxes)
            cb.IsEnabled = canEarn;

        var done = _checkboxes.Count(c => c.IsChecked == true);
        ProgressText.Text = $"{done} / {_checkboxes.Count} DONE";

        EarnButton.IsEnabled = canEarn && AllChecked;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void EarnButton_Click(object sender, RoutedEventArgs e)
    {
        if (!AllChecked)
        {
            MessageText.Foreground = Brush("WarnBrush");
            MessageText.Text = "✋ Complete the list first. No shortcuts.";
            return;
        }

        EarnButton.IsEnabled = false;
        var result = await _client.RequestUnlockAsync(_cts.Token);

        if (result is null)
        {
            MessageText.Foreground = Brush("DangerBrush");
            MessageText.Text = "Could not reach the service.";
        }
        else if (result.Status == BlockStatus.Unlocked)
        {
            MessageText.Foreground = Brush("SuccessBrush");
            MessageText.Text = $"😎 {result.Message}";

            // Session committed: mark the completed Notion tasks done (best-effort, fire-and-forget).
            foreach (var (cb, pageId) in _notionPageIds)
                if (cb.IsChecked == true)
                    _ = _notion.MarkTaskDoneAsync(_settings.Notion, pageId, _notionDoneOptionId, _cts.Token);

            foreach (var cb in _checkboxes) cb.IsChecked = false;
        }
        else
        {
            MessageText.Foreground = Brush("DangerBrush");
            MessageText.Text = $"🔒 {result.Message}";
        }

        await RefreshStatusAsync();
    }
}
