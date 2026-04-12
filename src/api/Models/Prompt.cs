using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class Prompt
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedBy")]
    public string? UpdatedBy { get; set; }

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => "prompts";
}

public static class PromptIds
{
    public const string AgentInstructions = "agent-instructions";
    public const string VisionAnalyst = "vision-analyst";
    public const string DomainExpert = "domain-expert";
    public const string DataCurator = "data-curator";
    public const string WishlistUrlExtractor = "wishlist-url-extractor";
    public const string VenueUrlExtractor = "venue-url-extractor";
}
