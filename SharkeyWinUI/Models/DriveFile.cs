using System.Text.Json.Serialization;

namespace SharkeyWinUI.Models;

/// <summary>
/// Represents a file stored in the Misskey/Sharkey drive.
/// </summary>
public class DriveFile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("md5")]
    public string Md5 { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("isSensitive")]
    public bool IsSensitive { get; set; }

    [JsonPropertyName("blurhash")]
    public string? Blurhash { get; set; }

    [JsonPropertyName("properties")]
    public DriveFileProperties Properties { get; set; } = new();

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("folderId")]
    public string? FolderId { get; set; }

    [JsonPropertyName("folder")]
    public DriveFolder? Folder { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("user")]
    public User? User { get; set; }

    [JsonIgnore]
    public bool IsImage => Type.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsVideo => Type.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsAudio => Type.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
}

public class DriveFileProperties
{
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("orientation")]
    public int? Orientation { get; set; }

    [JsonPropertyName("avgColor")]
    public string? AvgColor { get; set; }
}

public class DriveFolder
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("foldersCount")]
    public int FoldersCount { get; set; }

    [JsonPropertyName("filesCount")]
    public int FilesCount { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("parent")]
    public DriveFolder? Parent { get; set; }
}
