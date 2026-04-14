using System.Threading.Channels;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// Background service that processes venue URL extraction requests from a Channel queue.
/// Creates a placeholder venue immediately, then enriches it with AI-extracted data.
/// </summary>
public class VenueUrlProcessingService : BackgroundService
{
    private readonly Channel<VenueUrlWorkItem> _channel;
    private readonly IVenueUrlService _urlService;
    private readonly ICosmosDbService _cosmosDb;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<VenueUrlProcessingService> _logger;
    private const string ContainerName = "venues";

    public VenueUrlProcessingService(
        Channel<VenueUrlWorkItem> channel,
        IVenueUrlService urlService,
        ICosmosDbService cosmosDb,
        IBlobStorageService blobStorage,
        ILogger<VenueUrlProcessingService> logger)
    {
        _channel = channel;
        _urlService = urlService;
        _cosmosDb = cosmosDb;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Venue URL processing service started");

        await foreach (var workItem in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing venue URL extraction for venue {VenueId}: {Url}",
                    workItem.VenueId, workItem.Url);

                var venue = await _cosmosDb.GetAsync<Venue>(ContainerName, workItem.VenueId, workItem.UserId);
                if (venue == null)
                {
                    _logger.LogWarning("Venue {VenueId} not found after URL extraction", workItem.VenueId);
                    continue;
                }

                venue.WorkflowSteps = [];
                await LogStep(venue, "V01", "URL Fetch", WorkflowStepStatus.Running,
                    $"Fetching content from {new Uri(workItem.Url).Host}");

                var fetchResult = await _urlService.FetchPageContentAsync(workItem.Url);

                if (!fetchResult.Success)
                {
                    await LogStep(venue, "V01", "URL Fetch", WorkflowStepStatus.Error,
                        "Failed to fetch page content", fetchResult.Error);

                    if (IsPlaceholderName(venue.Name))
                        venue.Name = ExtractDomainLabel(workItem.Url);

                    venue.Status = VenueStatus.Failed;
                    venue.ProcessingError = fetchResult.Error;
                    venue.UpdatedAt = DateTime.UtcNow;
                    await _cosmosDb.UpsertAsync(ContainerName, venue, venue.PartitionKey);
                    continue;
                }

                await LogStep(venue, "V01", "URL Fetch", WorkflowStepStatus.Complete,
                    $"Retrieved {fetchResult.ContentLength} chars of content");

                await LogStep(venue, "V02", "Venue URL Extractor", WorkflowStepStatus.Running,
                    "AI agent analyzing page content for venue details");

                var result = await _urlService.ExtractFromContentAsync(workItem.Url, fetchResult.Content!);

                if (result.Success)
                {
                    if (!string.IsNullOrWhiteSpace(result.Name))
                        venue.Name = result.Name.Trim();
                    else if (IsPlaceholderName(venue.Name))
                        venue.Name = ExtractDomainLabel(workItem.Url);

                    if (!string.IsNullOrWhiteSpace(result.Address))
                        venue.Address = result.Address.Trim();

                    if (!string.IsNullOrWhiteSpace(result.Website))
                        venue.Website = result.Website.Trim();

                    var type = result.Type?.ToLowerInvariant() ?? VenueType.Restaurant;
                    if (VenueType.All.Contains(type))
                        venue.Type = type;

                    await LogStep(venue, "V02", "Venue URL Extractor", WorkflowStepStatus.Complete,
                        $"Extracted: {venue.Name}",
                        $"Type: {venue.Type}, Address: {venue.Address ?? "N/A"}");

                    venue.Status = VenueStatus.Completed;
                    _logger.LogInformation("Enriched venue {VenueId} with AI-extracted data from {Url}",
                        workItem.VenueId, workItem.Url);

                    // V03: Logo extraction
                    var logoUrlCandidates = new List<string>();
                    if (!string.IsNullOrWhiteSpace(result.LogoUrl))
                        logoUrlCandidates.Add(result.LogoUrl);
                    logoUrlCandidates.AddRange(fetchResult.ImageUrls);

                    if (logoUrlCandidates.Count > 0)
                    {
                        await LogStep(venue, "V03", "Logo Extraction", WorkflowStepStatus.Running,
                            $"Attempting to download logo from {logoUrlCandidates.Count} candidate(s)");

                        string? uploadedBlobUrl = null;
                        foreach (var candidateUrl in logoUrlCandidates.Distinct().Take(5))
                        {
                            var logoBase64 = await _urlService.DownloadLogoAsync(candidateUrl, workItem.Url);
                            if (logoBase64 != null)
                            {
                                try
                                {
                                    var logoBytes = Convert.FromBase64String(logoBase64);
                                    using var stream = new MemoryStream(logoBytes);
                                    uploadedBlobUrl = await _blobStorage.UploadAsync(
                                        workItem.UserId, $"venue-logo-{venue.Id}.png", stream);
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to upload logo for venue {VenueId}", venue.Id);
                                }
                            }
                        }

                        if (uploadedBlobUrl != null)
                        {
                            venue.PhotoUrls.Insert(0, uploadedBlobUrl);
                            await LogStep(venue, "V03", "Logo Extraction", WorkflowStepStatus.Complete,
                                "Logo downloaded and saved as venue photo");
                        }
                        else
                        {
                            await LogStep(venue, "V03", "Logo Extraction", WorkflowStepStatus.Complete,
                                "No suitable logo found — skipped",
                                "All candidate images were unavailable or too small");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("URL extraction failed for venue {VenueId}: {Error}",
                        workItem.VenueId, result.Error);

                    await LogStep(venue, "V02", "Venue URL Extractor", WorkflowStepStatus.Error,
                        "Extraction failed — used fallback naming",
                        result.Error);

                    if (IsPlaceholderName(venue.Name))
                        venue.Name = ExtractDomainLabel(workItem.Url);

                    venue.Status = VenueStatus.Failed;
                    venue.ProcessingError = result.Error;
                }

                venue.UpdatedAt = DateTime.UtcNow;
                await _cosmosDb.UpsertAsync(ContainerName, venue, venue.PartitionKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing venue URL for venue {VenueId}: {Url}",
                    workItem.VenueId, workItem.Url);

                try
                {
                    var venue = await _cosmosDb.GetAsync<Venue>(ContainerName, workItem.VenueId, workItem.UserId);
                    if (venue != null)
                    {
                        venue.Status = VenueStatus.Failed;
                        venue.ProcessingError = ex.Message;
                        await LogStep(venue, "V99", "System", WorkflowStepStatus.Error,
                            "Unexpected error during processing", ex.Message);
                        venue.UpdatedAt = DateTime.UtcNow;
                        await _cosmosDb.UpsertAsync(ContainerName, venue, venue.PartitionKey);
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogWarning(innerEx, "Failed to update venue {VenueId} status after error", workItem.VenueId);
                }
            }
        }

        _logger.LogInformation("Venue URL processing service stopped");
    }

    private async Task LogStep(Venue venue, string stepId, string agentName, string status,
        string summary, string? detail = null)
    {
        // Update existing step or add new one
        var existing = venue.WorkflowSteps.FindIndex(s => s.StepId == stepId);
        var step = new WorkflowStep
        {
            StepId = stepId,
            AgentName = agentName,
            Status = status,
            Summary = summary,
            Detail = detail,
            Timestamp = DateTime.UtcNow
        };

        if (existing >= 0)
            venue.WorkflowSteps[existing] = step;
        else
            venue.WorkflowSteps.Add(step);

        venue.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _cosmosDb.UpsertAsync(ContainerName, venue, venue.PartitionKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist workflow step {StepId} for venue {VenueId}", stepId, venue.Id);
        }
    }

    private static bool IsPlaceholderName(string name) =>
        string.IsNullOrWhiteSpace(name) ||
        name.Contains("Extracting from", StringComparison.OrdinalIgnoreCase);

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

        return "New Venue";
    }
}
