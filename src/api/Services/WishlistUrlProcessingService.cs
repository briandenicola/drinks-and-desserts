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
    private readonly ILogger<WishlistUrlProcessingService> _logger;
    private const string ContainerName = "items";
    private const string CapturesContainerName = "captures";

    public WishlistUrlProcessingService(
        Channel<WishlistUrlWorkItem> channel,
        IWishlistUrlService urlService,
        ICosmosDbService cosmosDb,
        ILogger<WishlistUrlProcessingService> logger)
    {
        _channel = channel;
        _urlService = urlService;
        _cosmosDb = cosmosDb;
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
                    if (!string.IsNullOrWhiteSpace(result.Name))
                        item.Name = result.Name.Trim();
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing wishlist URL for item {ItemId}: {Url}",
                    workItem.ItemId, workItem.Url);
            }
        }

        _logger.LogInformation("Wishlist URL processing service stopped");
    }
}
