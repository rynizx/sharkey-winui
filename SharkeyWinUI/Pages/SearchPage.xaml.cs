using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using SharkeyWinUI.Helpers;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class SearchPage : Page
{
    private readonly BulkObservableCollection<Note> _notes = new();
    private readonly BulkObservableCollection<User> _users = new();

    private string _lastQuery = string.Empty;
    private string? _notesUntilId;
    private int _usersOffset;

    private CancellationTokenSource _cts = new();
    private readonly object _ctsLock = new();

    public SearchPage()
    {
        Resources["AvatarUrlToImageSourceConverter"] = new AvatarUrlToImageSourceConverter();
        InitializeComponent();
        NotesList.ItemsSource = _notes;
        UsersList.ItemsSource = _users;
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ReplaceCancellationSource();
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
        // Cancel any in-flight request and capture a reference to the new CTS.
        // We compare at the end so that only the latest call clears the loading state.
        var cts = ReplaceCancellationSource();
        var ct = cts.Token;

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
        finally
        {
            // Only clear the loading indicator if this call is still the latest one.
            if (ReferenceEquals(_cts, cts))
                SetLoading(false);
        }
    }

    private async Task SearchNotesAsync(bool refresh, CancellationToken ct)
    {
        if (refresh) { _notes.ReplaceAll(Array.Empty<Note>()); _notesUntilId = null; }

        try
        {
            var batch = await App.ApiClient.SearchNotesAsync(
                _lastQuery, limit: 20, untilId: _notesUntilId, ct: ct);

            _notes.AddRange(batch);
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
        if (refresh) { _users.ReplaceAll(Array.Empty<User>()); _usersOffset = 0; }

        try
        {
            var batch = await App.ApiClient.SearchUsersAsync(
                _lastQuery, limit: 20, offset: _usersOffset, ct: ct);

            _users.AddRange(batch);
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

    private CancellationTokenSource ReplaceCancellationSource()
    {
        CancellationTokenSource current;
        CancellationTokenSource replacement = new();

        lock (_ctsLock)
        {
            current = _cts;
            _cts = replacement;
        }

        current.Cancel();
        current.Dispose();
        return replacement;
    }
}

internal sealed class AvatarUrlToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string avatarUrl || string.IsNullOrWhiteSpace(avatarUrl))
            return null;

        BitmapImage? image = App.ImageCache.GetBitmapImage(
            avatarUrl,
            decodePixelWidth: 96,
            decodePixelHeight: 96);
        return image;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
