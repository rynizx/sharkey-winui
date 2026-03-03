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
        // NOTE: Do NOT auto-restore the session here.
        // MainWindow.NavView_Loaded decides whether to show the Hello lock
        // page, silently restore, or go to login — after the window is ready.
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
