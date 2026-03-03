using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;

namespace SharkeyWinUI.Services;

/// <summary>
/// Wraps two Windows security APIs:
///
///   • <see cref="PasswordVault"/> — encrypts credentials with the current
///     Windows user account (DPAPI).  Survives reboots; cleared on sign-out.
///
///   • <see cref="UserConsentVerifier"/> — prompts for Windows Hello
///     (fingerprint, face, PIN) before revealing stored credentials.
///     Only used when the user has opted in via <c>HelloEnabled</c>.
///
/// Neither API leaks secrets to disk in plain text; both are
/// scoped to the current Windows user session.
/// </summary>
public sealed class WindowsHelloService
{
    // Resource name used for all PasswordVault entries
    private const string VaultResource = "SharkeyWinUI";

    // ── Availability ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the device has a Windows Hello authenticator
    /// (fingerprint reader, IR camera, or PIN) configured and ready.
    /// Always runs on a background thread to avoid blocking the UI.
    /// </summary>
    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            var status = await UserConsentVerifier.CheckAvailabilityAsync()
                .AsTask().ConfigureAwait(false);
            return status == UserConsentVerifierAvailability.Available;
        }
        catch
        {
            // Device does not support the API (e.g., very old Windows build)
            return false;
        }
    }

    // ── Consent verification ──────────────────────────────────────────────────

    /// <summary>
    /// Prompts the user for Windows Hello verification (biometrics / PIN).
    /// Returns <c>true</c> if the user was verified successfully.
    /// </summary>
    /// <param name="message">
    /// Message shown in the Windows Hello dialog, e.g. "Sign in to Sharkey WinUI".
    /// </param>
    public static async Task<UserConsentVerificationResult> RequestVerificationAsync(string message)
    {
        try
        {
            return await UserConsentVerifier.RequestVerificationAsync(message)
                .AsTask().ConfigureAwait(false);
        }
        catch
        {
            return UserConsentVerificationResult.DeviceNotPresent;
        }
    }

    // ── PasswordVault helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Stores an API token in the Windows Credential Manager under the given
    /// <paramref name="accountName"/> (typically "@user@host").
    /// Replaces any existing entry for the same account.
    /// </summary>
    public static void SaveToken(string accountName, string token)
    {
        var vault = new PasswordVault();

        // Remove any stale entry first to avoid duplicates
        RemoveToken(accountName);
        vault.Add(new PasswordCredential(VaultResource, accountName, token));
    }

    /// <summary>
    /// Retrieves the API token for the given <paramref name="accountName"/>.
    /// Returns <c>null</c> if no entry exists.
    /// </summary>
    public static string? LoadToken(string accountName)
    {
        if (string.IsNullOrEmpty(accountName)) return null;
        try
        {
            var vault = new PasswordVault();
            var cred  = vault.Retrieve(VaultResource, accountName);
            cred.RetrievePassword();
            return cred.Password;
        }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x80070490))
        {
            // Element not found (0x80070490) — no credential stored yet
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Removes the vault entry for the given <paramref name="accountName"/>.
    /// No-ops if the entry does not exist.
    /// </summary>
    public static void RemoveToken(string accountName)
    {
        if (string.IsNullOrEmpty(accountName)) return;
        try
        {
            var vault = new PasswordVault();
            var cred  = vault.Retrieve(VaultResource, accountName);
            vault.Remove(cred);
        }
        catch { /* not found — ignore */ }
    }

    /// <summary>
    /// Returns all account names that have a stored token in the vault,
    /// so the UI can list available accounts.
    /// </summary>
    public static IReadOnlyList<string> GetSavedAccounts()
    {
        try
        {
            var vault = new PasswordVault();
            return vault.FindAllByResource(VaultResource)
                        .Select(c => c.UserName)
                        .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
