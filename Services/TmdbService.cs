using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Moviebase.Services;

public class TmdbService
{
    private static readonly HttpClient Http = new();
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string PosterBaseUrl = "https://image.tmdb.org/t/p/";

    private readonly string _apiKey;

    public TmdbService(string apiKey) => _apiKey = apiKey;

    public async Task<TmdbMovieResult?> SearchAndGetFirstAsync(string title, int year = 0, CancellationToken ct = default)
    {
        var query = Uri.EscapeDataString(title);
        var url = $"{BaseUrl}/search/movie?api_key={_apiKey}&query={query}&include_adult=false";
        if (year > 0) url += $"&year={year}";

        var response = await Http.GetFromJsonAsync<SearchResponse>(url, ct);
        if (response?.Results is not { Count: > 0 }) return null;

        return await GetByIdAsync(response.Results[0].Id, ct);
    }

    public async Task<TmdbMovieResult?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/movie/{id}?api_key={_apiKey}&append_to_response=alternative_titles";
        var details = await Http.GetFromJsonAsync<MovieDetails>(url, ct);
        if (details is null) return null;

        var altNames = new List<string> { details.Title };
        if (details.AlternativeTitles?.Titles is { } titles)
            altNames.AddRange(titles.Select(t => t.Title));

        return new TmdbMovieResult
        {
            TmdbId = details.Id,
            Title = details.Title,
            Year = DateTime.TryParse(details.ReleaseDate, out var d) ? d.Year : 0,
            Genre = string.Join(", ", details.Genres?.Select(g => g.Name) ?? []),
            ImdbId = details.ImdbId ?? "",
            Plot = details.Overview ?? "",
            PosterPath = details.PosterPath ?? "",
            AlternativeNames = altNames.ToArray(),
            CollectionName = details.BelongsToCollection?.Name
        };
    }

    public string GetPosterUrl(string posterPath, string size = "w342")
        => string.IsNullOrEmpty(posterPath) ? "" : $"{PosterBaseUrl}{size}{posterPath}";

    public async Task DownloadPosterAsync(string posterPath, string outputFile, CancellationToken ct = default)
    {
        var url = GetPosterUrl(posterPath, "original");
        if (string.IsNullOrEmpty(url)) return;

        var response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var fs = File.Create(outputFile);
        await response.Content.CopyToAsync(fs, ct);
    }

    // JSON DTOs
    private record SearchResponse([property: JsonPropertyName("results")] List<SearchResult> Results);
    private record SearchResult([property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("release_date")] string ReleaseDate,
        [property: JsonPropertyName("poster_path")] string? PosterPath);
    private record MovieDetails
    {
        [JsonPropertyName("id")] public int Id { get; init; }
        [JsonPropertyName("title")] public string Title { get; init; } = "";
        [JsonPropertyName("release_date")] public string ReleaseDate { get; init; } = "";
        [JsonPropertyName("overview")] public string Overview { get; init; } = "";
        [JsonPropertyName("poster_path")] public string? PosterPath { get; init; }
        [JsonPropertyName("imdb_id")] public string? ImdbId { get; init; }
        [JsonPropertyName("genres")] public List<GenreDto>? Genres { get; init; }
        [JsonPropertyName("alternative_titles")] public AltTitlesContainer? AlternativeTitles { get; init; }
        [JsonPropertyName("belongs_to_collection")] public CollectionRef? BelongsToCollection { get; init; }
    }
    private record GenreDto([property: JsonPropertyName("name")] string Name);
    private record AltTitlesContainer([property: JsonPropertyName("titles")] List<AltTitle> Titles);
    private record AltTitle([property: JsonPropertyName("title")] string Title);
    private record CollectionRef(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name);
    private record CollectionDetails(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("parts")] List<CollectionPart> Parts);
    private record CollectionPart(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("release_date")] string ReleaseDate);

    /// <summary>Search TMDB and return multiple results for manual selection.</summary>
    public async Task<List<TmdbSearchHit>> SearchAsync(string query, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(query)}&include_adult=false";
        var response = await Http.GetFromJsonAsync<SearchResponse>(url, ct);
        if (response?.Results is null) return [];

        return response.Results.Select(r => new TmdbSearchHit
        {
            Id = r.Id,
            Title = r.Title,
            Year = DateTime.TryParse(r.ReleaseDate, out var d) ? d.Year : 0,
            PosterPath = r.PosterPath ?? ""
        }).ToList();
    }

    /// <summary>Get the TMDB collection (franchise) a movie belongs to.</summary>
    public async Task<TmdbCollection?> GetCollectionAsync(int movieId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/movie/{movieId}?api_key={_apiKey}";
        var details = await Http.GetFromJsonAsync<MovieDetails>(url, ct);
        if (details?.BelongsToCollection is null) return null;

        var colUrl = $"{BaseUrl}/collection/{details.BelongsToCollection.Id}?api_key={_apiKey}";
        var collection = await Http.GetFromJsonAsync<CollectionDetails>(colUrl, ct);
        if (collection is null) return null;

        return new TmdbCollection
        {
            Id = collection.Id,
            Name = collection.Name,
            Parts = collection.Parts.Select(p => new TmdbCollectionPart
            {
                Id = p.Id,
                Title = p.Title,
                Year = DateTime.TryParse(p.ReleaseDate, out var d) ? d.Year : 0
            }).OrderBy(p => p.Year).ToList()
        };
    }
}

public class TmdbMovieResult
{
    public int TmdbId { get; set; }
    public string Title { get; set; } = "";
    public int Year { get; set; }
    public string Genre { get; set; } = "";
    public string ImdbId { get; set; } = "";
    public string Plot { get; set; } = "";
    public string PosterPath { get; set; } = "";
    public string[] AlternativeNames { get; set; } = [];
    public string? CollectionName { get; set; }
}

public class TmdbSearchHit
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int Year { get; set; }
    public string PosterPath { get; set; } = "";
}

public class TmdbCollection
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<TmdbCollectionPart> Parts { get; set; } = [];
}

public class TmdbCollectionPart
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int Year { get; set; }
}
