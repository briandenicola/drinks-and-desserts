using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<RecommendationsController> _logger;

    public RecommendationsController(
        IRecommendationService recommendationService,
        ILogger<RecommendationsController> logger)
    {
        _recommendationService = recommendationService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get personalized recommendations based on user's rating history
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RecommendationResponse>> GetRecommendations(
        [FromBody] RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        _logger.LogInformation("Getting recommendations for user {UserId}", userId);

        try
        {
            var response = await _recommendationService.GetRecommendationsAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to generate recommendations" });
        }
    }

    /// <summary>
    /// Get user's rating profile (for debugging/analytics)
    /// </summary>
    [HttpGet("profile")]
    public async Task<ActionResult<UserRatingProfile>> GetUserProfile(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        _logger.LogInformation("Getting rating profile for user {UserId}", userId);

        try
        {
            var profile = await _recommendationService.BuildUserProfileAsync(userId, cancellationToken);
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to build user profile" });
        }
    }

    /// <summary>
    /// Extract menu items from a photo
    /// </summary>
    [HttpPost("extract-menu")]
    public async Task<ActionResult<List<string>>> ExtractMenuItems(
        [FromBody] MenuExtractionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        _logger.LogInformation("Extracting menu items for user {UserId}", userId);

        if (string.IsNullOrEmpty(request.PhotoUrl))
        {
            return BadRequest(new { error = "PhotoUrl is required" });
        }

        // Validate that the photo URL belongs to the authenticated user's blob path
        if (!request.PhotoUrl.Contains($"/{userId}/"))
        {
            _logger.LogWarning("User {UserId} attempted to access photo not in their blob path: {PhotoUrl}", userId, request.PhotoUrl);
            return BadRequest(new { error = "Invalid photo URL" });
        }

        try
        {
            var items = await _recommendationService.ExtractMenuItemsAsync(request.PhotoUrl, cancellationToken);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting menu items for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to extract menu items" });
        }
    }

    /// <summary>
    /// Save a generated recommendation thread so it can be revisited later
    /// </summary>
    [HttpPost("threads")]
    public async Task<ActionResult<RecommendationThread>> SaveThread(
        [FromBody] SaveRecommendationThreadRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (request.Response.Recommendations.Count == 0)
        {
            return BadRequest(new { error = "There are no recommendations to save" });
        }

        try
        {
            var thread = await _recommendationService.SaveRecommendationThreadAsync(userId, request.Request, request.Response, cancellationToken);
            return Ok(thread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving recommendation thread for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to save recommendation thread" });
        }
    }

    /// <summary>
    /// List the user's saved recommendation threads, most recent first
    /// </summary>
    [HttpGet("threads")]
    public async Task<ActionResult<List<RecommendationThread>>> GetThreads(CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        try
        {
            var threads = await _recommendationService.GetRecommendationThreadsAsync(userId, cancellationToken);
            return Ok(threads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing recommendation threads for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to load recommendation history" });
        }
    }

    /// <summary>
    /// Get a single saved recommendation thread
    /// </summary>
    [HttpGet("threads/{id}")]
    public async Task<ActionResult<RecommendationThread>> GetThread(string id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        try
        {
            var thread = await _recommendationService.GetRecommendationThreadAsync(userId, id, cancellationToken);
            if (thread == null)
            {
                return NotFound(new { error = "Recommendation thread not found" });
            }

            return Ok(thread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendation thread {ThreadId} for user {UserId}", id, userId);
            return StatusCode(500, new { error = "Failed to load recommendation thread" });
        }
    }

    /// <summary>
    /// Delete a saved recommendation thread
    /// </summary>
    [HttpDelete("threads/{id}")]
    public async Task<ActionResult> DeleteThread(string id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        try
        {
            await _recommendationService.DeleteRecommendationThreadAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting recommendation thread {ThreadId} for user {UserId}", id, userId);
            return StatusCode(500, new { error = "Failed to delete recommendation thread" });
        }
    }
}

public class MenuExtractionRequest
{
    public string PhotoUrl { get; set; } = string.Empty;
}

public class SaveRecommendationThreadRequest
{
    public RecommendationRequest Request { get; set; } = new();
    public RecommendationResponse Response { get; set; } = new();
}
