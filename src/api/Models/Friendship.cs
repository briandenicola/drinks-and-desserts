using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class Friendship
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("friendId")]
    public string FriendId { get; set; } = string.Empty;

    [JsonPropertyName("friendDisplayName")]
    public string FriendDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("friendEmail")]
    public string? FriendEmail { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = FriendshipStatus.PendingSent;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => UserId;
}

public static class FriendshipStatus
{
    public const string PendingSent = "pending-sent";
    public const string PendingReceived = "pending-received";
    public const string Accepted = "accepted";
    public const string Declined = "declined";
    public const string Blocked = "blocked";
}
