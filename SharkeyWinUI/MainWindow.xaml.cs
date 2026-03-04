using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    public MainWindow()
    {
        InitializeComponent();
        Title = "Sharkey WinUI";
        App.Streaming = _streaming;
    }

    private async void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.IsPaneVisible    = false;
        ComposeButton.Visibility = Visibility.Collapsed;

        // Sync the New Note label with the initial pane state
        SetNewNoteLabelVisible(NavView.IsPaneOpen);

        if (!App.AuthService.HasSavedSession)
        {
            // First run — show login
            ContentFrame.Navigate(typeof(LoginPage));
            return;
        }

        if (App.AuthService.HelloEnabled)
        {
            // Credentials saved + Hello required — show lock screen
            // The lock page calls OnLoggedIn() after successful verification
            ContentFrame.Navigate(typeof(WindowsHelloLockPage));
            return;
        }

        // Credentials saved, no Hello required — silently restore
        if (App.AuthService.TryRestoreSession())
        {
            ShowAuthenticatedUI();
            await ConnectStreamingAsync();
        }
        else
        {
            // Vault entry missing (e.g., OS re-install) — re-authenticate
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
        NavView.IsPaneVisible = true;
        ComposeButton.Visibility = Visibility.Visible;
        NavView.SelectedItem = HomeItem;
        Navigate("home");
        // Remove auth pages (LoginPage, WindowsHelloLockPage) from the back stack
        // so the user can never press Back into them after signing in.
        ContentFrame.BackStack.Clear();
    }

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

    private void NavView_BackRequested(NavigationView sender,
        NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack();
    }

    private void ContentFrame_Navigated(object sender,
        Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        // IsBackEnabled is bound via x:Bind in XAML, so no manual update needed.
    }

    private void NavView_PaneOpening(NavigationView sender, object args)
        => SetNewNoteLabelVisible(true);

    private void NavView_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        => SetNewNoteLabelVisible(false);

    /// <summary>Shows or hides the "New Note" label inside ComposeButton to match the pane state.</summary>
    private void SetNewNoteLabelVisible(bool visible)
    {
        if (ComposeButton.Content is StackPanel panel)
        {
            foreach (var tb in panel.Children.OfType<TextBlock>())
                tb.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        // The compact pane is only ~48 px wide. With the default 12 px side margins the
        // button content area shrinks to ~24 px which clips the icon entirely.
        // Use smaller margins/padding in compact mode so the icon is always visible.
        if (visible)
        {
            ComposeButton.Margin  = new Thickness(12, 8, 12, 8);
            ComposeButton.Padding = new Thickness(8, 5, 8, 6);
        }
        else
        {
            ComposeButton.Margin  = new Thickness(4, 8, 4, 8);
            ComposeButton.Padding = new Thickness(4, 5, 4, 6);
        }
    }

    private void ComposeButton_Click(object sender, RoutedEventArgs e)
        => ContentFrame.Navigate(typeof(ComposePage));

    private void Navigate(string tag)
    {
        if (!PageMap.TryGetValue(tag, out var pageType)) return;

        // TimelinePage receives the timeline kind as a string parameter
        object? param = pageType == typeof(TimelinePage) ? tag : null;

        // Skip only if we're already showing exactly this page with this tag.
        // Comparing tag (not just type) is necessary because multiple tags share
        // TimelinePage — without this, switching from Home to Local was silently dropped.
        if (_currentNavTag == tag && ContentFrame.CurrentSourcePageType == pageType) return;

        _currentNavTag = tag;
        ContentFrame.Navigate(pageType, param);
    }

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
