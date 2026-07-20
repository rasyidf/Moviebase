namespace Moviebase.Models;

/// <summary>
/// Display DTO for the movie list. Supports poster thumbnail and series grouping.
/// </summary>
public class MovieEntryDisplay
{
    public string Title { get; set; } = "";
    public string Year { get; set; } = "";
    public string Size { get; set; } = "";
    public string Status { get; set; } = "";
    public string Folder { get; set; } = "";
    public string PosterUrl { get; set; } = ""; // w92 thumbnail or empty
    public string SeriesName { get; set; } = "";
    public bool IsGroupHeader { get; set; } // true = this is a series header, not a movie
}
