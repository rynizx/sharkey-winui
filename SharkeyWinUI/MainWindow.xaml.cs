using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SharkeyWinUI.Pages;
using SharkeyWinUI.Services;

namespace SharkeyWinUI;

public sealed partial class MainWindow : Window
{
    private readonly MisskeyStreamingService _streaming = new();
    private string? _currentNavTag;

    // Maps NavView tag strings to page types
    private static readonly Dictionary<string, Type> PageMap = new()
    {
        ["home"]          = typeof(TimelinePage),
        ["local"]         = typeof(TimelinePage),
        ["social"]        = typeof(TimelinePage),
        ["global"]        = typeof(TimelinePage),
        ["bubble"]        = typeof(TimelinePage),
        ["search"]        = typeof(SearchPage),
        ["notifications"] = typeof(NotificationsPage),
        ["profile"]       = typeof(ProfilePage),
        ["settings"]      = typeof(AccountSettingsPage),
    };

    // Maps nav tags to their display titles
    private static readonly Dictionary<string, string> TagTitles = new()
    {
        ["home"]          = "Home",
        ["local"]         = "Local Timeline",
        ["social"]        = "Social Timeline",
        ["global"]        = "Global Timeline",
        ["bubble"]        = "Bubble Timeline",
        ["search"]        = "Search",
        ["notifications"] = "Notifications",
        ["profile"]       = "Profile",
        ["settings"]      = "Settings",
    };

    public MainWindow()
    {
        InitializeComponent();
        Title = "Sharkey WinUI";
        App.Streaming = _streaming;

        // Mica backdrop
        SystemBackdrop = new MicaBackdrop();

        // Extend client area into the title bar so the NavView and Mica
        // fill the full window, and our custom title bar overlay shows at top.
        ExtendsContentIntoTitleBar = true;

        // Make the OS caption buttons (min/max/close) blend with Mica.
        var tb = AppWindow.TitleBar;
        tb.ButtonBackgroundColor         = Colors.Transparent;
        tb.ButtonInactiveBackgroundColor = Colors.Transparent;
        tb.ButtonHoverBackgroundColor    = Colors.Transparent;
        tb.ButtonPressedBackgroundColor  = Colors.Transparent;

        // Register our overlay Grid as the title bar drag region.
        // This also tells the NavigationView how tall the title bar is so it
        // offsets its content (pane items + frame) correctly.
        SetTitleBar(AppTitleBar);

        // Update left/right padding columns whenever the caption-button insets change
        // (DPI change, snap layout, window state change, etc.).
        // AppWindowTitleBar.Changed is not available in Windows App SDK 1.5; use
        // Window.SizeChanged which fires on every resize/DPI change instead.
        SizeChanged += (_, _) => SetTitleBarColumnWidths();
        Activated   += (_, _) => SetTitleBarColumnWidths();
    }

    // ── Title bar helpers ─────────────────────────────────────────────────────

    /// <summary>Keeps the left/right padding columns in sync with caption-button insets.</summary>
    private void SetTitleBarColumnWidths()
    {
        var titleBar = AppWindow.TitleBar;
        // LeftInset: typically 0 on standard Windows, non-zero in RTL / tablet modes.
        // RightInset: width of the min/max/close buttons (≈138 px at 100% DPI).
        // Insets can be -1 when the window is minimized; clamp to 0 to avoid ArgumentException.
        LeftPaddingColumn.Width  = new GridLength(Math.Max(0, titleBar.LeftInset));
        RightPaddingColumn.Width = new GridLength(Math.Max(0, titleBar.RightInset));
    }

    private void UpdateTitleBar()
    {
        // Resolve the page title from the current nav tag first, then fall back
        // to the source page type for pages reached outside the NavView.
        string title;
        if (_currentNavTag != null && TagTitles.TryGetValue(_currentNavTag, out var tagTitle))
        {
            title = tagTitle;
        }
        else
        {
            title = ContentFrame.CurrentSourcePageType switch
            {
                Type t when t == typeof(NoteDetailPage)            => "Note",
                Type t when t == typeof(ComposePage)               => "New Note",
                Type t when t == typeof(NotificationSettingsPage)  => "Notification Settings",
                _                                                   => "Sharkey WinUI",
            };
        }

        TitleBarTitle.Text = title;
        TitleBarBackButton.Visibility =
            ContentFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TitleBarBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack();
    }

    // ── Navigation loading ────────────────────────────────────────────────────

    private async void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        if (!App.AuthService.HasSavedSession)
        {
            ContentFrame.Navigate(typeof(LoginPage));
            return;
        }

        if (App.AuthService.HelloEnabled)
        {
            ContentFrame.Navigate(typeof(WindowsHelloLockPage));
            return;
        }

        if (App.AuthService.TryRestoreSession())
        {
            ShowAuthenticatedUI();
            await ConnectStreamingAsync();
        }
        else
        {
            ContentFrame.Navigate(typeof(LoginPage));
        }
    }

    /// <summary>Called by LoginPage or WindowsHelloLockPage after successful auth.</summary>
    public async void OnLoggedIn()
    {
        ShowAuthenticatedUI();
        await ConnectStreamingAsync();
    }

    private void ShowAuthenticatedUI()
    {
        NavView.IsPaneVisible    = true;
        NavView.IsPaneOpen       = true;
        ComposeButton.Visibility = Visibility.Visible;
        NavView.SelectedItem     = HomeItem;
        Navigate("home");
        // Remove auth pages (LoginPage, WindowsHelloLockPage) from the back stack
        // so the user can never press Back into them after signing in.
        ContentFrame.BackStack.Clear();
        UpdateTitleBar();
    }

    // ── Navigation events ─────────────────────────────────────────────────────

    private void NavView_SelectionChanged(NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            Navigate("settings");
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            Navigate(tag);
    }

    private void ContentFrame_Navigated(object sender,
        Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        // Keep the title bar title and back-button visibility in sync for all
        // navigations — including GoBack() and navigations from within pages.
        UpdateTitleBar();
    }

    private void ComposeButton_Click(object sender, RoutedEventArgs e)
        => ContentFrame.Navigate(typeof(ComposePage));

    private void Navigate(string tag)
    {
        if (!PageMap.TryGetValue(tag, out var pageType)) return;

        object? param = pageType == typeof(TimelinePage) ? tag : null;

        if (_currentNavTag == tag && ContentFrame.CurrentSourcePageType == pageType) return;

        _currentNavTag = tag;
        ContentFrame.Navigate(pageType, param);
        UpdateTitleBar();
    }

    // ── Streaming ─────────────────────────────────────────────────────────────

    private async Task ConnectStreamingAsync()
    {
        try
        {
            var url   = App.AuthService.ServerUrl!;
            var token = App.ApiClient.Token!;
            await _streaming.ConnectAsync(url, token);
            await _streaming.SubscribeChannelAsync("main");
            await _streaming.SubscribeChannelAsync("homeTimeline");
        }
        catch
        {
            // Streaming is best-effort — the app still works without it
        }
    }
}

