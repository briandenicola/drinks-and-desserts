using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "MultiAuth,ApiKey")]
public class ThoughtsController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ThoughtsController> _logger;
    private const string ContainerName = "thoughts";
    private const string FriendshipsContainer = "friendships";

    public ThoughtsController(ICosmosDbService cosmosDb, INotificationService notificationService, ILogger<ThoughtsController> logger)
    {
        _cosmosDb = cosmosDb;
        _notificationService = notificationService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    private string GetDisplayName() => User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";

    /// <summary>Get all thoughts on a specific item or venue (partitioned by target owner).</summary>
    [HttpGet("{targetType}/{targetId}")]
    public async Task<ActionResult<List<Thought>>> GetThoughts(string targetType, string targetId, [FromQuery] string targetUserId)
    {
        if (targetType != ThoughtTargetType.Item && targetType != ThoughtTargetType.Venue)
            return BadRequest(new { error = "targetType must be 'item' or 'venue'." });

        if (string.IsNullOrWhiteSpace(targetUserId))
            return BadRequest(new { error = "targetUserId is required." });

        var userId = GetUserId();

        // Must be the owner or a friend of the owner
        if (targetUserId != userId && !await AreFriendsAsync(userId, targetUserId))
            return Forbid();

        var (thoughts, _) = await _cosmosDb.QueryAsync<Thought>(
            ContainerName, targetUserId, maxItems: 100,
            predicate: t => t.TargetId == targetId && t.TargetType == targetType);

        return Ok(thoughts.OrderByDescending(t => t.CreatedAt).ToList());
    }

    /// <summary>Leave a thought on a friend's item or venue.</summary>
    [HttpPost]
    public async Task<ActionResult<Thought>> CreateThought([FromBody] CreateThoughtRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Content cannot be empty." });

        if (request.TargetUserId == userId)
            return BadRequest(new { error = "You cannot leave a thought on your own item." });

        if (!await AreFriendsAsync(userId, request.TargetUserId))
            return Forbid();

        if (request.Rating.HasValue && (request.Rating < 1 || request.Rating > 5))
            return BadRequest(new { error = "Rating must be between 1 and 5." });

        // Resolve target name
        var targetName = await ResolveTargetNameAsync(request.TargetType, request.TargetId, request.TargetUserId);

        var thought = new Thought
        {
            AuthorId = userId,
            AuthorDisplayName = GetDisplayName(),
            TargetUserId = request.TargetUserId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            TargetName = targetName ?? "Unknown",
            Content = request.Content.Trim(),
            Rating = request.Rating,
        };

        await _cosmosDb.CreateAsync(ContainerName, thought, thought.PartitionKey);

        // Notify the item/venue owner
        await _notificationService.CreateAsync(new Notification
        {
            UserId = request.TargetUserId,
            Type = NotificationType.NewThought,
            Title = $"{GetDisplayName()} shared a thought on {targetName ?? "your " + request.TargetType}",
            Detail = thought.Content.Length > 100 ? thought.Content[..100] + "..." : thought.Content,
            SourceUserId = userId,
            SourceDisplayName = GetDisplayName(),
            ReferenceType = request.TargetType,
            ReferenceId = request.TargetId,
        });

        _logger.LogInformation("User {UserId} left thought on {TargetType}/{TargetId} owned by {OwnerId}",
            userId, request.TargetType, request.TargetId, request.TargetUserId);

        return Ok(thought);
    }

    /// <summary>Edit own thought.</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<Thought>> UpdateThought(string id, [FromBody] UpdateThoughtRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem();

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Content cannot be empty." });

        var userId = GetUserId();

        // Thoughts are partitioned by targetUserId — we need to find it
        // Use cross-partition query to locate the thought by its id
        var thoughts = await _cosmosDb.QueryCrossPartitionAsync<Thought>(
            ContainerName,
            "SELECT * FROM c WHERE c.id = @id AND c.authorId = @authorId",
            new Dictionary<string, object> { ["@id"] = id, ["@authorId"] = userId },
            maxItems: 1);

        if (thoughts.Count == 0) return NotFound();

        var thought = thoughts[0];
        thought.Content = request.Content.Trim();
        if (request.Rating.HasValue)
            thought.Rating = request.Rating;
        thought.UpdatedAt = DateTime.UtcNow;

        await _cosmosDb.UpsertAsync(ContainerName, thought, thought.PartitionKey);
        return Ok(thought);
    }

    /// <summary>Delete own thought.</summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteThought(string id)
    {
        var userId = GetUserId();

        var thoughts = await _cosmosDb.QueryCrossPartitionAsync<Thought>(
            ContainerName,
            "SELECT * FROM c WHERE c.id = @id AND c.authorId = @authorId",
            new Dictionary<string, object> { ["@id"] = id, ["@authorId"] = userId },
            maxItems: 1);

        if (thoughts.Count == 0) return NotFound();

        var thought = thoughts[0];
        await _cosmosDb.DeleteAsync(ContainerName, id, thought.PartitionKey);
        return NoContent();
    }

    /// <summary>Get all thoughts I've written.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<Thought>>> GetMyThoughts()
    {
        var userId = GetUserId();
        var thoughts = await _cosmosDb.QueryCrossPartitionAsync<Thought>(
            ContainerName,
            "SELECT * FROM c WHERE c.authorId = @authorId ORDER BY c.createdAt DESC",
            new Dictionary<string, object> { ["@authorId"] = userId },
            maxItems: 100);
        return Ok(thoughts);
    }

    /// <summary>Get all thoughts others left on my items/venues.</summary>
    [HttpGet("on-my-items")]
    public async Task<ActionResult<List<Thought>>> GetThoughtsOnMyItems()
    {
        var userId = GetUserId();
        var (thoughts, _) = await _cosmosDb.QueryAsync<Thought>(
            ContainerName, userId, maxItems: 100);
        return Ok(thoughts.OrderByDescending(t => t.CreatedAt).ToList());
    }

    // ── Helpers ──

    private async Task<bool> AreFriendsAsync(string userId, string friendId)
    {
        var (friends, _) = await _cosmosDb.QueryAsync<Friendship>(
            FriendshipsContainer, userId, maxItems: 1,
            predicate: f => f.FriendId == friendId && f.Status == FriendshipStatus.Accepted);
        return friends.Count > 0;
    }

    private async Task<string?> ResolveTargetNameAsync(string targetType, string targetId, string targetUserId)
    {
        if (targetType == ThoughtTargetType.Item)
        {
            var item = await _cosmosDb.GetAsync<Item>("items", targetId, targetUserId);
            return item?.Name;
        }
        if (targetType == ThoughtTargetType.Venue)
        {
            var venue = await _cosmosDb.GetAsync<Venue>("venues", targetId, targetUserId);
            return venue?.Name;
        }
        return null;
    }
}

// Request models
public class CreateThoughtRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string Content { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    public string TargetUserId { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression("^(item|venue)$")]
    public string TargetType { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    public string TargetId { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(1, 5)]
    public int? Rating { get; set; }
}

public class UpdateThoughtRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string Content { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(1, 5)]
    public int? Rating { get; set; }
}
