using System.ComponentModel;
using ModelContextProtocol.Server;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Mcp;

/// <summary>
/// Invokes the AI recommendation pipeline on behalf of the authenticated owner.
/// Requires the "agentic" API key capability in addition to the baseline MCP "read" capability.
/// This tool reads the user's rating history and runs a direct AI inference call; it does not
/// create, modify, or delete any collection data.
/// </summary>
[McpServerToolType]
public class McpRecommendationTools(IRecommendationService recommendationService, McpToolContext context)
{
    [McpServerTool(Name = "get_recommendations"), Description(
        "Get AI-powered personalized item recommendations for the authenticated user, based on their " +
        "rating history and optional text preferences. Requires the 'agentic' API key capability. " +
        "Does not modify any data.")]
    public async Task<RecommendationResponse> GetRecommendations(
        [Description("Optional free-text preference, e.g. 'something smoky' or 'fruity dessert'")] string? preferences = null,
        [Description("Optional item type filters, e.g. [\"whiskey\",\"cigar\"]")] List<string>? itemTypes = null,
        [Description("Maximum recommendations to return (default 5, max 10)")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        context.RequireCapability(ApiKeyCapability.Agentic);

        var request = new RecommendationRequest
        {
            Preferences = preferences,
            ItemTypes = itemTypes,
            Limit = Math.Clamp(limit ?? 5, 1, 10),
        };

        return await recommendationService.GetRecommendationsAsync(context.UserId, request, cancellationToken);
    }
}
