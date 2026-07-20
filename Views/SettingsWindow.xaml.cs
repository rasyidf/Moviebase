using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Moviebase.Models;
using Windows.Graphics;

namespace Moviebase.Views;

public sealed partial class SettingsWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    private readonly AppSettings _settings;
    private readonly List<string> _folders;
    private readonly StackPanel[] _pages;
    private bool _saved;

    public bool WasSaved => _saved;

    public SettingsWindow(AppSettings settings, List<string> watchFolders)
    {
        InitializeComponent();
        _settings = settings;

        // Size the window
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(700 * scale), (int)(500 * scale)));
        AppWindow.SetIcon("Assets/AppIcon.ico");

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

        ThemeBox.SelectedIndex = settings.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };

        _folders = new List<string>(watchFolders);
        FolderList.ItemsSource = _folders;

        // Auto-save on close
        Closed += OnClosed;
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        // Save all settings on window close
        _settings.TmdbApiKey = ApiKeyBox.Text;
        _settings.MovieExtensions = ExtensionsBox.Text;
        _settings.FileRenamePattern = FilePatternBox.Text;
        _settings.FolderRenamePattern = FolderPatternBox.Text;
        _settings.SwapThe = SwapTheToggle.IsOn;
        _settings.LibraryRoot = LibraryRootBox.Text;
        _settings.DefaultImportMode = ImportModeBox.SelectedIndex switch { 1 => "Move", 2 => "Copy", _ => "Symlink" };
        _settings.Theme = ThemeBox.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" };
        _settings.Save();
        _saved = true;
    }

    // Results for the caller
    public List<string> WatchFolders => _folders;

    // --- Navigation ---

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pages is null) return;
        var idx = NavList.SelectedIndex;
        for (int i = 0; i < _pages.Length; i++)
            _pages[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- Folder management ---

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
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
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null) LibraryRootBox.Text = folder.Path;
    }
}
