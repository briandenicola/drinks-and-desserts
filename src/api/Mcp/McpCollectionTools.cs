using System.ComponentModel;
using ModelContextProtocol.Server;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Mcp;

/// <summary>
/// Read-only MCP tools over the authenticated owner's collection and venues.
/// Available to any valid, enabled API key (baseline "read" capability).
/// </summary>
[McpServerToolType]
public class McpCollectionTools(ICosmosDbService cosmosDb, McpToolContext context)
{
    private const string ItemsContainer = "items";
    private const string VenuesContainer = "venues";
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    private static int Clamp(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

    [McpServerTool(Name = "search_items"), Description(
        "Search the authenticated user's collection items (drinks, coffee, desserts, cigars). Excludes wishlist items. Read-only.")]
    public async Task<ItemSummary[]> SearchItems(
        [Description("Optional text to match against name, brand, category, type, or tags")] string? query = null,
        [Description("Optional item type filter, e.g. whiskey, wine, cigar, dessert")] string? type = null,
        [Description("Maximum results to return (default 10, max 50)")] int? limit = null)
    {
        var userId = context.UserId;
        var normalized = string.IsNullOrWhiteSpace(query) ? null : query.Trim().ToLowerInvariant();
        var max = Clamp(limit);

        System.Linq.Expressions.Expression<Func<Item, bool>> predicate = normalized != null
            ? i => i.Status != ItemStatus.Wishlist &&
                   (string.IsNullOrEmpty(type) || i.Type == type) &&
                   (i.Name.ToLower().Contains(normalized) ||
                    i.Type.ToLower().Contains(normalized) ||
                    (i.Brand != null && i.Brand.ToLower().Contains(normalized)) ||
                    (i.Category != null && i.Category.ToLower().Contains(normalized)) ||
                    i.Tags.Any(t => t.ToLower().Contains(normalized)))
            : i => i.Status != ItemStatus.Wishlist && (string.IsNullOrEmpty(type) || i.Type == type);

        var (items, _) = await cosmosDb.QueryAsync(ItemsContainer, userId, maxItems: max, predicate: predicate);
        return items.Select(ToSummary).ToArray();
    }

    [McpServerTool(Name = "get_item"), Description("Get one collection item owned by the authenticated user. Read-only.")]
    public async Task<ItemSummary> GetItem([Description("Item id")] string itemId)
    {
        var item = await cosmosDb.GetAsync<Item>(ItemsContainer, itemId, context.UserId)
            ?? throw new ModelContextProtocol.McpException("Item not found");
        return ToSummary(item);
    }

    [McpServerTool(Name = "list_venues"), Description(
        "List venues (bars, restaurants, cafes, lounges) owned by the authenticated user. Read-only.")]
    public async Task<VenueSummary[]> ListVenues(
        [Description("Optional text to match against name, address, website, or labels")] string? query = null,
        [Description("Optional venue type filter, e.g. bar, restaurant, cafe, lounge")] string? type = null,
        [Description("Maximum results to return (default 10, max 50)")] int? limit = null)
    {
        var userId = context.UserId;
        var normalized = string.IsNullOrWhiteSpace(query) ? null : query.Trim().ToLowerInvariant();
        var max = Clamp(limit);

        System.Linq.Expressions.Expression<Func<Venue, bool>> predicate = normalized != null
            ? v => (string.IsNullOrEmpty(type) || v.Type == type) &&
                   (v.Name.ToLower().Contains(normalized) ||
                    v.Type.ToLower().Contains(normalized) ||
                    (v.Address != null && v.Address.ToLower().Contains(normalized)) ||
                    (v.Website != null && v.Website.ToLower().Contains(normalized)) ||
                    v.Labels.Any(l => l.ToLower().Contains(normalized)))
            : v => string.IsNullOrEmpty(type) || v.Type == type;

        var (venues, _) = await cosmosDb.QueryAsync(VenuesContainer, userId, maxItems: max, predicate: predicate);
        return venues.Select(ToSummary).ToArray();
    }

    [McpServerTool(Name = "get_venue"), Description("Get one venue owned by the authenticated user. Read-only.")]
    public async Task<VenueSummary> GetVenue([Description("Venue id")] string venueId)
    {
        var venue = await cosmosDb.GetAsync<Venue>(VenuesContainer, venueId, context.UserId)
            ?? throw new ModelContextProtocol.McpException("Venue not found");
        return ToSummary(venue);
    }

    [McpServerTool(Name = "collection_stats"), Description(
        "Read aggregate collection statistics for the authenticated user: counts by type/status and average rating. Read-only.")]
    public async Task<CollectionStatsSummary> CollectionStats()
    {
        var userId = context.UserId;
        var all = new List<Item>();
        string? token = null;
        do
        {
            var (items, next) = await cosmosDb.QueryAsync<Item>(ItemsContainer, userId, token);
            all.AddRange(items);
            token = next;
        } while (token != null);

        var active = all.Where(i => i.Status != ItemStatus.Wishlist).ToList();
        var wishlist = all.Where(i => i.Status == ItemStatus.Wishlist).ToList();
        var rated = active.Where(i => i.UserRating.HasValue).ToList();

        return new CollectionStatsSummary
        {
            TotalItems = active.Count,
            WishlistItems = wishlist.Count,
            AverageRating = rated.Count > 0 ? Math.Round(rated.Average(i => i.UserRating!.Value), 2) : null,
            CountsByType = active.GroupBy(i => i.Type).ToDictionary(g => g.Key, g => g.Count()),
        };
    }

    private static ItemSummary ToSummary(Item i) => new()
    {
        Id = i.Id,
        Name = i.Name,
        Type = i.Type,
        Brand = i.Brand,
        Category = i.Category,
        UserRating = i.UserRating,
        Status = i.Status,
        Tags = i.Tags,
        VenueName = i.Venue?.Name,
        CreatedAt = i.CreatedAt,
    };

    private static VenueSummary ToSummary(Venue v) => new()
    {
        Id = v.Id,
        Name = v.Name,
        Type = v.Type,
        Address = v.Address,
        Website = v.Website,
        Rating = v.Rating,
        Labels = v.Labels,
    };
}

public class ItemSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public double? UserRating { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string? VenueName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class VenueSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Website { get; set; }
    public double? Rating { get; set; }
    public List<string> Labels { get; set; } = [];
}

public class CollectionStatsSummary
{
    public int TotalItems { get; set; }
    public int WishlistItems { get; set; }
    public double? AverageRating { get; set; }
    public Dictionary<string, int> CountsByType { get; set; } = [];
}
