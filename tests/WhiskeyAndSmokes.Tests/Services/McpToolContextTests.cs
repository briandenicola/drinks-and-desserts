using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using WhiskeyAndSmokes.Api.Mcp;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Services;

public class McpToolContextTests
{
    private static McpToolContext CreateContext(string? userId, string? capabilitiesClaim)
    {
        var claims = new List<Claim>();
        if (userId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        if (capabilitiesClaim != null) claims.Add(new Claim("api_key_capabilities", capabilitiesClaim));

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey")) };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new McpToolContext(accessor);
    }

    [Fact]
    public void HasCapability_WithNoClaim_DefaultsToReadOnly()
    {
        var context = CreateContext("user-1", null);

        context.HasCapability(ApiKeyCapability.Read).Should().BeTrue();
        context.HasCapability(ApiKeyCapability.Agentic).Should().BeFalse();
    }

    [Fact]
    public void HasCapability_WithAgenticClaim_GrantsAgentic()
    {
        var context = CreateContext("user-1", "read,agentic");

        context.HasCapability(ApiKeyCapability.Read).Should().BeTrue();
        context.HasCapability(ApiKeyCapability.Agentic).Should().BeTrue();
    }

    [Fact]
    public void RequireCapability_Missing_ThrowsMcpException()
    {
        var context = CreateContext("user-1", "read");

        var act = () => context.RequireCapability(ApiKeyCapability.Agentic);

        act.Should().Throw<McpException>();
    }

    [Fact]
    public void RequireCapability_Present_DoesNotThrow()
    {
        var context = CreateContext("user-1", "read,agentic");

        var act = () => context.RequireCapability(ApiKeyCapability.Agentic);

        act.Should().NotThrow();
    }

    [Fact]
    public void UserId_MissingClaim_ThrowsMcpException()
    {
        var context = CreateContext(null, "read");

        var act = () => context.UserId;

        act.Should().Throw<McpException>();
    }
}
