using System.Text.Json;

namespace Moviebase.Models;

public class AppSettings
{
    public string TmdbApiKey { get; set; } = "";
    public string MovieExtensions { get; set; } = ".mp4;.mkv;.avi;.m4v";
    public string FileRenamePattern { get; set; } = "{Title} ({Year})";
    public string FolderRenamePattern { get; set; } = "{Title} ({Year})";
    public bool SwapThe { get; set; } = true;
    public string LastDirectory { get; set; } = "";

    private static readonly string SettingsPath = Path.Combine(
        AppContext.BaseDirectory, "moviebase-settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
