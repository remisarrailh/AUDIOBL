namespace AUDIOBL.Models;

public class AppSettings
{
    public double OverlayLeft { get; set; } = 100;
    public double OverlayTop  { get; set; } = 100;
    public bool   AutoStart   { get; set; } = false;
    /// <summary>"auto", "fr", or "en"</summary>
    public string Language    { get; set; } = "auto";
    /// <summary>Bluetooth device name fragment to match. Empty = first connected device.</summary>
    public string DeviceName  { get; set; } = "";
    /// <summary>Last battery percentage read, kept to display when the device disconnects.</summary>
    public int?     LastBatteryLevel     { get; set; } = null;
    /// <summary>When LastBatteryLevel was read. Drives the age-based colour of the percentage.</summary>
    public DateTime? LastBatteryTimestamp { get; set; } = null;
    /// <summary>True once the low-battery warning has been shown; reset when the headset is recharged to >= 50%.</summary>
    public bool LowBatteryWarned { get; set; } = false;
}
