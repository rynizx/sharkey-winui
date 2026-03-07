using System.Text.Json.Serialization;

namespace SharkeyWinUI.Models;

/// <summary>
/// Represents a Misskey/Sharkey channel.
/// </summary>
public class Channel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("bannerUrl")]
    public string? BannerUrl { get; set; }

    [JsonPropertyName("isArchived")]
    public bool IsArchived { get; set; }

    [JsonPropertyName("isSensitive")]
    public bool IsSensitive { get; set; }

    [JsonPropertyName("notesCount")]
    public int NotesCount { get; set; }

    [JsonPropertyName("usersCount")]
    public int UsersCount { get; set; }

    [JsonPropertyName("isFollowing")]
    public bool IsFollowing { get; set; }

    [JsonPropertyName("isFavorited")]
    public bool IsFavorited { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("pinnedNoteIds")]
    public List<string> PinnedNoteIds { get; set; } = new();

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#000000";
}

/// <summary>
/// Represents a Misskey/Sharkey emoji (custom or standard).
/// </summary>
public class Emoji
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = new();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("host")]
    public string? Host { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("license")]
    public string? License { get; set; }

    [JsonPropertyName("isSensitive")]
    public bool IsSensitive { get; set; }

    [JsonPropertyName("localOnly")]
    public bool LocalOnly { get; set; }

    [JsonPropertyName("roleIdsThatCanBeUsedThisEmojiAsReaction")]
    public List<string> RoleIdsThatCanBeUsedThisEmojiAsReaction { get; set; } = new();
}

/// <summary>
/// Represents a Misskey/Sharkey instance's metadata.
/// </summary>
public class InstanceMeta
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("shortName")]
    public string? ShortName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("bannerUrl")]
    public string? BannerUrl { get; set; }

    [JsonPropertyName("maintainerName")]
    public string? MaintainerName { get; set; }

    [JsonPropertyName("maintainerEmail")]
    public string? MaintainerEmail { get; set; }

    [JsonPropertyName("langs")]
    public List<string> Langs { get; set; } = new();

    [JsonPropertyName("tosUrl")]
    public string? TosUrl { get; set; }

    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    [JsonPropertyName("feedbackUrl")]
    public string? FeedbackUrl { get; set; }

    [JsonPropertyName("disableRegistration")]
    public bool DisableRegistration { get; set; }

    [JsonPropertyName("emailRequiredForSignup")]
    public bool EmailRequiredForSignup { get; set; }

    [JsonPropertyName("enableHcaptcha")]
    public bool EnableHcaptcha { get; set; }

    [JsonPropertyName("enableMcaptcha")]
    public bool EnableMcaptcha { get; set; }

    [JsonPropertyName("enableRecaptcha")]
    public bool EnableRecaptcha { get; set; }

    [JsonPropertyName("enableTurnstile")]
    public bool EnableTurnstile { get; set; }

    [JsonPropertyName("swPublickey")]
    public string? SwPublickey { get; set; }

    [JsonPropertyName("themeColor")]
    public string? ThemeColor { get; set; }

    [JsonPropertyName("defaultLightTheme")]
    public string? DefaultLightTheme { get; set; }

    [JsonPropertyName("defaultDarkTheme")]
    public string? DefaultDarkTheme { get; set; }

    [JsonPropertyName("maxNoteTextLength")]
    public int MaxNoteTextLength { get; set; } = 3000;

    [JsonPropertyName("emojis")]
    public List<Emoji> Emojis { get; set; } = new();

    [JsonPropertyName("enableEmail")]
    public bool EnableEmail { get; set; }

    [JsonPropertyName("enableServiceWorker")]
    public bool EnableServiceWorker { get; set; }

    [JsonPropertyName("translatorAvailable")]
    public bool TranslatorAvailable { get; set; }

    [JsonPropertyName("serverRules")]
    public List<string> ServerRules { get; set; } = new();
}

/// <summary>
/// Represents a MiAuth session used for OAuth-like authentication with Misskey/Sharkey.
/// </summary>
public class MiAuthSession
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Represents the response from checking a MiAuth session.
/// </summary>
public class MiAuthCheckResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("user")]
    public User? User { get; set; }
}

/// <summary>
/// Represents an ActivityPub object fetched via /api/ap/show.
/// </summary>
public class ActivityPubObject
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public System.Text.Json.JsonElement? Object { get; set; }
}

/// <summary>Daily note-count chart returned by charts/user/notes. Arrays are newest-first.</summary>
public class UserNotesChartData
{
    [JsonPropertyName("local")]
    public UserNotesChartSeries Local { get; set; } = new();

    [JsonPropertyName("remote")]
    public UserNotesChartSeries Remote { get; set; } = new();
}

/// <summary>One series (local or remote) in a user notes chart.</summary>
public class UserNotesChartSeries
{
    /// <summary>Per-day incremental note counts (newest first).</summary>
    [JsonPropertyName("inc")]
    public List<int> Inc { get; set; } = new();
}
