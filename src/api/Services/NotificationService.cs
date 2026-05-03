using System.Net;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface INotificationService
{
    Task CreateAsync(Notification notification);
}

public class NotificationService : INotificationService
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IPushoverService _pushover;
    private readonly ILogger<NotificationService> _logger;
    private const string ContainerName = "notifications";
    private const string UsersContainer = "users";

    public NotificationService(ICosmosDbService cosmosDb, IPushoverService pushover, ILogger<NotificationService> logger)
    {
        _cosmosDb = cosmosDb;
        _pushover = pushover;
        _logger = logger;
    }

    public async Task CreateAsync(Notification notification)
    {
        // 1. Persist to Cosmos
        try
        {
            await _cosmosDb.CreateAsync(ContainerName, notification, notification.PartitionKey);
            _logger.LogInformation("Created {Type} notification for user {UserId} from {SourceUserId}",
                notification.Type, notification.UserId, notification.SourceUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create {Type} notification for user {UserId} (ref: {RefType}/{RefId})",
                notification.Type, notification.UserId, notification.ReferenceType, notification.ReferenceId);
            return; // Don't attempt push if we couldn't persist
        }

        // 2. Attempt Pushover push (separate error path)
        try
        {
            var user = await _cosmosDb.GetAsync<User>(UsersContainer, notification.UserId, notification.UserId);
            if (user?.Preferences is { PushoverEnabled: true, PushoverAppToken: not null and not "", PushoverUserKey: not null and not "" } prefs)
            {
                var title = WebUtility.HtmlEncode(notification.Title);
                var detail = !string.IsNullOrEmpty(notification.Detail)
                    ? WebUtility.HtmlEncode(notification.Detail)
                    : "";
                var message = $"<b>{title}</b>\n{detail}";

                var url = BuildNotificationUrl(notification);

                await _pushover.SendAsync(prefs.PushoverAppToken, prefs.PushoverUserKey, notification.Title, message, url, prefs.PushoverSound);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Pushover push for notification {NotificationId}", notification.Id);
        }
    }

    private static string? BuildNotificationUrl(Notification notification)
    {
        if (string.IsNullOrEmpty(notification.ReferenceType) || string.IsNullOrEmpty(notification.ReferenceId))
            return null;

        return notification.ReferenceType switch
        {
            "capture" => $"/history/{notification.ReferenceId}",
            "item" => $"/items/{notification.ReferenceId}",
            "venue" => $"/venues/{notification.ReferenceId}",
            _ => null,
        };
    }
}
