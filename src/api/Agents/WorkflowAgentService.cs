using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using WhiskeyAndSmokes.Api.Agents.Models;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Agents;

/// <summary>
/// IAgentService implementation that uses the multi-agent workflow pipeline.
/// Falls back to local keyword extraction when AI Foundry is not configured.
/// </summary>
public class WorkflowAgentService : IAgentService
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IPromptService _promptService;
    private readonly IBlobStorageService _blobService;
    private readonly ILogger<WorkflowAgentService> _logger;
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly bool _isFoundryConfigured;

    public WorkflowAgentService(
        ICosmosDbService cosmosDb,
        IPromptService promptService,
        IBlobStorageService blobService,
        ILogger<WorkflowAgentService> logger,
        IConfiguration config,
        ILoggerFactory loggerFactory)
    {
        _cosmosDb = cosmosDb;
        _promptService = promptService;
        _blobService = blobService;
        _logger = logger;
        _config = config;
        _loggerFactory = loggerFactory;
        _isFoundryConfigured = !string.IsNullOrEmpty(config["AiFoundry:Endpoint"]);
    }

    public async Task ProcessCaptureAsync(Capture capture)
    {
        using var activity = Diagnostics.Agent.StartActivity("ProcessCapture");
        activity?.SetTag("capture.id", capture.Id);
        activity?.SetTag("capture.user_id", capture.UserId);
        activity?.SetTag("capture.photo_count", capture.Photos.Count);

        _logger.LogInformation(
            "Processing capture {CaptureId} for user {UserId} with {PhotoCount} photos, noteLength={NoteLength}",
            capture.Id, capture.UserId, capture.Photos.Count, capture.UserNote?.Length ?? 0);

        try
        {
            capture.Status = CaptureStatus.Processing;
            capture.UpdatedAt = DateTime.UtcNow;
            await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);

            List<Item> items;

            if (_isFoundryConfigured)
            {
                _logger.LogInformation("Running multi-agent workflow for capture {CaptureId}", capture.Id);
                items = await RunWorkflowAsync(capture);
            }
            else
            {
                _logger.LogWarning("AI Foundry not configured — using local extraction for capture {CaptureId}", capture.Id);
                items = LocalExtraction.Process(capture, _logger);
            }

            activity?.SetTag("items.count", items.Count);

            foreach (var item in items)
            {
                await _cosmosDb.CreateAsync("items", item, item.PartitionKey);
                capture.ItemIds.Add(item.Id);
                _logger.LogDebug("Persisted item {ItemId} (type={ItemType}, name={ItemName})",
                    item.Id, item.Type, item.Name);
            }

            capture.Status = CaptureStatus.Completed;
            capture.UpdatedAt = DateTime.UtcNow;
            await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);

            _logger.LogInformation(
                "Capture {CaptureId} completed: {ItemCount} items, types=[{Types}]",
                capture.Id, items.Count, string.Join(", ", items.Select(i => i.Type).Distinct()));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to process capture {CaptureId}: {Error}", capture.Id, ex.Message);

            // Fall back to local extraction if the workflow fails
            try
            {
                _logger.LogWarning("Attempting local extraction fallback for capture {CaptureId}", capture.Id);
                var fallbackItems = LocalExtraction.Process(capture, _logger);

                foreach (var item in fallbackItems)
                {
                    await _cosmosDb.CreateAsync("items", item, item.PartitionKey);
                    capture.ItemIds.Add(item.Id);
                }

                capture.Status = CaptureStatus.Completed;
                capture.ProcessingError = $"AI workflow failed ({ex.Message}), used local extraction";
                capture.UpdatedAt = DateTime.UtcNow;
                await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);

                _logger.LogWarning(
                    "Capture {CaptureId} completed via local extraction fallback: {ItemCount} items",
                    capture.Id, fallbackItems.Count);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Local extraction fallback also failed for capture {CaptureId}", capture.Id);
                capture.Status = CaptureStatus.Failed;
                capture.ProcessingError = ex.Message;
                capture.UpdatedAt = DateTime.UtcNow;
                await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);
            }
        }
    }

    private async Task<List<Item>> RunWorkflowAsync(Capture capture)
    {
        using var activity = Diagnostics.Agent.StartActivity("RunWorkflow");
        activity?.SetTag("capture.id", capture.Id);

        var workflow = CaptureWorkflow.Build(_config, _promptService, _blobService, _loggerFactory);

        var input = new CaptureInput
        {
            CaptureId = capture.Id,
            UserId = capture.UserId,
            PhotoUrls = capture.Photos,
            UserNote = capture.UserNote,
            Location = capture.Location != null
                ? new Agents.Models.GeoCoordinate
                {
                    Latitude = capture.Location.Latitude,
                    Longitude = capture.Location.Longitude
                }
                : null
        };

        _logger.LogDebug("Starting workflow for capture {CaptureId}", capture.Id);

        await using var run = await InProcessExecution.RunAsync(workflow, input);

        CuratorDecision? decision = null;
        foreach (var evt in run.NewEvents)
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                _logger.LogDebug("Workflow output event: {EventType}", outputEvent.Data?.GetType().Name);
                if (outputEvent.Data is CuratorDecision d)
                {
                    decision = d;
                }
            }
            else if (evt is WorkflowErrorEvent errorEvent)
            {
                _logger.LogError("Workflow error: {Error}", errorEvent.ToString());
            }
        }

        if (decision?.IsApproved == true && decision.Items != null)
        {
            _logger.LogInformation("Workflow approved {ItemCount} items for capture {CaptureId}",
                decision.Items.Count, capture.Id);
            return ConvertToItems(decision.Items, capture);
        }

        _logger.LogWarning("Workflow did not produce approved items for capture {CaptureId} — falling back to local extraction",
            capture.Id);
        return LocalExtraction.Process(capture, _logger);
    }

    private static List<Item> ConvertToItems(List<CuratorItemResult> curatorItems, Capture capture)
    {
        return curatorItems.Select(p => new Item
        {
            UserId = capture.UserId,
            CaptureId = capture.Id,
            Type = NormalizeType(p.Type),
            Name = p.Name ?? "Unknown Item",
            Brand = p.Brand,
            Category = p.Category,
            Details = p.Details != null ? JsonSerializer.SerializeToElement(p.Details) : null,
            Venue = p.Venue != null ? new VenueInfo
            {
                Name = p.Venue.Name ?? "Unknown Venue",
                Address = p.Venue.Address
            } : null,
            PhotoUrls = capture.Photos,
            AiConfidence = p.Confidence ?? 0.8,
            AiSummary = p.Summary,
            Tags = p.Tags ?? [],
            Status = ItemStatus.AiDraft
        }).ToList();
    }

    private static string NormalizeType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "whiskey" or "whisky" or "bourbon" or "scotch" or "rye" => ItemType.Whiskey,
            "wine" or "red wine" or "white wine" or "rosé" => ItemType.Wine,
            "cocktail" or "mixed drink" => ItemType.Cocktail,
            "cigar" => ItemType.Cigar,
            _ => type?.ToLowerInvariant() ?? "unknown"
        };
    }
}
