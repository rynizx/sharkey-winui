using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class SearchPage : Page
{
    private readonly ObservableCollection<Note> _notes = new();
    private readonly ObservableCollection<User> _users = new();

    private string _searchMode = "notes"; // "notes" or "users"
    private string? _query;

    // Pagination
    private string? _notesUntilId;
    private int _usersOffset;

    private CancellationTokenSource _cts = new();

    public SearchPage()
    {
        InitializeComponent();
        NotesList.ItemsSource = _notes;
        UsersList.ItemsSource = _users;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    // ── Search type toggle ────────────────────────────────────────────────────

    private void SearchTypeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb) return;

        _searchMode = tb.Tag as string ?? "notes";

        NotesToggle.IsChecked = _searchMode == "notes";
        UsersToggle.IsChecked = _searchMode == "users";

        NotesList.Visibility = _searchMode == "notes" ? Visibility.Visible : Visibility.Collapsed;
        UsersList.Visibility = _searchMode == "users" ? Visibility.Visible : Visibility.Collapsed;

        // Re-run search if there's already a query
        if (!string.IsNullOrWhiteSpace(_query))
            _ = RunSearchAsync(refresh: true, _cts.Token);
    }

    // ── Search submission ─────────────────────────────────────────────────────

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _query = args.QueryText?.Trim();
        if (!string.IsNullOrWhiteSpace(_query))
            _ = RunSearchAsync(refresh: true, _cts.Token);
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _query = SearchBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(_query))
                _ = RunSearchAsync(refresh: true, _cts.Token);
        }
    }

    // ── Load more ─────────────────────────────────────────────────────────────

    private void LoadMoreNotesButton_Click(object sender, RoutedEventArgs e)
        => _ = RunSearchAsync(refresh: false, _cts.Token);

    private void LoadMoreUsersButton_Click(object sender, RoutedEventArgs e)
        => _ = RunSearchAsync(refresh: false, _cts.Token);

    // ── Item click ────────────────────────────────────────────────────────────

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

    // ── Search logic ──────────────────────────────────────────────────────────

    private async Task RunSearchAsync(bool refresh, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_query)) return;

        SetLoading(true);
        ErrorBar.IsOpen = false;

        if (refresh)
        {
            _notes.Clear();
            _users.Clear();
            _notesUntilId = null;
            _usersOffset = 0;
        }

        try
        {
            if (_searchMode == "notes")
            {
                var batch = await App.ApiClient.SearchNotesAsync(
                    _query!, limit: 20, untilId: _notesUntilId, ct: ct);

                foreach (var note in batch) _notes.Add(note);
                if (batch.Count > 0) _notesUntilId = batch[^1].Id;

                LoadMoreNotesButton.Visibility =
                    batch.Count == 20 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                var batch = await App.ApiClient.SearchUsersAsync(
                    _query!, limit: 20, offset: _usersOffset, ct: ct);

                foreach (var user in batch) _users.Add(user);
                _usersOffset += batch.Count;

                LoadMoreUsersButton.Visibility =
                    batch.Count == 20 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        catch (OperationCanceledException) { }
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetLoading(bool loading)
        => LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

    private void ShowError(string msg)
    {
        ErrorBar.Message = msg;
        ErrorBar.IsOpen = true;
    }
}
