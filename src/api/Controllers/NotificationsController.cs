using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "MultiAuth,ApiKey")]
public class NotificationsController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly ILogger<NotificationsController> _logger;
    private const string ContainerName = "notifications";

    public NotificationsController(ICosmosDbService cosmosDb, ILogger<NotificationsController> logger)
    {
        _cosmosDb = cosmosDb;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<ActionResult<object>> ListNotifications([FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        var (notifications, _) = await _cosmosDb.QueryAsync<Notification>(
            ContainerName, userId, maxItems: limit);

        var sorted = notifications.OrderByDescending(n => n.CreatedAt).ToList();
        var unreadCount = sorted.Count(n => !n.IsRead);

        return Ok(new { notifications = sorted, unreadCount });
    }

    [HttpPut("{id}/read")]
    public async Task<ActionResult> MarkRead(string id)
    {
        var userId = GetUserId();
        var notification = await _cosmosDb.GetAsync<Notification>(ContainerName, id, userId);
        if (notification == null || notification.UserId != userId) return NotFound();

        notification.IsRead = true;
        await _cosmosDb.UpsertAsync(ContainerName, notification, notification.PartitionKey);
        return Ok();
    }

    [HttpPut("read-all")]
    public async Task<ActionResult> MarkAllRead()
    {
        var userId = GetUserId();
        var (notifications, _) = await _cosmosDb.QueryAsync<Notification>(
            ContainerName, userId, maxItems: 200,
            predicate: n => !n.IsRead);

        foreach (var n in notifications)
        {
            n.IsRead = true;
            await _cosmosDb.UpsertAsync(ContainerName, n, n.PartitionKey);
        }

        return Ok(new { updated = notifications.Count });
    }
}
