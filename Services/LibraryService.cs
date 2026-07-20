using System.Text.Json;
using Moviebase.Models;

namespace Moviebase.Services;

/// <summary>
/// Central library persistence. Stores all movies in a single JSON file in AppData.
/// Survives folder changes — movies stay in the library until explicitly removed.
/// </summary>
public class LibraryService
{
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Moviebase", "library.json");

    private LibraryData _data = new();

    public List<MovieEntry> Movies => _data.Movies;
    public List<string> WatchFolders => _data.WatchFolders;

    public void Load()
    {
        if (!File.Exists(LibraryPath))
        {
            _data = new LibraryData();
            return;
        }

        try
        {
            var json = File.ReadAllText(LibraryPath);
            _data = JsonSerializer.Deserialize<LibraryData>(json, JsonOpts) ?? new LibraryData();
        }
        catch
        {
            _data = new LibraryData();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(LibraryPath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_data, JsonOpts);
        File.WriteAllText(LibraryPath, json);
    }

    /// <summary>Add a watch folder and scan it for movies.</summary>
    public List<MovieEntry> AddFolder(string folderPath, string extensions)
    {
        if (!WatchFolders.Contains(folderPath, StringComparer.OrdinalIgnoreCase))
            WatchFolders.Add(folderPath);

        var scanned = MovieScanner.Scan(folderPath, extensions);

        // Merge: add new entries, skip already-known paths
        var existingPaths = new HashSet<string>(Movies.Select(m => m.FullPath), StringComparer.OrdinalIgnoreCase);
        var added = new List<MovieEntry>();

        foreach (var entry in scanned)
        {
            if (!existingPaths.Contains(entry.FullPath))
            {
                Movies.Add(entry);
                added.Add(entry);
            }
        }

        Save();
        return added;
    }

    /// <summary>Add a single movie file to the library.</summary>
    public MovieEntry? AddFile(string filePath)
    {
        if (Movies.Any(m => m.FullPath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            return null; // already in library

        var entry = new MovieEntry();
        entry.SetFileInfo(filePath);

        // Analyze with Terka
        var guess = Terka.GuessIt.Guess(Path.GetFileName(filePath));
        entry.Title = guess.Title ?? Path.GetFileNameWithoutExtension(filePath);
        entry.Year = guess.Year ?? 0;
        entry.ScreenSize = guess.ScreenSize ?? "";
        entry.Source = guess.Source ?? "";
        entry.VideoCodec = guess.VideoCodec ?? "";
        entry.AudioCodec = guess.AudioCodec ?? "";
        entry.ReleaseGroup = guess.ReleaseGroup ?? "";
        entry.Edition = guess.Edition.Count > 0 ? string.Join(", ", guess.Edition) : "";

        Movies.Add(entry);
        Save();
        return entry;
    }

    /// <summary>Move or symlink a file into the library root folder.</summary>
    public MovieEntry? ImportFile(string sourceFile, string libraryRoot, ImportMode mode)
    {
        var fileName = Path.GetFileName(sourceFile);
        // Create a folder for the movie
        var guess = Terka.GuessIt.Guess(fileName);
        var title = guess.Title ?? Path.GetFileNameWithoutExtension(sourceFile);
        var year = guess.Year ?? 0;
        var folderName = year > 0 ? $"{title} ({year})" : title;

        // Sanitize
        foreach (var c in Path.GetInvalidFileNameChars())
            folderName = folderName.Replace(c, '_');

        var destFolder = Path.Combine(libraryRoot, folderName);
        if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);

        var destFile = Path.Combine(destFolder, fileName);
        if (File.Exists(destFile)) return null; // already exists

        switch (mode)
        {
            case ImportMode.Move:
                File.Move(sourceFile, destFile);
                break;
            case ImportMode.Symlink:
                File.CreateSymbolicLink(destFile, sourceFile);
                break;
            case ImportMode.Copy:
                File.Copy(sourceFile, destFile);
                break;
        }

        return AddFile(destFile);
    }

    /// <summary>Remove a movie from the library (does NOT delete file).</summary>
    public void Remove(MovieEntry entry)
    {
        Movies.RemoveAll(m => m.FullPath.Equals(entry.FullPath, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    /// <summary>Rescan all watch folders and merge new movies.</summary>
    public int Rescan(string extensions)
    {
        int added = 0;
        foreach (var folder in WatchFolders.ToList())
        {
            if (!Directory.Exists(folder)) continue;
            added += AddFolder(folder, extensions).Count;
        }
        // Remove entries whose files no longer exist
        var removed = Movies.RemoveAll(m => !File.Exists(m.FullPath));
        if (removed > 0) Save();
        return added;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

public enum ImportMode
{
    Move,
    Symlink,
    Copy
}

public class LibraryData
{
    public List<MovieEntry> Movies { get; set; } = [];
    public List<string> WatchFolders { get; set; } = [];
}
