using System.Text.Json;
using Moviebase.Models;

namespace Moviebase.Services;

/// <summary>
/// Saves and loads scan results to .moviebase.json in the scanned folder.
/// Enables instant reload without re-scanning.
/// </summary>
public static class PersistenceService
{
    private const string FileName = ".moviebase.json";

    public static void Save(string folderPath, IEnumerable<MovieEntry> entries)
    {
        var path = Path.Combine(folderPath, FileName);
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(path, json);
    }

    public static List<MovieEntry>? Load(string folderPath)
    {
        var path = Path.Combine(folderPath, FileName);
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<MovieEntry>>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch
        {
            return null; // corrupt file, will rescan
        }
    }

    public static bool HasSavedData(string folderPath)
        => File.Exists(Path.Combine(folderPath, FileName));
}
