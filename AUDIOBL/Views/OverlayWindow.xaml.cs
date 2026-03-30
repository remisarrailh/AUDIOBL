using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AUDIOBL.Services;

namespace AUDIOBL.Views;

public partial class OverlayWindow : Window
{
    private readonly SettingsService _settings;

    public OverlayWindow(SettingsService settings)
    {
        _settings = settings;
        InitializeComponent();

        Left = _settings.Settings.OverlayLeft;
        Top = _settings.Settings.OverlayTop;

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        LocationChanged += (_, _) => SavePosition();
        Deactivated += (_, _) => { Topmost = false; Topmost = true; };
    }

    public void UpdateBattery(int? level)
    {
        if (level == null)
        {
            BatteryText.Text = "---";
            BatteryIcon.Text = "\U0001F50B";
            BatteryIcon.Foreground = Brushes.Gray;
            return;
        }

        BatteryText.Text = $"{level}%";
        BatteryIcon.Text = level switch
        {
            >= 80 => "\U0001F50B",
            >= 50 => "\U0001F50B",
            >= 20 => "\U0001FAAB",
            _ => "\U0001FAAB"
        };
        BatteryIcon.Foreground = level switch
        {
            >= 50 => Brushes.LightGreen,
            >= 20 => new SolidColorBrush(Color.FromRgb(255, 200, 0)),
            _ => Brushes.OrangeRed
        };
    }

    public void UpdateConnectionState(bool connected)
    {
        if (!connected)
        {
            BatteryText.Text = "---";
            BatteryIcon.Text = "\U0001F50B";
            BatteryIcon.Foreground = Brushes.Gray;
        }
    }

    private void SavePosition()
    {
        _settings.Settings.OverlayLeft = Left;
        _settings.Settings.OverlayTop = Top;
        _settings.Save();
    }
}
