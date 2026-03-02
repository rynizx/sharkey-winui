using System.Net.Http.Json;
using System.Text.Json;
using SharkeyWinUI.Models;

namespace SharkeyWinUI.Services;

/// <summary>
/// HTTP client for the Misskey/Sharkey REST API.
/// All requests follow the Misskey API convention: POST /api/{endpoint} with a JSON body.
/// Authentication is provided by including the "i" field (API token) in the request body.
/// </summary>
public class MisskeyApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public string? ServerUrl { get; private set; }
    public string? Token { get; private set; }

    public MisskeyApiClient()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "SharkeyWinUI/1.0");
    }

    /// <summary>
    /// Configures the client with the target server and authentication token.
    /// </summary>
    public void Configure(string serverUrl, string? token)
    {
        ServerUrl = serverUrl.TrimEnd('/');
        Token = token;
    }

    // ── Authentication ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a MiAuth session URL. The user opens this URL in a browser,
    /// grants permission, and then the app calls <see cref="CheckMiAuthAsync"/>.
    /// </summary>
    public (string checkUrl, string browserUrl) GenerateMiAuthSession(string appName, IEnumerable<string> permissions)
    {
        var sessionId = Guid.NewGuid().ToString();
        var permStr = string.Join(",", permissions);
        var browserUrl = $"{ServerUrl}/miauth/{sessionId}?name={Uri.EscapeDataString(appName)}&permission={Uri.EscapeDataString(permStr)}";
        var checkUrl = $"{ServerUrl}/api/miauth/{sessionId}/check";
        return (checkUrl, browserUrl);
    }

    /// <summary>
    /// Checks whether a MiAuth session has been approved and retrieves the token.
    /// </summary>
    public Task<MiAuthCheckResponse> CheckMiAuthAsync(string checkUrl, CancellationToken ct = default)
        => PostRawAsync<MiAuthCheckResponse>(checkUrl, new { }, ct);

    /// <summary>
    /// Fetches the current user's account information.
    /// </summary>
    public Task<User> GetCurrentUserAsync(CancellationToken ct = default)
        => PostAsync<User>("i", new { }, ct);

    // ── Instance ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches instance metadata (name, version, emoji list, rules, etc.).
    /// </summary>
    public Task<InstanceMeta> GetInstanceMetaAsync(CancellationToken ct = default)
        => PostAsync<InstanceMeta>("meta", new { detail = true }, ct);

    /// <summary>
    /// Fetches custom emoji for this instance.
    /// </summary>
    public Task<EmojisResponse> GetEmojisAsync(CancellationToken ct = default)
        => PostAsync<EmojisResponse>("emojis", new { }, ct);

    // ── Timelines ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the authenticated user's home timeline.
    /// </summary>
    public Task<List<Note>> GetHomeTimelineAsync(
        int limit = 20, string? untilId = null, string? sinceId = null, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/timeline", BuildPaginationBody(limit, untilId, sinceId), ct);

    /// <summary>
    /// Fetches the local timeline (notes from this server only).
    /// </summary>
    public Task<List<Note>> GetLocalTimelineAsync(
        int limit = 20, string? untilId = null, string? sinceId = null, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/local-timeline", BuildPaginationBody(limit, untilId, sinceId), ct);

    /// <summary>
    /// Fetches the social (hybrid) timeline: local + followed remote users.
    /// </summary>
    public Task<List<Note>> GetSocialTimelineAsync(
        int limit = 20, string? untilId = null, string? sinceId = null, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/hybrid-timeline", BuildPaginationBody(limit, untilId, sinceId), ct);

    /// <summary>
    /// Fetches the global timeline (all federated notes).
    /// </summary>
    public Task<List<Note>> GetGlobalTimelineAsync(
        int limit = 20, string? untilId = null, string? sinceId = null, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/global-timeline", BuildPaginationBody(limit, untilId, sinceId), ct);

    /// <summary>
    /// Fetches the bubble timeline (Sharkey-specific: notes from servers in the bubble).
    /// </summary>
    public Task<List<Note>> GetBubbleTimelineAsync(
        int limit = 20, string? untilId = null, string? sinceId = null, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/bubble-timeline", BuildPaginationBody(limit, untilId, sinceId), ct);

    /// <summary>
    /// Fetches notes from a specific channel.
    /// </summary>
    public Task<List<Note>> GetChannelTimelineAsync(
        string channelId, int limit = 20, string? untilId = null, string? sinceId = null, CancellationToken ct = default)
        => PostAsync<List<Note>>("channels/timeline", BuildPaginationBody(limit, untilId, sinceId, new { channelId }), ct);

    // ── Notes ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches a single note by ID.
    /// </summary>
    public Task<Note> GetNoteAsync(string noteId, CancellationToken ct = default)
        => PostAsync<Note>("notes/show", new { noteId }, ct);

    /// <summary>
    /// Fetches replies to a note.
    /// </summary>
    public Task<List<Note>> GetNoteRepliesAsync(
        string noteId, int limit = 20, string? untilId = null, string? sinceId = null, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/replies", BuildPaginationBody(limit, untilId, sinceId, new { noteId }), ct);

    /// <summary>
    /// Fetches renotes of a note.
    /// </summary>
    public Task<List<Note>> GetNoteRenotesAsync(
        string noteId, int limit = 20, string? untilId = null, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/renotes", BuildPaginationBody(limit, untilId, null, new { noteId }), ct);

    /// <summary>
    /// Creates a new note.
    /// </summary>
    public Task<CreateNoteResponse> CreateNoteAsync(
        string? text,
        string visibility = "public",
        string? cw = null,
        bool localOnly = false,
        string? replyId = null,
        string? renoteId = null,
        List<string>? fileIds = null,
        bool? sensitive = null,
        string? channelId = null,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["text"] = text, ["visibility"] = visibility };
        if (cw != null) body["cw"] = cw;
        if (localOnly) body["localOnly"] = true;
        if (replyId != null) body["replyId"] = replyId;
        if (renoteId != null) body["renoteId"] = renoteId;
        if (fileIds?.Count > 0) body["fileIds"] = fileIds;
        if (sensitive.HasValue) body["sensitive"] = sensitive.Value;
        if (channelId != null) body["channelId"] = channelId;
        return PostAsync<CreateNoteResponse>("notes/create", body, ct);
    }

    /// <summary>
    /// Deletes a note by ID.
    /// </summary>
    public Task DeleteNoteAsync(string noteId, CancellationToken ct = default)
        => PostAsync<EmptyResponse>("notes/delete", new { noteId }, ct);

    /// <summary>
    /// Searches notes by text query.
    /// </summary>
    public Task<List<Note>> SearchNotesAsync(
        string query, int limit = 20, string? untilId = null, string? sinceId = null,
        string? userId = null, string? channelId = null, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/search", BuildPaginationBody(limit, untilId, sinceId,
            new { query, userId, channelId }), ct);

    // ── Reactions ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a reaction (custom emoji or Unicode) to a note.
    /// </summary>
    public Task AddReactionAsync(string noteId, string reaction, CancellationToken ct = default)
        => PostAsync<EmptyResponse>("notes/reactions/create", new { noteId, reaction }, ct);

    /// <summary>
    /// Removes the current user's reaction from a note.
    /// </summary>
    public Task RemoveReactionAsync(string noteId, CancellationToken ct = default)
        => PostAsync<EmptyResponse>("notes/reactions/delete", new { noteId }, ct);

    /// <summary>
    /// Fetches the list of reactions on a note.
    /// </summary>
    public Task<List<ReactionEntry>> GetNoteReactionsAsync(
        string noteId, string? type = null, int limit = 50, string? untilId = null, CancellationToken ct = default)
        => PostAsync<List<ReactionEntry>>("notes/reactions", BuildPaginationBody(limit, untilId, null,
            new { noteId, type }), ct);

    // ── Users ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches a user by ID or username+host.
    /// </summary>
    public Task<User> GetUserAsync(string? userId = null, string? username = null, string? host = null, CancellationToken ct = default)
        => PostAsync<User>("users/show", new { userId, username, host }, ct);

    /// <summary>
    /// Fetches notes created by a user.
    /// </summary>
    public Task<List<Note>> GetUserNotesAsync(
        string userId, int limit = 20, string? untilId = null, string? sinceId = null,
        bool includeReplies = false, bool includeMyRenotes = true, bool withFiles = false,
        CancellationToken ct = default)
        => PostAsync<List<Note>>("users/notes", BuildPaginationBody(limit, untilId, sinceId,
            new { userId, includeReplies, includeMyRenotes, withFiles }), ct);

    /// <summary>
    /// Fetches users that a given user is following.
    /// </summary>
    public Task<List<FollowEntry>> GetFollowingAsync(
        string userId, int limit = 30, string? untilId = null, CancellationToken ct = default)
        => PostAsync<List<FollowEntry>>("users/following", BuildPaginationBody(limit, untilId, null,
            new { userId }), ct);

    /// <summary>
    /// Fetches a user's followers.
    /// </summary>
    public Task<List<FollowEntry>> GetFollowersAsync(
        string userId, int limit = 30, string? untilId = null, CancellationToken ct = default)
        => PostAsync<List<FollowEntry>>("users/followers", BuildPaginationBody(limit, untilId, null,
            new { userId }), ct);

    // ── Following ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Follows a user.
    /// </summary>
    public Task<FollowResponse> FollowUserAsync(string userId, CancellationToken ct = default)
        => PostAsync<FollowResponse>("following/create", new { userId }, ct);

    /// <summary>
    /// Unfollows a user.
    /// </summary>
    public Task<User> UnfollowUserAsync(string userId, CancellationToken ct = default)
        => PostAsync<User>("following/delete", new { userId }, ct);

    // ── Notifications ─────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the current user's notifications.
    /// </summary>
    public Task<List<Notification>> GetNotificationsAsync(
        int limit = 20, string? untilId = null, string? sinceId = null,
        IEnumerable<string>? includeTypes = null, IEnumerable<string>? excludeTypes = null,
        CancellationToken ct = default)
        => PostAsync<List<Notification>>("i/notifications", BuildPaginationBody(limit, untilId, sinceId,
            new { includeTypes, excludeTypes }), ct);

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    public Task MarkNotificationsReadAsync(CancellationToken ct = default)
        => PostAsync<EmptyResponse>("notifications/mark-all-as-read", new { }, ct);

    // ── Drive ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists files in the current user's drive.
    /// </summary>
    public Task<List<DriveFile>> GetDriveFilesAsync(
        int limit = 20, string? untilId = null, string? folderId = null,
        string? type = null, CancellationToken ct = default)
        => PostAsync<List<DriveFile>>("drive/files", BuildPaginationBody(limit, untilId, null,
            new { folderId, type }), ct);

    /// <summary>
    /// Uploads a file to the current user's drive.
    /// </summary>
    public async Task<DriveFile> UploadFileAsync(
        Stream fileStream, string fileName, string mimeType,
        bool isSensitive = false, string? comment = null, string? folderId = null,
        CancellationToken ct = default)
    {
        var form = new MultipartFormDataContent();
        if (Token != null) form.Add(new StringContent(Token), "i");
        form.Add(new StreamContent(fileStream), "file", fileName);
        form.Add(new StringContent(isSensitive.ToString().ToLower()), "isSensitive");
        if (comment != null) form.Add(new StringContent(comment), "comment");
        if (folderId != null) form.Add(new StringContent(folderId), "folderId");

        var response = await _http.PostAsync($"{ServerUrl}/api/drive/files/create", form, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DriveFile>(_jsonOptions, ct))!;
    }

    // ── ActivityPub ───────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves an ActivityPub object (note or user) by its URI.
    /// This allows viewing content from any compatible Fediverse instance.
    /// </summary>
    public Task<ActivityPubObject> ShowActivityPubObjectAsync(string uri, CancellationToken ct = default)
        => PostAsync<ActivityPubObject>("ap/show", new { uri }, ct);

    /// <summary>
    /// Fetches a remote user's profile via ActivityPub resolution.
    /// </summary>
    public async Task<User?> ResolveRemoteUserAsync(string acctUri, CancellationToken ct = default)
    {
        var result = await ShowActivityPubObjectAsync(acctUri, ct);
        if (result.Type == "User" && result.Object.HasValue)
        {
            return result.Object.Value.Deserialize<User>(_jsonOptions);
        }
        return null;
    }

    /// <summary>
    /// Fetches a remote note via ActivityPub resolution.
    /// </summary>
    public async Task<Note?> ResolveRemoteNoteAsync(string noteUri, CancellationToken ct = default)
    {
        var result = await ShowActivityPubObjectAsync(noteUri, ct);
        if (result.Type == "Note" && result.Object.HasValue)
        {
            return result.Object.Value.Deserialize<Note>(_jsonOptions);
        }
        return null;
    }

    // ── Fetch note by AP/remote URI (Sharkey supports fetching remote notes) ──

    /// <summary>
    /// Searches notes by URI (for ActivityPub lookup).
    /// </summary>
    public Task<List<Note>> SearchNotesByUriAsync(string uri, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/search-by-tag", new { query = uri }, ct);

    // ── Blocking / Muting ────────────────────────────────────────────────────

    /// <summary>Blocks a user.</summary>
    public Task<User> BlockUserAsync(string userId, CancellationToken ct = default)
        => PostAsync<User>("blocking/create", new { userId }, ct);

    /// <summary>Unblocks a user.</summary>
    public Task<User> UnblockUserAsync(string userId, CancellationToken ct = default)
        => PostAsync<User>("blocking/delete", new { userId }, ct);

    /// <summary>Mutes a user.</summary>
    public Task<EmptyResponse> MuteUserAsync(string userId, DateTimeOffset? expiresAt = null, CancellationToken ct = default)
        => PostAsync<EmptyResponse>("mute/create", new { userId, expiresAt }, ct);

    /// <summary>Unmutes a user.</summary>
    public Task<EmptyResponse> UnmuteUserAsync(string userId, CancellationToken ct = default)
        => PostAsync<EmptyResponse>("mute/delete", new { userId }, ct);

    // ── Favourites ────────────────────────────────────────────────────────────

    /// <summary>Adds a note to favourites.</summary>
    public Task<EmptyResponse> FavouriteNoteAsync(string noteId, CancellationToken ct = default)
        => PostAsync<EmptyResponse>("notes/favorites/create", new { noteId }, ct);

    /// <summary>Removes a note from favourites.</summary>
    public Task<EmptyResponse> UnfavouriteNoteAsync(string noteId, CancellationToken ct = default)
        => PostAsync<EmptyResponse>("notes/favorites/delete", new { noteId }, ct);

    // ── Search ────────────────────────────────────────────────────────────────

    /// <summary>Searches users by query.</summary>
    public Task<List<User>> SearchUsersAsync(
        string query, int limit = 10, int offset = 0, bool localOnly = false, CancellationToken ct = default)
        => PostAsync<List<User>>("users/search", new { query, limit, offset, localOnly }, ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private object BuildPaginationBody(int limit, string? untilId, string? sinceId, object? extra = null)
    {
        var dict = new Dictionary<string, object?> { ["limit"] = limit };
        if (untilId != null) dict["untilId"] = untilId;
        if (sinceId != null) dict["sinceId"] = sinceId;

        if (extra != null)
        {
            foreach (var prop in extra.GetType().GetProperties())
            {
                var val = prop.GetValue(extra);
                if (val != null) dict[prop.Name] = val;
            }
        }

        return dict;
    }

    private async Task<T> PostAsync<T>(string endpoint, object body, CancellationToken ct)
    {
        var url = $"{ServerUrl}/api/{endpoint}";
        return await PostRawAsync<T>(url, body, ct);
    }

    private async Task<T> PostRawAsync<T>(string url, object body, CancellationToken ct)
    {
        // Merge the auth token into the request body
        var dict = MergeWithToken(body);
        var response = await _http.PostAsJsonAsync(url, dict, _jsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new MisskeyApiException(response.StatusCode, error);
        }

        // Some endpoints return 204 No Content
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return default!;

        return (await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct))!;
    }

    private Dictionary<string, object?> MergeWithToken(object body)
    {
        var dict = new Dictionary<string, object?>();
        if (Token != null) dict["i"] = Token;

        foreach (var prop in body.GetType().GetProperties())
            dict[prop.Name] = prop.GetValue(body);

        // Handle Dictionary input too
        if (body is Dictionary<string, object?> bodyDict)
        {
            dict.Clear();
            if (Token != null) dict["i"] = Token;
            foreach (var kv in bodyDict) dict[kv.Key] = kv.Value;
        }

        return dict;
    }
}

// ── Supporting response types ────────────────────────────────────────────────

public class CreateNoteResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("createdNote")]
    public Note CreatedNote { get; set; } = null!;
}

public class EmojisResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("emojis")]
    public List<Emoji> Emojis { get; set; } = new();
}

public class ReactionEntry
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("user")]
    public User User { get; set; } = null!;

    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class FollowEntry
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("followerId")]
    public string FollowerId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("followeeId")]
    public string FolloweeId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("follower")]
    public User? Follower { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("followee")]
    public User? Followee { get; set; }
}

public class FollowResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public class EmptyResponse { }

/// <summary>
/// Thrown when the Misskey/Sharkey API returns a non-success status code.
/// </summary>
public class MisskeyApiException : Exception
{
    public System.Net.HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }

    public MisskeyApiException(System.Net.HttpStatusCode statusCode, string responseBody)
        : base($"Misskey API error {(int)statusCode}: {responseBody}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
