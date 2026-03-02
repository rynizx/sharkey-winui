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

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
        // Restore saved credentials so IsAuthenticated is correct before the window opens.
        AuthService.TryRestoreSession();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
