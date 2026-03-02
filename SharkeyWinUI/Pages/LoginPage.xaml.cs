using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using SharkeyWinUI.Services;

namespace SharkeyWinUI.Pages;

public sealed partial class LoginPage : Page
{
    // MiAuth: stored between "open browser" and "check" steps
    private string? _miAuthCheckUrl;

    // MiAuth permissions requested
    private static readonly string[] MiAuthPermissions =
    [
        "read:account", "write:account",
        "read:notifications", "write:notifications",
        "read:drive", "write:drive",
        "read:favorites", "write:favorites",
        "read:following", "write:following",
        "read:reactions", "write:reactions",
        "write:notes",
    ];

    public LoginPage()
    {
        InitializeComponent();
    }

    // ── MiAuth flow ───────────────────────────────────────────────────────────

    private async void OpenMiAuthButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetServerUrl(out var serverUrl)) return;

        SetBusy(true);
        try
        {
            App.ApiClient.Configure(serverUrl, null);
            var (checkUrl, browserUrl) = App.ApiClient.GenerateMiAuthSession(
                "Sharkey WinUI", MiAuthPermissions);

            _miAuthCheckUrl = checkUrl;
            await Launcher.LaunchUriAsync(new Uri(browserUrl));

            CheckMiAuthButton.IsEnabled = true;
            ShowInfo("Browser opened — approve the request, then click the button below.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void CheckMiAuthButton_Click(object sender, RoutedEventArgs e)
    {
        if (_miAuthCheckUrl == null) return;
        if (!TryGetServerUrl(out var serverUrl)) return;

        SetBusy(true);
        try
        {
            var result = await App.ApiClient.CheckMiAuthAsync(_miAuthCheckUrl);
            if (!result.Ok || result.Token == null || result.User == null)
            {
                ShowError("Not approved yet — please approve in the browser first.");
                return;
            }

            await FinalizeLoginAsync(serverUrl, result.Token, result.User);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ── Token flow ────────────────────────────────────────────────────────────

    private async void TokenLoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetServerUrl(out var serverUrl)) return;

        var token = TokenBox.Password.Trim();
        if (string.IsNullOrEmpty(token))
        {
            ShowError("Please enter an API token.");
            return;
        }

        SetBusy(true);
        try
        {
            App.ApiClient.Configure(serverUrl, token);
            var me = await App.ApiClient.GetMeAsync();
            await FinalizeLoginAsync(serverUrl, token, me);
        }
        catch (MisskeyApiException ex)
        {
            ShowError($"Authentication failed ({(int)ex.StatusCode}): {ex.ResponseBody}");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ── Shared ────────────────────────────────────────────────────────────────

    private async Task FinalizeLoginAsync(string serverUrl, string token, SharkeyWinUI.Models.User user)
    {
        App.AuthService.SaveCredentials(serverUrl, token, user);
        ShowInfo($"Signed in as {user.EffectiveName}. Loading…");
        await Task.Delay(300); // brief pause so the user sees the success message
        App.MainWindow?.OnLoggedIn();
    }

    private bool TryGetServerUrl(out string serverUrl)
    {
        serverUrl = ServerBox.Text.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(serverUrl))
        {
            ShowError("Please enter the URL of your instance.");
            return false;
        }
        if (!serverUrl.StartsWith("https://") && !serverUrl.StartsWith("http://"))
            serverUrl = "https://" + serverUrl;
        return true;
    }

    private void SetBusy(bool busy)
    {
        Spinner.IsActive = busy;
        OpenMiAuthButton.IsEnabled = !busy;
        TokenLoginButton.IsEnabled = !busy;
        ServerBox.IsEnabled = !busy;
    }

    private void ShowError(string msg)
    {
        StatusBar.Severity = InfoBarSeverity.Error;
        StatusBar.Title = "Error";
        StatusBar.Message = msg;
        StatusBar.IsOpen = true;
    }

    private void ShowInfo(string msg, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = string.Empty;
        StatusBar.Message = msg;
        StatusBar.IsOpen = true;
    }
}
