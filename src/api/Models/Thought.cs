using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class Thought
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("authorId")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("authorDisplayName")]
    public string AuthorDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("targetUserId")]
    public string TargetUserId { get; set; } = string.Empty;

    [JsonPropertyName("targetType")]
    public string TargetType { get; set; } = string.Empty;

    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = string.Empty;

    [JsonPropertyName("targetName")]
    public string TargetName { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("rating")]
    public int? Rating { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => TargetUserId;
}

public static class ThoughtTargetType
{
    public const string Item = "item";
    public const string Venue = "venue";
}
