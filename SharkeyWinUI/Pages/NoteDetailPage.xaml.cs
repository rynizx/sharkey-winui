using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class NoteDetailPage : Page
{
    private readonly ObservableCollection<Note> _replies = new();
    private string? _noteId;
    private string? _repliesUntilId;
    private CancellationTokenSource _cts = new();

    public NoteDetailPage()
    {
        InitializeComponent();
        RepliesList.ItemsSource = _replies;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _noteId = e.Parameter as string;
        if (_noteId != null)
            _ = LoadAsync(_cts.Token);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // Unsubscribe streaming events before cancelling so the handler
        // is never called with a disposed CancellationTokenSource.
        App.Streaming.NoteUpdated -= OnNoteUpdated;
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        if (_noteId != null)
            _ = App.Streaming.UnsubscribeNoteAsync(_noteId);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        SetLoading(true);
        ErrorBar.IsOpen = false;
        _replies.Clear();
        _repliesUntilId = null;

        try
        {
            // Subscribe before the await so we don't miss any updates that
            // arrive while the HTTP request is in flight.
            App.Streaming.NoteUpdated += OnNoteUpdated;
            await App.Streaming.SubscribeNoteAsync(_noteId!);

            var note = await App.ApiClient.GetNoteAsync(_noteId!, ct);
            RootNoteCard.Note = note;

            await LoadRepliesAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (MisskeyApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            ShowError("Note not found — it may have been deleted or is from a private account.");
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

    private async Task LoadRepliesAsync(CancellationToken ct)
    {
        try
        {
            var batch = await App.ApiClient.GetNoteRepliesAsync(
                _noteId!, limit: 20, untilId: _repliesUntilId, ct: ct);

            foreach (var r in batch)
                _replies.Add(r);

            if (batch.Count > 0)
                _repliesUntilId = batch[^1].Id;

            LoadMoreRepliesButton.Visibility =
                batch.Count == 20 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ShowError($"Could not load replies: {ex.Message}");
        }
    }

    private void OnNoteUpdated(NoteUpdatedEvent ev)
    {
        if (ev.Id != _noteId) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            // Reload the note to pick up new reaction counts
            _ = RefreshRootNoteAsync();
        });
    }

    private async Task RefreshRootNoteAsync()
    {
        try
        {
            var note = await App.ApiClient.GetNoteAsync(_noteId!);
            RootNoteCard.Note = note;
        }
        catch { /* best-effort */ }
    }

    private void LoadMoreRepliesButton_Click(object sender, RoutedEventArgs e)
        => _ = LoadRepliesAsync(_cts.Token);

    private void SetLoading(bool loading)
        => LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

    private void ShowError(string msg)
    {
        ErrorBar.Message = msg;
        ErrorBar.IsOpen = true;
    }
}
