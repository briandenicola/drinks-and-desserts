using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class Notification
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("sourceUserId")]
    public string SourceUserId { get; set; } = string.Empty;

    [JsonPropertyName("sourceDisplayName")]
    public string SourceDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("referenceType")]
    public string? ReferenceType { get; set; }

    [JsonPropertyName("referenceId")]
    public string? ReferenceId { get; set; }

    [JsonPropertyName("isRead")]
    public bool IsRead { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => UserId;
}

public static class NotificationType
{
    public const string FriendRequest = "friend-request";
    public const string FriendAccepted = "friend-accepted";
    public const string NewThought = "new-thought";
    public const string WorkflowCompleted = "workflow-completed";
    public const string WorkflowFailed = "workflow-failed";
}
