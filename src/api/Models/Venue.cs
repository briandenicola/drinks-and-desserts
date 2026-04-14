using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class Venue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = VenueType.Restaurant;

    [JsonPropertyName("rating")]
    public double? Rating { get; set; }

    [JsonPropertyName("photoUrls")]
    public List<string> PhotoUrls { get; set; } = [];

    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }

    [JsonPropertyName("placeId")]
    public string? PlaceId { get; set; }

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = VenueStatus.Completed;

    [JsonPropertyName("workflowSteps")]
    public List<WorkflowStep> WorkflowSteps { get; set; } = [];

    [JsonPropertyName("processingError")]
    public string? ProcessingError { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => UserId;
}

public static class VenueStatus
{
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public static class VenueType
{
    public const string Bar = "bar";
    public const string Lounge = "lounge";
    public const string Restaurant = "restaurant";
    public const string Other = "other";

    public static readonly string[] All = [Bar, Lounge, Restaurant, Other];
}

public class VenueUrlWorkItem
{
    public string VenueId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
