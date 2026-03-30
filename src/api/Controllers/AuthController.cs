using Microsoft.AspNetCore.Mvc;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private const string ContainerName = "users";

    public AuthController(ICosmosDbService cosmosDb, IAuthService authService, ILogger<AuthController> logger)
    {
        _cosmosDb = cosmosDb;
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        using var activity = Diagnostics.Auth.StartActivity("AuthRegister");
        activity?.SetTag("auth.method", "register");
        activity?.SetTag("user.email", request.Email);
        _logger.LogDebug("Registration attempt for email {Email}", request.Email);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Registration failed: email or password is empty");
            return BadRequest(new { message = "Email and password are required" });
        }

        if (request.Password.Length < 8)
        {
            _logger.LogWarning("Registration failed for {Email}: password too short", request.Email);
            return BadRequest(new { message = "Password must be at least 8 characters" });
        }

        var existing = await _authService.FindByEmailAsync(request.Email.ToLowerInvariant());
        if (existing != null)
        {
            _logger.LogWarning("Registration failed for {Email}: account already exists", request.Email);
            return Conflict(new { message = "An account with this email already exists" });
        }

        var user = new User
        {
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.ToLowerInvariant().Trim(),
            PasswordHash = _authService.HashPassword(request.Password),
            Role = "user"
        };

        // First user becomes admin
        var existingUsers = await _cosmosDb.QueryCrossPartitionAsync<User>(ContainerName, "SELECT TOP 1 c.id FROM c", maxItems: 1);
        var isFirstUser = existingUsers.Count == 0;
        activity?.SetTag("auth.is_first_user", isFirstUser);
        if (isFirstUser)
        {
            user.Role = "admin";
            _logger.LogInformation("First user registration — {Email} will be assigned admin role", request.Email);
        }

        user = await _cosmosDb.CreateAsync(ContainerName, user, user.PartitionKey);
        var response = _authService.GenerateToken(user);
        response.User = user.Sanitized();

        _logger.LogInformation("User registered successfully: {UserId} ({Email}), role={Role}",
            user.Id, user.Email, user.Role);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        using var activity = Diagnostics.Auth.StartActivity("AuthLogin");
        activity?.SetTag("auth.method", "login");
        activity?.SetTag("user.email", request.Email);
        _logger.LogDebug("Login attempt for email {Email}", request.Email);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Login failed: email or password is empty");
            return BadRequest(new { message = "Email and password are required" });
        }

        var user = await _authService.FindByEmailAsync(request.Email.ToLowerInvariant());
        if (user == null || !_authService.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for {Email}: invalid credentials", request.Email);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        if (user.IsDisabled)
        {
            _logger.LogWarning("Login failed for {Email}: account is disabled", request.Email);
            return Unauthorized(new { message = "Account is disabled" });
        }

        var response = _authService.GenerateToken(user);
        response.User = user.Sanitized();

        _logger.LogInformation("User logged in successfully: {UserId} ({Email})", user.Id, user.Email);
        return Ok(response);
    }
}
