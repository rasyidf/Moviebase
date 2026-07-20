namespace Moviebase.Models;

public class MovieEntry
{
    public string FullPath { get; set; } = "";
    public string FileName => Path.GetFileName(FullPath);
    public string FolderName => Path.GetFileName(Path.GetDirectoryName(FullPath) ?? "");
    public string Title { get; set; } = "";
    public int Year { get; set; }
    public string Genre { get; set; } = "";
    public string ImdbId { get; set; } = "";
    public string Plot { get; set; } = "";
    public string PosterPath { get; set; } = "";
    public int TmdbId { get; set; } = -1;
    public string[] AlternativeNames { get; set; } = [];
    public string Size { get; set; } = "";
    public long SizeBytes { get; set; }
    public string SeriesName { get; set; } = "";

    // Terka-detected quality info
    public string ScreenSize { get; set; } = "";   // 1080p, 2160p, etc.
    public string Source { get; set; } = "";        // Blu-ray, Web, HDTV
    public string VideoCodec { get; set; } = "";    // H.264, H.265
    public string AudioCodec { get; set; } = "";    // DTS, AAC
    public string ReleaseGroup { get; set; } = "";
    public string Edition { get; set; } = "";       // Extended, Remastered

    // Subtitle info
    public List<string> Subtitles { get; set; } = []; // e.g. ["eng.srt", "ind.ass"]
    public bool HasSubtitles => Subtitles.Count > 0;

    public bool IsFetched => TmdbId > 0;
    public bool IsInSeries => !string.IsNullOrEmpty(SeriesName);

    // Watch status
    public bool IsWatched { get; set; }

    // Duplicate flag (set by ViewModel after scan)
    public bool IsDuplicate { get; set; }

    // TMDB collection/franchise name
    public string CollectionName { get; set; } = "";

    public string ThumbnailUrl => string.IsNullOrEmpty(PosterPath) ? "" : $"https://image.tmdb.org/t/p/w92{PosterPath}";

    /// <summary>Quality badge text for the list (e.g. "1080p • Blu-ray")</summary>
    public string QualityBadge
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(ScreenSize)) parts.Add(ScreenSize);
            if (!string.IsNullOrEmpty(Source)) parts.Add(Source);
            return string.Join(" · ", parts);
        }
    }

    public void SetFileInfo(string fullPath)
    {
        FullPath = fullPath;
        var fi = new FileInfo(fullPath);
        SizeBytes = fi.Length;
        Size = FormatBytes(fi.Length);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        if (bytes == 0) return "0 B";
        int i = (int)Math.Floor(Math.Log(bytes, 1024));
        return $"{bytes / Math.Pow(1024, i):0.#} {sizes[i]}";
    }
}
