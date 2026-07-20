using System.Text;
using System.Text.Json;
using Moviebase.Models;

namespace Moviebase.Services;

/// <summary>
/// Exports movie library to CSV or JSON.
/// </summary>
public static class ExportService
{
    public static void ExportCsv(string outputPath, IEnumerable<MovieEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Title,Year,Genre,IMDB,Resolution,Source,Codec,Size,Subtitles,Series,File");

        foreach (var m in entries)
        {
            sb.AppendLine(string.Join(",",
                Escape(m.Title),
                m.Year,
                Escape(m.Genre),
                Escape(m.ImdbId),
                Escape(m.ScreenSize),
                Escape(m.Source),
                Escape(m.VideoCodec),
                Escape(m.Size),
                m.Subtitles.Count,
                Escape(m.SeriesName),
                Escape(m.FileName)));
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportJson(string outputPath, IEnumerable<MovieEntry> entries)
    {
        var export = entries.Select(m => new
        {
            m.Title,
            m.Year,
            m.Genre,
            m.ImdbId,
            m.TmdbId,
            m.Plot,
            m.ScreenSize,
            m.Source,
            m.VideoCodec,
            m.AudioCodec,
            m.Edition,
            m.ReleaseGroup,
            m.Size,
            m.SeriesName,
            m.Subtitles,
            m.FileName,
            m.FullPath
        });

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
