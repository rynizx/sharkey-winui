using System.Text.Json.Serialization;

namespace SharkeyWinUI.Models;

/// <summary>
/// Represents a Misskey/Sharkey user (local or federated).
/// </summary>
public class User
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The remote host for federated (ActivityPub) users. Null for local users.
    /// </summary>
    [JsonPropertyName("host")]
    public string? Host { get; set; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("avatarBlurhash")]
    public string? AvatarBlurhash { get; set; }

    [JsonPropertyName("avatarDecorations")]
    public List<AvatarDecoration> AvatarDecorations { get; set; } = new();

    [JsonPropertyName("isBot")]
    public bool IsBot { get; set; }

    [JsonPropertyName("isCat")]
    public bool IsCat { get; set; }

    [JsonPropertyName("instance")]
    public InstanceLite? Instance { get; set; }

    [JsonPropertyName("emojis")]
    public Dictionary<string, string> Emojis { get; set; } = new();

    [JsonPropertyName("onlineStatus")]
    public string? OnlineStatus { get; set; }

    // Detailed fields (only present when fetching full user profile)

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("movedTo")]
    public string? MovedTo { get; set; }

    [JsonPropertyName("alsoKnownAs")]
    public List<string>? AlsoKnownAs { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("lastFetchedAt")]
    public DateTimeOffset? LastFetchedAt { get; set; }

    [JsonPropertyName("bannerUrl")]
    public string? BannerUrl { get; set; }

    [JsonPropertyName("bannerBlurhash")]
    public string? BannerBlurhash { get; set; }

    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }

    [JsonPropertyName("isSilenced")]
    public bool IsSilenced { get; set; }

    [JsonPropertyName("isSuspended")]
    public bool IsSuspended { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("birthday")]
    public string? Birthday { get; set; }

    [JsonPropertyName("lang")]
    public string? Lang { get; set; }

    [JsonPropertyName("fields")]
    public List<UserField> Fields { get; set; } = new();

    [JsonPropertyName("verifiedLinks")]
    public List<string> VerifiedLinks { get; set; } = new();

    [JsonPropertyName("followersCount")]
    public int FollowersCount { get; set; }

    [JsonPropertyName("followingCount")]
    public int FollowingCount { get; set; }

    [JsonPropertyName("notesCount")]
    public int NotesCount { get; set; }

    [JsonPropertyName("pinnedNoteIds")]
    public List<string> PinnedNoteIds { get; set; } = new();

    [JsonPropertyName("pinnedNotes")]
    public List<Note> PinnedNotes { get; set; } = new();

    [JsonPropertyName("roles")]
    public List<Role> Roles { get; set; } = new();

    [JsonPropertyName("memo")]
    public string? Memo { get; set; }

    [JsonPropertyName("followingVisibility")]
    public string? FollowingVisibility { get; set; }

    [JsonPropertyName("followersVisibility")]
    public string? FollowersVisibility { get; set; }

    // ── MeDetailed-only fields (present when fetching own profile via i/) ──────

    [JsonPropertyName("isExplorable")]
    public bool IsExplorable { get; set; }

    [JsonPropertyName("hideOnlineStatus")]
    public bool HideOnlineStatus { get; set; }

    [JsonPropertyName("publicReactions")]
    public bool PublicReactions { get; set; }

    [JsonPropertyName("preventAiLearning")]
    public bool PreventAiLearning { get; set; }

    [JsonPropertyName("noCrawle")]
    public bool NoCrawle { get; set; }

    /// <summary>
    /// Muted word rules. Each element is either a <c>string</c> (single term /
    /// regex) or a <c>List&lt;object&gt;</c> (all words must match).
    /// </summary>
    [JsonPropertyName("mutedWords")]
    public List<object> MutedWords { get; set; } = new();

    [JsonPropertyName("mutedInstances")]
    public List<string> MutedInstances { get; set; } = new();

    /// <summary>Per-notification-type receive configuration (MeDetailed only).</summary>
    /// <remarks>
    /// The JSON field name is "notificationRecieveConfig" — this is a deliberate
    /// spelling preserved from the Misskey API for wire-format compatibility.
    /// </remarks>
    [JsonPropertyName("notificationRecieveConfig")]
    public NotificationReceiveConfigMap? NotificationReceiveConfig { get; set; }

    /// <summary>Notification types that trigger emails (MeDetailed only).</summary>
    [JsonPropertyName("emailNotificationTypes")]
    public List<string> EmailNotificationTypes { get; set; } = new();

    // ── Relationship fields (present in some endpoints) ───────────────────────
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

    /// <summary>
    /// Fully qualified username in user@host format. Returns just username for local users.
    /// </summary>
    [JsonIgnore]
    public string FullUsername => Host != null ? $"@{Username}@{Host}" : $"@{Username}";

    [JsonIgnore]
    public string EffectiveName => DisplayName ?? Username;

    /// <summary>Alias for <see cref="DisplayName"/> — matches the Misskey API field name.</summary>
    [JsonIgnore]
    public string? Name => DisplayName;

    [JsonIgnore]
    public bool IsRemote => Host != null;
}

public class UserField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class AvatarDecoration
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("angle")]
    public double Angle { get; set; }

    [JsonPropertyName("flipH")]
    public bool FlipH { get; set; }
}

/// <summary>
/// Lightweight instance info attached to remote users.
/// </summary>
public class InstanceLite
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("softwareName")]
    public string? SoftwareName { get; set; }

    [JsonPropertyName("softwareVersion")]
    public string? SoftwareVersion { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("faviconUrl")]
    public string? FaviconUrl { get; set; }

    [JsonPropertyName("themeColor")]
    public string? ThemeColor { get; set; }
}

public class Role
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; }
}
