using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Channels;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ItemsController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IBlobStorageService _blobStorage;
    private readonly INotificationService _notificationService;
    private readonly Channel<WishlistUrlWorkItem> _wishlistUrlQueue;
    private readonly ILogger<ItemsController> _logger;
    private const string ContainerName = "items";
    private const string FriendshipsContainer = "friendships";
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public ItemsController(ICosmosDbService cosmosDb, IBlobStorageService blobStorage, INotificationService notificationService, Channel<WishlistUrlWorkItem> wishlistUrlQueue, ILogger<ItemsController> logger)
    {
        _cosmosDb = cosmosDb;
        _blobStorage = blobStorage;
        _notificationService = notificationService;
        _wishlistUrlQueue = wishlistUrlQueue;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    private string GetDisplayName() => User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";

    private async Task<bool> AreFriendsAsync(string userId, string friendId)
    {
        var (friends, _) = await _cosmosDb.QueryAsync<Friendship>(
            FriendshipsContainer, userId, maxItems: 1,
            predicate: f => f.FriendId == friendId && f.Status == FriendshipStatus.Accepted);
        return friends.Count > 0;
    }

    private static readonly HashSet<string> AllowedImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif"];

    [HttpGet]
    public async Task<ActionResult<PagedResponse<Item>>> ListItems(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] string? continuationToken,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] string? groupBy,
        [FromQuery] string? search,
        [FromQuery] int? pageSize)
    {
        using var activity = Diagnostics.General.StartActivity("ItemsList");
        var userId = GetUserId();
        var normalizedSearch = NormalizeSearch(search);
        var clampedPageSize = ClampPageSize(pageSize);
        activity?.SetTag("user.id", userId);
        activity?.SetTag("item.type", type);
        activity?.SetTag("item.status", status);
        activity?.SetTag("item.search", normalizedSearch);
        _logger.LogDebug("Listing items for user {UserId}, typeFilter={TypeFilter}, statusFilter={StatusFilter}, sortBy={SortBy}, sortDirection={SortDirection}, groupBy={GroupBy}, search={Search}, pageSize={PageSize}",
            userId, type, status, sortBy, sortDirection, groupBy, normalizedSearch, clampedPageSize);

        System.Linq.Expressions.Expression<Func<Item, bool>>? predicate = null;

        if (status == ItemStatus.Wishlist && normalizedSearch != null)
        {
            predicate = !string.IsNullOrEmpty(type)
                ? i => i.Status == ItemStatus.Wishlist && i.Type == type && (
                    i.Name.ToLower().Contains(normalizedSearch) ||
                    i.Type.ToLower().Contains(normalizedSearch) ||
                    (i.Brand != null && i.Brand.ToLower().Contains(normalizedSearch)) ||
                    (i.Category != null && i.Category.ToLower().Contains(normalizedSearch)) ||
                    (i.Venue != null && i.Venue.Name.ToLower().Contains(normalizedSearch)) ||
                    i.Tags.Any(t => t.ToLower().Contains(normalizedSearch)))
                : i => i.Status == ItemStatus.Wishlist && (
                    i.Name.ToLower().Contains(normalizedSearch) ||
                    i.Type.ToLower().Contains(normalizedSearch) ||
                    (i.Brand != null && i.Brand.ToLower().Contains(normalizedSearch)) ||
                    (i.Category != null && i.Category.ToLower().Contains(normalizedSearch)) ||
                    (i.Venue != null && i.Venue.Name.ToLower().Contains(normalizedSearch)) ||
                    i.Tags.Any(t => t.ToLower().Contains(normalizedSearch)));
        }
        else if (status == ItemStatus.Wishlist)
        {
            predicate = !string.IsNullOrEmpty(type)
                ? i => i.Status == ItemStatus.Wishlist && i.Type == type
                : i => i.Status == ItemStatus.Wishlist;
        }
        else if (normalizedSearch != null)
        {
            predicate = !string.IsNullOrEmpty(type)
                ? i => i.Status != ItemStatus.Wishlist && i.Type == type && (
                    i.Name.ToLower().Contains(normalizedSearch) ||
                    i.Type.ToLower().Contains(normalizedSearch) ||
                    (i.Brand != null && i.Brand.ToLower().Contains(normalizedSearch)) ||
                    (i.Category != null && i.Category.ToLower().Contains(normalizedSearch)) ||
                    (i.Venue != null && i.Venue.Name.ToLower().Contains(normalizedSearch)) ||
                    i.Tags.Any(t => t.ToLower().Contains(normalizedSearch)))
                : i => i.Status != ItemStatus.Wishlist && (
                    i.Name.ToLower().Contains(normalizedSearch) ||
                    i.Type.ToLower().Contains(normalizedSearch) ||
                    (i.Brand != null && i.Brand.ToLower().Contains(normalizedSearch)) ||
                    (i.Category != null && i.Category.ToLower().Contains(normalizedSearch)) ||
                    (i.Venue != null && i.Venue.Name.ToLower().Contains(normalizedSearch)) ||
                    i.Tags.Any(t => t.ToLower().Contains(normalizedSearch)));
        }
        else
        {
            predicate = !string.IsNullOrEmpty(type)
                ? i => i.Status != ItemStatus.Wishlist && i.Type == type
                : i => i.Status != ItemStatus.Wishlist;
        }

        var normalizedGroupBy = groupBy?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedGroupBy) && normalizedGroupBy is not ("type" or "brand" or "status"))
        {
            return BadRequest(new { message = $"Invalid groupBy field: {groupBy}. Allowed: type, brand, status" });
        }

        // Grouping on paged responses is represented as primary ordering by the group field.
        var effectiveSortBy = !string.IsNullOrEmpty(normalizedGroupBy) ? normalizedGroupBy : sortBy;

        // Server-side ordering: build ORDER BY expression
        System.Linq.Expressions.Expression<Func<Item, object>>? orderByExpr = null;
        bool descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(effectiveSortBy))
        {
            orderByExpr = effectiveSortBy.ToLowerInvariant() switch
            {
                "name" => i => i.Name,
                "createdat" => i => i.CreatedAt,
                "updatedat" => i => i.UpdatedAt,
                "userrating" => i => i.UserRating ?? 0,
                "rating" => i => i.UserRating ?? 0,
                "brand" => i => i.Brand ?? string.Empty,
                "type" => i => i.Type,
                _ => null
            };

            if (orderByExpr == null)
            {
                return BadRequest(new { message = $"Invalid sortBy field: {effectiveSortBy}. Allowed: name, createdAt, updatedAt, rating, userRating, brand, type" });
            }
        }

        var (items, nextToken) = await _cosmosDb.QueryAsync(
            ContainerName, userId, continuationToken,
            maxItems: clampedPageSize,
            predicate: predicate,
            orderBy: orderByExpr,
            orderDescending: descending);

        activity?.SetTag("items.count", items.Count);
        _logger.LogInformation("Listed {Count} items for user {UserId} (type={TypeFilter}, status={StatusFilter}, sortBy={SortBy}, direction={SortDirection}, groupBy={GroupBy}, search={Search}), hasMore={HasMore}",
            items.Count, userId, type, status, effectiveSortBy, sortDirection, normalizedGroupBy, normalizedSearch, nextToken != null);
        return Ok(new PagedResponse<Item>
        {
            Items = items,
            ContinuationToken = nextToken,
            HasMore = nextToken != null
        });
    }

    private static int ClampPageSize(int? pageSize) => Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

    private static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

    /// <summary>
    /// GET /api/items/grouped?groupBy={field} - Returns all items grouped by specified field.
    /// Note: Grouping is incompatible with continuation-token pagination (aggregation requires full dataset).
    /// This endpoint fetches all user items in memory and groups client-side. Use for analytics/overview, not large datasets.
    /// </summary>
    [HttpGet("grouped")]
    public async Task<ActionResult<GroupedResponse<Item>>> GetGroupedItems(
        [FromQuery] string groupBy,
        [FromQuery] string? type,
        [FromQuery] string? status)
    {
        using var activity = Diagnostics.General.StartActivity("ItemsGrouped");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);
        activity?.SetTag("group_by", groupBy);

        if (string.IsNullOrEmpty(groupBy))
            return BadRequest(new { message = "groupBy parameter is required" });

        // Fetch all items (no pagination for grouping)
        var allItems = new List<Item>();
        string? token = null;
        do
        {
            System.Linq.Expressions.Expression<Func<Item, bool>>? predicate = null;
            if (status == ItemStatus.Wishlist)
            {
                predicate = !string.IsNullOrEmpty(type)
                    ? i => i.Status == ItemStatus.Wishlist && i.Type == type
                    : i => i.Status == ItemStatus.Wishlist;
            }
            else
            {
                predicate = !string.IsNullOrEmpty(type)
                    ? i => i.Status != ItemStatus.Wishlist && i.Type == type
                    : i => i.Status != ItemStatus.Wishlist;
            }

            var (items, nextToken) = await _cosmosDb.QueryAsync<Item>(ContainerName, userId, token, predicate: predicate);
            allItems.AddRange(items);
            token = nextToken;
        } while (token != null);

        // Client-side grouping
        var groups = new Dictionary<string, List<Item>>();
        foreach (var item in allItems)
        {
            var key = groupBy.ToLowerInvariant() switch
            {
                "type" => item.Type ?? "(none)",
                "brand" => item.Brand ?? "(none)",
                "status" => item.Status ?? "(none)",
                _ => "(unknown)"
            };

            if (!groups.ContainsKey(key))
                groups[key] = new List<Item>();
            groups[key].Add(item);
        }

        if (groupBy.ToLowerInvariant() is not ("type" or "brand" or "status"))
        {
            return BadRequest(new { message = $"Invalid groupBy field: {groupBy}. Allowed: type, brand, status" });
        }

        _logger.LogInformation("Grouped {Count} items by {GroupBy} for user {UserId} ({GroupCount} groups)",
            allItems.Count, groupBy, userId, groups.Count);

        return Ok(new GroupedResponse<Item>
        {
            Groups = groups,
            GroupBy = groupBy,
            TotalCount = allItems.Count
        });
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult<Dictionary<string, List<string>>>> GetSuggestions()
    {
        var userId = GetUserId();

        var all = new List<Item>();
        string? token = null;
        do
        {
            var (items, nextToken) = await _cosmosDb.QueryAsync<Item>(ContainerName, userId, token);
            all.AddRange(items);
            token = nextToken;
        } while (token != null);

        var names = all.Select(i => i.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n).ToList();
        var brands = all.Select(i => i.Brand).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().OrderBy(b => b).ToList()!;
        var tags = all.SelectMany(i => i.Tags).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().OrderBy(t => t).ToList();

        return Ok(new Dictionary<string, List<string>>
        {
            ["names"] = names,
            ["brands"] = brands!,
            ["tags"] = tags,
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Item>> GetItem(string id)
    {
        using var activity = Diagnostics.General.StartActivity("ItemGet");
        var userId = GetUserId();
        activity?.SetTag("item.id", id);
        activity?.SetTag("user.id", userId);
        _logger.LogDebug("Getting item {ItemId} for user {UserId}", id, userId);

        var item = await _cosmosDb.GetAsync<Item>(ContainerName, id, userId);
        if (item == null)
        {
            _logger.LogWarning("Item {ItemId} not found for user {UserId}", id, userId);
            return NotFound();
        }

        activity?.SetTag("item.type", item.Type);
        _logger.LogInformation("Retrieved item {ItemId} (type={ItemType}) for user {UserId}", id, item.Type, userId);
        return Ok(item);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Item>> UpdateItem(string id, [FromBody] UpdateItemRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var activity = Diagnostics.General.StartActivity("ItemUpdate");
        var userId = GetUserId();
        activity?.SetTag("item.id", id);
        activity?.SetTag("user.id", userId);
        _logger.LogDebug("Updating item {ItemId} for user {UserId}", id, userId);

        var item = await _cosmosDb.GetAsync<Item>(ContainerName, id, userId);
        if (item == null)
        {
            _logger.LogWarning("Item {ItemId} not found for update by user {UserId}", id, userId);
            return NotFound();
        }

        if (request.Name != null) item.Name = request.Name;
        if (request.Type != null) item.Type = request.Type;
        if (request.Brand != null) item.Brand = request.Brand;
        if (request.Category != null) item.Category = request.Category;
        if (request.Venue != null)
        {
            // Clear venue if empty name is provided
            if (string.IsNullOrWhiteSpace(request.Venue.Name))
                item.Venue = null;
            else
                item.Venue = request.Venue;
        }
        if (request.UserRating.HasValue) item.UserRating = request.UserRating.Value > 0 ? request.UserRating.Value : null;
        if (request.UserNotes != null) item.UserNotes = request.UserNotes;
        if (!string.IsNullOrWhiteSpace(request.JournalEntry))
        {
            item.Journal.Add(new JournalEntry
            {
                Text = request.JournalEntry.Trim(),
                Date = DateTime.UtcNow,
                Source = "user"
            });
        }
        if (request.Tags != null) item.Tags = request.Tags;
        if (request.Status != null) item.Status = request.Status;
        item.UpdatedAt = DateTime.UtcNow;

        item = await _cosmosDb.UpsertAsync(ContainerName, item, item.PartitionKey);

        activity?.SetTag("item.type", item.Type);
        _logger.LogInformation("Updated item {ItemId} (type={ItemType}) for user {UserId}", id, item.Type, userId);
        return Ok(item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(string id)
    {
        using var activity = Diagnostics.General.StartActivity("ItemDelete");
        var userId = GetUserId();
        activity?.SetTag("item.id", id);
        activity?.SetTag("user.id", userId);
        _logger.LogDebug("Deleting item {ItemId} for user {UserId}", id, userId);

        var item = await _cosmosDb.GetAsync<Item>(ContainerName, id, userId);
        if (item == null)
            return NoContent();

        await _cosmosDb.DeleteAsync(ContainerName, id, userId);

        // Cascade: remove item from capture's itemIds and purge capture if no items remain
        if (!string.IsNullOrEmpty(item.CaptureId))
        {
            try
            {
                var capture = await _cosmosDb.GetAsync<Capture>("captures", item.CaptureId, userId);
                if (capture != null)
                {
                    capture.ItemIds.Remove(id);

                    if (capture.ItemIds.Count == 0)
                    {
                        _logger.LogInformation("No items remain for capture {CaptureId} — purging", capture.Id);
                        await _cosmosDb.DeleteAsync("captures", capture.Id, userId);
                    }
                    else
                    {
                        await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cascade-update capture {CaptureId} after deleting item {ItemId}",
                    item.CaptureId, id);
            }
        }

        _logger.LogInformation("Deleted item {ItemId} for user {UserId}", id, userId);
        return NoContent();
    }

    [HttpPost("wishlist")]
    public async Task<ActionResult<Item>> CreateWishlistItem([FromBody] CreateWishlistRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var activity = Diagnostics.General.StartActivity("WishlistCreate");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required" });
        if (string.IsNullOrWhiteSpace(request.Type) || !ItemType.All.Contains(request.Type))
            return BadRequest(new { message = "Valid type is required (whiskey, wine, cocktail, vodka, gin, cigar, dessert)" });

        var item = new Item
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Type = request.Type,
            Brand = request.Brand?.Trim(),
            UserNotes = request.Notes?.Trim(),
            Tags = request.Tags ?? [],
            Venue = !string.IsNullOrWhiteSpace(request.VenueName)
                ? new VenueInfo { Name = request.VenueName.Trim() }
                : null,
            Status = ItemStatus.Wishlist,
            ProcessedBy = ProcessingSource.Manual,
        };

        item = await _cosmosDb.CreateAsync(ContainerName, item, item.PartitionKey);

        _logger.LogInformation("Created wishlist item {ItemId} (type={ItemType}) for user {UserId}", item.Id, item.Type, userId);
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item);
    }

    [HttpPost("wishlist/from-url")]
    public async Task<ActionResult> CreateWishlistFromUrl([FromBody] CreateWishlistFromUrlRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var activity = Diagnostics.General.StartActivity("WishlistFromUrl");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);

        _logger.LogInformation("Queuing wishlist URL extraction for user {UserId}: {Url}", userId, request.Url);

        // Create placeholder item with domain name while AI extracts details
        var item = new Item
        {
            UserId = userId,
            Name = ExtractDomainLabel(request.Url),
            Type = "custom",
            Tags = ["from-url"],
            Status = ItemStatus.Wishlist,
            ProcessedBy = ProcessingSource.Pending,
        };

        item = await _cosmosDb.CreateAsync(ContainerName, item, item.PartitionKey);

        // Queue background extraction
        await _wishlistUrlQueue.Writer.WriteAsync(new WishlistUrlWorkItem
        {
            ItemId = item.Id,
            UserId = userId,
            Url = request.Url,
        });

        _logger.LogInformation("Created placeholder wishlist item {ItemId} and queued URL extraction for user {UserId}", item.Id, userId);
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item);
    }

    [HttpPost("{id}/convert")]
    public async Task<ActionResult<Item>> ConvertWishlistItem(string id)
    {
        using var activity = Diagnostics.General.StartActivity("WishlistConvert");
        var userId = GetUserId();
        activity?.SetTag("item.id", id);
        activity?.SetTag("user.id", userId);

        var item = await _cosmosDb.GetAsync<Item>(ContainerName, id, userId);
        if (item == null)
            return NotFound();

        if (item.Status != ItemStatus.Wishlist)
            return BadRequest(new { message = "Item is not a wishlist item" });

        item.Status = ItemStatus.Reviewed;
        item.UpdatedAt = DateTime.UtcNow;
        item = await _cosmosDb.UpsertAsync(ContainerName, item, item.PartitionKey);

        _logger.LogInformation("Converted wishlist item {ItemId} to collection for user {UserId}", id, userId);
        return Ok(item);
    }

    [HttpPost("{id}/share")]
    public async Task<ActionResult> ShareItem(string id, [FromBody] ShareRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();

        var item = await _cosmosDb.GetAsync<Item>(ContainerName, id, userId);
        if (item == null)
            return NotFound();

        if (!await AreFriendsAsync(userId, request.FriendId))
            return Forbid();

        var sharerName = GetDisplayName();

        await _notificationService.CreateAsync(new Notification
        {
            UserId = request.FriendId,
            Type = NotificationType.ItemShared,
            Title = $"{sharerName} shared {item.Name} with you",
            SourceUserId = userId,
            SourceDisplayName = sharerName,
            ReferenceType = "item",
            ReferenceId = item.Id,
        });

        _logger.LogInformation("User {UserId} shared item {ItemId} with friend {FriendId}", userId, id, request.FriendId);
        return Ok();
    }

    [HttpGet("{id}/photos/upload-url")]
    public async Task<ActionResult<UploadUrlResponse>> GetPhotoUploadUrl(string id, [FromQuery] string fileName)
    {
        var userId = GetUserId();

        var item = await _cosmosDb.GetAsync<Item>(ContainerName, id, userId);
        if (item == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest(new { message = "fileName is required" });

        if (fileName.Length > 255)
            return BadRequest(new { message = "fileName too long (max 255 chars)" });

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            return BadRequest(new { message = $"File type {ext} is not allowed. Accepted: jpg, jpeg, png, gif, webp, heic, heif" });

        var (uploadUrl, blobUrl) = await _blobStorage.GenerateUploadUrlAsync(userId, fileName);
        return Ok(new UploadUrlResponse { UploadUrl = uploadUrl, BlobUrl = blobUrl });
    }

    [HttpPost("{id}/photos")]
    public async Task<ActionResult<Item>> AddPhoto(string id, [FromBody] AddPhotoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();

        var item = await _cosmosDb.GetAsync<Item>(ContainerName, id, userId);
        if (item == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.BlobUrl))
            return BadRequest(new { message = "blobUrl is required" });

        if (!ValidateBlobOwnership(request.BlobUrl, userId))
            return BadRequest(new { message = "Invalid blob URL" });

        if (!item.PhotoUrls.Contains(request.BlobUrl))
        {
            item.PhotoUrls.Add(request.BlobUrl);
            item.UpdatedAt = DateTime.UtcNow;
            item = await _cosmosDb.UpsertAsync(ContainerName, item, item.PartitionKey);
        }

        _logger.LogInformation("Added photo to item {ItemId} for user {UserId}, total photos: {Count}", id, userId, item.PhotoUrls.Count);
        return Ok(item);
    }

    [HttpDelete("{id}/photos")]
    public async Task<ActionResult<Item>> RemovePhoto(string id, [FromBody] RemovePhotoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();

        var item = await _cosmosDb.GetAsync<Item>(ContainerName, id, userId);
        if (item == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.BlobUrl))
            return BadRequest(new { message = "blobUrl is required" });

        if (item.PhotoUrls.Remove(request.BlobUrl))
        {
            item.UpdatedAt = DateTime.UtcNow;
            item = await _cosmosDb.UpsertAsync(ContainerName, item, item.PartitionKey);

            try
            {
                await _blobStorage.DeleteBlobAsync(request.BlobUrl);
                _logger.LogInformation("Deleted blob for item {ItemId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete blob for item {ItemId}", id);
            }
        }

        _logger.LogInformation("Removed photo from item {ItemId} for user {UserId}, remaining: {Count}", id, userId, item.PhotoUrls.Count);
        return Ok(item);
    }

    /// <summary>
    /// Validates that a blob URL belongs to the specified user by parsing URL segments structurally.
    /// Blob naming convention: {baseUrl}/{userId}/yyyy/MM/dd/{guid}.{ext}
    /// </summary>
    private static bool ValidateBlobOwnership(string blobUrl, string userId)
    {
        try
        {
            // Handle both absolute URLs and relative paths
            string[] segments;
            if (Uri.TryCreate(blobUrl, UriKind.Absolute, out var uri))
            {
                segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                segments = blobUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
            }

            // userId must appear as an exact path segment
            return segments.Any(s => s.Equals(userId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts a human-readable label from a URL's domain name.
    /// e.g. "https://www.coolvenue.com/product/123" → "coolvenue"
    /// </summary>
    private static string ExtractDomainLabel(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var host = uri.Host;
                // Strip common prefixes
                if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    host = host[4..];
                // Take the first segment before .com/.co/.org etc.
                var dotIndex = host.IndexOf('.');
                if (dotIndex > 0)
                    host = host[..dotIndex];
                if (!string.IsNullOrWhiteSpace(host))
                    return host;
            }
        }
        catch { }

        return "Wishlist Item";
    }
}
