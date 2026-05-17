using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using WhiskeyAndSmokes.Api.Controllers;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class RecommendationsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private const string TestUserId = CustomWebApplicationFactory.TestUserId;

    public RecommendationsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRecommendations_ReturnsOk_WithValidRequest()
    {
        var request = new RecommendationRequest
        {
            Preferences = "I like smoky whiskeys",
            ItemTypes = ["Whiskey"],
            Limit = 3
        };

        var expectedResponse = new RecommendationResponse
        {
            Recommendations =
            [
                new RecommendedItem
                {
                    Name = "Ardbeg Uigeadail",
                    Type = "Whiskey",
                    Confidence = 0.92,
                    Reason = "Similar peaty profile to your favorites"
                }
            ],
            Reasoning = "Based on your preference for smoky whiskeys",
            BasedOnItems = ["item-1", "item-2"]
        };

        _factory.RecommendationService
            .GetRecommendationsAsync(TestUserId, Arg.Any<RecommendationRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        var response = await _client.PostAsJsonAsync("/api/recommendations", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RecommendationResponse>();
        body.Should().NotBeNull();
        body!.Recommendations.Should().HaveCount(1);
        body.Recommendations[0].Name.Should().Be("Ardbeg Uigeadail");
    }

    [Fact]
    public async Task GetUserProfile_ReturnsOk()
    {
        var expectedProfile = new UserRatingProfile
        {
            UserId = TestUserId,
            AverageRating = 4.2,
            TotalRatedItems = 15,
            TopRatedItems =
            [
                new RatedItemSummary
                {
                    ItemId = "item-1",
                    Name = "Lagavulin 16",
                    Type = "Whiskey",
                    Rating = 5.0
                }
            ],
            ItemTypePreferences = new Dictionary<string, TypePreference>
            {
                ["Whiskey"] = new() { Count = 10, AverageRating = 4.5, TopRated = ["Lagavulin 16"] }
            }
        };

        _factory.RecommendationService
            .BuildUserProfileAsync(TestUserId, Arg.Any<CancellationToken>())
            .Returns(expectedProfile);

        var response = await _client.GetAsync("/api/recommendations/profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserRatingProfile>();
        body.Should().NotBeNull();
        body!.TotalRatedItems.Should().Be(15);
        body.AverageRating.Should().Be(4.2);
    }

    [Fact]
    public async Task ExtractMenu_ReturnsBadRequest_WhenPhotoUrlEmpty()
    {
        var request = new MenuExtractionRequest { PhotoUrl = "" };

        var response = await _client.PostAsJsonAsync("/api/recommendations/extract-menu", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExtractMenu_ReturnsBadRequest_WhenPhotoUrlNotOwnedByUser()
    {
        var request = new MenuExtractionRequest
        {
            PhotoUrl = "https://storage.blob.core.windows.net/photos/other-user-id/menu.jpg"
        };

        var response = await _client.PostAsJsonAsync("/api/recommendations/extract-menu", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExtractMenu_ReturnsOk_WithValidPhotoUrl()
    {
        var request = new MenuExtractionRequest
        {
            PhotoUrl = $"https://storage.blob.core.windows.net/photos/{TestUserId}/menu.jpg"
        };

        var expectedItems = new List<string> { "Old Fashioned", "Manhattan", "Negroni" };

        _factory.RecommendationService
            .ExtractMenuItemsAsync(request.PhotoUrl, Arg.Any<CancellationToken>())
            .Returns(expectedItems);

        var response = await _client.PostAsJsonAsync("/api/recommendations/extract-menu", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<string>>();
        body.Should().NotBeNull();
        body.Should().HaveCount(3);
        body.Should().Contain("Old Fashioned");
    }

    [Fact]
    public async Task GetRecommendations_Returns500_WhenServiceThrows()
    {
        var request = new RecommendationRequest { Limit = 5 };

        _factory.RecommendationService
            .GetRecommendationsAsync(TestUserId, Arg.Any<RecommendationRequest>(), Arg.Any<CancellationToken>())
            .Returns<RecommendationResponse>(_ => throw new InvalidOperationException("AI service unavailable"));

        var response = await _client.PostAsJsonAsync("/api/recommendations", request);
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
