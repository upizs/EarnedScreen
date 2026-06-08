using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EarnedScreen.Core;

namespace EarnedScreen.App;

/// <summary>
/// Full-screen, un-closeable cool-down lock shown when the Guillotine drops. The network is already
/// re-blocked by the service; this is the "anti-potato" nudge. Closes only when every item is ticked.
/// </summary>
public partial class CoolDownWindow : Window
{
    private readonly List<CheckBox> _checkboxes = new();
    private bool _completed;

    public CoolDownWindow(Settings settings)
    {
        InitializeComponent();

        foreach (var item in settings.CoolDownChecklist)
        {
            var cb = new CheckBox
            {
                Content = item,
                Margin = new Thickness(0, 6, 0, 6),
                FontSize = 16,
                Foreground = Brushes.White,
            };
            cb.Checked += (_, _) => UpdateDoneButton();
            cb.Unchecked += (_, _) => UpdateDoneButton();
            _checkboxes.Add(cb);
            ChecklistPanel.Children.Add(cb);
        }

        UpdateDoneButton();
    }

    private void UpdateDoneButton()
        => DoneButton.IsEnabled = _checkboxes.All(c => c.IsChecked == true);

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_checkboxes.All(c => c.IsChecked == true))
        {
            _completed = true;
            Close();
        }
    }

    // Block Alt+F4 / programmatic close until the checklist is done.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_completed) e.Cancel = true;
        base.OnClosing(e);
    }
}
