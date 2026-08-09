using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ClearExtensions;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class SearchControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private const string TestUserId = CustomWebApplicationFactory.TestUserId;

    public SearchControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_WithMemoryQuery_ReturnsScoredItemWithReasons()
    {
        _factory.CosmosDb.ClearSubstitute();

        var venue = new Venue
        {
            Id = "venue-1",
            UserId = TestUserId,
            Name = "The May Room",
            Type = VenueType.Restaurant,
            Rating = 4.5,
            CreatedAt = new DateTime(2024, 5, 2, 0, 0, 0, DateTimeKind.Utc)
        };
        var matchingItem = new Item
        {
            Id = "item-1",
            UserId = TestUserId,
            Name = "Barrel Aged Old Fashioned",
            Type = ItemType.Cocktail,
            Status = ItemStatus.Reviewed,
            Venue = new VenueInfo { VenueId = venue.Id, Name = venue.Name },
            UserRating = 5,
            UserNotes = "Really good and memorable.",
            CreatedAt = new DateTime(2024, 5, 12, 0, 0, 0, DateTimeKind.Utc)
        };
        var nonMatch = new Item
        {
            Id = "item-2",
            UserId = TestUserId,
            Name = "Winter Dessert",
            Type = ItemType.Dessert,
            Status = ItemStatus.Reviewed,
            CreatedAt = new DateTime(2024, 1, 12, 0, 0, 0, DateTimeKind.Utc)
        };

        _factory.CosmosDb.QueryAsync<Item>(
            "items",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Item, bool>>?>())
            .Returns((new List<Item> { matchingItem, nonMatch }, (string?)null));

        _factory.CosmosDb.QueryAsync<Venue>(
            "venues",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Venue, bool>>?>())
            .Returns((new List<Venue> { venue }, (string?)null));

        var response = await _client.PostAsJsonAsync("/api/search", new SearchRequest
        {
            Query = "I had this really good drink but I can't remember anything about it. It might have been back in May 2024 at a restaurant"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchResponse>();

        body.Should().NotBeNull();
        body!.InterpretedQuery.Source.Should().Be("rules");
        body!.InterpretedQuery.DateRange.Should().NotBeNull();
        body.InterpretedQuery.ItemTypes.Should().Contain(ItemType.Cocktail);
        body.InterpretedQuery.VenueHints.Should().Contain(VenueType.Restaurant);
        body.Results.Should().Contain(r => r.Kind == "item" && r.Item!.Id == matchingItem.Id);

        var result = body.Results.First(r => r.Kind == "item" && r.Item!.Id == matchingItem.Id);
        result.Score.Should().BeGreaterThan(0.7);
        result.Reasons.Should().Contain(r => r.Contains("Added in", StringComparison.OrdinalIgnoreCase));
        result.Reasons.Should().Contain(r => r.Contains("restaurant", StringComparison.OrdinalIgnoreCase));
        result.Reasons.Should().Contain(r => r.Contains("Rated 5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_UsesAuthenticatedUserPartition()
    {
        _factory.CosmosDb.ClearSubstitute();

        _factory.CosmosDb.QueryAsync<Item>(
            "items",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Item, bool>>?>())
            .Returns((new List<Item>(), (string?)null));

        _factory.CosmosDb.QueryAsync<Venue>(
            "venues",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Venue, bool>>?>())
            .Returns((new List<Venue>(), (string?)null));

        var response = await _client.PostAsJsonAsync("/api/search", new SearchRequest { Query = "really good drink in May" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.CosmosDb.Received().QueryAsync<Item>(
            "items",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Item, bool>>?>());
        await _factory.CosmosDb.Received().QueryAsync<Venue>(
            "venues",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Venue, bool>>?>());
    }
}
