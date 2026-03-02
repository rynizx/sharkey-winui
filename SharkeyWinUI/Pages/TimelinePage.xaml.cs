using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class TimelinePage : Page
{
    private readonly ObservableCollection<Note> _notes = new();
    private string _kind = "home";
    private CancellationTokenSource _cts = new();

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
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        // Unsubscribe this timeline channel from streaming
        _ = UnsubscribeStreamAsync();
    }

    // ── Load / refresh ────────────────────────────────────────────────────────

    private async Task LoadAsync(bool refresh)
    {
        SetLoading(true);
        ErrorBar.IsOpen = false;

        if (refresh)
        {
            _notes.Clear();
            _untilId = null;
        }

        try
        {
            var batch = await FetchAsync(_untilId, _cts.Token);
            foreach (var note in batch)
                _notes.Add(note);

            if (batch.Count > 0)
                _untilId = batch[^1].Id;

            LoadMoreButton.Visibility = batch.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
            SetLoading(false);
        }
    }

    private Task<List<Note>> FetchAsync(string? untilId, CancellationToken ct) =>
        _kind switch
        {
            "home"   => App.ApiClient.GetHomeTimelineAsync(limit: 30, untilId: untilId, ct: ct),
            "local"  => App.ApiClient.GetLocalTimelineAsync(limit: 30, untilId: untilId, ct: ct),
            "social" => App.ApiClient.GetSocialTimelineAsync(limit: 30, untilId: untilId, ct: ct),
            "global" => App.ApiClient.GetGlobalTimelineAsync(limit: 30, untilId: untilId, ct: ct),
            "bubble" => App.ApiClient.GetBubbleTimelineAsync(limit: 30, untilId: untilId, ct: ct),
            _        => App.ApiClient.GetHomeTimelineAsync(limit: 30, untilId: untilId, ct: ct),
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
        App.Streaming.NoteReceived -= OnStreamNote;
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
        DispatcherQueue.TryEnqueue(() =>
        {
            // Prepend the new note to the top, de-duplicating by ID
            if (_notes.All(n => n.Id != note.Id))
                _notes.Insert(0, note);
        });
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
