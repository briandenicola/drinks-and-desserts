using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WhiskeyAndSmokes.Api.Controllers;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class AdminControllerTests
{
    [Fact]
    public async Task UpdateUserRole_CanPromoteOidcLinkedUserToAdmin()
    {
        var cosmos = Substitute.For<ICosmosDbService>();
        var auth = Substitute.For<IAuthService>();
        var user = new User { Id = "user-1", Email = "user@example.com", Role = "user", AuthProvider = "oidc" };
        cosmos.GetAsync<User>("users", user.Id, user.Id).Returns(user);
        cosmos.UpsertAsync("users", Arg.Any<User>(), user.Id).Returns(call => call.ArgAt<User>(1));
        var controller = CreateController(cosmos, "admin-1", auth);

        var result = await controller.UpdateUserRole(user.Id, new UpdateUserRoleRequest { Role = "admin" });

        result.Result.Should().BeOfType<OkObjectResult>();
        await cosmos.Received(1).UpsertAsync("users", Arg.Is<User>(u => u.Role == "admin"), user.Id);
        await auth.Received(1).RevokeAllRefreshTokensAsync(user.Id);
    }

    [Fact]
    public async Task UpdateUserRole_WhenTargetIsLastAdmin_ReturnsBadRequest()
    {
        var cosmos = Substitute.For<ICosmosDbService>();
        var admin = new User { Id = "admin-1", Email = "admin@example.com", Role = "admin" };
        cosmos.GetAsync<User>("users", admin.Id, admin.Id).Returns(admin);
        cosmos.QueryCrossPartitionAsync<User>("users", Arg.Is<string>(q => q.Contains("role = 'admin'")), Arg.Any<int>())
            .Returns([admin]);
        var controller = CreateController(cosmos, admin.Id);

        var result = await controller.UpdateUserRole(admin.Id, new UpdateUserRoleRequest { Role = "user" });

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        await cosmos.DidNotReceiveWithAnyArgs().UpsertAsync<User>(default!, default!, default!);
    }

    [Fact]
    public async Task DeleteUser_WhenTargetIsLastAdmin_ReturnsBadRequest()
    {
        var cosmos = Substitute.For<ICosmosDbService>();
        var admin = new User { Id = "other-admin", Email = "admin@example.com", Role = "admin" };
        cosmos.GetAsync<User>("users", admin.Id, admin.Id).Returns(admin);
        cosmos.QueryCrossPartitionAsync<User>("users", Arg.Is<string>(q => q.Contains("role = 'admin'")), Arg.Any<int>())
            .Returns([admin]);
        var controller = CreateController(cosmos, "acting-admin");

        var result = await controller.DeleteUser(admin.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
        await cosmos.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default!);
    }

    [Fact]
    public async Task UpdateAuthSettings_WithInvalidOrigin_ReturnsBadRequest()
    {
        var cosmos = Substitute.For<ICosmosDbService>();
        var controller = CreateController(cosmos, "admin-1");

        var result = await controller.UpdateAuthSettings(new UpdateAppAuthSettingsRequest
        {
            OidcPublicOrigin = "https://app.example.com/callback"
        });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        await cosmos.DidNotReceiveWithAnyArgs().UpsertAsync<AppAuthSettingsDocument>(default!, default!, default!);
    }

    private static AdminController CreateController(ICosmosDbService cosmos, string userId, IAuthService? authService = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:PublicOrigin"] = "https://fallback.example.com"
            })
            .Build();
        var controller = new AdminController(
            cosmos,
            authService ?? Substitute.For<IAuthService>(),
            Substitute.For<IPromptService>(),
            new DynamicLogLevelService(),
            new FoundryStatusService(),
            configuration,
            NullLogger<AdminController>.Instance);
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, "admin")
        ], "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }
}
