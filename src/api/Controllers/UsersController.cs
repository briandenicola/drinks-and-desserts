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
public class UsersController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IAuthService _authService;
    private readonly ILogger<UsersController> _logger;
    private const string ContainerName = "users";

    public UsersController(ICosmosDbService cosmosDb, IAuthService authService, ILogger<UsersController> logger)
    {
        _cosmosDb = cosmosDb;
        _authService = authService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    [HttpGet("me")]
    public async Task<ActionResult<User>> GetCurrentUser()
    {
        using var activity = Diagnostics.Auth.StartActivity("UserGetProfile");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);
        _logger.LogDebug("Getting profile for user {UserId}", userId);

        var user = await _cosmosDb.GetAsync<User>(ContainerName, userId, userId);
        if (user == null)
        {
            _logger.LogWarning("Profile not found for user {UserId}", userId);
            return NotFound();
        }

        _logger.LogInformation("Retrieved profile for user {UserId}", userId);
        return Ok(user.Sanitized());
    }

    [HttpPut("me")]
    public async Task<ActionResult<User>> UpdateCurrentUser([FromBody] UpdateUserRequest request)
    {
        using var activity = Diagnostics.Auth.StartActivity("UserUpdateProfile");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);
        _logger.LogDebug("Updating profile for user {UserId}", userId);

        var user = await _cosmosDb.GetAsync<User>(ContainerName, userId, userId);
        if (user == null)
        {
            _logger.LogWarning("Profile not found for update, user {UserId}", userId);
            return NotFound();
        }

        if (request.DisplayName != null) user.DisplayName = request.DisplayName;
        if (request.Preferences != null) user.Preferences = request.Preferences;
        user.UpdatedAt = DateTime.UtcNow;

        user = await _cosmosDb.UpsertAsync(ContainerName, user, user.PartitionKey);

        _logger.LogInformation("Updated profile for user {UserId}", userId);
        return Ok(user.Sanitized());
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        using var activity = Diagnostics.Auth.StartActivity("UserChangePassword");
        _logger.LogDebug("Password change attempt");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            _logger.LogWarning("Password change failed: new password too short or empty");
            return BadRequest(new { message = "New password must be at least 8 characters" });
        }

        var userId = GetUserId();
        activity?.SetTag("user.id", userId);

        var user = await _cosmosDb.GetAsync<User>(ContainerName, userId, userId);
        if (user == null)
        {
            _logger.LogWarning("Password change failed: user {UserId} not found", userId);
            return NotFound();
        }

        if (!_authService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Password change failed for user {UserId}: current password incorrect", userId);
            return BadRequest(new { message = "Current password is incorrect" });
        }

        user.PasswordHash = _authService.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _cosmosDb.UpsertAsync(ContainerName, user, user.PartitionKey);

        _logger.LogInformation("Password changed successfully for user {UserId}", userId);
        return Ok(new { message = "Password changed successfully" });
    }
}
