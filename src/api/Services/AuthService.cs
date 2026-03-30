using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface IAuthService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
    AuthResponse GenerateToken(User user);
    Task<User?> FindByEmailAsync(string email);
}

public class AuthService : IAuthService
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;
    private const string ContainerName = "users";

    public AuthService(ICosmosDbService cosmosDb, IConfiguration config, ILogger<AuthService> logger)
    {
        _cosmosDb = cosmosDb;
        _config = config;
        _logger = logger;
    }

    public string HashPassword(string password)
    {
        using var activity = Diagnostics.Auth.StartActivity("HashPassword");
        _logger.LogDebug("Hashing password with workFactor=12");
        var sw = Stopwatch.StartNew();
        var result = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        sw.Stop();
        _logger.LogDebug("Password hashing completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
        return result;
    }

    public bool VerifyPassword(string password, string hash)
    {
        using var activity = Diagnostics.Auth.StartActivity("VerifyPassword");
        _logger.LogDebug("Verifying password hash");
        var sw = Stopwatch.StartNew();
        var result = BCrypt.Net.BCrypt.Verify(password, hash);
        sw.Stop();
        _logger.LogDebug("Password verification completed in {ElapsedMs}ms, result={IsValid}", sw.ElapsedMilliseconds, result);
        return result;
    }

    public AuthResponse GenerateToken(User user)
    {
        using var activity = Diagnostics.Auth.StartActivity("GenerateToken");
        activity?.SetTag("auth.user_id", user.Id);
        activity?.SetTag("auth.role", user.Role);

        _logger.LogInformation("Generating JWT token for user {UserId} (email={Email}, role={Role})",
            user.Id, user.Email, user.Role);

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured")));

        var expirationDays = int.TryParse(_config["Jwt:ExpirationDays"], out var days) ? days : 7;
        var expiresAt = DateTime.UtcNow.AddDays(expirationDays);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "whiskey-and-smokes",
            audience: _config["Jwt:Audience"] ?? "whiskey-and-smokes",
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        _logger.LogInformation(
            "JWT token generated for user {UserId}: expiresAt={ExpiresAt}, expirationDays={ExpirationDays}, issuer={Issuer}",
            user.Id, expiresAt, expirationDays, _config["Jwt:Issuer"] ?? "whiskey-and-smokes");

        return new AuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
            User = user
        };
    }

    public async Task<User?> FindByEmailAsync(string email)
    {
        using var activity = Diagnostics.Auth.StartActivity("FindByEmail");
        activity?.SetTag("auth.email_lookup", email);

        _logger.LogDebug("Looking up user by email {Email}", email);

        var users = await _cosmosDb.QueryCrossPartitionAsync<User>(
            ContainerName,
            $"SELECT * FROM c WHERE c.email = '{email.Replace("'", "''")}'",
            maxItems: 1);

        var user = users.FirstOrDefault();
        if (user != null)
        {
            _logger.LogInformation("User found by email {Email}: userId={UserId}, role={Role}", email, user.Id, user.Role);
        }
        else
        {
            _logger.LogInformation("No user found for email {Email}", email);
        }

        return user;
    }
}
