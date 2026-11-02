using System.Security.Claims;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "ApiKey")]
public class ExternalController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IBlobStorageService _blobStorage;
    private readonly Channel<Capture> _captureQueue;
    private readonly ILogger<ExternalController> _logger;
    private const string CaptureContainer = "captures";
    private const long MaxFileSize = 15_000_000; // 15MB per file

    private static readonly HashSet<string> AllowedImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif"];

    public ExternalController(
        ICosmosDbService cosmosDb,
        IBlobStorageService blobStorage,
        Channel<Capture> captureQueue,
        ILogger<ExternalController> logger)
    {
        _cosmosDb = cosmosDb;
        _blobStorage = blobStorage;
        _captureQueue = captureQueue;
        _logger = logger;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// External capture endpoint for iOS Shortcuts and other integrations.
    /// Accepts multipart form with image files and an optional note.
    /// </summary>
    [HttpPost("capture")]
    [RequestSizeLimit(50_000_000)] // 50MB
    public async Task<ActionResult<CaptureResponse>> CreateCapture(
        [FromForm] string? note,
        [FromForm] List<IFormFile>? images)
    {
        var userId = GetUserId();

        // A capture adds an item, so the key needs write access.
        var capabilities = (User.FindFirstValue("api_key_capabilities") ?? "").Split(',', StringSplitOptions.TrimEntries);
        if (!capabilities.Contains(ApiKeyCapability.Write))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "This API key can only read. Give it Read + write access on your Profile page to capture with it." });

        if (note != null && note.Length > 1000)
            return BadRequest(new { message = "Note must be 1000 characters or less" });

        _logger.LogInformation("External capture from user {UserId}: images={ImageCount}, hasNote={HasNote}",
            userId, images?.Count ?? 0, !string.IsNullOrWhiteSpace(note));

        if (images == null || images.Count == 0)
            return BadRequest(new { message = "At least one image is required" });

        var photoUrls = new List<string>();
        GeoLocation? location = null;

        foreach (var image in images)
        {
            if (image.Length == 0) continue;

            if (image.Length > MaxFileSize)
            {
                _logger.LogWarning("Rejected upload exceeding size limit: {Size} bytes", image.Length);
                return BadRequest(new { message = $"File {image.FileName} exceeds the 15MB size limit" });
            }

            var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext))
            {
                _logger.LogWarning("Rejected upload with disallowed extension: {Extension}", ext);
                return BadRequest(new { message = $"File type {ext} is not allowed. Accepted: jpg, png, gif, webp, heic" });
            }

            using var memStream = new MemoryStream();
            await image.OpenReadStream().CopyToAsync(memStream);
            memStream.Position = 0;

            // Try to extract GPS from first image
            if (location == null)
            {
                try { location = ExifLocationService.ExtractGpsFromStream(memStream); }
                catch (Exception ex) { _logger.LogWarning(ex, "EXIF extraction failed for uploaded file {FileName}", image.FileName); }
                memStream.Position = 0;
            }

            var blobUrl = await _blobStorage.UploadAsync(userId, image.FileName, memStream);
            photoUrls.Add(blobUrl);
        }

        var capture = new Capture
        {
            UserId = userId,
            UserNote = note,
            Location = location,
            Photos = photoUrls,
            Status = CaptureStatus.Pending
        };

        capture = await _cosmosDb.CreateAsync(CaptureContainer, capture, capture.PartitionKey);
        await _captureQueue.Writer.WriteAsync(capture);

        _logger.LogInformation("External capture {CaptureId} created for user {UserId} with {PhotoCount} photo(s)",
            capture.Id, userId, photoUrls.Count);

        return CreatedAtAction(nameof(GetCapture), new { id = capture.Id }, MapToResponse(capture));
    }

    [HttpGet("capture/{id}")]
    public async Task<ActionResult<CaptureResponse>> GetCapture(string id)
    {
        var userId = GetUserId();
        var capture = await _cosmosDb.GetAsync<Capture>(CaptureContainer, id, userId);
        if (capture == null) return NotFound();
        return Ok(MapToResponse(capture));
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
}
