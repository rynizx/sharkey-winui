using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

/// <summary>
/// Shown on cold start when the user has previously signed in AND has
/// Windows Hello protection enabled.  The token is only retrieved from
/// PasswordVault after a successful Windows Hello (biometric / PIN) check.
/// </summary>
public sealed partial class WindowsHelloLockPage : Page
{
    // Cancelled when the page is navigated away from, preventing the
    // async-void OnNavigatedTo continuation from running on a "zombie" page.
    // Per Microsoft Learn: https://learn.microsoft.com/en-us/windows/apps/winui/winui3/
    // (Page lifecycle — OnNavigatedFrom)
    private CancellationTokenSource _pageCts = new();

    public WindowsHelloLockPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        PopulateAccountInfo();

        // Auto-prompt on arrival so the user only needs one click.
        // Give the page 200 ms to fully render first.
        try
        {
            await Task.Delay(200, _pageCts.Token);
            await TryHelloUnlockAsync();
        }
        catch (OperationCanceledException)
        {
            // Page was navigated away before the delay completed — stop here.
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _pageCts.Cancel();
        _pageCts.Dispose();
        _pageCts = new CancellationTokenSource();
    }

    private void PopulateAccountInfo()
    {
        var auth = App.AuthService;
        AccountNameText.Text = auth.Username != null ? $"@{auth.Username}" : "Saved account";
        ServerText.Text      = auth.ServerUrl ?? string.Empty;

        // Load avatar from the last known URL if it was cached
        // (best-effort — the page works fine without an avatar)
        try
        {
            var avatarKey = $"cached_avatar_{auth.Username}";
            if (App.AuthService is { } a &&
                Windows.Storage.ApplicationData.Current.LocalSettings
                    .Values[avatarKey] is string avatarUrl &&
                !string.IsNullOrEmpty(avatarUrl))
            {
                AvatarBrush.ImageSource = new BitmapImage(new Uri(avatarUrl));
            }
        }
        catch { /* avatar is cosmetic — ignore */ }
    }

    // ── Windows Hello flow ────────────────────────────────────────────────────

    private async void HelloButton_Click(object sender, RoutedEventArgs e)
        => await TryHelloUnlockAsync();

    private async Task TryHelloUnlockAsync()
    {
        SetBusy(true);
        StatusBar.IsOpen = false;

        var result = await App.AuthService.TryRestoreWithHelloAsync();

        SetBusy(false);

        switch (result)
        {
            case HelloRestoreResult.Success:
                App.MainWindow?.OnLoggedIn();
                break;

            case HelloRestoreResult.Cancelled:
                ShowStatus("Verification cancelled — tap the button to try again.",
                    InfoBarSeverity.Informational);
                break;

            case HelloRestoreResult.HelloUnavailable:
                ShowStatus(
                    "Windows Hello is not available on this device. " +
                    "Sign in with your token instead.",
                    InfoBarSeverity.Warning);
                // Disable Hello and fall back to password-vault auto-restore
                App.AuthService.HelloEnabled = false;
                if (App.AuthService.TryRestoreSession())
                    App.MainWindow?.OnLoggedIn();
                break;

            case HelloRestoreResult.NoSavedSession:
                ShowStatus("No saved session found. Please sign in.",
                    InfoBarSeverity.Warning);
                NavigateToLogin();
                break;

            default:
                ShowStatus("Verification failed. Please try again.",
                    InfoBarSeverity.Error);
                break;
        }
    }

    // ── Fallback / sign-out ───────────────────────────────────────────────────

    private void UseTokenButton_Click(object sender, RoutedEventArgs e)
        => NavigateToLogin();

    private async void SignOutButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            Title = "Sign out",
            Content = "This will remove your saved credentials from this device. You will need to sign in again.",
            PrimaryButtonText = "Sign out",
            CloseButtonText   = "Cancel",
            DefaultButton     = ContentDialogButton.Close,
            XamlRoot          = XamlRoot,
        };

        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            App.AuthService.SignOut();
            NavigateToLogin();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void NavigateToLogin()
        => Frame.Navigate(typeof(LoginPage));

    private void SetBusy(bool busy)
    {
        Spinner.IsActive    = busy;
        HelloButton.IsEnabled = !busy;
    }

    private void ShowStatus(string msg, InfoBarSeverity severity)
    {
        StatusBar.Message  = msg;
        StatusBar.Severity = severity;
        StatusBar.IsOpen   = true;
    }
}
