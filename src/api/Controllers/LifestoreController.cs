using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Controllers;

/// <summary>
/// The feed the household life store reads with an API key: every item and venue the key's owner
/// has, paged. Read-only; see <see cref="LifestoreFeedService"/>.
/// </summary>
[ApiController]
[Route("api/lifestore")]
[Authorize(AuthenticationSchemes = "ApiKey")]
public class LifestoreController(ILifestoreFeedService feed) : ControllerBase
{
    [HttpGet("records")]
    public async Task<ActionResult<LifestoreFeedPage>> Records([FromQuery] string? page, [FromQuery] int limit = LifestoreFeedService.DefaultLimit, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        Response.Headers.CacheControl = "no-store";
        try
        {
            return Ok(await feed.PageAsync(userId, page, limit, ct));
        }
        catch (LifestoreTokenException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
