using System.Text;
using Moviebase.Models;

namespace Moviebase.Services;

/// <summary>
/// Generates Kodi/Jellyfin/Plex compatible .nfo files for movies.
/// Writes movie.nfo next to the movie file.
/// </summary>
public static class NfoService
{
    public static (int written, int skipped) GenerateAll(IEnumerable<MovieEntry> entries, bool overwrite = false)
    {
        int written = 0, skipped = 0;

        foreach (var entry in entries.Where(e => e.IsFetched))
        {
            var dir = Path.GetDirectoryName(entry.FullPath);
            if (dir is null) { skipped++; continue; }

            var nfoPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(entry.FullPath) + ".nfo");
            if (!overwrite && File.Exists(nfoPath)) { skipped++; continue; }

            File.WriteAllText(nfoPath, BuildNfo(entry), Encoding.UTF8);
            written++;
        }

        return (written, skipped);
    }

    private static string BuildNfo(MovieEntry m)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.AppendLine("<movie>");
        sb.AppendLine($"  <title>{Xml(m.Title)}</title>");
        sb.AppendLine($"  <year>{m.Year}</year>");
        sb.AppendLine($"  <plot>{Xml(m.Plot)}</plot>");

        if (!string.IsNullOrEmpty(m.ImdbId))
            sb.AppendLine($"  <uniqueid type=\"imdb\" default=\"true\">{Xml(m.ImdbId)}</uniqueid>");

        if (m.TmdbId > 0)
            sb.AppendLine($"  <uniqueid type=\"tmdb\">{m.TmdbId}</uniqueid>");

        // Genres as separate tags
        foreach (var genre in m.Genre.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            sb.AppendLine($"  <genre>{Xml(genre)}</genre>");

        // Poster
        if (!string.IsNullOrEmpty(m.PosterPath))
            sb.AppendLine($"  <thumb aspect=\"poster\">https://image.tmdb.org/t/p/original{m.PosterPath}</thumb>");

        // File info
        if (!string.IsNullOrEmpty(m.VideoCodec) || !string.IsNullOrEmpty(m.AudioCodec))
        {
            sb.AppendLine("  <fileinfo>");
            sb.AppendLine("    <streamdetails>");
            if (!string.IsNullOrEmpty(m.VideoCodec))
            {
                sb.AppendLine("      <video>");
                sb.AppendLine($"        <codec>{Xml(m.VideoCodec)}</codec>");
                if (!string.IsNullOrEmpty(m.ScreenSize))
                    sb.AppendLine($"        <width>{ParseWidth(m.ScreenSize)}</width>");
                sb.AppendLine("      </video>");
            }
            if (!string.IsNullOrEmpty(m.AudioCodec))
            {
                sb.AppendLine("      <audio>");
                sb.AppendLine($"        <codec>{Xml(m.AudioCodec)}</codec>");
                sb.AppendLine("      </audio>");
            }
            sb.AppendLine("    </streamdetails>");
            sb.AppendLine("  </fileinfo>");
        }

        sb.AppendLine("</movie>");
        return sb.ToString();
    }

    private static string Xml(string value)
        => System.Security.SecurityElement.Escape(value ?? "") ?? "";

    private static int ParseWidth(string screenSize)
    {
        // ponytail: crude width from common resolutions
        return screenSize switch
        {
            "2160p" => 3840,
            "1080p" => 1920,
            "720p" => 1280,
            "480p" => 854,
            _ => 0
        };
    }
}
