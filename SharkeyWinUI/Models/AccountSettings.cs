using System.Text.Json.Serialization;

namespace SharkeyWinUI.Models;

/// <summary>
/// Controls who can send a given notification type to the current user.
/// Mirrors the Misskey notificationRecieveConfig schema (note: Misskey spells it "Recieve").
/// </summary>
public class NotificationReceiveConfig
{
    /// <summary>
    /// One of: "all", "following", "follower", "mutualFollow",
    /// "followingOrFollower", "never", or "list".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "all";

    /// <summary>Only set when Type == "list".</summary>
    [JsonPropertyName("userListId")]
    public string? UserListId { get; set; }
}

/// <summary>
/// Per-notification-type receive configuration, matching the full set of
/// Misskey notification types (types.ts notificationTypes array).
/// </summary>
public class NotificationReceiveConfigMap
{
    [JsonPropertyName("note")]
    public NotificationReceiveConfig? Note { get; set; }

    [JsonPropertyName("follow")]
    public NotificationReceiveConfig? Follow { get; set; }

    [JsonPropertyName("mention")]
    public NotificationReceiveConfig? Mention { get; set; }

    [JsonPropertyName("reply")]
    public NotificationReceiveConfig? Reply { get; set; }

    [JsonPropertyName("renote")]
    public NotificationReceiveConfig? Renote { get; set; }

    [JsonPropertyName("quote")]
    public NotificationReceiveConfig? Quote { get; set; }

    [JsonPropertyName("reaction")]
    public NotificationReceiveConfig? Reaction { get; set; }

    [JsonPropertyName("pollEnded")]
    public NotificationReceiveConfig? PollEnded { get; set; }

    [JsonPropertyName("scheduledNotePosted")]
    public NotificationReceiveConfig? ScheduledNotePosted { get; set; }

    [JsonPropertyName("scheduledNotePostFailed")]
    public NotificationReceiveConfig? ScheduledNotePostFailed { get; set; }

    [JsonPropertyName("receiveFollowRequest")]
    public NotificationReceiveConfig? ReceiveFollowRequest { get; set; }

    [JsonPropertyName("followRequestAccepted")]
    public NotificationReceiveConfig? FollowRequestAccepted { get; set; }

    [JsonPropertyName("roleAssigned")]
    public NotificationReceiveConfig? RoleAssigned { get; set; }

    [JsonPropertyName("chatRoomInvitationReceived")]
    public NotificationReceiveConfig? ChatRoomInvitationReceived { get; set; }

    [JsonPropertyName("achievementEarned")]
    public NotificationReceiveConfig? AchievementEarned { get; set; }

    [JsonPropertyName("exportCompleted")]
    public NotificationReceiveConfig? ExportCompleted { get; set; }

    [JsonPropertyName("login")]
    public NotificationReceiveConfig? Login { get; set; }

    [JsonPropertyName("createToken")]
    public NotificationReceiveConfig? CreateToken { get; set; }

    [JsonPropertyName("app")]
    public NotificationReceiveConfig? App { get; set; }

    [JsonPropertyName("test")]
    public NotificationReceiveConfig? Test { get; set; }
}

/// <summary>
/// Request body for the i/update endpoint.
/// Only include fields you want to change; omitted fields are left unchanged.
/// </summary>
public class AccountUpdateRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("followedMessage")]
    public string? FollowedMessage { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    /// <summary>Birthday in YYYY-MM-DD format, or null to clear.</summary>
    [JsonPropertyName("birthday")]
    public string? Birthday { get; set; }

    /// <summary>BCP-47 language tag, or null to clear.</summary>
    [JsonPropertyName("lang")]
    public string? Lang { get; set; }

    [JsonPropertyName("avatarId")]
    public string? AvatarId { get; set; }

    [JsonPropertyName("bannerId")]
    public string? BannerId { get; set; }

    /// <summary>Whether the account requires a follow request (locked account).</summary>
    [JsonPropertyName("isLocked")]
    public bool? IsLocked { get; set; }

    /// <summary>Whether the account appears in explore/trending.</summary>
    [JsonPropertyName("isExplorable")]
    public bool? IsExplorable { get; set; }

    /// <summary>Hides the online/active status from other users.</summary>
    [JsonPropertyName("hideOnlineStatus")]
    public bool? HideOnlineStatus { get; set; }

    /// <summary>Whether reactions are publicly visible.</summary>
    [JsonPropertyName("publicReactions")]
    public bool? PublicReactions { get; set; }

    /// <summary>Marks the account as a bot.</summary>
    [JsonPropertyName("isBot")]
    public bool? IsBot { get; set; }

    /// <summary>Enables the cat-ear decoration (isCat).</summary>
    [JsonPropertyName("isCat")]
    public bool? IsCat { get; set; }

    /// <summary>Visibility of the following list: "public", "followers", or "private".</summary>
    [JsonPropertyName("followingVisibility")]
    public string? FollowingVisibility { get; set; }

    /// <summary>Visibility of the followers list: "public", "followers", or "private".</summary>
    [JsonPropertyName("followersVisibility")]
    public string? FollowersVisibility { get; set; }

    /// <summary>Prevents AI training services from crawling the account.</summary>
    [JsonPropertyName("preventAiLearning")]
    public bool? PreventAiLearning { get; set; }

    /// <summary>Prevents search engine indexing.</summary>
    [JsonPropertyName("noCrawle")]
    public bool? NoCrawle { get; set; }

    /// <summary>
    /// Muted word lists. Each item is either a string (regex) or a
    /// string[] (word array — all words must match).
    /// </summary>
    [JsonPropertyName("mutedWords")]
    public List<object>? MutedWords { get; set; }

    /// <summary>Muted remote instance hostnames.</summary>
    [JsonPropertyName("mutedInstances")]
    public List<string>? MutedInstances { get; set; }

    /// <summary>Per-notification-type receive configuration.</summary>
    [JsonPropertyName("notificationRecieveConfig")]
    public NotificationReceiveConfigMap? NotificationReceiveConfig { get; set; }

    /// <summary>
    /// Which notification types trigger emails.
    /// Valid values: "mention", "reply", "quote", "reaction", "follow",
    /// "receiveFollowRequest", "groupInvited".
    /// </summary>
    [JsonPropertyName("emailNotificationTypes")]
    public List<string>? EmailNotificationTypes { get; set; }

    /// <summary>Custom profile fields (up to 16).</summary>
    [JsonPropertyName("fields")]
    public List<UserField>? Fields { get; set; }
}
