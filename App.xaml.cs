using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace Moviebase;

public partial class App : Application
{
    public static Window Window { get; private set; } = null!;
    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Window = new MainWindow();
            Window.Activate();

            // Apply theme from settings
            var settings = Models.AppSettings.Load();
            if (Window.Content is FrameworkElement root)
            {
                root.RequestedTheme = settings.Theme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default,
                };
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), ex.ToString());
            throw;
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[Unhandled] {e.Exception}");
        File.AppendAllText(
            Path.Combine(AppContext.BaseDirectory, "crash.log"),
            $"[{DateTime.Now:O}] {e.Exception}\n\n");
        e.Handled = true;
    }
}
