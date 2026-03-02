using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class ProfilePage : Page
{
    private readonly ObservableCollection<Note> _notes = new();
    private User? _user;
    private UserRelation? _relation;
    private string? _notesUntilId;
    private CancellationTokenSource _cts = new();

    // Navigation parameter: userId string, or null to show own profile
    private string? _userId;

    public ProfilePage()
    {
        InitializeComponent();
        NotesList.ItemsSource = _notes;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _userId = e.Parameter as string ?? App.AuthService.UserId;
        if (string.IsNullOrEmpty(_userId))
        {
            ShowError("No user ID available. Please sign in first.");
            return;
        }
        _ = LoadAsync(_cts.Token);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _cts.Cancel();
        _cts = new CancellationTokenSource();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        SetLoading(true);
        ErrorBar.IsOpen = false;
        _notes.Clear();
        _notesUntilId = null;

        try
        {
            _user = await App.ApiClient.GetUserAsync(userId: _userId, ct: ct);
            PopulateProfile(_user);

            // Load relationship if this isn't the current user's own profile
            var isOwn = _userId == App.AuthService.UserId;
            if (!isOwn && _userId != null)
            {
                try
                {
                    _relation = await App.ApiClient.GetUserRelationAsync(_userId, ct);
                    UpdateFollowButtons();
                }
                catch { /* non-critical */ }
            }
            else
            {
                // Own profile — hide follow/block buttons
                FollowButton.Visibility   = Visibility.Collapsed;
                UnfollowButton.Visibility = Visibility.Collapsed;
                PendingButton.Visibility  = Visibility.Collapsed;
                BlockButton.Visibility    = Visibility.Collapsed;
            }

            await LoadNotesAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (MisskeyApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            ShowError("User not found.");
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

    private void PopulateProfile(User user)
    {
        // Banner
        if (!string.IsNullOrEmpty(user.BannerUrl))
        {
            try { BannerImage.Source = new BitmapImage(new Uri(user.BannerUrl)); }
            catch { /* leave blank */ }
        }

        // Avatar
        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            try { AvatarBrush.ImageSource = new BitmapImage(new Uri(user.AvatarUrl)); }
            catch { /* leave blank */ }
        }

        DisplayNameText.Text = user.EffectiveName;
        UsernameText.Text    = user.FullUsername;
        BioText.Text         = user.Description ?? string.Empty;

        NotesCountText.Text    = user.NotesCount.ToString("N0");
        FollowingCountText.Text = user.FollowingCount.ToString("N0");
        FollowersCountText.Text = user.FollowersCount.ToString("N0");

        // Roles
        RolesPanel.Children.Clear();
        if (user.Roles.Count > 0)
        {
            RolesPanel.Visibility = Visibility.Visible;
            foreach (var role in user.Roles.Where(r => r.IsPublic).Take(5))
            {
                RolesPanel.Children.Add(new Border
                {
                    Padding = new Thickness(6, 2, 6, 2),
                    CornerRadius = new CornerRadius(12),
                    Background = ParseBrush(role.Color) ??
                        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                            "SystemFillColorAttentionBackgroundBrush"],
                    Child = new TextBlock
                    {
                        Text = role.Name,
                        Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style,
                    }
                });
            }
        }

        // Profile fields
        FieldsList.ItemsSource = user.Fields;

        // Remote instance
        if (user.IsRemote && user.Instance?.Name != null)
        {
            InstanceText.Text = $"From {user.Instance.Name} ({user.Host})";
            InstanceText.Visibility = Visibility.Visible;
        }
    }

    private void UpdateFollowButtons()
    {
        if (_relation == null) return;

        FollowButton.Visibility   = Visibility.Collapsed;
        UnfollowButton.Visibility = Visibility.Collapsed;
        PendingButton.Visibility  = Visibility.Collapsed;
        BlockButton.Visibility    = Visibility.Collapsed;

        if (_relation.IsBlocking)
        {
            BlockButton.Visibility = Visibility.Visible;
        }
        else if (_relation.HasPendingFollowRequestFromYou)
        {
            PendingButton.Visibility = Visibility.Visible;
        }
        else if (_relation.IsFollowing)
        {
            UnfollowButton.Visibility = Visibility.Visible;
        }
        else
        {
            FollowButton.Visibility = Visibility.Visible;
        }
    }

    private async Task LoadNotesAsync(CancellationToken ct)
    {
        if (_user == null) return;
        try
        {
            var batch = await App.ApiClient.GetUserNotesAsync(
                _user.Id, limit: 20, untilId: _notesUntilId, ct: ct);

            foreach (var n in batch) _notes.Add(n);
            if (batch.Count > 0) _notesUntilId = batch[^1].Id;

            LoadMoreNotesButton.Visibility =
                batch.Count == 20 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowError($"Could not load notes: {ex.Message}"); }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private async void FollowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_user == null) return;
        FollowButton.IsEnabled = false;
        try
        {
            await App.ApiClient.FollowUserAsync(_user.Id);
            _relation = await App.ApiClient.GetUserRelationAsync(_user.Id);
            UpdateFollowButtons();
        }
        catch (MisskeyApiException ex)
        {
            ShowError($"Could not follow: {ex.ResponseBody}");
        }
        finally { FollowButton.IsEnabled = true; }
    }

    private async void UnfollowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_user == null) return;
        UnfollowButton.IsEnabled = false;
        try
        {
            await App.ApiClient.UnfollowUserAsync(_user.Id);
            _relation = await App.ApiClient.GetUserRelationAsync(_user.Id);
            UpdateFollowButtons();
        }
        catch (MisskeyApiException ex)
        {
            ShowError($"Could not unfollow: {ex.ResponseBody}");
        }
        finally { UnfollowButton.IsEnabled = true; }
    }

    private async void BlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (_user == null) return;
        var dlg = new ContentDialog
        {
            Title = "Unblock user",
            Content = $"Unblock @{_user.Username}?",
            PrimaryButtonText = "Unblock",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                await App.ApiClient.UnblockUserAsync(_user.Id);
                _relation = await App.ApiClient.GetUserRelationAsync(_user.Id);
                UpdateFollowButtons();
            }
            catch (MisskeyApiException ex) { ShowError($"Could not unblock: {ex.ResponseBody}"); }
        }
    }

    private void LoadMoreNotesButton_Click(object sender, RoutedEventArgs e)
        => _ = LoadNotesAsync(_cts.Token);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetLoading(bool loading)
        => LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

    private void ShowError(string msg)
    {
        ErrorBar.Message = msg;
        ErrorBar.IsOpen  = true;
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush? ParseBrush(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                var r = Convert.ToByte(hex[..2], 16);
                var g = Convert.ToByte(hex[2..4], 16);
                var b = Convert.ToByte(hex[4..6], 16);
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, r, g, b));
            }
        }
        catch { /* invalid color — return null */ }
        return null;
    }
}
