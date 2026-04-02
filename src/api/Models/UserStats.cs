using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class UserStats
{
    [JsonPropertyName("overview")]
    public OverviewStats Overview { get; set; } = new();

    [JsonPropertyName("typeBreakdown")]
    public List<TypeCount> TypeBreakdown { get; set; } = [];

    [JsonPropertyName("avgRatingByType")]
    public List<TypeRating> AvgRatingByType { get; set; } = [];

    [JsonPropertyName("topRated")]
    public List<TopItem> TopRated { get; set; } = [];

    [JsonPropertyName("topVenues")]
    public List<VenueCount> TopVenues { get; set; } = [];

    [JsonPropertyName("topTags")]
    public List<TagCount> TopTags { get; set; } = [];

    [JsonPropertyName("activityByDay")]
    public List<DayActivity> ActivityByDay { get; set; } = [];

    [JsonPropertyName("monthlyTrend")]
    public List<MonthlyCount> MonthlyTrend { get; set; } = [];

    [JsonPropertyName("ratingTrend")]
    public List<MonthlyRating> RatingTrend { get; set; } = [];
}

public class OverviewStats
{
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("totalCaptures")]
    public int TotalCaptures { get; set; }

    [JsonPropertyName("totalRated")]
    public int TotalRated { get; set; }

    [JsonPropertyName("uniqueBrands")]
    public int UniqueBrands { get; set; }

    [JsonPropertyName("uniqueVenues")]
    public int UniqueVenues { get; set; }

    [JsonPropertyName("memberSince")]
    public DateTime MemberSince { get; set; }
}

public class TypeCount
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class TypeRating
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("avgRating")]
    public double AvgRating { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class TopItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("rating")]
    public int Rating { get; set; }

    [JsonPropertyName("photoUrl")]
    public string? PhotoUrl { get; set; }
}

public class VenueCount
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class TagCount
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class DayActivity
{
    [JsonPropertyName("day")]
    public string Day { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class MonthlyCount
{
    [JsonPropertyName("month")]
    public string Month { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class MonthlyRating
{
    [JsonPropertyName("month")]
    public string Month { get; set; } = string.Empty;

    [JsonPropertyName("avgRating")]
    public double AvgRating { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
