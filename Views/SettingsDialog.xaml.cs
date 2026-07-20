using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Moviebase.Models;

namespace Moviebase.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly List<string> _folders;
    private readonly StackPanel[] _pages;

    public SettingsDialog(AppSettings settings, List<string> watchFolders)
    {
        InitializeComponent();

        _pages = [PageLibrary, PageTmdb, PageRename, PageAbout];

        // Load settings
        ApiKeyBox.Text = settings.TmdbApiKey;
        ExtensionsBox.Text = settings.MovieExtensions;
        FilePatternBox.Text = settings.FileRenamePattern;
        FolderPatternBox.Text = settings.FolderRenamePattern;
        SwapTheToggle.IsOn = settings.SwapThe;
        LibraryRootBox.Text = settings.LibraryRoot;

        ImportModeBox.SelectedIndex = settings.DefaultImportMode switch
        {
            "Move" => 1,
            "Copy" => 2,
            _ => 0
        };

        _folders = new List<string>(watchFolders);
        FolderList.ItemsSource = _folders;
    }

    // Public props for reading back
    public string ApiKey => ApiKeyBox.Text;
    public string Extensions => ExtensionsBox.Text;
    public string FilePattern => FilePatternBox.Text;
    public string FolderPattern => FolderPatternBox.Text;
    public bool SwapThe => SwapTheToggle.IsOn;
    public string LibraryRoot => LibraryRootBox.Text;
    public List<string> WatchFolders => _folders;
    public string ImportMode => ImportModeBox.SelectedIndex switch { 1 => "Move", 2 => "Copy", _ => "Symlink" };

    // --- Navigation ---

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pages is null) return; // called during InitializeComponent
        var idx = NavList.SelectedIndex;
        for (int i = 0; i < _pages.Length; i++)
            _pages[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- Folder management ---

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        if (!_folders.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
        {
            _folders.Add(folder.Path);
            FolderList.ItemsSource = null;
            FolderList.ItemsSource = _folders;
        }
    }

    private void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is string path)
        {
            _folders.Remove(path);
            FolderList.ItemsSource = null;
            FolderList.ItemsSource = _folders;
        }
    }

    private async void BrowseLibraryRoot_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null) LibraryRootBox.Text = folder.Path;
    }
}
