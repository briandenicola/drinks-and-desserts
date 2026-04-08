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

                var result = await _urlService.ExtractFromUrlAsync(workItem.Url);

                var item = await _cosmosDb.GetAsync<Item>(ContainerName, workItem.ItemId, workItem.UserId);
                if (item == null)
                {
                    _logger.LogWarning("Wishlist item {ItemId} not found after URL extraction", workItem.ItemId);
                    continue;
                }

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
                    _logger.LogInformation("Enriched wishlist item {ItemId} with AI-extracted data from {Url}",
                        workItem.ItemId, workItem.Url);
                }
                else
                {
                    _logger.LogWarning("URL extraction failed for item {ItemId}: {Error}",
                        workItem.ItemId, result.Error);
                    item.UserNotes = $"URL extraction failed: {result.Error}\nSource: {workItem.Url}";
                }

                item.UpdatedAt = DateTime.UtcNow;
                await _cosmosDb.UpsertAsync(ContainerName, item, item.PartitionKey);
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
