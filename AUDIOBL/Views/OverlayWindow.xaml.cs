using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AUDIOBL.Helpers;
using AUDIOBL.Services;
using Microsoft.Win32;

namespace AUDIOBL.Views;

public partial class OverlayWindow : Window
{
    private const double EdgeMargin = 24;

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

        // The overlay "disappears" mostly because the saved position lands off-screen
        // after a resolution / monitor / DPI change. Re-validate it once measured and
        // whenever the display layout changes at runtime.
        Loaded += (_, _) => EnsureOnScreen();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.Invoke(EnsureOnScreen);

    /// <summary>Clamps the overlay back onto a visible screen, or resets it to a default
    /// corner if its saved position is entirely outside every monitor.</summary>
    private void EnsureOnScreen()
    {
        double w = ActualWidth  > 0 ? ActualWidth  : 120;
        double h = ActualHeight > 0 ? ActualHeight : 48;

        double vL = SystemParameters.VirtualScreenLeft;
        double vT = SystemParameters.VirtualScreenTop;
        double vR = vL + SystemParameters.VirtualScreenWidth;
        double vB = vT + SystemParameters.VirtualScreenHeight;

        bool offScreen = double.IsNaN(Left) || double.IsNaN(Top)
            || Left + w - EdgeMargin < vL || Left + EdgeMargin > vR
            || Top  + h - EdgeMargin < vT || Top  + EdgeMargin > vB;

        if (offScreen)
        {
            ResetToDefaultCorner(w, h);
        }
        else
        {
            // Nudge fully into view if partly clipped.
            Left = Math.Min(Math.Max(Left, vL), vR - w);
            Top  = Math.Min(Math.Max(Top,  vT), vB - h);
        }
    }

    private void ResetToDefaultCorner(double w, double h)
    {
        var wa = SystemParameters.WorkArea; // primary monitor work area
        Left = wa.Right - w - EdgeMargin;
        Top  = wa.Top + EdgeMargin;
    }

    /// <summary>Recovery action: make the overlay visible, on-screen and on top.</summary>
    public void RecenterAndShow()
    {
        if (!IsVisible) Show();
        UpdateLayout();
        ResetToDefaultCorner(ActualWidth > 0 ? ActualWidth : 120,
                             ActualHeight > 0 ? ActualHeight : 48);
        Topmost = false; Topmost = true;
        Activate();
    }

    public void UpdateBattery(int? level, DateTime? timestamp)
    {
        if (level == null)
        {
            BatteryText.Text = "---";
            BatteryText.Foreground = Brushes.White;
            BatteryIcon.Text = "\U0001F50B";
            BatteryIcon.Foreground = Brushes.Gray;
            return;
        }

        BatteryText.Text = $"{level}%";
        // Percentage colour reflects how old the reading is.
        BatteryText.Foreground = BatteryAge.Evaluate(timestamp) switch
        {
            BatteryFreshness.Stale6h => Brushes.OrangeRed,
            BatteryFreshness.Stale1h => new SolidColorBrush(Color.FromRgb(255, 200, 0)),
            _                        => Brushes.White
        };
        // Icon colour still reflects the charge level.
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

    private void SavePosition()
    {
        _settings.Settings.OverlayLeft = Left;
        _settings.Settings.OverlayTop = Top;
        _settings.Save();
    }
}
