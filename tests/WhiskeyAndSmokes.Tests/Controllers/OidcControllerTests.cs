using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
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

public class OidcControllerTests
{
    [Fact]
    public async Task StartLogin_UsesConfiguredPublicOriginAndIgnoresForwardedHost()
    {
        var oidcService = Substitute.For<IOidcService>();
        var cosmos = Substitute.For<ICosmosDbService>();
        cosmos.GetAsync<AppAuthSettingsDocument>(
                "settings",
                AppAuthSettingsDocument.DocumentId,
                AppAuthSettingsDocument.PartitionKeyValue)
            .Returns((AppAuthSettingsDocument?)null);
        oidcService.StartLoginAsync("provider-1", "/", null, "https://app.example.com")
            .Returns(new OidcStartLoginResult
            {
                AuthorizationUrl = "https://id.example.com/authorize",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            });
        var controller = CreateController(oidcService, cosmos, new Dictionary<string, string?>
        {
            ["Oidc:PublicOrigin"] = "https://app.example.com"
        });
        controller.ControllerContext.HttpContext.Request.Headers["X-Forwarded-Host"] = "evil.example.com";
        controller.ControllerContext.HttpContext.Request.Headers["X-Forwarded-Proto"] = "https";

        var result = await controller.StartLogin("provider-1", new OidcStartLoginRequest { RedirectPath = "/" });

        result.Result.Should().BeOfType<OkObjectResult>();
        await oidcService.Received(1).StartLoginAsync("provider-1", "/", null, "https://app.example.com");
    }

    [Fact]
    public async Task StartLogin_UsesAdminManagedPublicOriginBeforeConfiguredFallback()
    {
        var oidcService = Substitute.For<IOidcService>();
        var cosmos = Substitute.For<ICosmosDbService>();
        cosmos.GetAsync<AppAuthSettingsDocument>(
                "settings",
                AppAuthSettingsDocument.DocumentId,
                AppAuthSettingsDocument.PartitionKeyValue)
            .Returns(new AppAuthSettingsDocument
            {
                Settings = new AppAuthSettings { OidcPublicOrigin = "https://admin.example.com" }
            });
        oidcService.StartLoginAsync("provider-1", "/", null, "https://admin.example.com")
            .Returns(new OidcStartLoginResult
            {
                AuthorizationUrl = "https://id.example.com/authorize",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            });
        var controller = CreateController(oidcService, cosmos, new Dictionary<string, string?>
        {
            ["Oidc:PublicOrigin"] = "https://fallback.example.com"
        });

        var result = await controller.StartLogin("provider-1", new OidcStartLoginRequest { RedirectPath = "/" });

        result.Result.Should().BeOfType<OkObjectResult>();
        await oidcService.Received(1).StartLoginAsync("provider-1", "/", null, "https://admin.example.com");
    }

    [Fact]
    public async Task StartLogin_WithoutConfiguredPublicOriginInProductionRejectsRequest()
    {
        var oidcService = Substitute.For<IOidcService>();
        var cosmos = Substitute.For<ICosmosDbService>();
        cosmos.GetAsync<AppAuthSettingsDocument>(
                "settings",
                AppAuthSettingsDocument.DocumentId,
                AppAuthSettingsDocument.PartitionKeyValue)
            .Returns((AppAuthSettingsDocument?)null);
        var controller = CreateController(oidcService, cosmos, new Dictionary<string, string?>(), "Production");

        var result = await controller.StartLogin("provider-1", new OidcStartLoginRequest { RedirectPath = "/" });

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
        await oidcService.DidNotReceiveWithAnyArgs().StartLoginAsync(default!, default, default, default!);
    }

    [Fact]
    public async Task LinkCallback_UsesAuthenticatedUserForLinkCompletion()
    {
        var oidcService = Substitute.For<IOidcService>();
        var cosmos = Substitute.For<ICosmosDbService>();
        cosmos.GetAsync<AppAuthSettingsDocument>(
                "settings",
                AppAuthSettingsDocument.DocumentId,
                AppAuthSettingsDocument.PartitionKeyValue)
            .Returns((AppAuthSettingsDocument?)null);
        oidcService.CompleteLinkCallbackAsync("provider-1", "user-1", "code", "state", "https://app.example.com")
            .Returns(new OidcLinkCallbackResult());
        var controller = CreateController(oidcService, cosmos, new Dictionary<string, string?>
        {
            ["Oidc:PublicOrigin"] = "https://app.example.com"
        });
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        ], "Test"));

        var result = await controller.LinkCallback("provider-1", "code", "state", null);

        result.Result.Should().BeOfType<OkObjectResult>();
        await oidcService.Received(1).CompleteLinkCallbackAsync(
            "provider-1",
            "user-1",
            "code",
            "state",
            "https://app.example.com");
    }

    private static OidcController CreateController(
        IOidcService oidcService,
        ICosmosDbService cosmosDb,
        IDictionary<string, string?> configurationValues,
        string environmentName = "Production")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        var controller = new OidcController(
            oidcService,
            NullLogger<OidcController>.Instance,
            configuration,
            environment,
            cosmosDb);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("api.internal");
        return controller;
    }
}
