using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using SharkeyWinUI.Helpers;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class ProfilePage : Page
{
    private readonly BulkObservableCollection<Note> _notes = new();
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
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        SetLoading(true);
        ErrorBar.IsOpen = false;
        _notes.ReplaceAll(Array.Empty<Note>());
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

            // Load sidebar data in parallel — failures are non-critical
            await Task.WhenAll(
                LoadSidebarFilesAsync(ct),
                LoadActivityChartAsync(ct));
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
        // Banner — show image if available, otherwise keep the accent fallback
        if (!string.IsNullOrEmpty(user.BannerUrl))
        {
            try
            {
                var bannerSource = App.ImageCache.GetBitmapImage(user.BannerUrl, decodePixelWidth: 1200);
                if (bannerSource != null)
                {
                    BannerImage.Source = bannerSource;
                    BannerFallback.Visibility = Visibility.Collapsed;
                }
                else
                {
                    BannerImage.Source = null;
                    BannerFallback.Visibility = Visibility.Visible;
                }
            }
            catch { /* leave fallback visible */ }
        }
        else
        {
            BannerImage.Source = null;
            BannerFallback.Visibility = Visibility.Visible;
        }

        // Avatar
        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            try { AvatarBrush.ImageSource = App.ImageCache.GetBitmapImage(user.AvatarUrl, decodePixelWidth: 160, decodePixelHeight: 160); }
            catch { /* leave blank */ }
        }
        else
        {
            AvatarBrush.ImageSource = null;
        }

        DisplayNameText.Text = user.EffectiveName;
        UsernameText.Text    = user.FullUsername;

        // Bio
        if (!string.IsNullOrWhiteSpace(user.Description))
        {
            BioText.Text = user.Description;
            BioText.Visibility = Visibility.Visible;
        }

        NotesCountText.Text     = user.NotesCount.ToString("N0");
        FollowingCountText.Text = user.FollowingCount.ToString("N0");
        FollowersCountText.Text = user.FollowersCount.ToString("N0");

        // Metadata row
        var hasAnyMeta = false;

        if (!string.IsNullOrWhiteSpace(user.Location))
        {
            LocationText.Text = user.Location;
            LocationItem.Visibility = Visibility.Visible;
            hasAnyMeta = true;
        }

        if (!string.IsNullOrWhiteSpace(user.Birthday))
        {
            BirthdayText.Text = user.Birthday;
            BirthdayItem.Visibility = Visibility.Visible;
            hasAnyMeta = true;
        }

        if (user.CreatedAt.HasValue)
        {
            JoinedText.Text = $"Joined {user.CreatedAt.Value:MMM yyyy}";
            JoinedItem.Visibility = Visibility.Visible;
            hasAnyMeta = true;
        }

        MetaRow.Visibility = hasAnyMeta ? Visibility.Visible : Visibility.Collapsed;

        // Roles / badges
        RolesPanel.Children.Clear();
        var publicRoles = user.Roles.Where(r => r.IsPublic).Take(5).ToList();
        if (publicRoles.Count > 0)
        {
            RolesPanel.Visibility = Visibility.Visible;
            foreach (var role in publicRoles)
            {
                RolesPanel.Children.Add(new Border
                {
                    Padding = new Thickness(8, 3, 8, 3),
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
        FieldsCard.Visibility = user.Fields.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Remote instance badge
        if (user.IsRemote && user.Instance?.Name != null)
        {
            InstanceText.Text = $"From {user.Instance.Name} ({user.Host})";
            InstanceBadge.Visibility = Visibility.Visible;
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

    private async Task LoadSidebarFilesAsync(CancellationToken ct)
    {
        if (_user == null) return;
        try
        {
            // Fetch notes that carry files; collect up to 9 unique image thumbnails.
            var notesWithFiles = await App.ApiClient.GetUserNotesAsync(
                _user.Id, limit: 20, withFiles: true, ct: ct);

            var files = notesWithFiles
                .SelectMany(n => n.Files)
                .Where(f => f.IsImage && !string.IsNullOrEmpty(f.ThumbnailUrl))
                .DistinctBy(f => f.Id)
                .Take(9)
                .ToList();

            if (files.Count > 0)
            {
                FilesPanel.ItemsSource = files;
                FilesCard.Visibility = Visibility.Visible;
            }
        }
        catch { /* sidebar — non-critical */ }
    }

    private async Task LoadActivityChartAsync(CancellationToken ct)
    {
        if (_user == null) return;
        try
        {
            var chart = await App.ApiClient.GetUserNotesChartAsync(_user.Id, limit: 30, ct: ct);

            // Combine local + remote counts; the arrays are newest-first, so reverse for left→right display.
            var counts = chart.Local.Inc
                .Zip(chart.Remote.Inc, (l, r) => l + r)
                .Reverse()
                .ToList();

            if (counts.All(c => c == 0)) return;

            RenderActivityChart(counts);
            ActivityCard.Visibility = Visibility.Visible;
        }
        catch { /* sidebar — non-critical */ }
    }

    private void RenderActivityChart(List<int> counts)
    {
        ActivityCanvas.Children.Clear();

        var max = counts.Max();
        if (max == 0) return;

        var canvasHeight = ActivityCanvas.Height;  // 60 px set in XAML
        const double barGap = 2.0;

        // Canvas width isn't known until layout; use ActualWidth if available, fall back to 276 (300 - 24px padding).
        var totalWidth = ActivityCanvas.ActualWidth > 0 ? ActivityCanvas.ActualWidth : 276.0;
        var barWidth = (totalWidth - barGap * (counts.Count - 1)) / counts.Count;

        var accentBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemAccentColorLight1Brush"];

        for (var i = 0; i < counts.Count; i++)
        {
            var barHeight = Math.Max(2, canvasHeight * counts[i] / max);
            var rect = new Rectangle
            {
                Width = Math.Max(1, barWidth),
                Height = barHeight,
                Fill = accentBrush,
                RadiusX = 2,
                RadiusY = 2,
            };
            Canvas.SetLeft(rect, i * (barWidth + barGap));
            Canvas.SetTop(rect, canvasHeight - barHeight);
            ActivityCanvas.Children.Add(rect);
        }
    }

    private async Task LoadNotesAsync(CancellationToken ct)
    {
        if (_user == null) return;
        try
        {
            var batch = await App.ApiClient.GetUserNotesAsync(
                _user.Id, limit: 20, untilId: _notesUntilId, ct: ct);

            _notes.AddRange(batch);
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
        catch (Exception ex)
        {
            ShowError(ex.Message);
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
        catch (Exception ex)
        {
            ShowError(ex.Message);
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
            catch (Exception ex) { ShowError(ex.Message); }
        }
    }

    private void LoadMoreNotesButton_Click(object sender, RoutedEventArgs e)
        => _ = LoadNotesAsync(_cts.Token);

    private void NotesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Note note)
            Frame.Navigate(typeof(NoteDetailPage), note.Id);
    }

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
