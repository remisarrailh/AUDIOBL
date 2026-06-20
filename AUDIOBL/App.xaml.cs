using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AUDIOBL.Helpers;
using AUDIOBL.Services;
using AUDIOBL.Views;
using Hardcodet.Wpf.TaskbarNotification;

namespace AUDIOBL;

public partial class App : Application
{
    private static Mutex? _mutex;
    private TaskbarIcon? _trayIcon;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private LowBatteryWindow? _lowBatteryWindow;

    private const int LowBatteryThreshold     = 35; // warn at or below this
    private const int RechargedResetThreshold = 50; // re-arm the warning once recharged here
    private BluetoothService? _bluetoothService;
    private SettingsService? _settingsService;

    private int?     _lastBatteryLevel;
    private DateTime? _lastBatteryTime;

    private nint _hTrayIcon = nint.Zero;

    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint hIcon);

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            WriteLog($"UnhandledException: {ex.ExceptionObject}");
        DispatcherUnhandledException += (_, ex) =>
        {
            WriteLog($"DispatcherUnhandledException: {ex.Exception}");
            ex.Handled = true;
        };

        _mutex = new Mutex(true, "Global\\AUDIOBL", out bool createdNew);
        if (!createdNew) { Shutdown(); return; }

        base.OnStartup(e);

        try
        {
            _settingsService = new SettingsService();

            // Apply saved language (or detect from system)
            ApplyLanguage(_settingsService.Settings.Language);

            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            RefreshMenuStrings();

            _overlay = new OverlayWindow(_settingsService);
            _overlay.Show();

            // Restore the last known battery value so it shows immediately at launch.
            _lastBatteryLevel = _settingsService.Settings.LastBatteryLevel;
            _lastBatteryTime  = _settingsService.Settings.LastBatteryTimestamp;
            RenderBattery();

            _bluetoothService = new BluetoothService(_settingsService.Settings.DeviceName);
            _bluetoothService.BatteryLevelChanged     += OnBatteryLevelChanged;
            _bluetoothService.DeviceConnectionChanged += OnDeviceConnectionChanged;
            _ = _bluetoothService.StartAsync();
            _ = CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{Loc.StartupError} :\n\n{ex.Message}",
                "AUDIOBL", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    // Called by SettingsWindow after language change
    public void RefreshLocalization() => RefreshMenuStrings();

    private void RefreshMenuStrings()
    {
        if (_trayIcon?.ContextMenu == null) return;
        var items = _trayIcon.ContextMenu.Items;
        ((MenuItem)items[0]).Header = Loc.MenuShowHide;
        ((MenuItem)items[1]).Header = Loc.MenuRecenter;
        ((MenuItem)items[3]).Header = Loc.MenuSettings;
        ((MenuItem)items[5]).Header = Loc.MenuQuit;
    }

    private static void ApplyLanguage(string code)
    {
        Loc.Set(code switch
        {
            "fr"   => AppLanguage.French,
            "en"   => AppLanguage.English,
            _      => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr"
                          ? AppLanguage.French : AppLanguage.English
        });
    }

    private void OnBatteryLevelChanged(int? level)
    {
        Dispatcher.Invoke(() =>
        {
            if (level.HasValue)
            {
                _lastBatteryLevel = level;
                _lastBatteryTime  = DateTime.Now;
                if (_settingsService != null)
                {
                    UpdateLowBatteryState(level.Value);
                    _settingsService.Settings.LastBatteryLevel     = level;
                    _settingsService.Settings.LastBatteryTimestamp = _lastBatteryTime;
                    _settingsService.Save();
                }
            }
            RenderBattery();
            if (_trayIcon != null)
                _trayIcon.ToolTipText = level.HasValue ? Loc.TooltipConnected : Loc.TooltipDisconnected;
        });
    }

    private void OnDeviceConnectionChanged(bool connected)
    {
        Dispatcher.Invoke(() =>
        {
            // Keep the last known value on screen; only refresh the age colour and tooltip.
            RenderBattery();
            if (!connected && _trayIcon != null)
                _trayIcon.ToolTipText = Loc.TooltipDisconnected;
        });
    }

    private void RenderBattery()
    {
        _overlay?.UpdateBattery(_lastBatteryLevel, _lastBatteryTime);
        UpdateTrayIcon(_lastBatteryLevel, _lastBatteryTime);
    }

    // Shows the low-battery warning at most once per discharge cycle. The flag is
    // persisted (settings) and only cleared once the headset is recharged to >= 50%.
    private void UpdateLowBatteryState(int level)
    {
        if (_settingsService == null) return;

        if (level >= RechargedResetThreshold)
        {
            _settingsService.Settings.LowBatteryWarned = false;
        }
        else if (level <= LowBatteryThreshold && !_settingsService.Settings.LowBatteryWarned)
        {
            _settingsService.Settings.LowBatteryWarned = true;
            ShowLowBatteryWarning(level);
        }
    }

    private void ShowLowBatteryWarning(int level)
    {
        if (_lowBatteryWindow is { IsVisible: true })
        {
            _lowBatteryWindow.Activate();
            return;
        }
        _lowBatteryWindow = new LowBatteryWindow(level);
        _lowBatteryWindow.Closed += (_, _) => _lowBatteryWindow = null;
        _lowBatteryWindow.Show();
        _lowBatteryWindow.Activate();
    }

    private void UpdateTrayIcon(int? level, DateTime? timestamp)
    {
        if (_trayIcon == null) return;

        using var bmp = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.Transparent);
            string text = level.HasValue ? $"{level}%" : "--";
            float fontSize = text.Length > 3 ? 9f : 11f;
            using var font  = new System.Drawing.Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold);
            var textColor = Helpers.BatteryAge.Evaluate(timestamp) switch
            {
                Helpers.BatteryFreshness.Stale6h => System.Drawing.Color.OrangeRed,
                Helpers.BatteryFreshness.Stale1h => System.Drawing.Color.FromArgb(255, 200, 0),
                _                                => System.Drawing.Color.White
            };
            using var brush = new System.Drawing.SolidBrush(textColor);
            var sf = new System.Drawing.StringFormat
            {
                Alignment     = System.Drawing.StringAlignment.Center,
                LineAlignment = System.Drawing.StringAlignment.Center
            };
            g.DrawString(text, font, brush, new System.Drawing.RectangleF(0, 0, 32, 32), sf);
        }

        nint newHIcon = bmp.GetHicon();
        _trayIcon.Icon = System.Drawing.Icon.FromHandle(newHIcon);
        if (_hTrayIcon != nint.Zero) DestroyIcon(_hTrayIcon);
        _hTrayIcon = newHIcon;
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => ToggleOverlay();
    private void MenuToggleOverlay_Click(object sender, RoutedEventArgs e)       => ToggleOverlay();
    private void MenuRecenterOverlay_Click(object sender, RoutedEventArgs e)     => _overlay?.RecenterAndShow();

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow == null || !_settingsWindow.IsVisible)
        {
            _settingsWindow = new SettingsWindow(_settingsService!);
            _settingsWindow.Show();
        }
        else _settingsWindow.Activate();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        _bluetoothService?.Dispose();
        _trayIcon?.Dispose();
        if (_hTrayIcon != nint.Zero) DestroyIcon(_hTrayIcon);
        _mutex?.ReleaseMutex();
        Shutdown();
    }

    private void ToggleOverlay()
    {
        if (_overlay == null) return;
        if (_overlay.IsVisible) _overlay.Hide(); else _overlay.Show();
    }

    private static string GetCurrentVersion()
    {
        try
        {
            var v = global::Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch { return "0.0.0"; }
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            var update = await UpdateService.CheckAsync(GetCurrentVersion());
            if (update == null) return;

            Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    $"A new version is available: {update.Version}\n\nInstall now?",
                    "AUDIOBL — Update available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    _ = UpdateService.DownloadAndInstallAsync(update.DownloadUrl);
            });
        }
        catch { }
    }

    internal static void WriteLog(string message) { /* logging disabled */ }
}
