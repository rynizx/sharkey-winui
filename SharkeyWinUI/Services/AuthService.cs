using Windows.Storage;
using SharkeyWinUI.Models;

namespace SharkeyWinUI.Services;

/// <summary>
/// Persists and restores authentication state (server URL + API token) using
/// Windows local application settings.
/// </summary>
public class AuthService
{
    private const string KeyServerUrl = "auth_server_url";
    private const string KeyToken = "auth_token";
    private const string KeyUserId = "auth_user_id";
    private const string KeyUsername = "auth_username";

    private readonly ApplicationDataContainer _settings =
        ApplicationData.Current.LocalSettings;

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(ServerUrl);

    public string? ServerUrl => _settings.Values[KeyServerUrl] as string;
    public string? Token => _settings.Values[KeyToken] as string;
    public string? UserId => _settings.Values[KeyUserId] as string;
    public string? Username => _settings.Values[KeyUsername] as string;

    /// <summary>
    /// Saves credentials and configures the global API client.
    /// </summary>
    public void SaveCredentials(string serverUrl, string token, User user)
    {
        _settings.Values[KeyServerUrl] = serverUrl.TrimEnd('/');
        _settings.Values[KeyToken] = token;
        _settings.Values[KeyUserId] = user.Id;
        _settings.Values[KeyUsername] = user.Username;

        App.ApiClient.Configure(serverUrl, token);
    }

    /// <summary>
    /// Restores credentials from local settings and configures the API client.
    /// Returns true if credentials were found.
    /// </summary>
    public bool TryRestoreSession()
    {
        var url = ServerUrl;
        var token = Token;
        if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(token))
        {
            App.ApiClient.Configure(url, token);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all stored credentials and resets the API client.
    /// </summary>
    public void SignOut()
    {
        _settings.Values.Remove(KeyServerUrl);
        _settings.Values.Remove(KeyToken);
        _settings.Values.Remove(KeyUserId);
        _settings.Values.Remove(KeyUsername);
        App.ApiClient.Configure(string.Empty, null);
    }
}
