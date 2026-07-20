using Moviebase.Models;
using Terka;

namespace Moviebase.Services;

/// <summary>
/// Scans a root directory for movie files using Terka for filename analysis.
/// Supports flat (one movie per folder) and nested (series folder) layouts.
/// </summary>
public static class MovieScanner
{
    public static List<MovieEntry> Scan(string rootPath, string extensions)
    {
        var exts = new HashSet<string>(
            extensions.Split(';', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);

        var entries = new List<MovieEntry>();

        foreach (var dir in Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.StartsWith('[') && dirName.EndsWith(']')) continue;

            // Check flat: movie file directly in this folder
            var movieFile = FindFirstMovie(dir, exts);
            if (movieFile is not null)
            {
                entries.Add(AnalyzeEntry(movieFile, seriesName: null));
                continue;
            }

            // Check nested: series folder containing sub-folders with movies
            var subDirs = Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly).ToList();
            if (subDirs.Count == 0) continue;

            foreach (var subDir in subDirs)
            {
                var subDirName = Path.GetFileName(subDir);
                if (subDirName.StartsWith('[') && subDirName.EndsWith(']')) continue;

                var subMovie = FindFirstMovie(subDir, exts);
                if (subMovie is null) continue;

                entries.Add(AnalyzeEntry(subMovie, seriesName: dirName));
            }
        }

        // Sort: standalone movies alphabetically, then series grouped by name + year
        entries.Sort((a, b) =>
        {
            var aKey = a.IsInSeries ? $"Z_{a.SeriesName}_{a.Year:D4}" : $"A_{a.Title}";
            var bKey = b.IsInSeries ? $"Z_{b.SeriesName}_{b.Year:D4}" : $"A_{b.Title}";
            return string.Compare(aKey, bKey, StringComparison.OrdinalIgnoreCase);
        });

        return entries;
    }

    private static readonly HashSet<string> SubtitleExts = new(StringComparer.OrdinalIgnoreCase)
        { ".srt", ".ass", ".ssa", ".sub", ".idx", ".vtt" };

    private static MovieEntry AnalyzeEntry(string movieFile, string? seriesName)
    {
        var entry = new MovieEntry();
        entry.SetFileInfo(movieFile);
        entry.SeriesName = seriesName ?? "";

        // Use Terka to analyze the filename
        var guess = GuessIt.Guess(Path.GetFileName(movieFile));

        entry.Title = guess.Title ?? Path.GetFileNameWithoutExtension(movieFile);
        entry.Year = guess.Year ?? 0;
        entry.ScreenSize = guess.ScreenSize ?? "";
        entry.Source = guess.Source ?? "";
        entry.VideoCodec = guess.VideoCodec ?? "";
        entry.AudioCodec = guess.AudioCodec ?? "";
        entry.ReleaseGroup = guess.ReleaseGroup ?? "";
        entry.Edition = guess.Edition.Count > 0 ? string.Join(", ", guess.Edition) : "";

        // ponytail: if Terka couldn't get title from filename, try folder name
        if (string.IsNullOrWhiteSpace(entry.Title) || entry.Title == Path.GetFileNameWithoutExtension(movieFile))
        {
            var folderName = Path.GetFileName(Path.GetDirectoryName(movieFile) ?? "");
            var folderGuess = GuessIt.Guess(folderName + ".mkv"); // add fake extension for tokenizer
            if (!string.IsNullOrWhiteSpace(folderGuess.Title))
            {
                entry.Title = folderGuess.Title;
                if (entry.Year == 0 && folderGuess.Year.HasValue)
                    entry.Year = folderGuess.Year.Value;
            }
        }

        // Detect subtitles in the same directory
        var dir = Path.GetDirectoryName(movieFile);
        if (dir is not null)
        {
            entry.Subtitles = Directory.EnumerateFiles(dir)
                .Where(f => SubtitleExts.Contains(Path.GetExtension(f)))
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Cast<string>()
                .ToList();
        }

        return entry;
    }

    private static string? FindFirstMovie(string dir, HashSet<string> exts)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f => exts.Contains(Path.GetExtension(f)));
        }
        catch { return null; }
    }
}
