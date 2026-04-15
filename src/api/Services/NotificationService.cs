using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface INotificationService
{
    Task CreateAsync(Notification notification);
}

public class NotificationService : INotificationService
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly ILogger<NotificationService> _logger;
    private const string ContainerName = "notifications";

    public NotificationService(ICosmosDbService cosmosDb, ILogger<NotificationService> logger)
    {
        _cosmosDb = cosmosDb;
        _logger = logger;
    }

    public async Task CreateAsync(Notification notification)
    {
        try
        {
            await _cosmosDb.CreateAsync(ContainerName, notification, notification.PartitionKey);
            _logger.LogInformation("Created {Type} notification for user {UserId} from {SourceUserId}",
                notification.Type, notification.UserId, notification.SourceUserId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create notification for user {UserId}", notification.UserId);
        }
    }
}
