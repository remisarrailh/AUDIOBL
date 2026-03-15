using System.IO;
using System.Text.Json;
using AUDIOBL.Models;
using Microsoft.Win32;

namespace AUDIOBL.Services;

public class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AUDIOBL");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");
    private const string AutoStartKey = "AUDIOBL";

    public AppSettings Settings { get; private set; }

    public SettingsService()
    {
        Settings = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            ApplyAutoStart();
        }
        catch { }
    }

    private void ApplyAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;

            if (Settings.AutoStart)
                key.SetValue(AutoStartKey, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(AutoStartKey, throwOnMissingValue: false);
        }
        catch { }
    }
}
