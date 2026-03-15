using System.Windows;
using System.Windows.Controls;
using AUDIOBL.Helpers;
using AUDIOBL.Services;

namespace AUDIOBL.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private bool _loading = true;

    public SettingsWindow(SettingsService settings)
    {
        _settings = settings;
        InitializeComponent();
        ApplyStrings();
        LoadValues();
        _loading = false;
    }

    private void ApplyStrings()
    {
        Title = Loc.SettingsTitle;
        LangLabel.Content  = Loc.SettingsLanguage;
        DeviceLabel.Content = Loc.SettingsDeviceName;
        AutoStartCheck.Content = Loc.SettingsAutoStart;
        LangAutoRadio.Content = Loc.LangAuto;
        LangFrRadio.Content   = Loc.LangFrench;
        LangEnRadio.Content   = Loc.LangEnglish;
    }

    private void LoadValues()
    {
        LangAutoRadio.IsChecked = _settings.Settings.Language == "auto" || _settings.Settings.Language == "";
        LangFrRadio.IsChecked   = _settings.Settings.Language == "fr";
        LangEnRadio.IsChecked   = _settings.Settings.Language == "en";
        DeviceBox.Text          = _settings.Settings.DeviceName;
        AutoStartCheck.IsChecked = _settings.Settings.AutoStart;
    }

    private void LangRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        string tag = LangFrRadio.IsChecked == true ? "fr"
                   : LangEnRadio.IsChecked == true ? "en"
                   : "auto";
        _settings.Settings.Language = tag;
        _settings.Save();

        Loc.Set(tag switch
        {
            "fr"  => AppLanguage.French,
            "en"  => AppLanguage.English,
            _     => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr"
                         ? AppLanguage.French : AppLanguage.English
        });
        ApplyStrings();
        (App.Current as App)?.RefreshLocalization();
    }

    private void DeviceBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Settings.DeviceName = DeviceBox.Text.Trim();
        _settings.Save();
    }

    private void AutoStartCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Settings.AutoStart = AutoStartCheck.IsChecked == true;
        _settings.Save();
    }
}
