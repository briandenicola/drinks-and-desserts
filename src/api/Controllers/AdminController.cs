using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;
using System.Security.Claims;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IAuthService _authService;
    private readonly IPromptService _promptService;
    private readonly DynamicLogLevelService _logLevelService;
    private readonly ILogger<AdminController> _logger;
    private const string UsersContainer = "users";

    public AdminController(
        ICosmosDbService cosmosDb,
        IAuthService authService,
        IPromptService promptService,
        DynamicLogLevelService logLevelService,
        ILogger<AdminController> logger)
    {
        _cosmosDb = cosmosDb;
        _authService = authService;
        _promptService = promptService;
        _logLevelService = logLevelService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    // ── User Management ──────────────────────────────────────

    [HttpGet("users")]
    public async Task<ActionResult<List<User>>> ListUsers()
    {
        using var activity = Diagnostics.Admin.StartActivity("AdminListUsers");
        _logger.LogDebug("Admin listing all users");

        var users = await _cosmosDb.QueryCrossPartitionAsync<User>(
            UsersContainer,
            "SELECT * FROM c ORDER BY c.createdAt DESC");

        _logger.LogInformation("Admin retrieved {Count} users", users.Count);
        activity?.SetTag("user.count", users.Count);
        return Ok(users.Select(u => u.Sanitized()).ToList());
    }

    [HttpPut("users/{userId}/role")]
    public async Task<ActionResult<User>> UpdateUserRole(string userId, [FromBody] UpdateUserRoleRequest request)
    {
        using var activity = Diagnostics.Admin.StartActivity("AdminUpdateRole");
        activity?.SetTag("target.user_id", userId);
        activity?.SetTag("target.new_role", request.Role);

        if (request.Role != "user" && request.Role != "admin")
            return BadRequest(new { message = "Role must be 'user' or 'admin'" });

        var user = await _cosmosDb.GetAsync<User>(UsersContainer, userId, userId);
        if (user == null)
        {
            _logger.LogWarning("Admin role update: user {UserId} not found", userId);
            return NotFound();
        }

        var previousRole = user.Role;
        user.Role = request.Role;
        user.UpdatedAt = DateTime.UtcNow;
        user = await _cosmosDb.UpsertAsync(UsersContainer, user, user.PartitionKey);

        _logger.LogInformation("Admin changed user {UserId} ({Email}) role from {OldRole} to {NewRole}",
            userId, user.Email, previousRole, request.Role);
        return Ok(user.Sanitized());
    }

    [HttpPut("users/{userId}/password")]
    public async Task<IActionResult> ResetUserPassword(string userId, [FromBody] AdminResetPasswordRequest request)
    {
        using var activity = Diagnostics.Admin.StartActivity("AdminResetPassword");
        activity?.SetTag("target.user_id", userId);

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters" });

        var user = await _cosmosDb.GetAsync<User>(UsersContainer, userId, userId);
        if (user == null)
        {
            _logger.LogWarning("Admin password reset: user {UserId} not found", userId);
            return NotFound();
        }

        user.PasswordHash = _authService.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _cosmosDb.UpsertAsync(UsersContainer, user, user.PartitionKey);

        _logger.LogInformation("Admin reset password for user {UserId} ({Email})", userId, user.Email);
        return Ok(new { message = "Password reset successfully" });
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        using var activity = Diagnostics.Admin.StartActivity("AdminDeleteUser");
        activity?.SetTag("target.user_id", userId);

        var currentUserId = GetUserId();
        if (userId == currentUserId)
        {
            _logger.LogWarning("Admin {AdminId} attempted to delete their own account", currentUserId);
            return BadRequest(new { message = "Cannot delete your own account" });
        }

        var user = await _cosmosDb.GetAsync<User>(UsersContainer, userId, userId);
        if (user == null)
        {
            _logger.LogWarning("Admin delete: user {UserId} not found", userId);
            return NotFound();
        }

        await _cosmosDb.DeleteAsync(UsersContainer, userId, userId);

        _logger.LogInformation("Admin {AdminId} deleted user {UserId} ({Email})", currentUserId, userId, user.Email);
        return Ok(new { message = "User deleted successfully" });
    }

    // ── Prompt Management ────────────────────────────────────

    [HttpGet("prompts")]
    public async Task<ActionResult<List<Prompt>>> ListPrompts()
    {
        _logger.LogDebug("Listing all prompts");
        var prompts = await _promptService.GetAllAsync();
        _logger.LogInformation("Retrieved {Count} prompts", prompts.Count);
        return Ok(prompts);
    }

    [HttpGet("prompts/{id}")]
    public async Task<ActionResult<Prompt>> GetPrompt(string id)
    {
        _logger.LogDebug("Getting prompt {PromptId}", id);
        var prompt = await _promptService.GetAsync(id);
        if (prompt == null)
        {
            _logger.LogWarning("Prompt {PromptId} not found", id);
            return NotFound();
        }
        return Ok(prompt);
    }

    [HttpPut("prompts/{id}")]
    public async Task<ActionResult<Prompt>> UpdatePrompt(string id, [FromBody] UpdatePromptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "Prompt content cannot be empty" });

        var prompt = await _promptService.GetAsync(id);
        if (prompt == null) return NotFound();

        prompt.Content = request.Content;
        prompt.UpdatedBy = GetUserId();
        prompt = await _promptService.UpsertAsync(prompt);

        _logger.LogInformation("Prompt {PromptId} updated by admin {AdminId} ({ContentLength} chars)",
            id, GetUserId(), request.Content.Length);
        return Ok(prompt);
    }

    // ── Logging Configuration ────────────────────────────────

    [HttpGet("logging")]
    public ActionResult<LoggingSettingsResponse> GetLoggingSettings()
    {
        _logger.LogDebug("Retrieving logging settings");
        var settings = _logLevelService.GetSettings();
        return Ok(new LoggingSettingsResponse
        {
            Settings = settings,
            AvailableCategories = LoggingSettings.DefaultCategories,
            AvailableLevels = ["Trace", "Debug", "Information", "Warning", "Error", "Critical", "None"]
        });
    }

    [HttpPut("logging")]
    public async Task<ActionResult<LoggingSettings>> UpdateLoggingSettings([FromBody] LoggingSettings settings)
    {
        var adminId = GetUserId();
        _logger.LogInformation("Admin {AdminId} updating log levels: default={DefaultLevel}, categories={Categories}",
            adminId, settings.DefaultLevel, string.Join(", ", settings.CategoryLevels.Select(kv => $"{kv.Key}={kv.Value}")));

        await _logLevelService.SaveToStoreAsync(_cosmosDb, settings, adminId);

        _logger.LogInformation("Log levels updated and persisted successfully");
        return Ok(_logLevelService.GetSettings());
    }
}

public class LoggingSettingsResponse
{
    public LoggingSettings Settings { get; set; } = new();
    public Dictionary<string, string> AvailableCategories { get; set; } = new();
    public string[] AvailableLevels { get; set; } = [];
}
