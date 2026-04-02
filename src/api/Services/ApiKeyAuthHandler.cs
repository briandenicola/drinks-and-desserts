using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ICosmosDbService _cosmosDb;
    private const string ApiKeyHeader = "X-API-Key";
    private const string ContainerName = "users";

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ICosmosDbService cosmosDb)
        : base(options, logger, encoder)
    {
        _cosmosDb = cosmosDb;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyValue))
            return AuthenticateResult.NoResult();

        var providedKey = apiKeyValue.FirstOrDefault();
        if (string.IsNullOrEmpty(providedKey))
            return AuthenticateResult.NoResult();

        var keyHash = HashKey(providedKey);

        var users = await _cosmosDb.QueryCrossPartitionAsync<User>(
            ContainerName,
            "SELECT * FROM c WHERE ARRAY_CONTAINS(c.apiKeys, {\"keyHash\": @hash}, true)",
            new Dictionary<string, object> { ["@hash"] = keyHash },
            maxItems: 1);

        var user = users.FirstOrDefault();
        if (user == null)
            return AuthenticateResult.Fail("Invalid API key");

        var matchedKey = user.ApiKeys.FirstOrDefault(k => k.KeyHash == keyHash && !k.IsRevoked);
        if (matchedKey == null)
            return AuthenticateResult.Fail("API key is revoked");

        // Update last used timestamp (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                matchedKey.LastUsedAt = DateTime.UtcNow;
                await _cosmosDb.UpsertAsync(ContainerName, user, user.PartitionKey);
            }
            catch { /* best effort */ }
        });

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("auth_method", "api_key"),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    public static string HashKey(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(bytes);
    }

    public static string GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"ws_{Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")}";
    }
}
