using System.Text.Json.Serialization;

namespace SharkeyWinUI.Models;

/// <summary>
/// Represents a poll attached to a Misskey/Sharkey note.
/// Mirrors the Misskey API Note.poll schema.
/// </summary>
public class Poll
{
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("multiple")]
    public bool Multiple { get; set; }

    [JsonPropertyName("choices")]
    public List<PollChoice> Choices { get; set; } = new();

    [JsonIgnore]
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;

    [JsonIgnore]
    public int TotalVotes => Choices.Sum(c => c.Votes);
}

public class PollChoice
{
    [JsonPropertyName("isVoted")]
    public bool IsVoted { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("votes")]
    public int Votes { get; set; }
}

/// <summary>
/// Request body for creating a poll alongside a note.
/// </summary>
public class PollCreate
{
    /// <summary>At least 2, at most 10 choices.</summary>
    public List<string> Choices { get; set; } = new();

    public bool Multiple { get; set; }

    /// <summary>Unix timestamp (ms) for when the poll expires. Mutually exclusive with ExpiredAfter.</summary>
    public long? ExpiresAt { get; set; }

    /// <summary>Duration in milliseconds after which the poll expires. Mutually exclusive with ExpiresAt.</summary>
    public long? ExpiredAfter { get; set; }
}
