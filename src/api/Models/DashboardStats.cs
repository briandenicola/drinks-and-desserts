using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class DashboardStats
{
    [JsonPropertyName("summary")]
    public DashboardSummary Summary { get; set; } = new();

    [JsonPropertyName("thisMonth")]
    public MonthlySnapshot ThisMonth { get; set; } = new();

    [JsonPropertyName("recentActivity")]
    public List<RecentActivity> RecentActivity { get; set; } = [];
}

public class DashboardSummary
{
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("drinkCount")]
    public int DrinkCount { get; set; }

    [JsonPropertyName("dessertCount")]
    public int DessertCount { get; set; }

    [JsonPropertyName("avgDrinkRating")]
    public double AvgDrinkRating { get; set; }

    [JsonPropertyName("avgDessertRating")]
    public double AvgDessertRating { get; set; }

    [JsonPropertyName("wishlistSize")]
    public int WishlistSize { get; set; }

    [JsonPropertyName("totalVenues")]
    public int TotalVenues { get; set; }
}

public class MonthlySnapshot
{
    [JsonPropertyName("newItemsCaptured")]
    public int NewItemsCaptured { get; set; }

    [JsonPropertyName("venuesVisited")]
    public int VenuesVisited { get; set; }

    [JsonPropertyName("wishlistConversions")]
    public int WishlistConversions { get; set; }

    [JsonPropertyName("month")]
    public string Month { get; set; } = string.Empty;
}

public class RecentActivity
{
    [JsonPropertyName("captureId")]
    public string CaptureId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("thumbnailUrls")]
    public List<string> ThumbnailUrls { get; set; } = [];

    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("venueName")]
    public string? VenueName { get; set; }
}
