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
    private readonly ILogger<CaptureProcessingService> _logger;

    public CaptureProcessingService(
        Channel<Capture> channel,
        IAgentService agentService,
        ILogger<CaptureProcessingService> logger)
    {
        _channel = channel;
        _agentService = agentService;
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
            try
            {
                _logger.LogInformation("Dequeued capture {CaptureId} for processing", capture.Id);
                await _agentService.ProcessCaptureAsync(capture);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing capture {CaptureId}", capture.Id);
            }
        }

        _logger.LogInformation("Capture processing service stopped");
    }
}
