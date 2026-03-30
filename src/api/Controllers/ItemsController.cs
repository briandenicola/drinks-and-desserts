using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;
using System.Security.Claims;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ItemsController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly ILogger<ItemsController> _logger;
    private const string ContainerName = "items";

    public ItemsController(ICosmosDbService cosmosDb, ILogger<ItemsController> logger)
    {
        _cosmosDb = cosmosDb;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<ActionResult<PagedResponse<Item>>> ListItems(
        [FromQuery] string? type,
        [FromQuery] string? continuationToken)
    {
        using var activity = Diagnostics.General.StartActivity("ItemsList");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);
        activity?.SetTag("item.type", type);
        _logger.LogDebug("Listing items for user {UserId}, typeFilter={TypeFilter}, hasContinuation={HasContinuation}",
            userId, type, continuationToken != null);

        System.Linq.Expressions.Expression<Func<Item, bool>>? predicate = null;

        if (!string.IsNullOrEmpty(type))
            predicate = i => i.Type == type;

        var (items, nextToken) = await _cosmosDb.QueryAsync(ContainerName, userId, continuationToken, predicate: predicate);

        activity?.SetTag("items.count", items.Count);
        _logger.LogInformation("Listed {Count} items for user {UserId} (typeFilter={TypeFilter}), hasMore={HasMore}",
            items.Count, userId, type, nextToken != null);
        return Ok(new PagedResponse<Item>
        {
            Items = items,
            ContinuationToken = nextToken,
            HasMore = nextToken != null
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
        if (request.Brand != null) item.Brand = request.Brand;
        if (request.Category != null) item.Category = request.Category;
        if (request.UserRating.HasValue) item.UserRating = request.UserRating;
        if (request.UserNotes != null) item.UserNotes = request.UserNotes;
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

        await _cosmosDb.DeleteAsync(ContainerName, id, userId);

        _logger.LogInformation("Deleted item {ItemId} for user {UserId}", id, userId);
        return NoContent();
    }
}
