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
    private readonly ILogger<VenueUrlProcessingService> _logger;
    private const string ContainerName = "venues";

    public VenueUrlProcessingService(
        Channel<VenueUrlWorkItem> channel,
        IVenueUrlService urlService,
        ICosmosDbService cosmosDb,
        ILogger<VenueUrlProcessingService> logger)
    {
        _channel = channel;
        _urlService = urlService;
        _cosmosDb = cosmosDb;
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

                var result = await _urlService.ExtractFromUrlAsync(workItem.Url);

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

                    _logger.LogInformation("Enriched venue {VenueId} with AI-extracted data from {Url}",
                        workItem.VenueId, workItem.Url);
                }
                else
                {
                    _logger.LogWarning("URL extraction failed for venue {VenueId}: {Error}",
                        workItem.VenueId, result.Error);

                    if (IsPlaceholderName(venue.Name))
                        venue.Name = ExtractDomainLabel(workItem.Url);
                }

                venue.UpdatedAt = DateTime.UtcNow;
                await _cosmosDb.UpsertAsync(ContainerName, venue, venue.PartitionKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing venue URL for venue {VenueId}: {Url}",
                    workItem.VenueId, workItem.Url);
            }
        }

        _logger.LogInformation("Venue URL processing service stopped");
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
