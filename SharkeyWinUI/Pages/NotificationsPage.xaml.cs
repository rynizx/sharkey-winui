using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class NotificationsPage : Page
{
    private readonly ObservableCollection<Notification> _notifs = new();
    private string? _activeTypeFilter; // null = all
    private string? _untilId;
    private CancellationTokenSource _cts = new();

    public NotificationsPage()
    {
        // Register value converters BEFORE InitializeComponent
        // so they're available when XAML is parsed
        Resources["RelTimeConverter"]          = new RelativeTimeConverter();
        Resources["NotifIconConverter"]        = new NotificationIconConverter();
        Resources["ReadBrushConverter"]        = new ReadBrushConverter();
        Resources["NullVisibilityConverter"]   = new NullToVisibilityConverter();
        Resources["UnreadVisibilityConverter"] = new IsReadToVisibilityConverter();

        InitializeComponent();
        NotifList.ItemsSource = _notifs;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        App.Streaming.NotificationReceived -= OnStreamNotification;
        App.Streaming.NotificationReceived += OnStreamNotification;
        _ = LoadAsync(refresh: true, _cts.Token);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        App.Streaming.NotificationReceived -= OnStreamNotification;
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    private async Task LoadAsync(bool refresh, CancellationToken ct)
    {
        SetLoading(true);
        ErrorBar.IsOpen = false;
        EmptyState.Visibility = Visibility.Collapsed;
        if (refresh) { _notifs.Clear(); _untilId = null; }

        try
        {
            var includeTypes = _activeTypeFilter != null
                ? new[] { _activeTypeFilter }
                : null;

            var batch = await App.ApiClient.GetNotificationsAsync(
                limit: 30, untilId: _untilId,
                includeTypes: includeTypes,
                markAsRead: true,
                ct: ct);

            foreach (var n in batch) _notifs.Add(n);
            if (batch.Count > 0) _untilId = batch[^1].Id;

            LoadMoreButton.Visibility = batch.Count == 30
                ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = _notifs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (MisskeyApiException ex) { ShowError($"API error {(int)ex.StatusCode}: {ex.ResponseBody}"); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetLoading(false); }
    }

    // ── Streaming ─────────────────────────────────────────────────────────────

    private void OnStreamNotification(Notification notif)
    {
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (_notifs.All(n => n.Id != notif.Id))
                _notifs.Insert(0, notif);
        }))
        {
            Debug.WriteLine("NotificationsPage: Dispatcher unavailable, dropping streamed notification update.");
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void FilterChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb) return;

        // Uncheck every chip except the one clicked
        foreach (var child in FilterChipsPanel.Children.OfType<ToggleButton>())
            child.IsChecked = child == tb;

        _activeTypeFilter = tb.Tag as string;
        if (string.IsNullOrEmpty(_activeTypeFilter)) _activeTypeFilter = null;
        _ = LoadAsync(refresh: true, _cts.Token);
    }

    private async void MarkAllReadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await App.ApiClient.MarkAllNotificationsReadAsync();
            // Mark all locally as read
            foreach (var n in _notifs) n.IsRead = true;
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
        => _ = LoadAsync(refresh: true, _cts.Token);

    private void LoadMoreButton_Click(object sender, RoutedEventArgs e)
        => _ = LoadAsync(refresh: false, _cts.Token);

    private void NotifList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not Notification notif)
        {
            Debug.WriteLine("NotificationsPage: ItemClick with null or invalid notification");
            return;
        }

        try
        {
            if (notif.Note != null)
            {
                // Note-based notifications (mention, reply, renote, reaction, etc.)
                Debug.WriteLine($"NotificationsPage: Navigating to note detail: {notif.Note.Id}");
                if (!Frame.Navigate(typeof(NoteDetailPage), notif.Note.Id))
                {
                    Debug.WriteLine($"NotificationsPage: Navigation to NoteDetailPage failed");
                    ShowError("Could not open note details");
                }
            }
            else if (notif.User != null)
            {
                // User-based notifications with no note (follow, follow request, etc.)
                Debug.WriteLine($"NotificationsPage: Navigating to profile: {notif.User.Id}");
                if (!Frame.Navigate(typeof(ProfilePage), notif.User.Id))
                {
                    Debug.WriteLine($"NotificationsPage: Navigation to ProfilePage failed");
                    ShowError("Could not open user profile");
                }
            }
            else
            {
                Debug.WriteLine($"NotificationsPage: Notification has no note or user (type: {notif.Type})");
            }
            // Notifications with neither note nor user (achievements, exports) — no navigation
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotificationsPage: Navigation error: {ex.Message}\n{ex.StackTrace}");
            ShowError($"Navigation failed: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetLoading(bool loading)
        => LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

    private void ShowError(string msg)
    {
        ErrorBar.Message = msg;
        ErrorBar.IsOpen  = true;
    }
}

// ── Value converters ──────────────────────────────────────────────────────────

internal sealed class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTimeOffset dt)
        {
            var diff = DateTimeOffset.UtcNow - dt;
            if (diff.TotalSeconds < 60)  return $"{(int)diff.TotalSeconds}s";
            if (diff.TotalMinutes < 60)  return $"{(int)diff.TotalMinutes}m";
            if (diff.TotalHours < 24)    return $"{(int)diff.TotalHours}h";
            if (diff.TotalDays < 7)      return $"{(int)diff.TotalDays}d";
            return dt.LocalDateTime.ToString("MMM d");
        }
        return string.Empty;
    }

    public object ConvertBack(object v, Type t, object p, string l) => throw new NotImplementedException();
}

internal sealed class NotificationIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value as string) switch
        {
            "follow"                => "\uE8FA",
            "followRequestAccepted" => "\uE8FB",
            "receiveFollowRequest"  => "\uE8F8",
            "mention"               => "\uE8BD",
            "reply"                 => "\uE97A",
            "renote"                => "\uE8EB",
            "quote"                 => "\uE8D4",
            "reaction"              => "\uE76E",
            "pollEnded"             => "\uE9D5",
            "achievementEarned"     => "\uE8D7",
            "roleAssigned"          => "\uECAA",
            _                       => "\uEA8F",
        };

    public object ConvertBack(object v, Type t, object p, string l) => throw new NotImplementedException();
}

internal sealed class ReadBrushConverter : IValueConverter
{
    // Per MS Learn: ResourceDictionary lookups can return null if the key is
    // absent in the current theme. Cast defensively and provide a fallback.
    // https://learn.microsoft.com/en-us/windows/apps/design/style/xaml-resource-dictionary
    private static Microsoft.UI.Xaml.Media.Brush Fallback =>
        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isRead && !isRead)
            return Application.Current.Resources["LayerFillColorDefaultBrush"]
                   as Microsoft.UI.Xaml.Media.Brush ?? Fallback;

        return Application.Current.Resources["ApplicationPageBackgroundThemeBrush"]
               as Microsoft.UI.Xaml.Media.Brush ?? Fallback;
    }

    public object ConvertBack(object v, Type t, object p, string l) => throw new NotImplementedException();
}

internal sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value != null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object v, Type t, object p, string l) => throw new NotImplementedException();
}

internal sealed class IsReadToVisibilityConverter : IValueConverter
{
    // Shows the unread dot when IsRead == false
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool isRead && !isRead ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object v, Type t, object p, string l) => throw new NotImplementedException();
}
