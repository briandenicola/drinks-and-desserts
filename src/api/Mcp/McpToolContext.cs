using System.Security.Claims;
using ModelContextProtocol;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Mcp;

/// <summary>
/// Resolves the authenticated owner and API-key capabilities for the current MCP request.
/// Registered per-request (scoped) so tool classes can depend on it via constructor injection.
/// </summary>
public class McpToolContext(IHttpContextAccessor httpContextAccessor)
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User
        ?? throw new McpException("Unauthorized: no authenticated MCP request context");

    public string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new McpException("Unauthorized: missing user identity");

    private IReadOnlySet<string> Capabilities
    {
        get
        {
            var raw = User.FindFirstValue("api_key_capabilities");
            if (string.IsNullOrWhiteSpace(raw))
                return new HashSet<string> { ApiKeyCapability.Read };
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        }
    }

    public bool HasCapability(string capability) => Capabilities.Contains(capability);

    /// <summary>Throws an MCP tool error if the current API key lacks the required capability.</summary>
    public void RequireCapability(string capability)
    {
        if (!HasCapability(capability))
            throw new McpException($"This tool requires the '{capability}' API key capability.");
    }
}
