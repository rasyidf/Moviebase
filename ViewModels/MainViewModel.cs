using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Moviebase.Models;
using Moviebase.Services;

namespace Moviebase.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly LibraryService _library = new();

    public MainViewModel()
    {
        _settings = AppSettings.Load();
        Movies = new ObservableCollection<MovieEntry>();
        StatusText = "Ready";
        CurrentPath = "";
        SearchText = "";

        // Load persisted library on startup
        _library.Load();
        foreach (var m in _library.Movies) Movies.Add(m);
        HasMovies = Movies.Count > 0;
        if (HasMovies) StatusText = FormatStatusBar();
    }

    /// <summary>Call from MainWindow after UI is ready to rescan watch folders.</summary>
    public async Task StartupRescanAsync()
    {
        if (_library.WatchFolders.Count == 0) return;

        var added = await Task.Run(() => _library.Rescan(_settings.MovieExtensions));
        if (added > 0)
        {
            // Sync new items to ObservableCollection
            Movies.Clear();
            foreach (var m in _library.Movies) Movies.Add(m);
            DetectDuplicates();
            HasMovies = Movies.Count > 0;
            StatusText = $"Added {added} new movies · {FormatStatusBar()}";
        }
    }

    // --- Properties ---

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchMetadataCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadPostersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCsvCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportJsonCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateNfoCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetFolderThumbnailsCommand))]
    [NotifyCanExecuteChangedFor(nameof(DetectCollectionsCommand))]
    public partial bool HasMovies { get; set; }

    [ObservableProperty] public partial string StatusText { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial MovieEntry? SelectedMovie { get; set; }
    [ObservableProperty] public partial string CurrentPath { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; }

    public ObservableCollection<MovieEntry> Movies { get; }

    public IEnumerable<MovieEntry> FilteredMovies
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return Movies;
            var q = SearchText.Trim();
            return Movies.Where(m =>
                m.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.Genre.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.SeriesName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.CollectionName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
    }

    // Settings
    public string TmdbApiKey { get => _settings.TmdbApiKey; set { _settings.TmdbApiKey = value; _settings.Save(); } }
    public string MovieExtensions { get => _settings.MovieExtensions; set { _settings.MovieExtensions = value; _settings.Save(); } }
    public string FileRenamePattern { get => _settings.FileRenamePattern; set { _settings.FileRenamePattern = value; _settings.Save(); } }
    public string FolderRenamePattern { get => _settings.FolderRenamePattern; set { _settings.FolderRenamePattern = value; _settings.Save(); } }
    public bool SwapThe { get => _settings.SwapThe; set { _settings.SwapThe = value; _settings.Save(); } }
    public string LibraryRoot { get => _settings.LibraryRoot; set { _settings.LibraryRoot = value; _settings.Save(); } }
    public string DefaultImportMode { get => _settings.DefaultImportMode; set { _settings.DefaultImportMode = value; _settings.Save(); } }
    public AppSettings GetSettings() => _settings;

    public List<string> GetWatchFolders() => _library.WatchFolders;

    public void UpdateWatchFolders(List<string> folders)
    {
        _library.WatchFolders.Clear();
        _library.WatchFolders.AddRange(folders);
        _library.Save();
    }

    // --- Commands ---

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        await ScanPathAsync(folder.Path);
    }

    public async Task ScanPathAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        CurrentPath = folderPath;
        IsBusy = true;
        StatusText = $"Scanning {Path.GetFileName(folderPath)}...";

        var added = await Task.Run(() => _library.AddFolder(folderPath, _settings.MovieExtensions));
        foreach (var e in added) Movies.Add(e);

        DetectDuplicates();
        HasMovies = Movies.Count > 0;
        StatusText = added.Count > 0
            ? $"Added {added.Count} movies ({Movies.Count} total)"
            : $"No new movies found ({Movies.Count} in library)";
        IsBusy = false;
    }

    /// <summary>Add individual movie files (drag-drop .mp4/.mkv)</summary>
    public async Task AddFilesAsync(IEnumerable<string> filePaths, string? libraryRoot = null, ImportMode mode = ImportMode.Symlink)
    {
        IsBusy = true;
        int added = 0;

        foreach (var file in filePaths)
        {
            var ext = Path.GetExtension(file);
            if (!_settings.MovieExtensions.Contains(ext, StringComparison.OrdinalIgnoreCase)) continue;

            MovieEntry? entry;
            if (libraryRoot is not null)
            {
                entry = await Task.Run(() => _library.ImportFile(file, libraryRoot, mode));
            }
            else
            {
                entry = await Task.Run(() => _library.AddFile(file));
            }

            if (entry is not null)
            {
                Movies.Add(entry);
                added++;
            }
        }

        DetectDuplicates();
        HasMovies = Movies.Count > 0;
        StatusText = $"Added {added} files ({Movies.Count} total)";
        IsBusy = false;
    }

    /// <summary>Scan common folders for importable movies.</summary>
    public List<string> ScanCommonFolders()
    {
        var exts = new HashSet<string>(
            _settings.MovieExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);

        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Movies"),
        };

        var existingPaths = new HashSet<string>(Movies.Select(m => m.FullPath), StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            try
            {
                var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .Where(f => exts.Contains(Path.GetExtension(f)) && !existingPaths.Contains(f));
                found.AddRange(files);
            }
            catch { /* access denied, etc. */ }
        }

        return found;
    }

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task FetchMetadataAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.TmdbApiKey))
        {
            StatusText = "Set TMDB API key in settings first";
            return;
        }

        IsBusy = true;
        var tmdb = new TmdbService(_settings.TmdbApiKey);
        var unfetched = Movies.Where(m => !m.IsFetched).ToList();
        int done = 0;

        foreach (var entry in unfetched)
        {
            try
            {
                StatusText = $"Fetching {done + 1}/{unfetched.Count}: {entry.Title}";
                var result = await tmdb.SearchAndGetFirstAsync(entry.Title, entry.Year);
                if (result is not null)
                {
                    entry.TmdbId = result.TmdbId;
                    entry.Title = result.Title;
                    entry.Year = result.Year;
                    entry.Genre = result.Genre;
                    entry.ImdbId = result.ImdbId;
                    entry.Plot = result.Plot;
                    entry.PosterPath = result.PosterPath;
                    entry.AlternativeNames = result.AlternativeNames;
                    entry.CollectionName = result.CollectionName ?? "";
                }
            }
            catch { /* skip */ }

            done++;
            Progress = (double)done / unfetched.Count * 100;
        }

        _library.Save();
        StatusText = $"Fetched metadata for {done} movies (saved)";
        Progress = 0;
        IsBusy = false;
    }

    /// <summary>Manual TMDB search for a specific movie.</summary>
    public async Task<List<TmdbSearchHit>> ManualSearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(_settings.TmdbApiKey)) return [];
        var tmdb = new TmdbService(_settings.TmdbApiKey);
        return await tmdb.SearchAsync(query);
    }

    /// <summary>Apply a manual search result to a movie entry.</summary>
    public async Task ApplySearchResultAsync(MovieEntry entry, int tmdbId)
    {
        var tmdb = new TmdbService(_settings.TmdbApiKey);
        var result = await tmdb.GetByIdAsync(tmdbId);
        if (result is null) return;

        entry.TmdbId = result.TmdbId;
        entry.Title = result.Title;
        entry.Year = result.Year;
        entry.Genre = result.Genre;
        entry.ImdbId = result.ImdbId;
        entry.Plot = result.Plot;
        entry.PosterPath = result.PosterPath;
        entry.AlternativeNames = result.AlternativeNames;
        entry.CollectionName = result.CollectionName ?? "";
        _library.Save();
    }

    /// <summary>Move selected movies into a series folder.</summary>
    public async Task MoveToSeriesAsync(IEnumerable<MovieEntry> movies, string seriesName)
    {
        await Task.Run(() =>
        {
            foreach (var movie in movies)
            {
                var currentDir = Path.GetDirectoryName(movie.FullPath)!;
                var parentDir = Path.GetDirectoryName(currentDir)!;
                var seriesDir = Path.Combine(parentDir, seriesName);

                if (!Directory.Exists(seriesDir)) Directory.CreateDirectory(seriesDir);

                var destDir = Path.Combine(seriesDir, Path.GetFileName(currentDir));
                if (Directory.Exists(destDir)) continue;

                Directory.Move(currentDir, destDir);
                movie.FullPath = Path.Combine(destDir, Path.GetFileName(movie.FullPath));
                movie.SeriesName = seriesName;
            }
        });

        _library.Save();
        StatusText = $"Moved {movies.Count()} movies to '{seriesName}'";
    }

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task DetectCollectionsAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.TmdbApiKey))
        {
            StatusText = "Set TMDB API key in settings first";
            return;
        }

        IsBusy = true;
        var tmdb = new TmdbService(_settings.TmdbApiKey);
        var fetched = Movies.Where(m => m.IsFetched && string.IsNullOrEmpty(m.CollectionName)).ToList();
        int found = 0;

        for (int i = 0; i < fetched.Count; i++)
        {
            var entry = fetched[i];
            StatusText = $"Checking collections {i + 1}/{fetched.Count}";
            try
            {
                var collection = await tmdb.GetCollectionAsync(entry.TmdbId);
                if (collection is not null)
                {
                    entry.CollectionName = collection.Name;
                    // Also tag other movies in the same collection
                    foreach (var part in collection.Parts)
                    {
                        var match = Movies.FirstOrDefault(m => m.TmdbId == part.Id);
                        if (match is not null) match.CollectionName = collection.Name;
                    }
                    found++;
                }
            }
            catch { /* skip */ }

            Progress = (double)(i + 1) / fetched.Count * 100;
            await Task.Delay(250); // ponytail: rate limit for TMDB
        }

        _library.Save();
        StatusText = $"Found {found} collections";
        Progress = 0;
        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task RenameAllAsync()
    {
        IsBusy = true;
        StatusText = "Renaming...";
        var (renamed, errors) = await Task.Run(() =>
            MovieRenamer.RenameAll(Movies, _settings.FileRenamePattern, _settings.FolderRenamePattern, _settings.SwapThe));
        _library.Save();
        StatusText = $"Renamed {renamed} movies ({errors} errors)";
        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task DownloadPostersAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.TmdbApiKey)) { StatusText = "Set TMDB API key first"; return; }
        IsBusy = true;
        var tmdb = new TmdbService(_settings.TmdbApiKey);
        var withPoster = Movies.Where(m => !string.IsNullOrEmpty(m.PosterPath)).ToList();
        int done = 0;

        foreach (var entry in withPoster)
        {
            try
            {
                var dir = Path.GetDirectoryName(entry.FullPath)!;
                var posterFile = Path.Combine(dir, "poster.jpg");
                if (File.Exists(posterFile)) { done++; continue; }
                StatusText = $"Downloading poster {done + 1}/{withPoster.Count}";
                await tmdb.DownloadPosterAsync(entry.PosterPath, posterFile);
            }
            catch { /* skip */ }
            done++;
            Progress = (double)done / withPoster.Count * 100;
        }

        StatusText = $"Downloaded {done} posters";
        Progress = 0;
        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task GenerateNfoAsync()
    {
        IsBusy = true;
        StatusText = "Generating NFO files...";
        var (written, skipped) = await Task.Run(() => NfoService.GenerateAll(Movies));
        StatusText = $"Generated {written} NFO files ({skipped} skipped)";
        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task SetFolderThumbnailsAsync()
    {
        IsBusy = true;
        StatusText = "Setting folder thumbnails...";
        var (set, skipped) = await Task.Run(() => ThumbnailService.SetAll(Movies));
        StatusText = $"Set {set} folder thumbnails ({skipped} skipped)";
        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task ExportCsvAsync()
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = "moviebase-export";
        picker.FileTypeChoices.Add("CSV", [".csv"]);
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await Task.Run(() => ExportService.ExportCsv(file.Path, Movies));
        StatusText = $"Exported {Movies.Count} movies to CSV";
    }

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task ExportJsonAsync()
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = "moviebase-export";
        picker.FileTypeChoices.Add("JSON", [".json"]);
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await Task.Run(() => ExportService.ExportJson(file.Path, Movies));
        StatusText = $"Exported {Movies.Count} movies to JSON";
    }

    public void ToggleWatched(MovieEntry movie)
    {
        movie.IsWatched = !movie.IsWatched;
        _library.Save();
    }

    public void RemoveMovie(MovieEntry movie)
    {
        _library.Remove(movie);
        Movies.Remove(movie);
        HasMovies = Movies.Count > 0;
        if (SelectedMovie == movie) SelectedMovie = null;
        StatusText = $"Removed — {Movies.Count} movies in library";
    }

    public int RescanWatchFolders()
    {
        var added = _library.Rescan(_settings.MovieExtensions);
        // Sync ObservableCollection with library
        var knownPaths = new HashSet<string>(Movies.Select(m => m.FullPath), StringComparer.OrdinalIgnoreCase);
        foreach (var m in _library.Movies)
        {
            if (!knownPaths.Contains(m.FullPath)) Movies.Add(m);
        }
        // Remove stale
        var libraryPaths = new HashSet<string>(_library.Movies.Select(m => m.FullPath), StringComparer.OrdinalIgnoreCase);
        for (int i = Movies.Count - 1; i >= 0; i--)
        {
            if (!libraryPaths.Contains(Movies[i].FullPath)) Movies.RemoveAt(i);
        }
        HasMovies = Movies.Count > 0;
        return added;
    }

    private void DetectDuplicates()
    {
        foreach (var m in Movies) m.IsDuplicate = false;
        var groups = Movies
            .GroupBy(m => $"{m.Title.ToLowerInvariant()}|{m.Year}")
            .Where(g => g.Count() > 1);
        foreach (var group in groups)
            foreach (var entry in group)
                entry.IsDuplicate = true;
    }

    /// <summary>Rich status bar text: count · size · watched</summary>
    public string FormatStatusBar()
    {
        var count = Movies.Count;
        var totalBytes = Movies.Sum(m => m.SizeBytes);
        var watched = Movies.Count(m => m.IsWatched);
        var sizeText = totalBytes >= 1L << 30
            ? $"{totalBytes / (1024.0 * 1024 * 1024):0.#} GB"
            : $"{totalBytes / (1024.0 * 1024):0.#} MB";
        return $"{count} movies · {sizeText} · {watched} watched";
    }
}
