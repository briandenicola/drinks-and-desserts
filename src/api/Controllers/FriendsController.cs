using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "MultiAuth,ApiKey")]
public class FriendsController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly INotificationService _notificationService;
    private readonly ILogger<FriendsController> _logger;
    private const string FriendshipsContainer = "friendships";
    private const string InvitesContainer = "friend-invites";

    public FriendsController(ICosmosDbService cosmosDb, INotificationService notificationService, ILogger<FriendsController> logger)
    {
        _cosmosDb = cosmosDb;
        _notificationService = notificationService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    private string GetDisplayName() => User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
    private string GetEmail() => User.FindFirstValue(ClaimTypes.Email) ?? "";

    // ── Friends list ──

    [HttpGet]
    public async Task<ActionResult<List<Friendship>>> ListFriends()
    {
        var userId = GetUserId();
        var (friends, _) = await _cosmosDb.QueryAsync<Friendship>(
            FriendshipsContainer, userId, maxItems: 100,
            predicate: f => f.Status == FriendshipStatus.Accepted);
        return Ok(friends);
    }

    [HttpGet("requests")]
    public async Task<ActionResult<object>> ListRequests()
    {
        var userId = GetUserId();
        var (all, _) = await _cosmosDb.QueryAsync<Friendship>(
            FriendshipsContainer, userId, maxItems: 100,
            predicate: f => f.Status == FriendshipStatus.PendingSent || f.Status == FriendshipStatus.PendingReceived);

        var sent = all.Where(f => f.Status == FriendshipStatus.PendingSent).ToList();
        var received = all.Where(f => f.Status == FriendshipStatus.PendingReceived).ToList();
        return Ok(new { sent, received });
    }

    // ── Invite generation ──

    [HttpPost("invite")]
    public async Task<ActionResult<FriendInvite>> CreateInvite()
    {
        var userId = GetUserId();
        var invite = new FriendInvite
        {
            UserId = userId,
            UserDisplayName = GetDisplayName(),
        };

        await _cosmosDb.CreateAsync(InvitesContainer, invite, invite.PartitionKey);
        _logger.LogInformation("User {UserId} created friend invite {InviteId}", userId, invite.Id);
        return Ok(invite);
    }

    // ── Join via invite code ──

    [HttpPost("join/{code}")]
    public async Task<ActionResult> JoinViaInvite(string code)
    {
        var userId = GetUserId();
        var invite = await _cosmosDb.GetAsync<FriendInvite>(InvitesContainer, code.ToUpperInvariant(), code.ToUpperInvariant());

        if (invite == null || !invite.IsActive || invite.ExpiresAt < DateTime.UtcNow)
            return NotFound(new { error = "Invite not found or expired." });

        if (invite.UsedBy.Count >= invite.MaxUses)
            return BadRequest(new { error = "This invite has already been used." });

        if (invite.UserId == userId)
            return BadRequest(new { error = "You cannot use your own invite." });

        // Check if already friends or pending
        var (existing, _) = await _cosmosDb.QueryAsync<Friendship>(
            FriendshipsContainer, userId, maxItems: 1,
            predicate: f => f.FriendId == invite.UserId);

        if (existing.Count > 0)
        {
            var status = existing[0].Status;
            if (status == FriendshipStatus.Accepted)
                return BadRequest(new { error = "You are already friends." });
            if (status == FriendshipStatus.PendingSent || status == FriendshipStatus.PendingReceived)
                return BadRequest(new { error = "A friend request is already pending." });
        }

        // Get inviter's user info
        var inviter = await _cosmosDb.GetAsync<User>("users", invite.UserId, invite.UserId);
        var inviterName = inviter?.DisplayName ?? invite.UserDisplayName;
        var inviterEmail = inviter?.Email;

        // Create dual friendship docs
        var myDoc = new Friendship
        {
            UserId = userId,
            FriendId = invite.UserId,
            FriendDisplayName = inviterName,
            FriendEmail = inviterEmail,
            Status = FriendshipStatus.PendingReceived,
        };

        var theirDoc = new Friendship
        {
            UserId = invite.UserId,
            FriendId = userId,
            FriendDisplayName = GetDisplayName(),
            FriendEmail = GetEmail(),
            Status = FriendshipStatus.PendingSent,
        };

        // Wait — actually the joiner is accepting the invite, so the inviter sent it.
        // The inviter's doc should be "pending-sent" and the joiner's doc is "pending-received"
        // But for invite flow, the joiner needs to confirm. Let's make it:
        // Inviter = pending-sent (they initiated by sharing the link)
        // Joiner = pending-received (they need to accept)
        // Actually, the joiner clicking the link IS the acceptance from their side.
        // The inviter still needs to see it. Let's auto-accept from joiner side:
        // Inviter gets pending-received (needs to accept), Joiner gets pending-sent

        // Re-think: invite link = inviter is offering friendship
        // Joiner clicking = joiner wants to accept
        // So: inviter has pending-received (confirm the joiner), joiner has pending-sent
        // Actually simplest UX: clicking invite = instant friendship (inviter pre-approved by sharing link)
        myDoc.Status = FriendshipStatus.Accepted;
        theirDoc.Status = FriendshipStatus.Accepted;

        await _cosmosDb.CreateAsync(FriendshipsContainer, myDoc, myDoc.PartitionKey);
        await _cosmosDb.CreateAsync(FriendshipsContainer, theirDoc, theirDoc.PartitionKey);

        // Mark invite used
        invite.UsedBy.Add(userId);
        if (invite.UsedBy.Count >= invite.MaxUses)
            invite.IsActive = false;
        await _cosmosDb.UpsertAsync(InvitesContainer, invite, invite.PartitionKey);

        // Notify the inviter
        await _notificationService.CreateAsync(new Notification
        {
            UserId = invite.UserId,
            Type = NotificationType.FriendAccepted,
            Title = $"{GetDisplayName()} accepted your friend invite",
            SourceUserId = userId,
            SourceDisplayName = GetDisplayName(),
            ReferenceType = "friendship",
            ReferenceId = theirDoc.Id,
        });

        _logger.LogInformation("User {UserId} joined via invite {InviteId} from {InviterId}", userId, code, invite.UserId);
        return Ok(new { friendshipId = myDoc.Id, friendDisplayName = inviterName });
    }

    // ── Accept / Decline ──

    [HttpPut("{friendshipId}/accept")]
    public async Task<ActionResult> AcceptRequest(string friendshipId)
    {
        var userId = GetUserId();
        var myDoc = await _cosmosDb.GetAsync<Friendship>(FriendshipsContainer, friendshipId, userId);
        if (myDoc == null || myDoc.UserId != userId || myDoc.Status != FriendshipStatus.PendingReceived)
            return NotFound();

        myDoc.Status = FriendshipStatus.Accepted;
        myDoc.UpdatedAt = DateTime.UtcNow;
        await _cosmosDb.UpsertAsync(FriendshipsContainer, myDoc, myDoc.PartitionKey);

        // Update the other side
        var (theirDocs, _) = await _cosmosDb.QueryAsync<Friendship>(
            FriendshipsContainer, myDoc.FriendId, maxItems: 1,
            predicate: f => f.FriendId == userId && f.Status == FriendshipStatus.PendingSent);

        if (theirDocs.Count > 0)
        {
            var theirDoc = theirDocs[0];
            theirDoc.Status = FriendshipStatus.Accepted;
            theirDoc.UpdatedAt = DateTime.UtcNow;
            await _cosmosDb.UpsertAsync(FriendshipsContainer, theirDoc, theirDoc.PartitionKey);
        }

        await _notificationService.CreateAsync(new Notification
        {
            UserId = myDoc.FriendId,
            Type = NotificationType.FriendAccepted,
            Title = $"{GetDisplayName()} accepted your friend request",
            SourceUserId = userId,
            SourceDisplayName = GetDisplayName(),
            ReferenceType = "friendship",
            ReferenceId = myDoc.Id,
        });

        return Ok();
    }

    [HttpPut("{friendshipId}/decline")]
    public async Task<ActionResult> DeclineRequest(string friendshipId)
    {
        var userId = GetUserId();
        var myDoc = await _cosmosDb.GetAsync<Friendship>(FriendshipsContainer, friendshipId, userId);
        if (myDoc == null || myDoc.UserId != userId || myDoc.Status != FriendshipStatus.PendingReceived)
            return NotFound();

        myDoc.Status = FriendshipStatus.Declined;
        myDoc.UpdatedAt = DateTime.UtcNow;
        await _cosmosDb.UpsertAsync(FriendshipsContainer, myDoc, myDoc.PartitionKey);

        // Update the other side
        var (theirDocs, _) = await _cosmosDb.QueryAsync<Friendship>(
            FriendshipsContainer, myDoc.FriendId, maxItems: 1,
            predicate: f => f.FriendId == userId && f.Status == FriendshipStatus.PendingSent);

        if (theirDocs.Count > 0)
        {
            var theirDoc = theirDocs[0];
            theirDoc.Status = FriendshipStatus.Declined;
            theirDoc.UpdatedAt = DateTime.UtcNow;
            await _cosmosDb.UpsertAsync(FriendshipsContainer, theirDoc, theirDoc.PartitionKey);
        }

        return Ok();
    }

    // ── Remove friend ──

    [HttpDelete("{friendshipId}")]
    public async Task<ActionResult> RemoveFriend(string friendshipId)
    {
        var userId = GetUserId();
        var myDoc = await _cosmosDb.GetAsync<Friendship>(FriendshipsContainer, friendshipId, userId);
        if (myDoc == null || myDoc.UserId != userId)
            return NotFound();

        await _cosmosDb.DeleteAsync(FriendshipsContainer, friendshipId, userId);

        // Delete the other side
        var (theirDocs, _) = await _cosmosDb.QueryAsync<Friendship>(
            FriendshipsContainer, myDoc.FriendId, maxItems: 1,
            predicate: f => f.FriendId == userId);

        if (theirDocs.Count > 0)
            await _cosmosDb.DeleteAsync(FriendshipsContainer, theirDocs[0].Id, myDoc.FriendId);

        _logger.LogInformation("User {UserId} removed friend {FriendId}", userId, myDoc.FriendId);
        return NoContent();
    }

    // ── Browse friend's data ──

    [HttpGet("{friendId}/items")]
    public async Task<ActionResult<PagedResponse<Item>>> GetFriendItems(string friendId, [FromQuery] string? continuationToken)
    {
        var userId = GetUserId();
        if (!await AreFriendsAsync(userId, friendId))
            return Forbid();

        var (items, nextToken) = await _cosmosDb.QueryAsync<Item>("items", friendId, continuationToken);
        return Ok(new PagedResponse<Item> { Items = items, ContinuationToken = nextToken, HasMore = nextToken != null });
    }

    [HttpGet("{friendId}/items/{itemId}")]
    public async Task<ActionResult<Item>> GetFriendItem(string friendId, string itemId)
    {
        var userId = GetUserId();
        if (!await AreFriendsAsync(userId, friendId))
            return Forbid();

        var item = await _cosmosDb.GetAsync<Item>("items", itemId, friendId);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpGet("{friendId}/venues")]
    public async Task<ActionResult<PagedResponse<Venue>>> GetFriendVenues(string friendId, [FromQuery] string? continuationToken)
    {
        var userId = GetUserId();
        if (!await AreFriendsAsync(userId, friendId))
            return Forbid();

        var (venues, nextToken) = await _cosmosDb.QueryAsync<Venue>("venues", friendId, continuationToken);
        return Ok(new PagedResponse<Venue> { Items = venues, ContinuationToken = nextToken, HasMore = nextToken != null });
    }

    [HttpGet("{friendId}/venues/{venueId}")]
    public async Task<ActionResult<Venue>> GetFriendVenue(string friendId, string venueId)
    {
        var userId = GetUserId();
        if (!await AreFriendsAsync(userId, friendId))
            return Forbid();

        var venue = await _cosmosDb.GetAsync<Venue>("venues", venueId, friendId);
        if (venue == null) return NotFound();
        return Ok(venue);
    }

    // ── Helpers ──

    private async Task<bool> AreFriendsAsync(string userId, string friendId)
    {
        var (friends, _) = await _cosmosDb.QueryAsync<Friendship>(
            FriendshipsContainer, userId, maxItems: 1,
            predicate: f => f.FriendId == friendId && f.Status == FriendshipStatus.Accepted);
        return friends.Count > 0;
    }
}
