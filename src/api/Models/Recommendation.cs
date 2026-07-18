using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class RecommendationRequest
{
    [JsonPropertyName("preferences")]
    public string? Preferences { get; set; }

    [JsonPropertyName("menuPhoto")]
    public string? MenuPhoto { get; set; }

    [JsonPropertyName("itemTypes")]
    public List<string>? ItemTypes { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 5;
}

public class RecommendationResponse
{
    [JsonPropertyName("recommendations")]
    public List<RecommendedItem> Recommendations { get; set; } = [];

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }

    [JsonPropertyName("basedOnItems")]
    public List<string> BasedOnItems { get; set; } = [];

    [JsonPropertyName("extractedMenuItems")]
    public List<string>? ExtractedMenuItems { get; set; }
}

public class RecommendedItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("similarToItemId")]
    public string? SimilarToItemId { get; set; }

    [JsonPropertyName("matchedFromMenu")]
    public bool MatchedFromMenu { get; set; }
}

public class RecommendationThread
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("request")]
    public RecommendationRequest Request { get; set; } = new();

    [JsonPropertyName("recommendations")]
    public List<RecommendedItem> Recommendations { get; set; } = [];

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }

    [JsonPropertyName("basedOnItems")]
    public List<string> BasedOnItems { get; set; } = [];

    [JsonPropertyName("extractedMenuItems")]
    public List<string>? ExtractedMenuItems { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => UserId;
}

public class UserRatingProfile
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("topRatedItems")]
    public List<RatedItemSummary> TopRatedItems { get; set; } = [];

    [JsonPropertyName("itemTypePreferences")]
    public Dictionary<string, TypePreference> ItemTypePreferences { get; set; } = [];

    [JsonPropertyName("averageRating")]
    public double AverageRating { get; set; }

    [JsonPropertyName("totalRatedItems")]
    public int TotalRatedItems { get; set; }
}

public class RatedItemSummary
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("rating")]
    public double Rating { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("aiSummary")]
    public string? AiSummary { get; set; }
}

public class TypePreference
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("averageRating")]
    public double AverageRating { get; set; }

    [JsonPropertyName("topRated")]
    public List<string> TopRated { get; set; } = [];
}
