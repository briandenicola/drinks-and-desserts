using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "MultiAuth,ApiKey")]
public class VenuesController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IBlobStorageService _blobStorage;
    private readonly Channel<VenueUrlWorkItem> _venueUrlChannel;
    private readonly ILogger<VenuesController> _logger;
    private const string ContainerName = "venues";

    private static readonly HashSet<string> AllowedImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif"];

    public VenuesController(ICosmosDbService cosmosDb, IBlobStorageService blobStorage, Channel<VenueUrlWorkItem> venueUrlChannel, ILogger<VenuesController> logger)
    {
        _cosmosDb = cosmosDb;
        _blobStorage = blobStorage;
        _venueUrlChannel = venueUrlChannel;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<ActionResult<PagedResponse<Venue>>> ListVenues(
        [FromQuery] string? type,
        [FromQuery] string? continuationToken)
    {
        var userId = GetUserId();

        System.Linq.Expressions.Expression<Func<Venue, bool>>? predicate = null;
        if (!string.IsNullOrEmpty(type))
            predicate = v => v.Type == type;

        var (venues, nextToken) = await _cosmosDb.QueryAsync(ContainerName, userId, continuationToken, predicate: predicate);

        _logger.LogInformation("Listed {Count} venues for user {UserId}", venues.Count, userId);
        return Ok(new PagedResponse<Venue>
        {
            Items = venues,
            ContinuationToken = nextToken,
            HasMore = nextToken != null
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Venue>> GetVenue(string id)
    {
        var userId = GetUserId();
        var venue = await _cosmosDb.GetAsync<Venue>(ContainerName, id, userId);
        if (venue == null)
            return NotFound();

        return Ok(venue);
    }

    [HttpPost]
    public async Task<ActionResult<Venue>> CreateVenue([FromBody] CreateVenueRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();

        if (!VenueType.All.Contains(request.Type))
            return BadRequest(new { message = $"Invalid venue type. Must be one of: {string.Join(", ", VenueType.All)}" });

        var venue = new Venue
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
            Website = request.Website?.Trim(),
            Type = request.Type,
            Rating = request.Rating,
            Location = request.Location,
            Labels = request.Labels?.Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length > 0).Distinct().ToList() ?? [],
        };

        venue = await _cosmosDb.CreateAsync(ContainerName, venue, venue.PartitionKey);

        _logger.LogInformation("Created venue {VenueId} ({VenueName}) for user {UserId}", venue.Id, venue.Name, userId);
        return CreatedAtAction(nameof(GetVenue), new { id = venue.Id }, venue);
    }

    [HttpPost("from-url")]
    public async Task<ActionResult<Venue>> CreateVenueFromUrl([FromBody] CreateVenueFromUrlRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();

        var venue = new Venue
        {
            UserId = userId,
            Name = $"Extracting from {new Uri(request.Url).Host}...",
            Type = VenueType.Restaurant,
        };

        venue = await _cosmosDb.CreateAsync(ContainerName, venue, venue.PartitionKey);

        await _venueUrlChannel.Writer.WriteAsync(new VenueUrlWorkItem
        {
            VenueId = venue.Id,
            UserId = userId,
            Url = request.Url,
        });

        _logger.LogInformation("Created placeholder venue {VenueId} for URL extraction: {Url}", venue.Id, request.Url);
        return AcceptedAtAction(nameof(GetVenue), new { id = venue.Id }, venue);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Venue>> UpdateVenue(string id, [FromBody] UpdateVenueRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();
        var venue = await _cosmosDb.GetAsync<Venue>(ContainerName, id, userId);
        if (venue == null)
            return NotFound();

        if (request.Name != null) venue.Name = request.Name.Trim();
        if (request.Address != null) venue.Address = request.Address.Trim();
        if (request.Website != null) venue.Website = request.Website.Trim();
        if (request.Type != null)
        {
            if (!VenueType.All.Contains(request.Type))
                return BadRequest(new { message = $"Invalid venue type. Must be one of: {string.Join(", ", VenueType.All)}" });
            venue.Type = request.Type;
        }
        if (request.Rating.HasValue) venue.Rating = request.Rating;
        if (request.Location != null) venue.Location = request.Location;
        if (request.Labels != null) venue.Labels = request.Labels.Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length > 0).Distinct().ToList();
        venue.UpdatedAt = DateTime.UtcNow;

        venue = await _cosmosDb.UpsertAsync(ContainerName, venue, venue.PartitionKey);

        _logger.LogInformation("Updated venue {VenueId} for user {UserId}", id, userId);
        return Ok(venue);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteVenue(string id)
    {
        var userId = GetUserId();
        var venue = await _cosmosDb.GetAsync<Venue>(ContainerName, id, userId);
        if (venue == null)
            return NoContent();

        // Delete venue photos from blob storage
        foreach (var photoUrl in venue.PhotoUrls)
        {
            try
            {
                await _blobStorage.DeleteBlobAsync(photoUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete venue photo blob {Url}", photoUrl);
            }
        }

        await _cosmosDb.DeleteAsync(ContainerName, id, userId);

        _logger.LogInformation("Deleted venue {VenueId} for user {UserId}", id, userId);
        return NoContent();
    }

    [HttpGet("{id}/items")]
    public async Task<ActionResult<PagedResponse<Item>>> GetVenueItems(string id, [FromQuery] string? continuationToken)
    {
        var userId = GetUserId();
        var venue = await _cosmosDb.GetAsync<Venue>(ContainerName, id, userId);
        if (venue == null)
            return NotFound();

        var (items, nextToken) = await _cosmosDb.QueryAsync<Item>("items", userId, continuationToken,
            predicate: i => i.Venue != null && i.Venue.VenueId == id);

        return Ok(new PagedResponse<Item>
        {
            Items = items,
            ContinuationToken = nextToken,
            HasMore = nextToken != null
        });
    }

    [HttpGet("{id}/photos/upload-url")]
    public async Task<ActionResult<UploadUrlResponse>> GetPhotoUploadUrl(string id, [FromQuery] string fileName)
    {
        var userId = GetUserId();
        var venue = await _cosmosDb.GetAsync<Venue>(ContainerName, id, userId);
        if (venue == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest(new { message = "fileName is required" });

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            return BadRequest(new { message = $"File type {ext} is not allowed. Accepted: jpg, jpeg, png, gif, webp, heic, heif" });

        var (uploadUrl, blobUrl) = await _blobStorage.GenerateUploadUrlAsync(userId, fileName);
        return Ok(new UploadUrlResponse { UploadUrl = uploadUrl, BlobUrl = blobUrl });
    }

    [HttpPost("{id}/photos")]
    public async Task<ActionResult<Venue>> AddPhoto(string id, [FromBody] AddPhotoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();
        var venue = await _cosmosDb.GetAsync<Venue>(ContainerName, id, userId);
        if (venue == null)
            return NotFound();

        if (!venue.PhotoUrls.Contains(request.BlobUrl))
        {
            venue.PhotoUrls.Add(request.BlobUrl);
            venue.UpdatedAt = DateTime.UtcNow;
            venue = await _cosmosDb.UpsertAsync(ContainerName, venue, venue.PartitionKey);
        }

        _logger.LogInformation("Added photo to venue {VenueId}, total: {Count}", id, venue.PhotoUrls.Count);
        return Ok(venue);
    }

    [HttpDelete("{id}/photos")]
    public async Task<ActionResult<Venue>> RemovePhoto(string id, [FromBody] RemovePhotoRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();
        var venue = await _cosmosDb.GetAsync<Venue>(ContainerName, id, userId);
        if (venue == null)
            return NotFound();

        if (venue.PhotoUrls.Remove(request.BlobUrl))
        {
            venue.UpdatedAt = DateTime.UtcNow;
            venue = await _cosmosDb.UpsertAsync(ContainerName, venue, venue.PartitionKey);

            try
            {
                await _blobStorage.DeleteBlobAsync(request.BlobUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete venue photo blob");
            }
        }

        return Ok(venue);
    }
}
