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
        [FromBody] RecommendationRequest request)
    {
        var userId = GetUserId();
        _logger.LogInformation("Getting recommendations for user {UserId}", userId);

        try
        {
            var response = await _recommendationService.GetRecommendationsAsync(userId, request);
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
    public async Task<ActionResult<UserRatingProfile>> GetUserProfile()
    {
        var userId = GetUserId();
        _logger.LogInformation("Getting rating profile for user {UserId}", userId);

        try
        {
            var profile = await _recommendationService.BuildUserProfileAsync(userId);
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
    public async Task<ActionResult<List<string>>> ExtractMenuItems([FromBody] MenuExtractionRequest request)
    {
        var userId = GetUserId();
        _logger.LogInformation("Extracting menu items for user {UserId}", userId);

        if (string.IsNullOrEmpty(request.PhotoUrl))
        {
            return BadRequest(new { error = "PhotoUrl is required" });
        }

        try
        {
            var items = await _recommendationService.ExtractMenuItemsAsync(request.PhotoUrl);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting menu items for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to extract menu items" });
        }
    }
}

public class MenuExtractionRequest
{
    public string PhotoUrl { get; set; } = string.Empty;
}
