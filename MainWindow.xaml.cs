using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Moviebase.Models;
using Moviebase.ViewModels;
using Windows.Graphics;

namespace Moviebase;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1100 * scale), (int)(720 * scale)));

        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.StatusText):
                    StatusText.Text = _vm.StatusText;
                    break;
                case nameof(MainViewModel.Progress):
                    ProgressBar.Value = _vm.Progress;
                    ProgressBar.Visibility = _vm.Progress > 0 ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(MainViewModel.IsBusy):
                    ProgressBar.IsIndeterminate = _vm.IsBusy && _vm.Progress == 0;
                    ProgressBar.Visibility = _vm.IsBusy ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }
        });
    }

    // --- Event handlers ---

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        await _vm.AddFolderCommand.ExecuteAsync(null);
        RefreshMovieList();
        PathText.Text = _vm.CurrentPath;
    }

    private async void Fetch_Click(object sender, RoutedEventArgs e)
    {
        await _vm.FetchMetadataCommand.ExecuteAsync(null);
        RefreshMovieList();
        UpdateDetail();
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        await _vm.RenameAllCommand.ExecuteAsync(null);
        RefreshMovieList();
    }

    private async void Posters_Click(object sender, RoutedEventArgs e)
    {
        await _vm.DownloadPostersCommand.ExecuteAsync(null);
        RefreshMovieList();
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = _vm.GetSettings();
        var dialog = new Views.SettingsDialog(settings, _vm.GetWatchFolders())
        {
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            _vm.TmdbApiKey = dialog.ApiKey;
            _vm.MovieExtensions = dialog.Extensions;
            _vm.FileRenamePattern = dialog.FilePattern;
            _vm.FolderRenamePattern = dialog.FolderPattern;
            _vm.SwapThe = dialog.SwapThe;
            _vm.LibraryRoot = dialog.LibraryRoot;
            _vm.DefaultImportMode = dialog.ImportMode;
            _vm.UpdateWatchFolders(dialog.WatchFolders);
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _vm.SearchText = sender.Text;
        RefreshMovieList();
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        await _vm.ExportCsvCommand.ExecuteAsync(null);
    }

    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        await _vm.ExportJsonCommand.ExecuteAsync(null);
    }

    private async void GenerateNfo_Click(object sender, RoutedEventArgs e)
    {
        await _vm.GenerateNfoCommand.ExecuteAsync(null);
    }

    private async void SetThumbnails_Click(object sender, RoutedEventArgs e)
    {
        await _vm.SetFolderThumbnailsCommand.ExecuteAsync(null);
    }

    private async void DetectCollections_Click(object sender, RoutedEventArgs e)
    {
        await _vm.DetectCollectionsCommand.ExecuteAsync(null);
        RefreshMovieList();
    }

    private async void ImportFiles_Click(object sender, RoutedEventArgs e)
    {
        // Scan common folders and show importable movies
        var found = _vm.ScanCommonFolders();
        if (found.Count == 0)
        {
            StatusText.Text = "No new movies found in Downloads/Videos/Movies";
            return;
        }

        // Show a dialog to confirm import
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = $"Found {found.Count} movies",
            Content = $"Found {found.Count} movie files in Downloads, Videos, and Movies folders.\n\nAdd them to the library?",
            PrimaryButtonText = "Add All",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await _vm.AddFilesAsync(found);
            RefreshMovieList();
        }
    }

    // --- Drag and drop ---

    private void Grid_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Link;
        e.DragUIOverride.Caption = "Add to library";
        e.DragUIOverride.IsCaptionVisible = true;
    }

    private async void Grid_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)) return;

        var items = await e.DataView.GetStorageItemsAsync();

        // Handle folders
        var folders = items.OfType<Windows.Storage.StorageFolder>().ToList();
        foreach (var folder in folders)
        {
            await _vm.ScanPathAsync(folder.Path);
        }

        // Handle files (.mp4, .mkv, etc.)
        var files = items.OfType<Windows.Storage.StorageFile>()
            .Where(f => _vm.GetSettings().MovieExtensions.Contains(f.FileType, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Path)
            .ToList();

        if (files.Count > 0)
        {
            await _vm.AddFilesAsync(files);
        }

        RefreshMovieList();
        PathText.Text = _vm.CurrentPath;
    }

    // --- List building with collapsible Expander groups ---

    private void RefreshMovieList()
    {
        MovieListPanel.Children.Clear();

        var filtered = _vm.FilteredMovies.ToList();

        if (filtered.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            MovieListPanel.Children.Add(EmptyState);
            MovieCountText.Text = _vm.Movies.Count > 0 ? $"{filtered.Count}/{_vm.Movies.Count}" : "";
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;

        // Group movies: standalone vs series/collections
        var standalone = filtered.Where(m => !m.IsInSeries && string.IsNullOrEmpty(m.CollectionName)).ToList();
        var seriesGroups = filtered.Where(m => m.IsInSeries)
            .GroupBy(m => m.SeriesName)
            .OrderBy(g => g.Key)
            .ToList();
        var collectionGroups = filtered.Where(m => !m.IsInSeries && !string.IsNullOrEmpty(m.CollectionName))
            .GroupBy(m => m.CollectionName)
            .OrderBy(g => g.Key)
            .ToList();

        if (standalone.Count > 0)
        {
            MovieListPanel.Children.Add(BuildMovieListView(standalone));
        }

        // Series groups (folder-based)
        foreach (var group in seriesGroups)
        {
            var expander = new Expander
            {
                Header = BuildSeriesHeader(group.Key, group.Count()),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsExpanded = true,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 4, 0, 0)
            };
            expander.Content = BuildMovieListView(group.ToList());
            MovieListPanel.Children.Add(expander);
        }

        // Collection groups (TMDB-detected)
        foreach (var group in collectionGroups)
        {
            var expander = new Expander
            {
                Header = BuildCollectionHeader(group.Key!, group.Count()),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsExpanded = true,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 4, 0, 0)
            };
            expander.Content = BuildMovieListView(group.ToList());
            MovieListPanel.Children.Add(expander);
        }

        MovieCountText.Text = filtered.Count == _vm.Movies.Count
            ? $"{_vm.Movies.Count} movies"
            : $"{filtered.Count}/{_vm.Movies.Count} movies";
    }

    private StackPanel BuildSeriesHeader(string name, int count)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new FontIcon
        {
            Glyph = "\uE1D3",
            FontSize = 14,
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"]
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{name} ({count})",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13
        });
        return panel;
    }

    private StackPanel BuildCollectionHeader(string name, int count)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new FontIcon
        {
            Glyph = "\uE8FD", // library icon
            FontSize = 14,
            Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"]
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{name} ({count})",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13
        });
        return panel;
    }

    private ListView BuildMovieListView(List<MovieEntry> movies)
    {
        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsItemClickEnabled = true,
        };

        // Use ItemContainerStyle to stretch items
        var style = new Style(typeof(ListViewItem));
        style.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(8, 4, 12, 4)));
        listView.ItemContainerStyle = style;

        listView.ItemsSource = movies.Select(m => new MovieEntryDisplay
        {
            Title = m.Title,
            Year = m.Year > 0 ? m.Year.ToString() : "",
            Size = m.Size,
            PosterUrl = GetPosterSource(m),
            Folder = BuildSubtitle(m),
        }).ToList();

        // Use a DataTemplate programmatically since we can't use x:Bind with dynamic ItemsSource
        listView.ItemTemplate = CreateMovieItemTemplate();
        listView.ItemClick += (s, args) =>
        {
            if (args.ClickedItem is MovieEntryDisplay display)
            {
                var idx = movies.FindIndex(m => m.Title == display.Title && m.Year.ToString() == display.Year);
                if (idx >= 0)
                {
                    _vm.SelectedMovie = movies[idx];
                    UpdateDetail();
                }
            }
        };

        return listView;
    }

    private static string GetPosterSource(MovieEntry m)
    {
        var dir = Path.GetDirectoryName(m.FullPath);
        var localPoster = dir is not null ? Path.Combine(dir, "poster.jpg") : null;
        if (localPoster is not null && File.Exists(localPoster)) return localPoster;
        if (!string.IsNullOrEmpty(m.ThumbnailUrl)) return m.ThumbnailUrl;
        return "";
    }

    private static string BuildSubtitle(MovieEntry m)
    {
        var parts = new List<string>();
        if (m.IsWatched) parts.Add("✓ Watched");
        if (m.IsDuplicate) parts.Add("⚠ Duplicate");
        if (!string.IsNullOrEmpty(m.QualityBadge)) parts.Add(m.QualityBadge);
        else if (parts.Count == 0) parts.Add(m.FolderName);
        if (m.HasSubtitles) parts.Add($"🗎 {m.Subtitles.Count} sub");
        return string.Join(" · ", parts);
    }

    private static DataTemplate CreateMovieItemTemplate()
    {
        // Build a DataTemplate with a Grid: [Poster 40px] [Title+Quality *] [Year 50px] [Size 60px]
        var xaml = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid ColumnSpacing="12" MinHeight="56">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="40" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="50" />
                        <ColumnDefinition Width="56" />
                    </Grid.ColumnDefinitions>
                    <Border Grid.Column="0" Width="40" Height="56" CornerRadius="4"
                            Background="{ThemeResource CardBackgroundFillColorSecondaryBrush}">
                        <Image Source="{Binding PosterUrl}" Stretch="UniformToFill" />
                    </Border>
                    <StackPanel Grid.Column="1" VerticalAlignment="Center" Spacing="2">
                        <TextBlock Text="{Binding Title}" FontSize="13" TextTrimming="CharacterEllipsis" />
                        <TextBlock Text="{Binding Folder}" FontSize="11"
                                   Foreground="{ThemeResource TextFillColorTertiaryBrush}" TextTrimming="CharacterEllipsis" />
                    </StackPanel>
                    <TextBlock Grid.Column="2" Text="{Binding Year}" VerticalAlignment="Center"
                               Foreground="{ThemeResource TextFillColorSecondaryBrush}" FontSize="12" />
                    <TextBlock Grid.Column="3" Text="{Binding Size}" VerticalAlignment="Center"
                               HorizontalAlignment="Right"
                               Foreground="{ThemeResource TextFillColorTertiaryBrush}" FontSize="12" />
                </Grid>
            </DataTemplate>
            """;

        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
    }

    // --- Detail panel ---

    private void UpdateDetail()
    {
        var movie = _vm.SelectedMovie;
        if (movie is null) return;

        DetailPanel.Visibility = Visibility.Visible;
        DetailEmpty.Visibility = Visibility.Collapsed;

        TitleText.Text = movie.IsFetched ? $"{movie.Title} ({movie.Year})" : movie.Title;
        YearText.Text = movie.Year > 0 ? movie.Year.ToString() : "";
        GenreText.Text = movie.Genre;
        SeriesText.Text = movie.IsInSeries ? $"Series: {movie.SeriesName}" :
            !string.IsNullOrEmpty(movie.CollectionName) ? $"Collection: {movie.CollectionName}" : "";
        ImdbText.Text = !string.IsNullOrEmpty(movie.ImdbId) ? $"IMDB: {movie.ImdbId}" : "";
        PlotText.Text = movie.Plot;

        var techParts = new List<string>();
        if (!string.IsNullOrEmpty(movie.ScreenSize)) techParts.Add(movie.ScreenSize);
        if (!string.IsNullOrEmpty(movie.Source)) techParts.Add(movie.Source);
        if (!string.IsNullOrEmpty(movie.VideoCodec)) techParts.Add(movie.VideoCodec);
        if (!string.IsNullOrEmpty(movie.AudioCodec)) techParts.Add(movie.AudioCodec);
        if (!string.IsNullOrEmpty(movie.Edition)) techParts.Add(movie.Edition);
        if (!string.IsNullOrEmpty(movie.ReleaseGroup)) techParts.Add($"[{movie.ReleaseGroup}]");
        QualityText.Text = string.Join(" · ", techParts);

        FileText.Text = movie.HasSubtitles
            ? $"{movie.FileName}  ·  🗎 {movie.Subtitles.Count} subtitle(s)"
            : movie.FileName;

        // Load poster
        var posterSource = GetPosterSource(movie);
        if (!string.IsNullOrEmpty(posterSource))
        {
            var uri = posterSource.StartsWith("http") ? posterSource : posterSource;
            PosterImage.Source = new BitmapImage(new Uri(
                posterSource.StartsWith("http") ? posterSource : $"https://image.tmdb.org/t/p/w342{movie.PosterPath}"));
        }
        else
        {
            PosterImage.Source = null;
        }

        // If we have a local poster or TMDB path, use larger version for detail
        if (!string.IsNullOrEmpty(movie.PosterPath))
        {
            PosterImage.Source = new BitmapImage(new Uri($"https://image.tmdb.org/t/p/w342{movie.PosterPath}"));
        }
        var dir = Path.GetDirectoryName(movie.FullPath);
        var localPoster = dir is not null ? Path.Combine(dir, "poster.jpg") : null;
        if (localPoster is not null && File.Exists(localPoster))
        {
            PosterImage.Source = new BitmapImage(new Uri(localPoster));
        }
    }

}
