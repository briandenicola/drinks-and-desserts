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
    public string Type { get; set; } = string.Empty;

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
    public double? UserRating { get; set; }

    [JsonPropertyName("userNotes")]
    public string? UserNotes { get; set; }

    [JsonPropertyName("journal")]
    public List<JournalEntry> Journal { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = ItemStatus.AiDraft;

    [JsonPropertyName("processedBy")]
    public string ProcessedBy { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => UserId;
}

public class VenueInfo
{
    [JsonPropertyName("venueId")]
    public string? VenueId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("placeId")]
    public string? PlaceId { get; set; }
}

public class JournalEntry
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("source")]
    public string? Source { get; set; }
}

public static class ItemType
{
    public const string Whiskey = "whiskey";
    public const string Wine = "wine";
    public const string Cocktail = "cocktail";
    public const string Vodka = "vodka";
    public const string Gin = "gin";
    public const string Cigar = "cigar";
    public const string Dessert = "dessert";
    public const string Espresso = "espresso";
    public const string Latte = "latte";
    public const string Cappuccino = "cappuccino";
    public const string ColdBrew = "cold-brew";
    public const string PourOver = "pour-over";
    public const string Coffee = "coffee";
    public const string Custom = "custom";

    public static readonly string[] All = [Whiskey, Wine, Cocktail, Vodka, Gin, Cigar, Dessert, Espresso, Latte, Cappuccino, ColdBrew, PourOver, Coffee, Custom];
}

public static class ItemStatus
{
    public const string AiDraft = "ai-draft";
    public const string Reviewed = "reviewed";
    public const string Wishlist = "wishlist";
}

public class WishlistUrlWorkItem
{
    public string ItemId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
