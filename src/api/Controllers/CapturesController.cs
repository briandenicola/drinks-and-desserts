using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;
using System.Security.Claims;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CapturesController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IBlobStorageService _blobStorage;
    private readonly Channel<Capture> _captureQueue;
    private readonly Channel<WishlistUrlWorkItem> _wishlistUrlQueue;
    private readonly ILogger<CapturesController> _logger;
    private const string ContainerName = "captures";

    public CapturesController(ICosmosDbService cosmosDb, IBlobStorageService blobStorage, Channel<Capture> captureQueue, Channel<WishlistUrlWorkItem> wishlistUrlQueue, ILogger<CapturesController> logger)
    {
        _cosmosDb = cosmosDb;
        _blobStorage = blobStorage;
        _captureQueue = captureQueue;
        _wishlistUrlQueue = wishlistUrlQueue;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    private static readonly HashSet<string> AllowedImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif"];

    [HttpGet("upload-url")]
    public async Task<ActionResult<UploadUrlResponse>> GetUploadUrl([FromQuery] string fileName)
    {
        using var activity = Diagnostics.Captures.StartActivity("CaptureGetUploadUrl");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);

        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
            return BadRequest(new { message = "Valid fileName is required (max 255 chars)" });

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            return BadRequest(new { message = $"File type {ext} is not allowed. Accepted: jpg, jpeg, png, gif, webp, heic, heif" });

        _logger.LogDebug("Upload URL requested by user {UserId} for file {FileName}", userId, fileName);

        var (uploadUrl, blobUrl) = await _blobStorage.GenerateUploadUrlAsync(userId, fileName);

        _logger.LogInformation("Upload URL generated for user {UserId}, file {FileName}", userId, fileName);
        return Ok(new UploadUrlResponse { UploadUrl = uploadUrl, BlobUrl = blobUrl });
    }

    [HttpPost]
    public async Task<ActionResult<CaptureResponse>> CreateCapture([FromBody] CreateCaptureRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var activity = Diagnostics.Captures.StartActivity("CaptureCreate");
        var userId = GetUserId();
        var photoCount = request.Photos?.Count ?? 0;
        var hasNote = !string.IsNullOrWhiteSpace(request.UserNote);
        var hasLocation = request.Location != null;

        activity?.SetTag("user.id", userId);
        activity?.SetTag("capture.photo_count", photoCount);
        activity?.SetTag("capture.has_note", hasNote);
        _logger.LogDebug("Creating capture for user {UserId}: photoCount={PhotoCount}, hasNote={HasNote}, hasLocation={HasLocation}",
            userId, photoCount, hasNote, hasLocation);

        var capture = new Capture
        {
            UserId = userId,
            UserNote = request.UserNote,
            Location = request.Location,
            Photos = request.Photos ?? [],
            Status = CaptureStatus.Pending
        };

        capture = await _cosmosDb.CreateAsync(ContainerName, capture, capture.PartitionKey);
        activity?.SetTag("capture.id", capture.Id);

        // Queue for background processing (observed, retryable, cancellable)
        await _captureQueue.Writer.WriteAsync(capture);

        _logger.LogInformation("Capture {CaptureId} created for user {UserId} with {PhotoCount} photo(s)",
            capture.Id, userId, photoCount);
        return CreatedAtAction(nameof(GetCapture), new { id = capture.Id }, MapToResponse(capture));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CaptureResponse>> GetCapture(string id)
    {
        using var activity = Diagnostics.Captures.StartActivity("CaptureGet");
        var userId = GetUserId();
        activity?.SetTag("capture.id", id);
        activity?.SetTag("user.id", userId);
        _logger.LogDebug("Getting capture {CaptureId} for user {UserId}", id, userId);

        var capture = await _cosmosDb.GetAsync<Capture>(ContainerName, id, userId);
        if (capture == null)
        {
            _logger.LogWarning("Capture {CaptureId} not found for user {UserId}", id, userId);
            return NotFound();
        }

        _logger.LogInformation("Retrieved capture {CaptureId} (status={Status})", id, capture.Status);
        return Ok(MapToResponse(capture));
    }

    [HttpPost("{id}/reprocess")]
    public async Task<ActionResult<CaptureResponse>> ReprocessCapture(string id)
    {
        using var activity = Diagnostics.Captures.StartActivity("CaptureReprocess");
        var userId = GetUserId();
        activity?.SetTag("capture.id", id);
        activity?.SetTag("user.id", userId);

        var capture = await _cosmosDb.GetAsync<Capture>(ContainerName, id, userId);
        if (capture == null)
            return NotFound();

        if (capture.Status == CaptureStatus.Processing)
            return Conflict(new { message = "Capture is already being processed" });

        // For wishlist URL captures, re-queue through the wishlist URL channel
        if (capture.Source == CaptureSource.WishlistUrl && !string.IsNullOrEmpty(capture.SourceUrl))
        {
            // Delete previously created items
            foreach (var itemId in capture.ItemIds)
            {
                try { await _cosmosDb.DeleteAsync("items", itemId, userId); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete item {ItemId} during reprocess", itemId); }
            }

            // Create a new placeholder wishlist item
            var placeholderItem = new Item
            {
                UserId = userId,
                Name = ExtractDomainLabel(capture.SourceUrl),
                Type = "custom",
                Tags = ["from-url"],
                Status = ItemStatus.Wishlist,
                ProcessedBy = ProcessingSource.Pending,
            };
            placeholderItem = await _cosmosDb.CreateAsync("items", placeholderItem, placeholderItem.PartitionKey);

            // Reset capture
            capture.ItemIds = [placeholderItem.Id];
            capture.WorkflowSteps = [];
            capture.ProcessingError = null;
            capture.ProcessedBy = ProcessingSource.Pending;
            capture.Status = CaptureStatus.Pending;
            capture.UpdatedAt = DateTime.UtcNow;
            await _cosmosDb.UpsertAsync(ContainerName, capture, capture.PartitionKey);

            // Re-queue to wishlist URL channel
            await _wishlistUrlQueue.Writer.WriteAsync(new WishlistUrlWorkItem
            {
                ItemId = placeholderItem.Id,
                UserId = userId,
                Url = capture.SourceUrl,
            });

            _logger.LogInformation("Wishlist capture {CaptureId} re-queued for URL extraction by user {UserId}", id, userId);
            return Ok(MapToResponse(capture));
        }

        // Delete previously created items
        foreach (var itemId in capture.ItemIds)
        {
            try { await _cosmosDb.DeleteAsync("items", itemId, userId); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete item {ItemId} during reprocess", itemId); }
        }

        // Reset capture for reprocessing
        capture.ItemIds = [];
        capture.WorkflowSteps = [];
        capture.ProcessingError = null;
        capture.ProcessedBy = ProcessingSource.Pending;
        capture.Status = CaptureStatus.Pending;
        capture.UpdatedAt = DateTime.UtcNow;
        await _cosmosDb.UpsertAsync(ContainerName, capture, capture.PartitionKey);

        await _captureQueue.Writer.WriteAsync(capture);

        _logger.LogInformation("Capture {CaptureId} queued for reprocessing by user {UserId}", id, userId);
        return Ok(MapToResponse(capture));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CaptureResponse>>> ListCaptures([FromQuery] string? continuationToken)
    {
        using var activity = Diagnostics.Captures.StartActivity("CaptureList");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);
        _logger.LogDebug("Listing captures for user {UserId}, hasContinuation={HasContinuation}",
            userId, continuationToken != null);

        var (captures, nextToken) = await _cosmosDb.QueryAsync<Capture>(ContainerName, userId, continuationToken);

        _logger.LogInformation("Listed {Count} captures for user {UserId}, hasMore={HasMore}",
            captures.Count, userId, nextToken != null);
        return Ok(new PagedResponse<CaptureResponse>
        {
            Items = captures.Select(MapToResponse).ToList(),
            ContinuationToken = nextToken,
            HasMore = nextToken != null
        });
    }

    private static CaptureResponse MapToResponse(Capture c) => new()
    {
        Id = c.Id,
        Status = c.Status,
        ProcessedBy = c.ProcessedBy,
        Photos = c.Photos,
        UserNote = c.UserNote,
        Location = c.Location,
        ItemIds = c.ItemIds,
        WorkflowSteps = c.WorkflowSteps,
        ProcessingError = c.ProcessingError,
        CreatedAt = c.CreatedAt
    };

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
