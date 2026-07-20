using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Moviebase.Models;
using Moviebase.Services;

namespace Moviebase.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    public MainViewModel()
    {
        _settings = AppSettings.Load();
        Movies = new ObservableCollection<MovieEntry>();
        StatusText = "Ready";
        CurrentPath = "";
        SearchText = "";
    }

    // --- Properties ---

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchMetadataCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadPostersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCsvCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportJsonCommand))]
    public partial bool HasMovies { get; set; }

    [ObservableProperty] public partial string StatusText { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial MovieEntry? SelectedMovie { get; set; }
    [ObservableProperty] public partial bool ShowSettings { get; set; }
    [ObservableProperty] public partial string CurrentPath { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; }

    public ObservableCollection<MovieEntry> Movies { get; }

    /// <summary>Returns movies filtered by SearchText.</summary>
    public IEnumerable<MovieEntry> FilteredMovies
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return Movies;
            var q = SearchText.Trim();
            return Movies.Where(m =>
                m.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.Genre.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.SeriesName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
    }

    // Settings pass-through
    public string TmdbApiKey { get => _settings.TmdbApiKey; set { _settings.TmdbApiKey = value; _settings.Save(); } }
    public string MovieExtensions { get => _settings.MovieExtensions; set { _settings.MovieExtensions = value; _settings.Save(); } }
    public string FileRenamePattern { get => _settings.FileRenamePattern; set { _settings.FileRenamePattern = value; _settings.Save(); } }
    public string FolderRenamePattern { get => _settings.FolderRenamePattern; set { _settings.FolderRenamePattern = value; _settings.Save(); } }
    public bool SwapThe { get => _settings.SwapThe; set { _settings.SwapThe = value; _settings.Save(); } }

    public AppSettings GetSettings() => _settings;

    // --- Commands ---

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        _settings.LastDirectory = folder.Path;
        _settings.Save();
        CurrentPath = folder.Path;

        IsBusy = true;
        StatusText = "Loading...";
        Movies.Clear();

        // Try loading persisted data first
        var cached = await Task.Run(() => PersistenceService.Load(folder.Path));
        if (cached is not null)
        {
            foreach (var e in cached) Movies.Add(e);
            StatusText = $"Loaded {Movies.Count} movies (cached)";
        }
        else
        {
            StatusText = "Scanning...";
            var entries = await Task.Run(() => MovieScanner.Scan(folder.Path, _settings.MovieExtensions));
            foreach (var e in entries) Movies.Add(e);
            StatusText = $"Found {Movies.Count} movies";
        }

        DetectDuplicates();
        HasMovies = Movies.Count > 0;
        IsBusy = false;
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
                }
            }
            catch { /* skip failures */ }

            done++;
            Progress = (double)done / unfetched.Count * 100;
        }

        // Auto-save after fetch
        if (!string.IsNullOrEmpty(CurrentPath))
            await Task.Run(() => PersistenceService.Save(CurrentPath, Movies));

        StatusText = $"Fetched metadata for {done} movies (saved)";
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

        StatusText = $"Renamed {renamed} movies ({errors} errors)";
        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task DownloadPostersAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.TmdbApiKey))
        {
            StatusText = "Set TMDB API key in settings first";
            return;
        }

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

    // --- NFO generation ---

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task GenerateNfoAsync()
    {
        IsBusy = true;
        StatusText = "Generating NFO files...";
        var (written, skipped) = await Task.Run(() => NfoService.GenerateAll(Movies));
        StatusText = $"Generated {written} NFO files ({skipped} skipped)";
        IsBusy = false;
    }

    // --- Folder thumbnails ---

    [RelayCommand(CanExecute = nameof(HasMovies))]
    private async Task SetFolderThumbnailsAsync()
    {
        IsBusy = true;
        StatusText = "Setting folder thumbnails...";
        var (set, skipped) = await Task.Run(() => ThumbnailService.SetAll(Movies));
        StatusText = $"Set {set} folder thumbnails ({skipped} skipped)";
        IsBusy = false;
    }

    // --- Watch status ---

    public void ToggleWatched(MovieEntry movie)
    {
        movie.IsWatched = !movie.IsWatched;
        // Auto-save
        if (!string.IsNullOrEmpty(CurrentPath))
            Task.Run(() => PersistenceService.Save(CurrentPath, Movies));
    }

    // --- Drag-drop scan ---

    public async Task ScanPathAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        _settings.LastDirectory = folderPath;
        _settings.Save();
        CurrentPath = folderPath;

        IsBusy = true;
        StatusText = "Loading...";
        Movies.Clear();

        var cached = await Task.Run(() => PersistenceService.Load(folderPath));
        if (cached is not null)
        {
            foreach (var e in cached) Movies.Add(e);
            StatusText = $"Loaded {Movies.Count} movies (cached)";
        }
        else
        {
            StatusText = "Scanning...";
            var entries = await Task.Run(() => MovieScanner.Scan(folderPath, _settings.MovieExtensions));
            foreach (var e in entries) Movies.Add(e);
            StatusText = $"Found {Movies.Count} movies";
        }

        // Detect duplicates
        DetectDuplicates();

        HasMovies = Movies.Count > 0;
        IsBusy = false;
    }

    // --- Duplicate detection ---

    private void DetectDuplicates()
    {
        // ponytail: O(n) group-by title+year, flag entries that appear more than once
        var groups = Movies
            .GroupBy(m => $"{m.Title.ToLowerInvariant()}|{m.Year}")
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
            foreach (var entry in group)
                entry.IsDuplicate = true;
    }
}
