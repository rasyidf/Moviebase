using Microsoft.UI.Xaml.Controls;
using Moviebase.Models;

namespace Moviebase.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialog(AppSettings settings)
    {
        InitializeComponent();

        // Load current settings
        ApiKeyBox.Text = settings.TmdbApiKey;
        ExtensionsBox.Text = settings.MovieExtensions;
        FilePatternBox.Text = settings.FileRenamePattern;
        FolderPatternBox.Text = settings.FolderRenamePattern;
        SwapTheToggle.IsOn = settings.SwapThe;
    }

    public string ApiKey => ApiKeyBox.Text;
    public string Extensions => ExtensionsBox.Text;
    public string FilePattern => FilePatternBox.Text;
    public string FolderPattern => FolderPatternBox.Text;
    public bool SwapThe => SwapTheToggle.IsOn;
}
