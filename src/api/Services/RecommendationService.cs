using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public class RecommendationService : IRecommendationService
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<RecommendationService> _logger;
    private readonly AiFoundryOptions _foundryOptions;
    private readonly bool _isFoundryConfigured;

    public RecommendationService(
        ICosmosDbService cosmosDb,
        IBlobStorageService blobStorage,
        ILogger<RecommendationService> logger,
        IOptions<AiFoundryOptions> foundryOptions)
    {
        _cosmosDb = cosmosDb;
        _blobStorage = blobStorage;
        _logger = logger;
        _foundryOptions = foundryOptions.Value;
        _isFoundryConfigured = !string.IsNullOrEmpty(_foundryOptions.ProjectEndpoint);
    }

    public async Task<RecommendationResponse> GetRecommendationsAsync(string userId, RecommendationRequest request)
    {
        using var activity = Diagnostics.General.StartActivity("GetRecommendations");
        activity?.SetTag("user.id", userId);
        activity?.SetTag("has_menu_photo", request.MenuPhoto != null);

        _logger.LogInformation(
            "Getting recommendations for user {UserId}, hasMenuPhoto={HasMenuPhoto}, preferences={Preferences}",
            userId, request.MenuPhoto != null, request.Preferences);

        if (!_isFoundryConfigured)
        {
            _logger.LogWarning("AI Foundry not configured — returning empty recommendations");
            return new RecommendationResponse
            {
                Reasoning = "AI Foundry is not configured. Please configure Azure AI Foundry to get personalized recommendations."
            };
        }

        // Build user profile from ratings
        var profile = await BuildUserProfileAsync(userId);

        if (profile.TotalRatedItems == 0)
        {
            _logger.LogInformation("User {UserId} has no rated items yet", userId);
            return new RecommendationResponse
            {
                Reasoning = "You haven't rated any items yet. Rate some drinks or desserts to get personalized recommendations."
            };
        }

        // Extract menu items if photo provided
        List<string>? menuItems = null;
        if (!string.IsNullOrEmpty(request.MenuPhoto))
        {
            menuItems = await ExtractMenuItemsAsync(request.MenuPhoto);
            _logger.LogInformation("Extracted {Count} menu items from photo", menuItems.Count);
        }

        // Generate recommendations using AI
        var recommendations = await GenerateRecommendationsAsync(profile, request, menuItems);

        return new RecommendationResponse
        {
            Recommendations = recommendations.Recommendations,
            Reasoning = recommendations.Reasoning,
            BasedOnItems = profile.TopRatedItems.Select(i => i.Name).Take(5).ToList(),
            ExtractedMenuItems = menuItems
        };
    }

    public async Task<UserRatingProfile> BuildUserProfileAsync(string userId)
    {
        using var activity = Diagnostics.General.StartActivity("BuildUserProfile");
        activity?.SetTag("user.id", userId);

        _logger.LogDebug("Building rating profile for user {UserId}", userId);

        var allItems = new List<Item>();
        string? token = null;

        // Fetch all user items
        do
        {
            var (items, nextToken) = await _cosmosDb.QueryAsync<Item>("items", userId, token);
            allItems.AddRange(items);
            token = nextToken;
        } while (token != null);

        // Filter to only rated items
        var ratedItems = allItems
            .Where(i => i.UserRating.HasValue && i.UserRating.Value > 0)
            .OrderByDescending(i => i.UserRating!.Value)
            .ToList();

        if (ratedItems.Count == 0)
        {
            return new UserRatingProfile
            {
                UserId = userId,
                TotalRatedItems = 0
            };
        }

        // Calculate average rating
        var avgRating = ratedItems.Average(i => i.UserRating!.Value);

        // Build type preferences
        var typePreferences = ratedItems
            .GroupBy(i => i.Type)
            .ToDictionary(
                g => g.Key,
                g => new TypePreference
                {
                    Count = g.Count(),
                    AverageRating = g.Average(i => i.UserRating!.Value),
                    TopRated = g.OrderByDescending(i => i.UserRating!.Value)
                        .Take(3)
                        .Select(i => i.Name)
                        .ToList()
                });

        // Get top rated items (4.0+)
        var topRated = ratedItems
            .Where(i => i.UserRating!.Value >= 4.0)
            .Take(10)
            .Select(i => new RatedItemSummary
            {
                ItemId = i.Id,
                Name = i.Name,
                Type = i.Type,
                Brand = i.Brand,
                Category = i.Category,
                Rating = i.UserRating!.Value,
                Details = i.Details.HasValue ? i.Details.Value.ToString() : null,
                AiSummary = i.AiSummary
            })
            .ToList();

        _logger.LogInformation(
            "Built profile for user {UserId}: {TotalRated} rated items, avg={AvgRating:F2}, topRated={TopRatedCount}",
            userId, ratedItems.Count, avgRating, topRated.Count);

        return new UserRatingProfile
        {
            UserId = userId,
            TopRatedItems = topRated,
            ItemTypePreferences = typePreferences,
            AverageRating = avgRating,
            TotalRatedItems = ratedItems.Count
        };
    }

    public async Task<List<string>> ExtractMenuItemsAsync(string photoUrl)
    {
        using var activity = Diagnostics.General.StartActivity("ExtractMenuItems");
        activity?.SetTag("photo.url", photoUrl);

        if (!_isFoundryConfigured)
        {
            _logger.LogWarning("AI Foundry not configured — cannot extract menu items");
            return [];
        }

        try
        {
            _logger.LogInformation("Extracting menu items from photo: {PhotoUrl}", photoUrl);

            var credential = CredentialFactory.Create();
            var projectClient = new AIProjectClient(new Uri(_foundryOptions.ProjectEndpoint), credential);
            var chatClient = projectClient.OpenAI.GetChatClient(_foundryOptions.Models.Vision).AsIChatClient();

            // Download the photo
            var photoBytes = await _blobStorage.DownloadAsync(photoUrl);
            if (photoBytes == null)
            {
                _logger.LogWarning("Could not download photo from {PhotoUrl}", photoUrl);
                return [];
            }

            var prompt = @"You are a menu analyst. Analyze this photo of a menu and extract all drink and dessert items listed.

For each item, provide:
1. The exact name as it appears on the menu
2. The type (whiskey, wine, cocktail, vodka, gin, cigar, dessert, coffee, espresso, latte, etc.)
3. Any visible brand, vintage, or category information

Return ONLY a JSON array of strings, where each string is the full item listing as it appears on the menu.
Example: [""Macallan 18 Year Whiskey"", ""Cabernet Sauvignon 2019"", ""Espresso Martini""]

If no items are visible or the photo is not of a menu, return an empty array: []";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, [
                    new TextContent(prompt),
                    new DataContent(photoBytes, "image/jpeg")
                ])
            };

            var response = await chatClient.GetResponseAsync(messages);
            var content = response.Text ?? "[]";
            _logger.LogDebug("Menu extraction response: {Response}", content);

            // Parse JSON response (strip markdown code fences if present)
            var items = JsonSerializer.Deserialize<List<string>>(StripMarkdownCodeFences(content)) ?? [];

            activity?.SetTag("items.count", items.Count);
            _logger.LogInformation("Extracted {Count} menu items", items.Count);

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract menu items from photo");
            return [];
        }
    }

    private static string StripMarkdownCodeFences(string text)
    {
        var json = text.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline > 0) json = json[(firstNewline + 1)..];
            var lastFence = json.LastIndexOf("```");
            if (lastFence > 0) json = json[..lastFence];
        }
        return json.Trim();
    }

    private async Task<RecommendationResponse> GenerateRecommendationsAsync(
        UserRatingProfile profile,
        RecommendationRequest request,
        List<string>? menuItems)
    {
        using var activity = Diagnostics.General.StartActivity("GenerateRecommendations");

        try
        {
            var credential = CredentialFactory.Create();
            var projectClient = new AIProjectClient(new Uri(_foundryOptions.ProjectEndpoint), credential);
            var chatClient = projectClient.OpenAI.GetChatClient(_foundryOptions.Models.Reasoning).AsIChatClient();

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("You are a personalized recommendation engine for drinks and desserts.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("## User's Rating History");
            promptBuilder.AppendLine($"Total rated items: {profile.TotalRatedItems}");
            promptBuilder.AppendLine($"Average rating: {profile.AverageRating:F2}/5.0");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("### Top Rated Items (4.0+):");
            foreach (var item in profile.TopRatedItems)
            {
                promptBuilder.AppendLine($"- {item.Name} ({item.Type}) - {item.Rating:F1} stars");
                if (!string.IsNullOrEmpty(item.Brand))
                    promptBuilder.AppendLine($"  Brand: {item.Brand}");
                if (!string.IsNullOrEmpty(item.Category))
                    promptBuilder.AppendLine($"  Category: {item.Category}");
                if (!string.IsNullOrEmpty(item.AiSummary))
                    promptBuilder.AppendLine($"  Notes: {item.AiSummary}");
            }

            promptBuilder.AppendLine();
            promptBuilder.AppendLine("### Type Preferences:");
            foreach (var (type, pref) in profile.ItemTypePreferences.OrderByDescending(kvp => kvp.Value.AverageRating))
            {
                promptBuilder.AppendLine($"- {type}: {pref.Count} rated, avg {pref.AverageRating:F2} stars");
                if (pref.TopRated.Any())
                    promptBuilder.AppendLine($"  Favorites: {string.Join(", ", pref.TopRated)}");
            }

            if (menuItems != null && menuItems.Any())
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("## Available Menu Items:");
                foreach (var item in menuItems)
                {
                    promptBuilder.AppendLine($"- {item}");
                }
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Task: Recommend items from the menu above that match the user's preferences.");
            }
            else
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Task: Recommend new items the user might enjoy based on their rating history.");
            }

            if (!string.IsNullOrEmpty(request.Preferences))
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine($"## User's Additional Preferences:");
                promptBuilder.AppendLine(request.Preferences);
            }

            if (request.ItemTypes != null && request.ItemTypes.Any())
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine($"## Focus on these types: {string.Join(", ", request.ItemTypes)}");
            }

            promptBuilder.AppendLine();
            promptBuilder.AppendLine($"## Instructions:");
            promptBuilder.AppendLine($"Provide {request.Limit} recommendations in JSON format:");
            promptBuilder.AppendLine(@"{
  ""recommendations"": [
    {
      ""name"": ""Item Name"",
      ""type"": ""whiskey"",
      ""brand"": ""Brand Name"",
      ""category"": ""Category"",
      ""confidence"": 0.95,
      ""reason"": ""Why you're recommending this based on user's history"",
      ""matchedFromMenu"": true
    }
  ],
  ""reasoning"": ""Overall explanation of recommendation strategy""
}");

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, promptBuilder.ToString())
            };

            var response = await chatClient.GetResponseAsync(messages);
            var content = response.Text ?? "{}";
            _logger.LogDebug("Recommendation response: {Response}", content);

            // Parse JSON response (strip markdown code fences if present)
            var result = JsonSerializer.Deserialize<RecommendationResponse>(StripMarkdownCodeFences(content),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new RecommendationResponse();

            activity?.SetTag("recommendations.count", result.Recommendations.Count);
            _logger.LogInformation("Generated {Count} recommendations", result.Recommendations.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate recommendations");
            return new RecommendationResponse
            {
                Reasoning = "Failed to generate recommendations. Please try again."
            };
        }
    }
}
