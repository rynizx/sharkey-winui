using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharkeyWinUI.Models;

namespace SharkeyWinUI.Services;

/// <summary>
/// HTTP client for the Misskey/Sharkey REST API.
/// All authenticated requests POST JSON bodies with the "i" field set to the API token.
/// Endpoint reference: https://github.com/misskey-dev/misskey (AGPL-3.0)
/// </summary>
public class MisskeyApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string? ServerUrl { get; private set; }
    public string? Token { get; private set; }

    public MisskeyApiClient()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "SharkeyWinUI/1.0");
    }

    /// <summary>Configures the client with the target server and auth token.</summary>
    public void Configure(string serverUrl, string? token)
    {
        ServerUrl = serverUrl.TrimEnd('/');
        Token = token;
    }

    // ── MiAuth ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a MiAuth session. The returned browser URL should be opened
    /// for the user to grant permission; afterwards call <see cref="CheckMiAuthAsync"/>.
    /// </summary>
    public (string checkUrl, string browserUrl) GenerateMiAuthSession(
        string appName, IEnumerable<string> permissions, string? callbackUrl = null)
    {
        var sessionId = Guid.NewGuid().ToString();
        var permStr = string.Join(",", permissions);
        var query = $"name={Uri.EscapeDataString(appName)}&permission={Uri.EscapeDataString(permStr)}";
        if (callbackUrl != null) query += $"&callback={Uri.EscapeDataString(callbackUrl)}";
        return (
            checkUrl: $"{ServerUrl}/api/miauth/{sessionId}/check",
            browserUrl: $"{ServerUrl}/miauth/{sessionId}?{query}"
        );
    }

    /// <summary>Polls whether a MiAuth session has been approved.</summary>
    public Task<MiAuthCheckResponse> CheckMiAuthAsync(string checkUrl, CancellationToken ct = default)
        => PostRawAsync<MiAuthCheckResponse>(checkUrl, new Dictionary<string, object?>(), ct);

    // ── Current user ──────────────────────────────────────────────────────────

    /// <summary>Returns full MeDetailed info for the authenticated user.</summary>
    public Task<User> GetMeAsync(CancellationToken ct = default)
        => PostAsync<User>("i", EmptyBody(), ct);

    // ── Instance ──────────────────────────────────────────────────────────────

    /// <summary>Returns instance metadata (name, version, rules, emoji, etc.).</summary>
    public Task<InstanceMeta> GetMetaAsync(CancellationToken ct = default)
        => PostAsync<InstanceMeta>("meta", Body("detail", true), ct);

    /// <summary>Returns all custom emoji for this instance.</summary>
    public Task<EmojisResponse> GetEmojisAsync(CancellationToken ct = default)
        => PostAsync<EmojisResponse>("emojis", EmptyBody(), ct);

    // ── Timelines ─────────────────────────────────────────────────────────────

    /// <summary>Home timeline (notes from followed users + self). Requires auth.</summary>
    public Task<List<Note>> GetHomeTimelineAsync(
        int limit = 20, string? untilId = null, string? sinceId = null,
        bool withRenotes = true, bool withFiles = false, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/timeline",
            Paginate(limit, untilId, sinceId,
                ("withRenotes", (object)withRenotes),
                ("withFiles", withFiles)), ct);

    /// <summary>Local timeline (public notes from this server only).</summary>
    public Task<List<Note>> GetLocalTimelineAsync(
        int limit = 20, string? untilId = null, string? sinceId = null,
        bool withRenotes = true, bool withReplies = false, bool withFiles = false,
        CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/local-timeline",
            Paginate(limit, untilId, sinceId,
                ("withRenotes", (object)withRenotes),
                ("withReplies", withReplies),
                ("withFiles", withFiles)), ct);

    /// <summary>Social / hybrid timeline (local + followed remote users).</summary>
    public Task<List<Note>> GetSocialTimelineAsync(
        int limit = 20, string? untilId = null, string? sinceId = null,
        bool withRenotes = true, bool withReplies = false, bool withFiles = false,
        CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/hybrid-timeline",
            Paginate(limit, untilId, sinceId,
                ("withRenotes", (object)withRenotes),
                ("withReplies", withReplies),
                ("withFiles", withFiles)), ct);

    /// <summary>Global timeline (all federated public notes).</summary>
    public Task<List<Note>> GetGlobalTimelineAsync(
        int limit = 20, string? untilId = null, string? sinceId = null,
        bool withRenotes = true, bool withFiles = false, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/global-timeline",
            Paginate(limit, untilId, sinceId,
                ("withRenotes", (object)withRenotes),
                ("withFiles", withFiles)), ct);

    /// <summary>Bubble timeline — Sharkey-specific (notes from bubble servers).</summary>
    public Task<List<Note>> GetBubbleTimelineAsync(
        int limit = 20, string? untilId = null, string? sinceId = null,
        bool withRenotes = true, bool withFiles = false, CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/bubble-timeline",
            Paginate(limit, untilId, sinceId,
                ("withRenotes", (object)withRenotes),
                ("withFiles", withFiles)), ct);

    /// <summary>Timeline for a specific channel.</summary>
    public Task<List<Note>> GetChannelTimelineAsync(
        string channelId, int limit = 20, string? untilId = null, string? sinceId = null,
        CancellationToken ct = default)
        => PostAsync<List<Note>>("channels/timeline",
            Paginate(limit, untilId, sinceId, ("channelId", (object)channelId)), ct);

    // ── Notes ─────────────────────────────────────────────────────────────────

    /// <summary>Returns a single note by ID.</summary>
    public Task<Note> GetNoteAsync(string noteId, CancellationToken ct = default)
        => PostAsync<Note>("notes/show", Body("noteId", noteId), ct);

    /// <summary>Returns replies to a note (paginated).</summary>
    public Task<List<Note>> GetNoteRepliesAsync(
        string noteId, int limit = 20, string? untilId = null, string? sinceId = null,
        CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/replies",
            Paginate(limit, untilId, sinceId, ("noteId", (object)noteId)), ct);

    /// <summary>Returns renotes of a note (paginated).</summary>
    public Task<List<Note>> GetNoteRenotesAsync(
        string noteId, int limit = 20, string? untilId = null,
        CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/renotes",
            Paginate(limit, untilId, null, ("noteId", (object)noteId)), ct);

    /// <summary>
    /// Creates a new note. Visibility values: "public", "home", "followers", "specified".
    /// reactionAcceptance: null | "likeOnly" | "likeOnlyForRemote" |
    ///   "nonSensitiveOnly" | "nonSensitiveOnlyForLocalLikeOnlyForRemote".
    /// </summary>
    public Task<CreateNoteResponse> CreateNoteAsync(
        string? text,
        string visibility = "public",
        List<string>? visibleUserIds = null,
        string? cw = null,
        bool localOnly = false,
        string? reactionAcceptance = null,
        bool noExtractMentions = false,
        bool noExtractHashtags = false,
        bool noExtractEmojis = false,
        string? replyId = null,
        string? renoteId = null,
        List<string>? fileIds = null,
        string? channelId = null,
        PollCreate? poll = null,
        CancellationToken ct = default)
    {
        var body = EmptyBody();
        body["visibility"] = visibility;
        if (text != null) body["text"] = text;
        if (cw != null) body["cw"] = cw;
        if (localOnly) body["localOnly"] = true;
        if (reactionAcceptance != null) body["reactionAcceptance"] = reactionAcceptance;
        if (noExtractMentions) body["noExtractMentions"] = true;
        if (noExtractHashtags) body["noExtractHashtags"] = true;
        if (noExtractEmojis) body["noExtractEmojis"] = true;
        if (replyId != null) body["replyId"] = replyId;
        if (renoteId != null) body["renoteId"] = renoteId;
        if (fileIds?.Count > 0) body["fileIds"] = fileIds;
        if (channelId != null) body["channelId"] = channelId;
        if (visibleUserIds?.Count > 0) body["visibleUserIds"] = visibleUserIds;
        if (poll != null)
        {
            var pollObj = new Dictionary<string, object?> { ["choices"] = poll.Choices, ["multiple"] = poll.Multiple };
            if (poll.ExpiresAt.HasValue) pollObj["expiresAt"] = poll.ExpiresAt.Value;
            if (poll.ExpiredAfter.HasValue) pollObj["expiredAfter"] = poll.ExpiredAfter.Value;
            body["poll"] = pollObj;
        }
        return PostAsync<CreateNoteResponse>("notes/create", body, ct);
    }

    /// <summary>Deletes a note by ID (must be the author).</summary>
    public Task DeleteNoteAsync(string noteId, CancellationToken ct = default)
        => PostVoidAsync("notes/delete", Body("noteId", noteId), ct);

    /// <summary>Votes on a poll choice. choiceIndex is zero-based.</summary>
    public Task VotePollAsync(string noteId, int choiceIndex, CancellationToken ct = default)
        => PostVoidAsync("notes/polls/vote",
            new Dictionary<string, object?> { ["noteId"] = noteId, ["choice"] = choiceIndex }, ct);

    /// <summary>Full-text note search.</summary>
    public Task<List<Note>> SearchNotesAsync(
        string query, int limit = 20, string? untilId = null, string? sinceId = null,
        string? userId = null, string? channelId = null, string? host = null,
        CancellationToken ct = default)
        => PostAsync<List<Note>>("notes/search",
            Paginate(limit, untilId, sinceId,
                ("query", (object)query),
                ("userId", userId!),
                ("channelId", channelId!),
                ("host", host!)), ct);

    // ── Reactions ─────────────────────────────────────────────────────────────

    /// <summary>Adds a reaction (Unicode emoji or :custom_name:) to a note.</summary>
    public Task AddReactionAsync(string noteId, string reaction, CancellationToken ct = default)
        => PostVoidAsync("notes/reactions/create",
            new Dictionary<string, object?> { ["noteId"] = noteId, ["reaction"] = reaction }, ct);

    /// <summary>Removes the current user's reaction from a note.</summary>
    public Task RemoveReactionAsync(string noteId, CancellationToken ct = default)
        => PostVoidAsync("notes/reactions/delete", Body("noteId", noteId), ct);

    /// <summary>Lists reactions on a note with user details.</summary>
    public Task<List<ReactionEntry>> GetNoteReactionsAsync(
        string noteId, string? reactionType = null, int limit = 50, string? untilId = null,
        CancellationToken ct = default)
        => PostAsync<List<ReactionEntry>>("notes/reactions",
            Paginate(limit, untilId, null,
                ("noteId", (object)noteId),
                ("type", reactionType!)), ct);

    // ── Favourites ────────────────────────────────────────────────────────────

    /// <summary>Adds a note to the authenticated user's favourites.</summary>
    public Task FavouriteNoteAsync(string noteId, CancellationToken ct = default)
        => PostVoidAsync("notes/favorites/create", Body("noteId", noteId), ct);

    /// <summary>Removes a note from the authenticated user's favourites.</summary>
    public Task UnfavouriteNoteAsync(string noteId, CancellationToken ct = default)
        => PostVoidAsync("notes/favorites/delete", Body("noteId", noteId), ct);

    // ── Users ─────────────────────────────────────────────────────────────────

    /// <summary>Returns detailed info for a user by ID or username+host.</summary>
    public Task<User> GetUserAsync(
        string? userId = null, string? username = null, string? host = null,
        CancellationToken ct = default)
    {
        var b = EmptyBody();
        if (userId != null) b["userId"] = userId;
        if (username != null) b["username"] = username;
        if (host != null) b["host"] = host;
        return PostAsync<User>("users/show", b, ct);
    }

    /// <summary>Returns notes created by a user.</summary>
    public Task<List<Note>> GetUserNotesAsync(
        string userId, int limit = 20, string? untilId = null, string? sinceId = null,
        bool includeReplies = false, bool includeMyRenotes = true, bool withFiles = false,
        CancellationToken ct = default)
        => PostAsync<List<Note>>("users/notes",
            Paginate(limit, untilId, sinceId,
                ("userId", (object)userId),
                ("includeReplies", includeReplies),
                ("includeMyRenotes", includeMyRenotes),
                ("withFiles", withFiles)), ct);

    /// <summary>
    /// Returns the daily note-count chart for a user.
    /// Arrays in the result are ordered newest-first.
    /// </summary>
    public Task<UserNotesChartData> GetUserNotesChartAsync(
        string userId, int limit = 30, string span = "day",
        CancellationToken ct = default)
        => PostAsync<UserNotesChartData>("charts/user/notes",
            new Dictionary<string, object?> { ["userId"] = userId, ["limit"] = limit, ["span"] = span }, ct);

    /// <summary>Returns the list of users a given user is following.</summary>
    public Task<List<FollowEntry>> GetFollowingAsync(
        string userId, int limit = 30, string? untilId = null, CancellationToken ct = default)
        => PostAsync<List<FollowEntry>>("users/following",
            Paginate(limit, untilId, null, ("userId", (object)userId)), ct);

    /// <summary>Returns the followers of a given user.</summary>
    public Task<List<FollowEntry>> GetFollowersAsync(
        string userId, int limit = 30, string? untilId = null, CancellationToken ct = default)
        => PostAsync<List<FollowEntry>>("users/followers",
            Paginate(limit, untilId, null, ("userId", (object)userId)), ct);

    /// <summary>Searches users by display name / username.</summary>
    public Task<List<User>> SearchUsersAsync(
        string query, int limit = 10, int offset = 0,
        string origin = "combined", CancellationToken ct = default)
        => PostAsync<List<User>>("users/search",
            new Dictionary<string, object?> { ["query"] = query, ["limit"] = limit, ["offset"] = offset, ["origin"] = origin }, ct);

    /// <summary>Returns a follow relationship between two users.</summary>
    public Task<UserRelation> GetUserRelationAsync(string userId, CancellationToken ct = default)
        => PostAsync<UserRelation>("users/relation", Body("userId", userId), ct);

    // ── Following ─────────────────────────────────────────────────────────────

    /// <summary>Follows a user. Returns a stub follow record.</summary>
    public Task<FollowResponse> FollowUserAsync(string userId, CancellationToken ct = default)
        => PostAsync<FollowResponse>("following/create", Body("userId", userId), ct);

    /// <summary>Unfollows a user.</summary>
    public Task UnfollowUserAsync(string userId, CancellationToken ct = default)
        => PostVoidAsync("following/delete", Body("userId", userId), ct);

    /// <summary>Accepts a pending follow request.</summary>
    public Task AcceptFollowRequestAsync(string userId, CancellationToken ct = default)
        => PostVoidAsync("following/requests/accept", Body("userId", userId), ct);

    /// <summary>Rejects a pending follow request.</summary>
    public Task RejectFollowRequestAsync(string userId, CancellationToken ct = default)
        => PostVoidAsync("following/requests/reject", Body("userId", userId), ct);

    // ── Blocking / Muting ────────────────────────────────────────────────────

    /// <summary>Blocks a user.</summary>
    public Task BlockUserAsync(string userId, CancellationToken ct = default)
        => PostVoidAsync("blocking/create", Body("userId", userId), ct);

    /// <summary>Unblocks a user.</summary>
    public Task UnblockUserAsync(string userId, CancellationToken ct = default)
        => PostVoidAsync("blocking/delete", Body("userId", userId), ct);

    /// <summary>Mutes a user (optionally with an expiry).</summary>
    public Task MuteUserAsync(string userId, DateTimeOffset? expiresAt = null, CancellationToken ct = default)
    {
        var b = Body("userId", userId);
        if (expiresAt.HasValue) b["expiresAt"] = expiresAt.Value.ToUnixTimeMilliseconds();
        return PostVoidAsync("mute/create", b, ct);
    }

    /// <summary>Unmutes a user.</summary>
    public Task UnmuteUserAsync(string userId, CancellationToken ct = default)
        => PostVoidAsync("mute/delete", Body("userId", userId), ct);

    // ── Notifications ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the authenticated user's notifications.
    /// Pass includeTypes/excludeTypes to filter by type
    /// (see Misskey notificationTypes in types.ts).
    /// </summary>
    public Task<List<Notification>> GetNotificationsAsync(
        int limit = 20, string? untilId = null, string? sinceId = null,
        IEnumerable<string>? includeTypes = null, IEnumerable<string>? excludeTypes = null,
        bool markAsRead = true, CancellationToken ct = default)
    {
        var b = Paginate(limit, untilId, sinceId);
        b["markAsRead"] = markAsRead;
        var inc = includeTypes?.ToList();
        var exc = excludeTypes?.ToList();
        if (inc?.Count > 0) b["includeTypes"] = inc;
        if (exc?.Count > 0) b["excludeTypes"] = exc;
        return PostAsync<List<Notification>>("i/notifications", b, ct);
    }

    /// <summary>Marks all notifications as read.</summary>
    public Task MarkAllNotificationsReadAsync(CancellationToken ct = default)
        => PostVoidAsync("notifications/mark-all-as-read", EmptyBody(), ct);

    // ── Account settings (i/update) ───────────────────────────────────────────

    /// <summary>
    /// Updates the authenticated user's profile and settings.
    /// Only set properties you want to change on the <paramref name="req"/> object.
    /// Returns the updated MeDetailed user object.
    /// </summary>
    public Task<User> UpdateAccountAsync(AccountUpdateRequest req, CancellationToken ct = default)
    {
        // Serialize to a dict, forwarding only non-null properties
        var json = JsonSerializer.Serialize(req, JsonOpts);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOpts)
                   ?? new Dictionary<string, object?>();
        return PostAsync<User>("i/update", dict, ct);
    }

    /// <summary>
    /// Updates per-notification-type receive configuration (who can send each type).
    /// This is a convenience wrapper that builds an AccountUpdateRequest with only
    /// the notificationRecieveConfig field populated.
    /// </summary>
    public Task<User> UpdateNotificationReceiveConfigAsync(
        NotificationReceiveConfigMap config, CancellationToken ct = default)
        => UpdateAccountAsync(new AccountUpdateRequest { NotificationReceiveConfig = config }, ct);

    /// <summary>
    /// Updates which notification types trigger emails.
    /// Valid values: "mention", "reply", "quote", "reaction", "follow",
    /// "receiveFollowRequest", "groupInvited".
    /// </summary>
    public Task<User> UpdateEmailNotificationTypesAsync(
        IEnumerable<string> types, CancellationToken ct = default)
        => UpdateAccountAsync(new AccountUpdateRequest { EmailNotificationTypes = types.ToList() }, ct);

    // ── Password / Email ──────────────────────────────────────────────────────

    /// <summary>
    /// Changes the current user's password.
    /// Provide the 2FA token if the account has two-factor authentication enabled.
    /// </summary>
    public Task ChangePasswordAsync(
        string currentPassword, string newPassword, string? twoFactorToken = null,
        CancellationToken ct = default)
    {
        var b = new Dictionary<string, object?> { ["currentPassword"] = currentPassword, ["newPassword"] = newPassword };
        if (twoFactorToken != null) b["token"] = twoFactorToken;
        return PostVoidAsync("i/change-password", b, ct);
    }

    /// <summary>
    /// Updates the email address. Requires the current password for verification.
    /// Pass null for email to remove the address (if the server allows it).
    /// </summary>
    public Task<User> UpdateEmailAsync(
        string password, string? email, string? twoFactorToken = null,
        CancellationToken ct = default)
    {
        var b = new Dictionary<string, object?> { ["password"] = password, ["email"] = email };
        if (twoFactorToken != null) b["token"] = twoFactorToken;
        return PostAsync<User>("i/update-email", b, ct);
    }

    // ── Drive ─────────────────────────────────────────────────────────────────

    /// <summary>Lists files in the authenticated user's drive.</summary>
    public Task<List<DriveFile>> GetDriveFilesAsync(
        int limit = 20, string? untilId = null, string? folderId = null,
        string? type = null, CancellationToken ct = default)
        => PostAsync<List<DriveFile>>("drive/files",
            Paginate(limit, untilId, null,
                ("folderId", (object?)folderId!),
                ("type", type!)), ct);

    /// <summary>Uploads a file to the authenticated user's drive.</summary>
    public async Task<DriveFile> UploadDriveFileAsync(
        Stream fileStream, string fileName, string mimeType,
        bool isSensitive = false, string? comment = null, string? folderId = null,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        if (Token != null) form.Add(new StringContent(Token), "i");
        var sc = new StreamContent(fileStream);
        sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        form.Add(sc, "file", fileName);
        form.Add(new StringContent(isSensitive.ToString().ToLowerInvariant()), "isSensitive");
        if (comment != null) form.Add(new StringContent(comment), "comment");
        if (folderId != null) form.Add(new StringContent(folderId), "folderId");

        using var resp = await _http.PostAsync($"{ServerUrl}/api/drive/files/create", form, ct);
        resp.EnsureSuccessStatusCode();
        var file = await resp.Content.ReadFromJsonAsync<DriveFile>(JsonOpts, ct);
        return file ?? throw new MisskeyApiException(
            resp.StatusCode,
            "Drive upload succeeded but the response body was empty or invalid JSON.");
    }

    // ── ActivityPub ───────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves an ActivityPub URI to a local Note or User object.
    /// Uses the /api/ap/show endpoint (requires auth, rate-limited to 30/hour).
    /// </summary>
    public Task<ActivityPubObject> ShowActivityPubObjectAsync(
        string uri, CancellationToken ct = default)
        => PostAsync<ActivityPubObject>("ap/show", Body("uri", uri), ct);

    /// <summary>Resolves and returns a remote user by their AP URI.</summary>
    public async Task<User?> ResolveRemoteUserAsync(string uri, CancellationToken ct = default)
    {
        var obj = await ShowActivityPubObjectAsync(uri, ct);
        if (obj.Type == "User" && obj.Object.HasValue)
            return obj.Object.Value.Deserialize<User>(JsonOpts);
        return null;
    }

    /// <summary>Resolves and returns a remote note by its AP URI.</summary>
    public async Task<Note?> ResolveRemoteNoteAsync(string uri, CancellationToken ct = default)
    {
        var obj = await ShowActivityPubObjectAsync(uri, ct);
        if (obj.Type == "Note" && obj.Object.HasValue)
            return obj.Object.Value.Deserialize<Note>(JsonOpts);
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, object?> EmptyBody() => new();

    private static Dictionary<string, object?> Body<T>(string key, T value)
        => new() { [key] = value };

    /// <summary>
    /// Builds a pagination body dict. Additional key-value tuples are merged in,
    /// ignoring any whose value is null.
    /// </summary>
    private static Dictionary<string, object?> Paginate(
        int limit, string? untilId, string? sinceId,
        params (string key, object? value)[] extra)
    {
        var d = new Dictionary<string, object?> { ["limit"] = limit };
        if (untilId != null) d["untilId"] = untilId;
        if (sinceId != null) d["sinceId"] = sinceId;
        foreach (var (k, v) in extra)
            if (v != null) d[k] = v;
        return d;
    }

    private async Task<T> PostAsync<T>(string endpoint, Dictionary<string, object?> body, CancellationToken ct)
        => await PostRawAsync<T>($"{ServerUrl}/api/{endpoint}", body, ct);

    private async Task PostVoidAsync(string endpoint, Dictionary<string, object?> body, CancellationToken ct)
    {
        if (Token != null) body["i"] = Token;
        using var resp = await _http.PostAsJsonAsync($"{ServerUrl}/api/{endpoint}", body, JsonOpts, ct);
        if (!resp.IsSuccessStatusCode)
            throw new MisskeyApiException(resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
    }

    private async Task<T> PostRawAsync<T>(string url, Dictionary<string, object?> body, CancellationToken ct)
    {
        if (Token != null) body["i"] = Token;
        using var resp = await _http.PostAsJsonAsync(url, body, JsonOpts, ct);
        if (!resp.IsSuccessStatusCode)
            throw new MisskeyApiException(resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
        if (resp.StatusCode == System.Net.HttpStatusCode.NoContent)
            return default!;
        var payload = await resp.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
        if (payload is null)
            throw new MisskeyApiException(
                resp.StatusCode,
                "Response body was empty or invalid JSON.");

        return payload;
    }
}

// ── Response / helper types ──────────────────────────────────────────────────

public class CreateNoteResponse
{
    [JsonPropertyName("createdNote")]
    public Note CreatedNote { get; set; } = null!;
}

public class EmojisResponse
{
    [JsonPropertyName("emojis")]
    public List<Emoji> Emojis { get; set; } = new();
}

public class ReactionEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("user")]
    public User User { get; set; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class FollowEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("followerId")]
    public string FollowerId { get; set; } = string.Empty;

    [JsonPropertyName("followeeId")]
    public string FolloweeId { get; set; } = string.Empty;

    [JsonPropertyName("follower")]
    public User? Follower { get; set; }

    [JsonPropertyName("followee")]
    public User? Followee { get; set; }
}

public class FollowResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Relationship between the authenticated user and another user.
/// Returned by users/relation.
/// </summary>
public class UserRelation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("isFollowing")]
    public bool IsFollowing { get; set; }

    [JsonPropertyName("isFollowed")]
    public bool IsFollowed { get; set; }

    [JsonPropertyName("hasPendingFollowRequestFromYou")]
    public bool HasPendingFollowRequestFromYou { get; set; }

    [JsonPropertyName("hasPendingFollowRequestToYou")]
    public bool HasPendingFollowRequestToYou { get; set; }

    [JsonPropertyName("isBlocking")]
    public bool IsBlocking { get; set; }

    [JsonPropertyName("isBlocked")]
    public bool IsBlocked { get; set; }

    [JsonPropertyName("isMuted")]
    public bool IsMuted { get; set; }

    [JsonPropertyName("isRenoteMuted")]
    public bool IsRenoteMuted { get; set; }
}

/// <summary>Thrown when the Misskey/Sharkey API returns a non-success status code.</summary>
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
