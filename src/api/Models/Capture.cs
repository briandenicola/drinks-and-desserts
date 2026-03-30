using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class Capture
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("photos")]
    public List<string> Photos { get; set; } = [];

    [JsonPropertyName("userNote")]
    public string? UserNote { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = CaptureStatus.Pending;

    [JsonPropertyName("itemIds")]
    public List<string> ItemIds { get; set; } = [];

    [JsonPropertyName("processingError")]
    public string? ProcessingError { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => UserId;
}

public class GeoLocation
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
}

public static class CaptureStatus
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
