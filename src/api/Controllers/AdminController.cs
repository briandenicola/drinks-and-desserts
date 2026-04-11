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
    private readonly FoundryStatusService _foundryStatus;
    private readonly ILogger<AdminController> _logger;
    private const string UsersContainer = "users";

    public AdminController(
        ICosmosDbService cosmosDb,
        IAuthService authService,
        IPromptService promptService,
        DynamicLogLevelService logLevelService,
        FoundryStatusService foundryStatus,
        ILogger<AdminController> logger)
    {
        _cosmosDb = cosmosDb;
        _authService = authService;
        _promptService = promptService;
        _logLevelService = logLevelService;
        _foundryStatus = foundryStatus;
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
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var activity = Diagnostics.Admin.StartActivity("AdminUpdateRole");
        activity?.SetTag("target.user_id", userId);
        activity?.SetTag("target.new_role", request.Role);

        // Constitution exception: admin operations inherently require cross-partition access
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
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var activity = Diagnostics.Admin.StartActivity("AdminResetPassword");
        activity?.SetTag("target.user_id", userId);

        // Constitution exception: admin operations inherently require cross-partition access
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
    public ActionResult UpdatePrompt(string id, [FromBody] UpdatePromptRequest request)
    {
        // Prompts are now managed as files in AgentInitiator/Prompts/ and baked into Foundry agents.
        // To change a prompt, edit the file and re-run the agent:init task.
        return StatusCode(405, new { message = "Prompts are read-only and managed through the deployment pipeline." });
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

    // ── Foundry Connectivity ─────────────────────────────────

    [HttpGet("foundry")]
    public ActionResult<FoundryStatus> GetFoundryStatus()
    {
        return Ok(_foundryStatus.GetStatus());
    }

    [HttpPost("foundry/test")]
    public async Task<ActionResult<FoundryStatus>> TestFoundryConnectivity()
    {
        var status = _foundryStatus.GetStatus();
        var testResult = new ConnectivityTestResult { TestedAt = DateTime.UtcNow };

        if (!status.IsProjectConfigured)
        {
            testResult.Status = "error";
            testResult.Message = "AiFoundry:ProjectEndpoint is not configured. Ensure PROJECT_ENDPOINT is set via Terraform and passed through the Taskfile.";
            _foundryStatus.Update(s => s.ConnectivityTest = testResult);
            return Ok(_foundryStatus.GetStatus());
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var credential = CredentialFactory.Create();

            var projectClient = new Azure.AI.Projects.AIProjectClient(
                new Uri(status.ProjectEndpoint), credential);

            // Invoke the domain expert agent with a trivial prompt to validate the
            // full Foundry pipeline: auth → agent resolution → Responses API → response
            const string healthCheckAgent = "dd-domain-expert";
            const string healthCheckPrompt = "Reply with exactly: OK";

            var agentRef = new Azure.AI.Projects.OpenAI.AgentReference(healthCheckAgent, "1");
            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentRef);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var response = await responsesClient.CreateResponseAsync(healthCheckPrompt, cancellationToken: cts.Token);

            sw.Stop();
            var responseText = response.Value.GetOutputText() ?? "(empty)";

            testResult.Status = "ok";
            testResult.Message = $"Foundry agent '{healthCheckAgent}' responded via Responses API in {sw.ElapsedMilliseconds}ms. Response: {responseText}";
            testResult.LatencyMs = sw.ElapsedMilliseconds;

            _logger.LogInformation("Foundry connectivity test passed: agent={Agent}, latency={Latency}ms, response={Response}",
                healthCheckAgent, sw.ElapsedMilliseconds, responseText);
        }
        catch (Exception ex)
        {
            sw.Stop();
            testResult.Status = "error";
            testResult.Message = "Foundry connectivity test failed. Check logs for details.";
            testResult.LatencyMs = sw.ElapsedMilliseconds;

            _logger.LogWarning(ex, "Foundry connectivity test failed");
        }

        _foundryStatus.Update(s => s.ConnectivityTest = testResult);
        return Ok(_foundryStatus.GetStatus());
    }
}

public class LoggingSettingsResponse
{
    public LoggingSettings Settings { get; set; } = new();
    public Dictionary<string, string> AvailableCategories { get; set; } = new();
    public string[] AvailableLevels { get; set; } = [];
}
