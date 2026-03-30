using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AUDIOBL.Services;

public static class UpdateService
{
    private const string ApiUrl = "https://api.github.com/repos/remisarrailh/AUDIOBL/releases/latest";

    public record UpdateInfo(string Version, string DownloadUrl);

    public static async Task<UpdateInfo?> CheckAsync(string currentVersion)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AUDIOBL");

            var release = await http.GetFromJsonAsync<GithubRelease>(ApiUrl);
            if (release == null) return null;

            string latest = release.TagName.TrimStart('v');
            if (!IsNewer(latest, currentVersion)) return null;

            var asset = release.Assets?.FirstOrDefault(
                a => a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));
            if (asset == null) return null;

            return new UpdateInfo(release.TagName, asset.BrowserDownloadUrl);
        }
        catch { return null; }
    }

    public static async Task DownloadAndInstallAsync(string downloadUrl)
    {
        string dest = Path.Combine(Path.GetTempPath(), "AUDIOBL-update.msix");
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AUDIOBL");

        var bytes = await http.GetByteArrayAsync(downloadUrl);
        await File.WriteAllBytesAsync(dest, bytes);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = dest,
            UseShellExecute = true
        });
    }

    private static bool IsNewer(string latest, string current)
        => Version.TryParse(latest, out var l)
        && Version.TryParse(current, out var c)
        && l > c;

    private class GithubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("assets")]   public List<Asset>? Assets { get; set; }
    }

    private class Asset
    {
        [JsonPropertyName("name")]                 public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
    }
}
