using System.Globalization;

namespace AUDIOBL.Helpers;

public enum AppLanguage { French, English }

public static class Loc
{
    public static AppLanguage Current { get; private set; } = AppLanguage.French;

    public static void Set(AppLanguage lang) => Current = lang;

    public static void SetFromSystem()
    {
        Current = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr"
            ? AppLanguage.French : AppLanguage.English;
    }

    private static string Fr(string fr, string en) => Current == AppLanguage.French ? fr : en;

    // Tray menu
    public static string MenuShowHide  => Fr("Afficher / Masquer",  "Show / Hide");
    public static string MenuRecenter  => Fr("Recentrer l'overlay", "Recenter overlay");
    public static string MenuSettings  => Fr("Paramètres",          "Settings");
    public static string MenuQuit      => Fr("Quitter",              "Quit");

    // Tray tooltip
    public static string TooltipConnected    => Fr("AUDIOBL – Connecté",   "AUDIOBL – Connected");
    public static string TooltipDisconnected => Fr("AUDIOBL – Déconnecté", "AUDIOBL – Disconnected");

    // Settings window
    public static string SettingsTitle      => Fr("AUDIOBL — Paramètres",         "AUDIOBL — Settings");
    public static string SettingsAutoStart  => Fr("Démarrer avec Windows",         "Start with Windows");
    public static string SettingsLanguage   => Fr("Langue",                        "Language");
    public static string SettingsDeviceName => Fr("Casque Bluetooth (vide = auto-détecté)", "Bluetooth headset (empty = auto-detect)");
    public static string LangAuto           => Fr("Automatique",                   "Automatic");
    public static string LangFrench         => "Français";
    public static string LangEnglish        => "English";

    // Low battery warning
    public static string LowBatteryTitle => Fr("Batterie faible", "Low battery");
    public static string LowBatteryClose => Fr("Fermer",          "Dismiss");
    public static string LowBatteryMessage(int level) => Fr(
        $"Le niveau de batterie de votre casque est à {level} %.\nPensez à le recharger pour ne pas tomber en panne.",
        $"Your headset battery is down to {level}%.\nConsider charging it before it runs out.");

    // Errors
    public static string StartupError => Fr("Erreur au démarrage", "Startup error");
}
