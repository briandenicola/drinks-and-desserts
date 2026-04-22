using System.Threading.Channels;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// Background service that processes wishlist URL extraction requests from a Channel queue.
/// Creates a placeholder wishlist item immediately, then enriches it with AI-extracted data.
/// </summary>
public class WishlistUrlProcessingService : BackgroundService
{
    private readonly Channel<WishlistUrlWorkItem> _channel;
    private readonly IWishlistUrlService _urlService;
    private readonly ICosmosDbService _cosmosDb;
    private readonly INotificationService _notificationService;
    private readonly ILogger<WishlistUrlProcessingService> _logger;
    private const string ContainerName = "items";
    private const string CapturesContainerName = "captures";

    public WishlistUrlProcessingService(
        Channel<WishlistUrlWorkItem> channel,
        IWishlistUrlService urlService,
        ICosmosDbService cosmosDb,
        INotificationService notificationService,
        ILogger<WishlistUrlProcessingService> logger)
    {
        _channel = channel;
        _urlService = urlService;
        _cosmosDb = cosmosDb;
        _notificationService = notificationService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Wishlist URL processing service started");

        await foreach (var workItem in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing wishlist URL extraction for item {ItemId}: {Url}",
                    workItem.ItemId, workItem.Url);

                // Create a capture record for history tracking
                var capture = new Capture
                {
                    UserId = workItem.UserId,
                    UserNote = $"Wishlist URL extraction: {workItem.Url}",
                    Status = CaptureStatus.Processing,
                    ProcessedBy = ProcessingSource.Pending,
                    ItemIds = [workItem.ItemId],
                };
                capture = await _cosmosDb.CreateAsync(CapturesContainerName, capture, capture.PartitionKey);

                // Link the item to this capture
                var item = await _cosmosDb.GetAsync<Item>(ContainerName, workItem.ItemId, workItem.UserId);
                if (item == null)
                {
                    _logger.LogWarning("Wishlist item {ItemId} not found after URL extraction", workItem.ItemId);
                    capture.Status = CaptureStatus.Failed;
                    capture.ProcessingError = "Wishlist item not found";
                    capture.UpdatedAt = DateTime.UtcNow;
                    await _cosmosDb.UpsertAsync(CapturesContainerName, capture, capture.PartitionKey);
                    continue;
                }

                item.CaptureId = capture.Id;

                // Step 1: Fetch and extract
                capture.WorkflowSteps.Add(new WorkflowStep
                {
                    StepId = "W01",
                    AgentName = "URL Fetcher",
                    Status = WorkflowStepStatus.Running,
                    Summary = $"Fetching content from {workItem.Url}",
                });
                await _cosmosDb.UpsertAsync(CapturesContainerName, capture, capture.PartitionKey);

                var result = await _urlService.ExtractFromUrlAsync(workItem.Url);

                // Update step 1 status
                capture.WorkflowSteps[0].Status = result.Success
                    ? WorkflowStepStatus.Complete
                    : WorkflowStepStatus.Error;
                capture.WorkflowSteps[0].Summary = result.Success
                    ? "Content fetched and analyzed"
                    : $"Extraction failed: {result.Error}";

                // Step 2: Enrich item
                capture.WorkflowSteps.Add(new WorkflowStep
                {
                    StepId = "W02",
                    AgentName = "Wishlist URL Extractor",
                    Status = result.Success ? WorkflowStepStatus.Complete : WorkflowStepStatus.Error,
                    Summary = result.Success
                        ? $"Extracted: {result.Name ?? "unknown"} ({result.Type ?? "custom"})"
                        : result.Error ?? "Unknown error",
                });

                if (result.Success)
                {
                    // Always update name — never leave the placeholder
                    if (!string.IsNullOrWhiteSpace(result.Name))
                        item.Name = result.Name.Trim();
                    else if (IsPlaceholderName(item.Name))
                        item.Name = ExtractDomainLabel(workItem.Url);

                    if (!string.IsNullOrWhiteSpace(result.Brand))
                        item.Brand = result.Brand.Trim();
                    if (!string.IsNullOrWhiteSpace(result.Category))
                        item.Category = result.Category.Trim();
                    if (!string.IsNullOrWhiteSpace(result.Notes))
                        item.UserNotes = result.Notes.Trim();

                    var type = result.Type?.ToLowerInvariant() ?? "custom";
                    if (ItemType.All.Contains(type))
                        item.Type = type;

                    item.ProcessedBy = ProcessingSource.AiFoundry;
                    capture.Status = CaptureStatus.Completed;
                    capture.ProcessedBy = ProcessingSource.AiFoundry;

                    _logger.LogInformation("Enriched wishlist item {ItemId} with AI-extracted data from {Url}",
                        workItem.ItemId, workItem.Url);
                }
                else
                {
                    _logger.LogWarning("URL extraction failed for item {ItemId}: {Error}",
                        workItem.ItemId, result.Error);

                    // Still provide a meaningful name from the domain
                    if (IsPlaceholderName(item.Name))
                        item.Name = ExtractDomainLabel(workItem.Url);

                    item.UserNotes = $"URL extraction failed: {result.Error}\nSource: {workItem.Url}";
                    capture.Status = CaptureStatus.Failed;
                    capture.ProcessingError = result.Error;
                }

                // Record the source URL in the journal
                item.Journal.Add(new JournalEntry
                {
                    Text = $"Added from URL: {workItem.Url}",
                    Date = DateTime.UtcNow,
                    Source = "url-extraction"
                });

                item.UpdatedAt = DateTime.UtcNow;
                capture.UpdatedAt = DateTime.UtcNow;

                await _cosmosDb.UpsertAsync(ContainerName, item, item.PartitionKey);
                await _cosmosDb.UpsertAsync(CapturesContainerName, capture, capture.PartitionKey);

                // Notify user when wishlist analysis completes successfully
                if (capture.Status == CaptureStatus.Completed)
                {
                    await _notificationService.CreateAsync(new Notification
                    {
                        UserId = workItem.UserId,
                        Type = NotificationType.WorkflowCompleted,
                        Title = $"Wishlist item analysis complete: {item.Name}",
                        Detail = $"Added {item.Type ?? "item"} from {(Uri.TryCreate(workItem.Url, UriKind.Absolute, out var uri) ? uri.Host : "URL")}",
                        SourceUserId = workItem.UserId,
                        SourceDisplayName = "System",
                        ReferenceType = "item",
                        ReferenceId = item.Id
                    });
                }
                else if (capture.Status == CaptureStatus.Failed)
                {
                    await _notificationService.CreateAsync(new Notification
                    {
                        UserId = workItem.UserId,
                        Type = NotificationType.WorkflowFailed,
                        Title = $"Wishlist extraction failed: {item.Name}",
                        Detail = capture.ProcessingError ?? "Could not extract item details from URL",
                        SourceUserId = workItem.UserId,
                        SourceDisplayName = "System",
                        ReferenceType = "item",
                        ReferenceId = item.Id
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing wishlist URL for item {ItemId}: {Url}",
                    workItem.ItemId, workItem.Url);
            }
        }

        _logger.LogInformation("Wishlist URL processing service stopped");
    }

    private static bool IsPlaceholderName(string name) =>
        string.IsNullOrWhiteSpace(name) ||
        name.Contains("Extracting from", StringComparison.OrdinalIgnoreCase);

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
                if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    host = host[4..];
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
