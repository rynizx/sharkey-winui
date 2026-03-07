using Microsoft.UI.Xaml;
using SharkeyWinUI.Services;

namespace SharkeyWinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public static MisskeyApiClient ApiClient { get; } = new();
    public static AuthService AuthService { get; } = new();
    public static MisskeyStreamingService Streaming { get; set; } = new();

    /// <summary>Reference to the main window, set in <see cref="OnLaunched"/>.</summary>
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        // NOTE: Do NOT auto-restore the session here.
        // MainWindow.NavView_Loaded decides whether to show the Hello lock
        // page, silently restore, or go to login — after the window is ready.
    }

    private void InitializeComponent()
    {
        throw new NotImplementedException();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Apply saved accent colour to resource dictionaries before any XAML loads.
        ThemeService.ApplySavedAccent();

        MainWindow = new MainWindow();
        MainWindow.Activate();

        // Theme must be applied after the window content exists.
        ThemeService.ApplySavedTheme();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Mark as handled to prevent the process from terminating.
        // The exception originated from an async void event handler without a catch-all.
        e.Handled = true;
        System.Diagnostics.Debug.WriteLine($"[UnhandledException] {e.Exception}");
    }
}
