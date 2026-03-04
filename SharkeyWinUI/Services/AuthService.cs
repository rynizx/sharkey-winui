using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;
using SharkeyWinUI.Models;

namespace SharkeyWinUI.Services;

/// <summary>
/// Persists and restores authentication state.
///
/// Credential storage strategy:
///   • Non-sensitive metadata (server URL, user ID, username) →
///     <see cref="LocalSettingsService"/> (JSON file in LocalAppData).
///   • API token → <see cref="PasswordVault"/> (Windows Credential Manager,
///     DPAPI-encrypted, scoped to the current Windows user account).
///   • Windows Hello enabled flag → LocalSettingsService.
///
/// On subsequent launches the token is only surfaced to the app after either:
///   (a) Windows Hello verification succeeds   (when HelloEnabled == true), or
///   (b) automatic restore without biometrics   (when HelloEnabled == false).
/// </summary>
public class AuthService
{
    // ── LocalSettings keys ────────────────────────────────────────────────────
    private const string KeyServerUrl    = "auth_server_url";
    private const string KeyUserId       = "auth_user_id";
    private const string KeyUsername     = "auth_username";
    private const string KeyHelloEnabled = "auth_hello_enabled";

    private readonly LocalSettingsService _settings = new();

    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>True once the API client has been configured with a valid token.</summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>Server URL (e.g. "https://example.social") — stored in LocalSettings.</summary>
    public string? ServerUrl  => _settings.Get<string>(KeyServerUrl);

    /// <summary>Misskey user ID — stored in LocalSettings.</summary>
    public string? UserId     => _settings.Get<string>(KeyUserId);

    /// <summary>Misskey @username — stored in LocalSettings.</summary>
    public string? Username   => _settings.Get<string>(KeyUsername);

    /// <summary>
    /// True when the user has opted in to Windows Hello protection.
    /// The API token is still stored in PasswordVault regardless; this flag
    /// merely controls whether biometric/PIN verification is required before
    /// the token is retrieved on startup.
    /// </summary>
    public bool HelloEnabled
    {
        get => _settings.Get<bool>(KeyHelloEnabled);
        set => _settings.Set(KeyHelloEnabled, value);
    }

    /// <summary>True when server URL and username are recorded (token may not yet be loaded).</summary>
    public bool HasSavedSession =>
        !string.IsNullOrEmpty(ServerUrl) && !string.IsNullOrEmpty(Username);

    // ── Save ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Persists credentials after a successful login.
    /// The token is stored in PasswordVault; other metadata in LocalSettings.
    /// Also immediately configures the global API client.
    /// </summary>
    public void SaveCredentials(string serverUrl, string token, User user)
    {
        var accountName = BuildAccountName(serverUrl, user.Username);

        _settings.Set(KeyServerUrl, serverUrl.TrimEnd('/'));
        _settings.Set(KeyUserId, user.Id);
        _settings.Set(KeyUsername, user.Username);

        // Always write the token to the vault (DPAPI-encrypted)
        WindowsHelloService.SaveToken(accountName, token);

        ConfigureClient(serverUrl, token);
    }

    // ── Restore ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to silently restore a previous session WITHOUT Hello verification.
    /// Should only be called when <see cref="HelloEnabled"/> is <c>false</c>.
    /// Returns <c>true</c> if a valid token was found and the client configured.
    /// </summary>
    public bool TryRestoreSession()
    {
        if (!HasSavedSession) return false;

        var token = WindowsHelloService.LoadToken(BuildAccountName(ServerUrl!, Username!));
        if (string.IsNullOrEmpty(token)) return false;

        ConfigureClient(ServerUrl!, token);
        return true;
    }

    /// <summary>
    /// Prompts the user for Windows Hello verification, then retrieves the
    /// stored token from PasswordVault and configures the API client.
    /// </summary>
    /// <returns>
    /// <c>true</c> on success.
    /// <c>false</c> when Hello is unavailable, the user cancels, or no token is stored.
    /// </returns>
    public async Task<HelloRestoreResult> TryRestoreWithHelloAsync()
    {
        if (!HasSavedSession)
            return HelloRestoreResult.NoSavedSession;

        var result = await WindowsHelloService.RequestVerificationAsync(
            $"Sign in to Sharkey WinUI as @{Username}");

        return result switch
        {
            UserConsentVerificationResult.Verified => CompleteHelloRestore(),
            UserConsentVerificationResult.Canceled => HelloRestoreResult.Cancelled,
            UserConsentVerificationResult.DeviceNotPresent
                or UserConsentVerificationResult.NotConfiguredForUser
                => HelloRestoreResult.HelloUnavailable,
            _ => HelloRestoreResult.Failed,
        };
    }

    private HelloRestoreResult CompleteHelloRestore()
    {
        var token = WindowsHelloService.LoadToken(BuildAccountName(ServerUrl!, Username!));
        if (string.IsNullOrEmpty(token))
            return HelloRestoreResult.NoSavedSession;

        ConfigureClient(ServerUrl!, token);
        return HelloRestoreResult.Success;
    }

    // ── Sign-out ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes all stored credentials (vault + LocalSettings) and resets the client.
    /// </summary>
    public void SignOut()
    {
        if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(ServerUrl))
            WindowsHelloService.RemoveToken(BuildAccountName(ServerUrl, Username));

        _settings.Remove(KeyServerUrl);
        _settings.Remove(KeyUserId);
        _settings.Remove(KeyUsername);
        _settings.Remove(KeyHelloEnabled);

        IsAuthenticated = false;
        App.ApiClient.Configure(string.Empty, null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ConfigureClient(string serverUrl, string token)
    {
        App.ApiClient.Configure(serverUrl, token);
        IsAuthenticated = true;
    }

    /// <summary>
    /// Vault account name format: "@username@host" for remote users,
    /// "@username@serverHost" for local users.
    /// This keeps multiple accounts (different servers) isolated.
    /// </summary>
    private static string BuildAccountName(string serverUrl, string username)
    {
        var host = new Uri(serverUrl).Host;
        return $"@{username}@{host}";
    }
}

/// <summary>Outcome of <see cref="AuthService.TryRestoreWithHelloAsync"/>.</summary>
public enum HelloRestoreResult
{
    /// <summary>Token retrieved and client configured successfully.</summary>
    Success,
    /// <summary>No saved session found (first run or after sign-out).</summary>
    NoSavedSession,
    /// <summary>The user cancelled the Windows Hello prompt.</summary>
    Cancelled,
    /// <summary>Windows Hello is not set up or unavailable on this device.</summary>
    HelloUnavailable,
    /// <summary>Verification failed for another reason (e.g., too many retries).</summary>
    Failed,
}

