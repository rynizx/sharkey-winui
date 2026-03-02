using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharkeyWinUI.Pages;
using SharkeyWinUI.Services;

namespace SharkeyWinUI;

public sealed partial class MainWindow : Window
{
    private readonly MisskeyStreamingService _streaming = new();

    // Maps NavView tag strings to page types
    private static readonly Dictionary<string, Type> PageMap = new()
    {
        ["home"]          = typeof(TimelinePage),
        ["local"]         = typeof(TimelinePage),
        ["social"]        = typeof(TimelinePage),
        ["global"]        = typeof(TimelinePage),
        ["bubble"]        = typeof(TimelinePage),
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

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.AuthService.IsAuthenticated)
        {
            ShowAuthenticatedUI();
            _ = ConnectStreamingAsync();
        }
        else
        {
            NavView.IsEnabled = false;
            ComposeButton.Visibility = Visibility.Collapsed;
            ContentFrame.Navigate(typeof(LoginPage));
        }
    }

    /// <summary>Called by LoginPage after successful auth.</summary>
    public void OnLoggedIn()
    {
        ShowAuthenticatedUI();
        _ = ConnectStreamingAsync();
    }

    private void ShowAuthenticatedUI()
    {
        NavView.IsEnabled = true;
        ComposeButton.Visibility = Visibility.Visible;
        NavView.SelectedItem = HomeItem;
        Navigate("home");
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

    private void ComposeButton_Click(object sender, RoutedEventArgs e)
        => ContentFrame.Navigate(typeof(ComposePage));

    private void Navigate(string tag)
    {
        if (!PageMap.TryGetValue(tag, out var pageType)) return;

        // TimelinePage receives the timeline kind as a string parameter
        object? param = pageType == typeof(TimelinePage) ? tag : null;

        // Avoid navigating to the same page+param twice in a row
        if (ContentFrame.CurrentSourcePageType == pageType &&
            ContentFrame.BackStack.Count > 0) return;

        ContentFrame.Navigate(pageType, param);
    }

    private async Task ConnectStreamingAsync()
    {
        try
        {
            var url   = App.AuthService.ServerUrl!;
            var token = App.AuthService.Token!;
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
