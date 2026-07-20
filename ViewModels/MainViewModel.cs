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
    }

    // --- Properties ---

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchMetadataCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadPostersCommand))]
    public partial bool HasMovies { get; set; }

    [ObservableProperty] public partial string StatusText { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial MovieEntry? SelectedMovie { get; set; }
    [ObservableProperty] public partial bool ShowSettings { get; set; }
    [ObservableProperty] public partial string CurrentPath { get; set; }

    public ObservableCollection<MovieEntry> Movies { get; }

    // Settings pass-through
    public string TmdbApiKey { get => _settings.TmdbApiKey; set { _settings.TmdbApiKey = value; _settings.Save(); } }
    public string MovieExtensions { get => _settings.MovieExtensions; set { _settings.MovieExtensions = value; _settings.Save(); } }
    public string FileRenamePattern { get => _settings.FileRenamePattern; set { _settings.FileRenamePattern = value; _settings.Save(); } }
    public string FolderRenamePattern { get => _settings.FolderRenamePattern; set { _settings.FolderRenamePattern = value; _settings.Save(); } }
    public bool SwapThe { get => _settings.SwapThe; set { _settings.SwapThe = value; _settings.Save(); } }

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
        StatusText = "Scanning...";
        Movies.Clear();

        var entries = await Task.Run(() => MovieScanner.Scan(folder.Path, _settings.MovieExtensions));
        foreach (var e in entries)
            Movies.Add(e);

        HasMovies = Movies.Count > 0;
        StatusText = $"Found {Movies.Count} movies";
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

        StatusText = $"Fetched metadata for {done} movies";
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

    [RelayCommand]
    private void ToggleSettings() => ShowSettings = !ShowSettings;
}
