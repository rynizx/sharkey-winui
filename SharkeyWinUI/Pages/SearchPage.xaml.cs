using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class SearchPage : Page
{
    private readonly ObservableCollection<Note> _notes = new();
    private readonly ObservableCollection<User> _users = new();

    private string _lastQuery = string.Empty;
    private string? _notesUntilId;
    private int _usersOffset;

    private CancellationTokenSource _cts = new();

    public SearchPage()
    {
        InitializeComponent();
        NotesList.ItemsSource = _notes;
        UsersList.ItemsSource = _users;
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    // ── Search entry ──────────────────────────────────────────────────────────

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var q = (args.QueryText ?? sender.Text).Trim();
        if (string.IsNullOrWhiteSpace(q)) return;

        _lastQuery = q;
        _ = RunSearchAsync(refresh: true);
    }

    private void ResultsPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Re-run search when the user switches tabs (if a query is active)
        if (!string.IsNullOrEmpty(_lastQuery))
            _ = RunSearchAsync(refresh: true);
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    private async Task RunSearchAsync(bool refresh)
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        ErrorBar.IsOpen = false;
        SetLoading(true);

        try
        {
            if (ResultsPivot.SelectedIndex == 0)
                await SearchNotesAsync(refresh, ct);
            else
                await SearchUsersAsync(refresh, ct);
        }
        catch (OperationCanceledException) { }
        finally { SetLoading(false); }
    }

    private async Task SearchNotesAsync(bool refresh, CancellationToken ct)
    {
        if (refresh) { _notes.Clear(); _notesUntilId = null; }

        try
        {
            var batch = await App.ApiClient.SearchNotesAsync(
                _lastQuery, limit: 20, untilId: _notesUntilId, ct: ct);

            foreach (var n in batch) _notes.Add(n);
            if (batch.Count > 0) _notesUntilId = batch[^1].Id;

            LoadMoreNotesButton.Visibility =
                batch.Count == 20 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { throw; }
        catch (MisskeyApiException ex)
        {
            ShowError($"Search failed ({(int)ex.StatusCode}): {ex.ResponseBody}");
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private async Task SearchUsersAsync(bool refresh, CancellationToken ct)
    {
        if (refresh) { _users.Clear(); _usersOffset = 0; }

        try
        {
            var batch = await App.ApiClient.SearchUsersAsync(
                _lastQuery, limit: 20, offset: _usersOffset, ct: ct);

            foreach (var u in batch) _users.Add(u);
            _usersOffset += batch.Count;

            LoadMoreUsersButton.Visibility =
                batch.Count == 20 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { throw; }
        catch (MisskeyApiException ex)
        {
            ShowError($"Search failed ({(int)ex.StatusCode}): {ex.ResponseBody}");
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void NotesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Note note)
            Frame.Navigate(typeof(NoteDetailPage), note.Id);
    }

    private void UsersList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is User user)
            Frame.Navigate(typeof(ProfilePage), user.Id);
    }

    private void LoadMoreNotesButton_Click(object sender, RoutedEventArgs e)
        => _ = RunSearchAsync(refresh: false);

    private void LoadMoreUsersButton_Click(object sender, RoutedEventArgs e)
        => _ = RunSearchAsync(refresh: false);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetLoading(bool loading)
        => LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

    private void ShowError(string msg)
    {
        ErrorBar.Message = msg;
        ErrorBar.IsOpen  = true;
    }
}
