using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class SearchRequest
{
    [JsonPropertyName("query")]
    [Required]
    [StringLength(1000)]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    [StringLength(20)]
    public string? Mode { get; set; }

    [JsonPropertyName("limit")]
    [Range(1, 50)]
    public int? Limit { get; set; }
}

public class SearchResponse
{
    [JsonPropertyName("interpretedQuery")]
    public SearchInterpretation InterpretedQuery { get; set; } = new();

    [JsonPropertyName("results")]
    public List<SearchResult> Results { get; set; } = [];
}

public class SearchInterpretation
{
    [JsonPropertyName("itemTypes")]
    public List<string> ItemTypes { get; set; } = [];

    [JsonPropertyName("dateRange")]
    public SearchDateRange? DateRange { get; set; }

    [JsonPropertyName("venueHints")]
    public List<string> VenueHints { get; set; } = [];

    [JsonPropertyName("qualityHints")]
    public List<string> QualityHints { get; set; } = [];

    [JsonPropertyName("terms")]
    public List<string> Terms { get; set; } = [];
}

public class SearchDateRange
{
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

public class SearchResult
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("item")]
    public Item? Item { get; set; }

    [JsonPropertyName("venue")]
    public Venue? Venue { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("reasons")]
    public List<string> Reasons { get; set; } = [];
}
