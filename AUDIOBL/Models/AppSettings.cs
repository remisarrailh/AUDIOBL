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
}
