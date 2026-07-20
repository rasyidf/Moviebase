using Moviebase.Models;

namespace Moviebase.Services;

/// <summary>
/// Sets folder thumbnails in Windows Explorer by writing desktop.ini + setting folder.jpg as icon.
/// </summary>
public static class ThumbnailService
{
    public static (int set, int skipped) SetAll(IEnumerable<MovieEntry> entries)
    {
        int set = 0, skipped = 0;

        foreach (var entry in entries)
        {
            var dir = Path.GetDirectoryName(entry.FullPath);
            if (dir is null) { skipped++; continue; }

            var posterFile = Path.Combine(dir, "poster.jpg");
            if (!File.Exists(posterFile)) { skipped++; continue; }

            // Copy poster as folder.jpg (Windows Explorer convention)
            var folderJpg = Path.Combine(dir, "folder.jpg");
            if (!File.Exists(folderJpg))
                File.Copy(posterFile, folderJpg);

            // Write desktop.ini
            var desktopIni = Path.Combine(dir, "desktop.ini");
            File.WriteAllText(desktopIni, """
                [.ShellClassInfo]
                IconResource=folder.jpg,0
                [ViewState]
                Mode=
                Vid=
                FolderType=Videos
                """);

            // Set system/hidden attributes
            File.SetAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);
            File.SetAttributes(folderJpg, FileAttributes.Hidden | FileAttributes.System);

            // Set folder as system (required for desktop.ini to take effect)
            var dirInfo = new DirectoryInfo(dir);
            dirInfo.Attributes |= FileAttributes.System;

            set++;
        }

        return (set, skipped);
    }
}
