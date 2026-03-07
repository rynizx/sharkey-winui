using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SharkeyWinUI.Helpers;
using SharkeyWinUI.Models;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class AccountSettingsPage : Page
{
    private User? _me;
    private CancellationTokenSource _cts = new();
    private bool _suppressThemeChange;

    public AccountSettingsPage()
    {
        InitializeComponent();
        PopulateAppearanceSection();
    }

    // OnNavigatedTo is defined in the Windows Hello section below

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    // ── Sign out ──────────────────────────────────────────────────────────────

    private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        => await SignOutAsync();

    private async Task SignOutAsync()
    {
        var dialog = new ContentDialog
        {
            Title             = "Sign out",
            Content           = "This will remove your saved credentials. You will need to sign in again.",
            PrimaryButtonText = "Sign out",
            CloseButtonText   = "Cancel",
            DefaultButton     = ContentDialogButton.Close,
            XamlRoot          = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        App.MainWindow!.OnSignedOut();
    }

    // ── Appearance section ────────────────────────────────────────────────────

    private void PopulateAppearanceSection()
    {
        // Theme combo
        var savedTheme = ThemeService.GetSavedThemeValue();
        _suppressThemeChange = true;
        foreach (ComboBoxItem item in ThemeCombo.Items.OfType<ComboBoxItem>())
        {
            if ((string)item.Tag == savedTheme)
            {
                ThemeCombo.SelectedItem = item;
                break;
            }
        }
        _suppressThemeChange = false;

        // Accent colour swatches
        var savedAccent = ThemeService.GetSavedAccentHex();
        foreach (var preset in ThemeService.Presets)
        {
            var isSelected = preset.Hex.Equals(savedAccent, StringComparison.OrdinalIgnoreCase);
            var btn = new Button
            {
                Width = 40,
                Height = 40,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(preset.Color),
                Tag = preset.Hex,
                BorderThickness = isSelected ? new Thickness(3) : new Thickness(0),
                BorderBrush = isSelected
                    ? (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                    : new SolidColorBrush(Colors.Transparent),
            };
            ToolTipService.SetToolTip(btn, preset.Name);

            if (isSelected)
                btn.Content = new FontIcon
                {
                    Glyph = "\uE73E",
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Colors.White),
                };

            btn.Click += AccentSwatch_Click;
            AccentSwatchesPanel.Children.Add(btn);
        }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemeChange) return;
        if (ThemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            ThemeService.SaveAndApplyTheme(tag);
    }

    private void AccentSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button clicked || clicked.Tag is not string hex) return;

        ThemeService.SaveAndApplyAccent(hex);

        // Update visual selection state on all swatches
        foreach (var child in AccentSwatchesPanel.Children.OfType<Button>())
        {
            var isSel = (string)child.Tag == hex;
            child.BorderThickness = isSel ? new Thickness(3) : new Thickness(0);
            child.BorderBrush = isSel
                ? (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                : new SolidColorBrush(Colors.Transparent);
            child.Content = isSel
                ? new FontIcon { Glyph = "\uE73E", FontSize = 16, Foreground = new SolidColorBrush(Colors.White) }
                : null;
        }

        AccentRestartBar.IsOpen = true;
    }

    // ── Load current settings ─────────────────────────────────────────────────

    private async Task LoadAsync(CancellationToken ct)
    {
        SetLoading(true);
        StatusBar.IsOpen = false;
        try
        {
            _me = await App.ApiClient.GetMeAsync(ct);
            PopulateFields(_me);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowStatus(ex.Message, InfoBarSeverity.Error); }
        finally { SetLoading(false); }
    }

    private void PopulateFields(User me)
    {
        // Profile
        DisplayNameBox.Text    = me.Name ?? string.Empty;

        // Show a rendered preview when the display name contains emoji shortcodes
        var nameForPreview = me.Name ?? string.Empty;
        if (me.Emojis.Count > 0 && nameForPreview.Contains(':'))
        {
            DisplayNamePreview.Visibility = Visibility.Visible;
            EmojiTextHelper.SetTextWithEmojis(
                DisplayNamePreview,
                nameForPreview,
                me.Emojis,
                Application.Current.Resources["CaptionTextBlockStyle"] as Microsoft.UI.Xaml.Style,
                (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]);
        }
        else
        {
            DisplayNamePreview.Visibility = Visibility.Collapsed;
        }

        DescriptionBox.Text    = me.Description ?? string.Empty;
        LocationBox.Text       = me.Location ?? string.Empty;

        if (me.Birthday != null &&
            DateTime.TryParse(me.Birthday, out var bd))
        {
            BirthdayPicker.SelectedDate = new DateTimeOffset(bd, TimeSpan.Zero);
        }

        SelectComboByTag(LangBox, me.Lang ?? string.Empty);

        // Privacy
        IsLockedSwitch.IsOn           = me.IsLocked;
        IsExplorableSwitch.IsOn       = me.IsExplorable;
        HideOnlineStatusSwitch.IsOn   = me.HideOnlineStatus;
        PublicReactionsSwitch.IsOn    = me.PublicReactions;
        PreventAiSwitch.IsOn          = me.PreventAiLearning;
        NoCrawleSwitch.IsOn           = me.NoCrawle;
        SelectComboByTag(FollowingVisBox, me.FollowingVisibility ?? "public");
        SelectComboByTag(FollowersVisBox, me.FollowersVisibility ?? "public");

        // Muted words — each item is either string[] or string
        var mutedLines = me.MutedWords
            .Select(item => item is string s ? s :
                            item is IEnumerable<object> arr ? string.Join(" ", arr) :
                            item?.ToString() ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s));
        MutedWordsBox.Text = string.Join(Environment.NewLine, mutedLines);
        MutedInstancesBox.Text = string.Join(Environment.NewLine, me.MutedInstances);
    }

    // ── Profile section ───────────────────────────────────────────────────────

    private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        SetLoading(true);
        StatusBar.IsOpen = false;
        try
        {
            var lang = (LangBox.SelectedItem as ComboBoxItem)?.Tag as string;
            string? birthday = null;
            if (BirthdayPicker.SelectedDate.HasValue)
                birthday = BirthdayPicker.SelectedDate.Value.ToString("yyyy-MM-dd");

            await App.ApiClient.UpdateAccountAsync(new AccountUpdateRequest
            {
                Name        = DisplayNameBox.Text.Trim().NullIfEmpty(),
                Description = DescriptionBox.Text.Trim().NullIfEmpty(),
                Location    = LocationBox.Text.Trim().NullIfEmpty(),
                Birthday    = birthday,
                Lang        = string.IsNullOrEmpty(lang) ? null : lang,
            });
            ShowStatus("Profile saved.", InfoBarSeverity.Success);
        }
        catch (MisskeyApiException ex)
        {
            ShowStatus($"Could not save profile ({(int)ex.StatusCode}): {ex.ResponseBody}", InfoBarSeverity.Error);
        }
        catch (Exception ex) { ShowStatus(ex.Message, InfoBarSeverity.Error); }
        finally { SetLoading(false); }
    }

    // ── Privacy section ───────────────────────────────────────────────────────

    private async void SavePrivacyButton_Click(object sender, RoutedEventArgs e)
    {
        SetLoading(true);
        StatusBar.IsOpen = false;
        try
        {
            var followingVis = (FollowingVisBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "public";
            var followersVis = (FollowersVisBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "public";

            await App.ApiClient.UpdateAccountAsync(new AccountUpdateRequest
            {
                IsLocked           = IsLockedSwitch.IsOn,
                IsExplorable       = IsExplorableSwitch.IsOn,
                HideOnlineStatus   = HideOnlineStatusSwitch.IsOn,
                PublicReactions    = PublicReactionsSwitch.IsOn,
                PreventAiLearning  = PreventAiSwitch.IsOn,
                NoCrawle           = NoCrawleSwitch.IsOn,
                FollowingVisibility = followingVis,
                FollowersVisibility = followersVis,
            });
            ShowStatus("Privacy settings saved.", InfoBarSeverity.Success);
        }
        catch (MisskeyApiException ex)
        {
            ShowStatus($"Could not save privacy settings ({(int)ex.StatusCode}): {ex.ResponseBody}", InfoBarSeverity.Error);
        }
        catch (Exception ex) { ShowStatus(ex.Message, InfoBarSeverity.Error); }
        finally { SetLoading(false); }
    }

    // ── Muted words section ───────────────────────────────────────────────────

    private async void SaveMutedWordsButton_Click(object sender, RoutedEventArgs e)
    {
        SetLoading(true);
        StatusBar.IsOpen = false;
        try
        {
            // Convert UI text into the API format:
            //   - single word -> string (regex / word match)
            //   - multiple words on one line -> string[] (all-words-must-match)
            var mutedWords = MutedWordsBox.Text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select<string, object>(line =>
                {
                    var parts = line.Split(' ',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return parts.Length > 1 ? (object)parts.ToList() : parts[0];
                })
                .ToList();

            var mutedInstances = MutedInstancesBox.Text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(h => h.Trim().ToLowerInvariant())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Distinct()
                .ToList();

            await App.ApiClient.UpdateAccountAsync(new AccountUpdateRequest
            {
                MutedWords     = mutedWords,
                MutedInstances = mutedInstances,
            });
            ShowStatus("Muted words saved.", InfoBarSeverity.Success);
        }
        catch (MisskeyApiException ex)
        {
            ShowStatus($"Could not save ({(int)ex.StatusCode}): {ex.ResponseBody}", InfoBarSeverity.Error);
        }
        catch (Exception ex) { ShowStatus(ex.Message, InfoBarSeverity.Error); }
        finally { SetLoading(false); }
    }

    // ── Security section ──────────────────────────────────────────────────────

    private async void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        StatusBar.IsOpen = false;

        var current = CurrentPasswordBox.Password;
        var newPw   = NewPasswordBox.Password;
        var confirm = ConfirmPasswordBox.Password;
        var totp    = PasswordTwoFactorBox.Text.Trim().NullIfEmpty();

        if (string.IsNullOrEmpty(current))
        {
            ShowStatus("Enter your current password.", InfoBarSeverity.Warning);
            return;
        }
        if (string.IsNullOrEmpty(newPw) || newPw.Length < 8)
        {
            ShowStatus("New password must be at least 8 characters.", InfoBarSeverity.Warning);
            return;
        }
        if (newPw != confirm)
        {
            ShowStatus("New passwords do not match.", InfoBarSeverity.Warning);
            return;
        }

        SetLoading(true);
        try
        {
            await App.ApiClient.ChangePasswordAsync(current, newPw, totp);
            CurrentPasswordBox.Password  = string.Empty;
            NewPasswordBox.Password      = string.Empty;
            ConfirmPasswordBox.Password  = string.Empty;
            PasswordTwoFactorBox.Text    = string.Empty;
            ShowStatus("Password changed successfully.", InfoBarSeverity.Success);
        }
        catch (MisskeyApiException ex)
        {
            ShowStatus($"Could not change password ({(int)ex.StatusCode}): {ex.ResponseBody}", InfoBarSeverity.Error);
        }
        catch (Exception ex) { ShowStatus(ex.Message, InfoBarSeverity.Error); }
        finally { SetLoading(false); }
    }

    private async void ChangeEmailButton_Click(object sender, RoutedEventArgs e)
    {
        StatusBar.IsOpen = false;

        var email    = NewEmailBox.Text.Trim().NullIfEmpty();
        var password = EmailPasswordBox.Password;

        if (string.IsNullOrEmpty(password))
        {
            ShowStatus("Enter your current password to change email.", InfoBarSeverity.Warning);
            return;
        }

        SetLoading(true);
        try
        {
            await App.ApiClient.UpdateEmailAsync(password, email);
            EmailPasswordBox.Password = string.Empty;
            ShowStatus(
                email == null
                    ? "Email address removed."
                    : "Email updated. Check your inbox for a verification link.",
                InfoBarSeverity.Success);
        }
        catch (MisskeyApiException ex)
        {
            ShowStatus($"Could not update email ({(int)ex.StatusCode}): {ex.ResponseBody}", InfoBarSeverity.Error);
        }
        catch (Exception ex) { ShowStatus(ex.Message, InfoBarSeverity.Error); }
        finally { SetLoading(false); }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OpenNotifSettingsButton_Click(object sender, RoutedEventArgs e)
        => Frame.Navigate(typeof(NotificationSettingsPage));

    // ── Windows Hello section ─────────────────────────────────────────────────

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Initialise the Hello toggle before loading the rest of the page
        // so it reflects the current state even if LoadAsync is slow.
        await InitHelloToggleAsync();
        _ = LoadAsync(_cts.Token);
    }

    private async Task InitHelloToggleAsync()
    {
        var available = await WindowsHelloService.IsAvailableAsync();

        if (!available)
        {
            HelloSwitch.IsEnabled = false;
            HelloStatusBar.Severity = InfoBarSeverity.Warning;
            HelloStatusBar.Message  =
                "Windows Hello is not set up on this device. " +
                "Go to Windows Settings → Accounts → Sign-in options to configure it.";
            HelloStatusBar.IsOpen = true;
            return;
        }

        // Suppress the Toggled event while we programmatically set the value
        _suppressHelloToggle = true;
        HelloSwitch.IsOn     = App.AuthService.HelloEnabled;
        _suppressHelloToggle = false;
    }

    private bool _suppressHelloToggle;

    private async void HelloSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressHelloToggle) return;

        var wantEnable = HelloSwitch.IsOn;

        if (wantEnable)
        {
            // Require a successful Hello verification before enabling the lock
            var result = await App.AuthService.TryRestoreWithHelloAsync();
            if (result == HelloRestoreResult.Success)
            {
                App.AuthService.HelloEnabled = true;
                ShowHelloStatus("Windows Hello protection enabled.", InfoBarSeverity.Success);
            }
            else
            {
                // Revert the toggle — verification didn't succeed
                _suppressHelloToggle = true;
                HelloSwitch.IsOn     = false;
                _suppressHelloToggle = false;

                var msg = result switch
                {
                    HelloRestoreResult.Cancelled
                        => "Verification cancelled.",
                    HelloRestoreResult.HelloUnavailable
                        => "Windows Hello is not available.",
                    _
                        => "Windows Hello verification failed. Please try again or check your device sign-in settings.",
                };
                ShowHelloStatus(msg, InfoBarSeverity.Error);
            }
        }
        else
        {
            App.AuthService.HelloEnabled = false;
            ShowHelloStatus("Windows Hello protection disabled.", InfoBarSeverity.Informational);
        }
    }

    private void ShowHelloStatus(string msg, InfoBarSeverity severity)
    {
        HelloStatusBar.Message  = msg;
        HelloStatusBar.Severity = severity;
        HelloStatusBar.IsOpen   = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetLoading(bool loading)
        => LoadingBar.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

    private void ShowStatus(string msg, InfoBarSeverity severity)
    {
        StatusBar.Message  = msg;
        StatusBar.Severity = severity;
        StatusBar.IsOpen   = true;
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag as string == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }
}

// ── String extension ──────────────────────────────────────────────────────────
internal static class AccountSettingsExtensions
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
