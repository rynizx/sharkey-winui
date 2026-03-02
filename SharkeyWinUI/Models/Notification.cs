using System.Text.Json.Serialization;

namespace SharkeyWinUI.Models;

/// <summary>
/// Represents a notification from the Misskey/Sharkey API.
/// </summary>
public class Notification
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("isRead")]
    public bool IsRead { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("user")]
    public User? User { get; set; }

    [JsonPropertyName("note")]
    public Note? Note { get; set; }

    [JsonPropertyName("reaction")]
    public string? Reaction { get; set; }

    [JsonPropertyName("achievement")]
    public string? Achievement { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("header")]
    public string? Header { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// Returns a human-readable summary of this notification.
    /// </summary>
    [JsonIgnore]
    public string Summary => Type switch
    {
        "follow" => $"{User?.EffectiveName ?? "Someone"} followed you",
        "followRequestAccepted" => $"{User?.EffectiveName ?? "Someone"} accepted your follow request",
        "receiveFollowRequest" => $"{User?.EffectiveName ?? "Someone"} sent you a follow request",
        "mention" => $"{User?.EffectiveName ?? "Someone"} mentioned you",
        "reply" => $"{User?.EffectiveName ?? "Someone"} replied to your note",
        "renote" => $"{User?.EffectiveName ?? "Someone"} renoted your note",
        "quote" => $"{User?.EffectiveName ?? "Someone"} quoted your note",
        "reaction" => $"{User?.EffectiveName ?? "Someone"} reacted {Reaction} to your note",
        "pollEnded" => "A poll you voted in has ended",
        "achievementEarned" => $"You earned the achievement: {Achievement}",
        "app" => Header ?? "App notification",
        _ => Type
    };
}
