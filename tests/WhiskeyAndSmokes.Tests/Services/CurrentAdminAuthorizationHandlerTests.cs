using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Services;

public class CurrentAdminAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirementAsync_WhenTokenClaimsAdminButStoredUserIsDemoted_DoesNotAuthorize()
    {
        var cosmos = Substitute.For<ICosmosDbService>();
        cosmos.GetAsync<User>("users", "user-1", "user-1")
            .Returns(new User { Id = "user-1", Role = "user" });
        var handler = new CurrentAdminAuthorizationHandler(
            cosmos,
            NullLogger<CurrentAdminAuthorizationHandler>.Instance);
        var requirement = new CurrentAdminRequirement();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "admin")
        ], "Test"));
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenStoredUserIsActiveAdmin_Authorizes()
    {
        var cosmos = Substitute.For<ICosmosDbService>();
        cosmos.GetAsync<User>("users", "admin-1", "admin-1")
            .Returns(new User { Id = "admin-1", Role = "admin" });
        var handler = new CurrentAdminAuthorizationHandler(
            cosmos,
            NullLogger<CurrentAdminAuthorizationHandler>.Instance);
        var requirement = new CurrentAdminRequirement();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin-1"),
            new Claim(ClaimTypes.Role, "user")
        ], "Test"));
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }
}
