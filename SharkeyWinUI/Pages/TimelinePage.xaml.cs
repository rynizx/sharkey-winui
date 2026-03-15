using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SharkeyWinUI.Helpers;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class TimelinePage : Page
{
    private const int TimelinePageSize = 30;
    private readonly BulkObservableCollection<Note> _notes = new();
    private string _kind = "home";
    private CancellationTokenSource _cts = new();
    private bool _isLoading;

    // For pagination – id of the oldest note currently loaded
    private string? _untilId;

    public TimelinePage()
    {
        InitializeComponent();
        NotesList.ItemsSource = _notes;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _kind = e.Parameter as string ?? "home";
        HeaderText.Text = _kind switch
        {
            "home"    => "Home",
            "local"   => "Local Timeline",
            "social"  => "Social Timeline",
            "global"  => "Global Timeline",
            "bubble"  => "Bubble Timeline",
            _         => "Timeline",
        };

        // Subscribe the streaming channel that matches this timeline
        _ = SubscribeStreamAsync();

        _ = LoadAsync(refresh: true);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // Unsubscribe before cancellation to avoid stream events racing against disposed state.
        App.Streaming.NoteReceived -= OnStreamNote;
        _ = UnsubscribeStreamAsync();
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    // ── Load / refresh ────────────────────────────────────────────────────────

    private async Task LoadAsync(bool refresh)
    {
        if (_isLoading)
            return;

        _isLoading = true;
        SetLoading(true);
        ErrorBar.IsOpen = false;
        EmptyState.Visibility = Visibility.Collapsed;

        if (refresh)
        {
            _notes.ReplaceAll(Array.Empty<Note>());
            _untilId = null;
        }

        try
        {
            var batch = await FetchAsync(_untilId, _cts.Token);
            _notes.AddRange(batch);

            if (batch.Count > 0)
                _untilId = batch[^1].Id;

            LoadMoreButton.Visibility = batch.Count == TimelinePageSize ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = _notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            // Navigation away — ignore
        }
        catch (MisskeyApiException ex)
        {
            ShowError($"API error {(int)ex.StatusCode}: {ex.ResponseBody}");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _isLoading = false;
            SetLoading(false);
        }
    }

    private Task<List<Note>> FetchAsync(string? untilId, CancellationToken ct) =>
        _kind switch
        {
            "home"   => App.ApiClient.GetHomeTimelineAsync(limit: TimelinePageSize, untilId: untilId, ct: ct),
            "local"  => App.ApiClient.GetLocalTimelineAsync(limit: TimelinePageSize, untilId: untilId, ct: ct),
            "social" => App.ApiClient.GetSocialTimelineAsync(limit: TimelinePageSize, untilId: untilId, ct: ct),
            "global" => App.ApiClient.GetGlobalTimelineAsync(limit: TimelinePageSize, untilId: untilId, ct: ct),
            "bubble" => App.ApiClient.GetBubbleTimelineAsync(limit: TimelinePageSize, untilId: untilId, ct: ct),
            _        => App.ApiClient.GetHomeTimelineAsync(limit: TimelinePageSize, untilId: untilId, ct: ct),
        };

    // ── Streaming ─────────────────────────────────────────────────────────────

    private async Task SubscribeStreamAsync()
    {
        if (!App.Streaming.IsConnected) return;
        var channel = _kind switch
        {
            "home"   => "homeTimeline",
            "local"  => "localTimeline",
            "social" => "hybridTimeline",
            "global" => "globalTimeline",
            _        => (string?)null,
        };
        if (channel == null) return;

        try { await App.Streaming.SubscribeChannelAsync(channel); }
        catch { /* streaming is best-effort */ }

        App.Streaming.NoteReceived -= OnStreamNote;
        App.Streaming.NoteReceived += OnStreamNote;
    }

    private async Task UnsubscribeStreamAsync()
    {
        var channel = _kind switch
        {
            "home"   => "homeTimeline",
            "local"  => "localTimeline",
            "social" => "hybridTimeline",
            "global" => "globalTimeline",
            _        => (string?)null,
        };
        if (channel == null) return;
        try { await App.Streaming.UnsubscribeChannelAsync(channel); }
        catch { /* ignore */ }
    }

    private void OnStreamNote(Note note)
    {
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            // Only prepend while near the top; inserting above while mid-scroll can
            // cause WinUI ListView to snap unexpectedly.
            if (_notes.All(n => n.Id != note.Id) && IsNearTop())
                _notes.Insert(0, note);
        }))
        {
            System.Diagnostics.Debug.WriteLine("TimelinePage: Dispatcher unavailable, dropping streamed note update.");
        }
    }

    private bool IsNearTop()
    {
        var scrollViewer = FindDescendant<ScrollViewer>(NotesList);
        if (scrollViewer == null)
            return true;

        return scrollViewer.VerticalOffset <= 24;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;

            var nested = FindDescendant<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
        => _ = LoadAsync(refresh: true);

    private void LoadMoreButton_Click(object sender, RoutedEventArgs e)
        => _ = LoadAsync(refresh: false);

    private void NotesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Note note)
            Frame.Navigate(typeof(NoteDetailPage), note.Id);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetLoading(bool loading)
    {
        LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !loading;
    }

    private void ShowError(string msg)
    {
        ErrorBar.Message = msg;
        ErrorBar.IsOpen = true;
    }
}
