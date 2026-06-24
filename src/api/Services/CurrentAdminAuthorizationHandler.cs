using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public sealed class CurrentAdminRequirement : IAuthorizationRequirement;

public sealed class CurrentAdminAuthorizationHandler : AuthorizationHandler<CurrentAdminRequirement>
{
    private const string UsersContainer = "users";
    private readonly ICosmosDbService _cosmosDb;
    private readonly ILogger<CurrentAdminAuthorizationHandler> _logger;

    public CurrentAdminAuthorizationHandler(
        ICosmosDbService cosmosDb,
        ILogger<CurrentAdminAuthorizationHandler> logger)
    {
        _cosmosDb = cosmosDb;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentAdminRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        try
        {
            var user = await _cosmosDb.GetAsync<User>(UsersContainer, userId, userId);
            if (user is { IsDisabled: false } &&
                string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify current admin role for user {UserId}", userId);
        }
    }
}
