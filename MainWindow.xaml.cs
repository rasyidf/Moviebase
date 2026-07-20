using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    private int _sortIndex; // 0=Title, 1=Year, 2=DateAdded, 3=Size

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1100 * scale), (int)(720 * scale)));

        // Restore window position/size from settings
        var settings = _vm.GetSettings();
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            AppWindow.Resize(new SizeInt32(settings.WindowWidth, settings.WindowHeight));
        }
        if (settings.WindowX >= 0 && settings.WindowY >= 0)
        {
            AppWindow.Move(new Windows.Graphics.PointInt32(settings.WindowX, settings.WindowY));
        }

        // Save position/size on close
        Closed += (_, _) =>
        {
            var pos = AppWindow.Position;
            var size = AppWindow.Size;
            settings.WindowX = pos.X;
            settings.WindowY = pos.Y;
            settings.WindowWidth = size.Width;
            settings.WindowHeight = size.Height;
            settings.Save();
        };

        _vm.PropertyChanged += OnVmPropertyChanged;

        // Keyboard accelerators
        var accelAdd = new KeyboardAccelerator { Modifiers = Windows.System.VirtualKeyModifiers.Control, Key = Windows.System.VirtualKey.O };
        accelAdd.Invoked += AccelAddFolder_Invoked;
        RootGrid.KeyboardAccelerators.Add(accelAdd);

        var accelRefresh = new KeyboardAccelerator { Key = Windows.System.VirtualKey.F5 };
        accelRefresh.Invoked += AccelRefresh_Invoked;
        RootGrid.KeyboardAccelerators.Add(accelRefresh);

        var accelDelete = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Delete };
        accelDelete.Invoked += AccelDelete_Invoked;
        RootGrid.KeyboardAccelerators.Add(accelDelete);
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

            // Apply theme immediately
            settings.Theme = dialog.Theme;
            settings.Save();
            if (Content is FrameworkElement root)
            {
                root.RequestedTheme = dialog.Theme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default,
                };
            }
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

    // --- Keyboard accelerators ---

    private async void AccelAddFolder_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await _vm.AddFolderCommand.ExecuteAsync(null);
        RefreshMovieList();
        PathText.Text = _vm.CurrentPath;
    }

    private async void AccelRefresh_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _vm.IsBusy = true;
        _vm.StatusText = "Refreshing...";
        var added = await Task.Run(() => _vm.RescanWatchFolders());
        RefreshMovieList();
        _vm.StatusText = added > 0 ? $"Added {added} new movies" : "Library is up to date";
        _vm.IsBusy = false;
    }

    private void AccelDelete_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        RemoveSelectedMovie();
    }

    private void RemoveSelectedMovie()
    {
        if (_vm.SelectedMovie is null) return;
        _vm.RemoveMovie(_vm.SelectedMovie);
        RefreshMovieList();
        DetailPanel.Visibility = Visibility.Collapsed;
        DetailEmpty.Visibility = Visibility.Visible;
    }

    // --- Sort ---

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            _sortIndex = cb.SelectedIndex;
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
        if (MovieListPanel is null) return; // called during InitializeComponent
        MovieListPanel.Children.Clear();

        var filtered = ApplySort(_vm.FilteredMovies).ToList();

        if (filtered.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            MovieListPanel.Children.Add(EmptyState);
            MovieCountText.Text = _vm.Movies.Count > 0 ? $"{filtered.Count}/{_vm.Movies.Count}" : "";
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;

        // Recently Added section — last 5 by add order (list index)
        var recentlyAdded = _vm.Movies.TakeLast(5).Reverse().ToList();
        if (recentlyAdded.Count > 0 && string.IsNullOrWhiteSpace(_vm.SearchText))
        {
            MovieListPanel.Children.Add(new TextBlock
            {
                Text = "Recently Added",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 4),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            MovieListPanel.Children.Add(BuildMovieListView(recentlyAdded));
        }

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

        // Rich status bar stats
        MovieCountText.Text = _vm.FormatStatusBar();
    }

    private IEnumerable<MovieEntry> ApplySort(IEnumerable<MovieEntry> movies)
    {
        return _sortIndex switch
        {
            1 => movies.OrderByDescending(m => m.Year),
            2 => movies, // ponytail: list order ≈ date added; no timestamp field
            3 => movies.OrderByDescending(m => m.SizeBytes),
            _ => movies.OrderBy(m => m.Title, StringComparer.OrdinalIgnoreCase),
        };
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
            if (s is ListView lv && lv.SelectedIndex >= 0 && lv.SelectedIndex < movies.Count)
            {
                _vm.SelectedMovie = movies[lv.SelectedIndex];
                UpdateDetail();
            }
        };

        // Double-click to play
        listView.DoubleTapped += (s, args) =>
        {
            if (s is ListView lv && lv.SelectedIndex >= 0 && lv.SelectedIndex < movies.Count)
            {
                var movie = movies[lv.SelectedIndex];
                if (File.Exists(movie.FullPath))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(movie.FullPath) { UseShellExecute = true });
            }
        };

        // Context menu on right-click
        listView.RightTapped += (s, args) =>
        {
            if (s is not ListView lv) return;

            // Determine the tapped item index
            var element = args.OriginalSource as FrameworkElement;
            while (element != null && element is not ListViewItem)
                element = element.Parent as FrameworkElement;

            if (element is not ListViewItem item) return;
            var idx = lv.IndexFromContainer(item);
            if (idx < 0 || idx >= movies.Count) return;

            var movie = movies[idx];
            _vm.SelectedMovie = movie;
            lv.SelectedIndex = idx;
            UpdateDetail();

            var menu = new MenuFlyout();

            var lookUp = new MenuFlyoutItem { Text = "Look Up", Icon = new FontIcon { Glyph = "\uE721" } };
            lookUp.Click += async (_, _) =>
            {
                var results = await _vm.ManualSearchAsync(movie.Title);
                if (results.Count > 0)
                {
                    await _vm.ApplySearchResultAsync(movie, results[0].Id);
                    RefreshMovieList();
                    UpdateDetail();
                }
            };
            menu.Items.Add(lookUp);

            var watchText = movie.IsWatched ? "Unwatch" : "Mark as Watched";
            var watchItem = new MenuFlyoutItem { Text = watchText, Icon = new FontIcon { Glyph = "\uE73E" } };
            watchItem.Click += (_, _) =>
            {
                _vm.ToggleWatched(movie);
                RefreshMovieList();
            };
            menu.Items.Add(watchItem);

            var openFolder = new MenuFlyoutItem { Text = "Open Folder", Icon = new FontIcon { Glyph = "\uE838" } };
            openFolder.Click += (_, _) =>
            {
                var dir = Path.GetDirectoryName(movie.FullPath);
                if (dir is not null && Directory.Exists(dir))
                    System.Diagnostics.Process.Start("explorer.exe", dir);
            };
            menu.Items.Add(openFolder);

            menu.Items.Add(new MenuFlyoutSeparator());

            var remove = new MenuFlyoutItem { Text = "Remove from Library", Icon = new FontIcon { Glyph = "\uE74D" } };
            remove.Click += (_, _) =>
            {
                _vm.RemoveMovie(movie);
                RefreshMovieList();
                DetailPanel.Visibility = Visibility.Collapsed;
                DetailEmpty.Visibility = Visibility.Visible;
            };
            menu.Items.Add(remove);

            menu.ShowAt(item, args.GetPosition(item));
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

        // IMDB/TMDB links
        if (movie.IsFetched)
        {
            if (!string.IsNullOrEmpty(movie.ImdbId))
            {
                ImdbLink.NavigateUri = new Uri($"https://www.imdb.com/title/{movie.ImdbId}/");
                ImdbLink.Visibility = Visibility.Visible;
            }
            else
            {
                ImdbLink.Visibility = Visibility.Collapsed;
            }

            TmdbLink.NavigateUri = new Uri($"https://www.themoviedb.org/movie/{movie.TmdbId}");
            TmdbLink.Visibility = Visibility.Visible;
        }
        else
        {
            ImdbLink.Visibility = Visibility.Collapsed;
            TmdbLink.Visibility = Visibility.Collapsed;
        }

        var techParts = new List<string>();
        if (!string.IsNullOrEmpty(movie.ScreenSize)) techParts.Add(movie.ScreenSize);
        if (!string.IsNullOrEmpty(movie.Source)) techParts.Add(movie.Source);
        if (!string.IsNullOrEmpty(movie.VideoCodec)) techParts.Add(movie.VideoCodec);
        if (!string.IsNullOrEmpty(movie.AudioCodec)) techParts.Add(movie.AudioCodec);
        if (!string.IsNullOrEmpty(movie.Edition)) techParts.Add(movie.Edition);
        QualityText.Text = string.Join(" · ", techParts);

        FileText.Text = movie.HasSubtitles
            ? $"{movie.FileName}  ·  {movie.Subtitles.Count} subtitle(s)"
            : movie.FileName;

        // Load poster
        var dir = Path.GetDirectoryName(movie.FullPath);
        var localPoster = dir is not null ? Path.Combine(dir, "poster.jpg") : null;
        if (localPoster is not null && File.Exists(localPoster))
        {
            PosterImage.Source = new BitmapImage(new Uri(localPoster));
        }
        else if (!string.IsNullOrEmpty(movie.PosterPath))
        {
            PosterImage.Source = new BitmapImage(new Uri($"https://image.tmdb.org/t/p/w185{movie.PosterPath}"));
        }
        else
        {
            PosterImage.Source = null;
        }
    }

}
