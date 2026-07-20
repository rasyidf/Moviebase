using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Moviebase.Models;

public class AppSettings
{
    public string TmdbApiKeyEncrypted { get; set; } = ""; // DPAPI-protected, base64
    public string MovieExtensions { get; set; } = ".mp4;.mkv;.avi;.m4v";
    public string FileRenamePattern { get; set; } = "{Title} ({Year})";
    public string FolderRenamePattern { get; set; } = "{Title} ({Year})";
    public bool SwapThe { get; set; } = true;
    public string LastDirectory { get; set; } = "";
    public string LibraryRoot { get; set; } = "";
    public string DefaultImportMode { get; set; } = "Symlink";

    // ponytail: DPAPI CurrentUser scope — encrypted value is only readable by this Windows user
    public string TmdbApiKey
    {
        get => Unprotect(TmdbApiKeyEncrypted);
        set => TmdbApiKeyEncrypted = Protect(value);
    }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Moviebase", "settings.json");

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
        var dir = Path.GetDirectoryName(SettingsPath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    private static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string Unprotect(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return "";
        try
        {
            var encrypted = Convert.FromBase64String(base64);
            var bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // If decryption fails (e.g. old plain-text value), return as-is for migration
            return base64;
        }
    }
}
