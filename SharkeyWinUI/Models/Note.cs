using System.Text.Json.Serialization;

namespace SharkeyWinUI.Models;

/// <summary>
/// Represents a Misskey/Sharkey note (post).
/// </summary>
public class Note
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public User? User { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("cw")]
    public string? ContentWarning { get; set; }

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = "public";

    [JsonPropertyName("localOnly")]
    public bool LocalOnly { get; set; }

    [JsonPropertyName("reactionAcceptance")]
    public string? ReactionAcceptance { get; set; }

    [JsonPropertyName("renoteCount")]
    public int RenoteCount { get; set; }

    [JsonPropertyName("repliesCount")]
    public int RepliesCount { get; set; }

    [JsonPropertyName("reactions")]
    public Dictionary<string, int> Reactions { get; set; } = new();

    [JsonPropertyName("myReaction")]
    public string? MyReaction { get; set; }

    [JsonPropertyName("emojis")]
    public Dictionary<string, string> Emojis { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("fileIds")]
    public List<string> FileIds { get; set; } = new();

    [JsonPropertyName("files")]
    public List<DriveFile> Files { get; set; } = new();

    [JsonPropertyName("replyId")]
    public string? ReplyId { get; set; }

    [JsonPropertyName("reply")]
    public Note? Reply { get; set; }

    [JsonPropertyName("renoteId")]
    public string? RenoteId { get; set; }

    [JsonPropertyName("renote")]
    public Note? Renote { get; set; }

    [JsonPropertyName("mentions")]
    public List<string> Mentions { get; set; } = new();

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("channelId")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("channel")]
    public Channel? Channel { get; set; }

    [JsonPropertyName("poll")]
    public Poll? Poll { get; set; }

    [JsonPropertyName("visibleUserIds")]
    public List<string> VisibleUserIds { get; set; } = new();

    /// <summary>
    /// Returns the note's effective display text (renote text if this is a pure renote).
    /// </summary>
    [JsonIgnore]
    public bool IsPureRenote => Text == null && RenoteId != null;

    [JsonIgnore]
    public string DisplayText => Text ?? string.Empty;

    [JsonIgnore]
    public bool HasContentWarning => !string.IsNullOrEmpty(ContentWarning);

    [JsonIgnore]
    public bool HasMedia => Files.Count > 0;

    [JsonIgnore]
    public bool IsReply => ReplyId != null;

    [JsonIgnore]
    public bool IsRenote => RenoteId != null;

    [JsonIgnore]
    public bool IsFederated => Uri != null && User?.Host != null;
}
