using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class RatingDistribution
{
    [JsonPropertyName("buckets")]
    public List<RatingBucket> Buckets { get; set; } = [];
}

public class RatingBucket
{
    [JsonPropertyName("rating")]
    public double Rating { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
