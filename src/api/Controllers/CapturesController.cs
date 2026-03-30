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
    private readonly IAgentService _agentService;
    private readonly ILogger<CapturesController> _logger;
    private const string ContainerName = "captures";

    public CapturesController(ICosmosDbService cosmosDb, IBlobStorageService blobStorage, IAgentService agentService, ILogger<CapturesController> logger)
    {
        _cosmosDb = cosmosDb;
        _blobStorage = blobStorage;
        _agentService = agentService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    [HttpPost("upload-url")]
    public async Task<ActionResult<UploadUrlResponse>> GetUploadUrl([FromQuery] string fileName)
    {
        using var activity = Diagnostics.Captures.StartActivity("CaptureGetUploadUrl");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);
        _logger.LogDebug("Upload URL requested by user {UserId} for file {FileName}", userId, fileName);

        var (uploadUrl, blobUrl) = await _blobStorage.GenerateUploadUrlAsync(userId, fileName);

        _logger.LogInformation("Upload URL generated for user {UserId}, file {FileName}", userId, fileName);
        return Ok(new UploadUrlResponse { UploadUrl = uploadUrl, BlobUrl = blobUrl });
    }

    [HttpPost]
    public async Task<ActionResult<CaptureResponse>> CreateCapture([FromBody] CreateCaptureRequest request)
    {
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

        // Fire-and-forget AI processing
        _ = Task.Run(() => _agentService.ProcessCaptureAsync(capture));

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
        Photos = c.Photos,
        UserNote = c.UserNote,
        Location = c.Location,
        ItemIds = c.ItemIds,
        CreatedAt = c.CreatedAt
    };
}
