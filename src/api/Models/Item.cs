using System.Text.Json.Serialization;
using System.Text.Json;

namespace WhiskeyAndSmokes.Api.Models;

public class Item
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("captureId")]
    public string CaptureId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // whiskey, wine, cocktail, cigar

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("details")]
    public JsonElement? Details { get; set; }

    [JsonPropertyName("venue")]
    public VenueInfo? Venue { get; set; }

    [JsonPropertyName("photoUrls")]
    public List<string> PhotoUrls { get; set; } = [];

    [JsonPropertyName("aiConfidence")]
    public double? AiConfidence { get; set; }

    [JsonPropertyName("aiSummary")]
    public string? AiSummary { get; set; }

    [JsonPropertyName("userRating")]
    public int? UserRating { get; set; }

    [JsonPropertyName("userNotes")]
    public string? UserNotes { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = ItemStatus.AiDraft;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => UserId;
}

public class VenueInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("placeId")]
    public string? PlaceId { get; set; }
}

public static class ItemType
{
    public const string Whiskey = "whiskey";
    public const string Wine = "wine";
    public const string Cocktail = "cocktail";
    public const string Cigar = "cigar";

    public static readonly string[] All = [Whiskey, Wine, Cocktail, Cigar];
}

public static class ItemStatus
{
    public const string AiDraft = "ai-draft";
    public const string Reviewed = "reviewed";
}
