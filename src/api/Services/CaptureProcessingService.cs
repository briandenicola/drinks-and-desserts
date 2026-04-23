using System.Threading.Channels;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// Background service that processes captures from a Channel queue.
/// Replaces fire-and-forget Task.Run with a durable, observable queue.
/// </summary>
public class CaptureProcessingService : BackgroundService
{
    private readonly Channel<Capture> _channel;
    private readonly IAgentService _agentService;
    private readonly ICosmosDbService _cosmosDb;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CaptureProcessingService> _logger;

    public CaptureProcessingService(
        Channel<Capture> channel,
        IAgentService agentService,
        ICosmosDbService cosmosDb,
        INotificationService notificationService,
        ILogger<CaptureProcessingService> logger)
    {
        _channel = channel;
        _agentService = agentService;
        _cosmosDb = cosmosDb;
        _notificationService = notificationService;
        _logger = logger;
    }

    public ValueTask QueueAsync(Capture capture, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(capture, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Capture processing service started");

        await foreach (var capture in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Processing capture {CaptureId} (attempt {Attempt}/{Max})",
                        capture.Id, attempt, maxRetries);
                    await _agentService.ProcessCaptureAsync(capture);
                    break;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(4, attempt - 1)); // 1s, 4s, 16s
                    _logger.LogWarning(ex,
                        "Error processing capture {CaptureId} on attempt {Attempt}/{Max}. Retrying in {Delay}s",
                        capture.Id, attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Capture {CaptureId} failed after {Max} attempts. UserId={UserId}, PhotoCount={PhotoCount}",
                        capture.Id, maxRetries, capture.UserId, capture.Photos.Count);

                    try
                    {
                        capture.Status = CaptureStatus.Failed;
                        capture.ProcessingError = ex.Message;
                        capture.UpdatedAt = DateTime.UtcNow;
                        await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);

                        await _notificationService.CreateAsync(new Notification
                        {
                            UserId = capture.UserId,
                            Type = NotificationType.WorkflowFailed,
                            Title = "Capture processing failed",
                            Detail = $"Failed after {maxRetries} attempts: {ex.Message}",
                            SourceUserId = capture.UserId,
                            SourceDisplayName = "System",
                            ReferenceType = "capture",
                            ReferenceId = capture.Id
                        });
                    }
                    catch (Exception statusEx)
                    {
                        _logger.LogWarning(statusEx, "Failed to update status for capture {CaptureId}", capture.Id);
                    }
                }
            }
        }

        _logger.LogInformation("Capture processing service stopped");
    }
}
