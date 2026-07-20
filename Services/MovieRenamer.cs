using Moviebase.Models;

namespace Moviebase.Services;

public static class MovieRenamer
{
    public static (int renamed, int errors) RenameAll(
        IEnumerable<MovieEntry> entries, string filePattern, string folderPattern, bool swapThe)
    {
        int renamed = 0, errors = 0;

        foreach (var entry in entries.Where(e => e.IsFetched && !string.IsNullOrWhiteSpace(e.Title)))
        {
            try
            {
                RenameFile(entry, filePattern, swapThe);
                RenameFolder(entry, folderPattern, swapThe);
                renamed++;
            }
            catch
            {
                errors++;
            }
        }

        return (renamed, errors);
    }

    private static void RenameFile(MovieEntry entry, string pattern, bool swapThe)
    {
        var dir = Path.GetDirectoryName(entry.FullPath)!;
        var ext = Path.GetExtension(entry.FullPath);
        var newName = ApplyPattern(pattern, entry, swapThe) + ext;
        var newPath = Path.Combine(dir, SanitizeFileName(newName));

        if (!string.Equals(entry.FullPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(entry.FullPath, newPath);
            entry.FullPath = newPath;
        }
    }

    private static void RenameFolder(MovieEntry entry, string pattern, bool swapThe)
    {
        var dir = Path.GetDirectoryName(entry.FullPath)!;
        var parent = Path.GetDirectoryName(dir)!;
        var newFolderName = SanitizeFileName(ApplyPattern(pattern, entry, swapThe));
        var newDir = Path.Combine(parent, newFolderName);

        if (!string.Equals(dir, newDir, StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(dir, newDir);
            entry.FullPath = Path.Combine(newDir, Path.GetFileName(entry.FullPath));
        }
    }

    private static string ApplyPattern(string pattern, MovieEntry entry, bool swapThe)
    {
        var result = pattern
            .Replace("{Title}", entry.Title)
            .Replace("{Year}", entry.Year.ToString());

        if (swapThe && result.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
            result = result[4..] + ", The";

        return result;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
