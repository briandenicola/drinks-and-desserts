using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using WhiskeyAndSmokes.Api.Controllers;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class ThoughtsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ThoughtsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetThoughts_InvalidTargetType_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/thoughts/invalid/some-id?targetUserId=user-1");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetThoughts_MissingTargetUserId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/thoughts/item/some-id");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetThoughts_OwnItems_ReturnsOk()
    {
        var thoughts = new List<Thought>
        {
            new() { Id = "t1", AuthorId = "friend-1", TargetUserId = CustomWebApplicationFactory.TestUserId, TargetType = "item", TargetId = "item-1", Content = "Great!" }
        };

        _factory.CosmosDb.QueryAsync(
            "thoughts",
            CustomWebApplicationFactory.TestUserId,
            Arg.Any<string?>(),
            100,
            Arg.Any<Expression<Func<Thought, bool>>?>())
            .Returns((thoughts, (string?)null));

        var response = await _client.GetAsync($"/api/thoughts/item/item-1?targetUserId={CustomWebApplicationFactory.TestUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetThoughts_NotFriendOrOwner_ReturnsForbid()
    {
        // Not the owner and not friends
        _factory.CosmosDb.QueryAsync(
            "friendships",
            CustomWebApplicationFactory.TestUserId,
            Arg.Any<string?>(),
            1,
            Arg.Any<Expression<Func<Friendship, bool>>?>())
            .Returns((new List<Friendship>(), (string?)null));

        var response = await _client.GetAsync("/api/thoughts/item/item-1?targetUserId=stranger");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateThought_OnOwnItem_ReturnsBadRequest()
    {
        var request = new CreateThoughtRequest
        {
            Content = "Nice!",
            TargetUserId = CustomWebApplicationFactory.TestUserId,
            TargetType = "item",
            TargetId = "item-1"
        };

        var response = await _client.PostAsJsonAsync("/api/thoughts", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateThought_NotFriends_ReturnsForbid()
    {
        _factory.CosmosDb.QueryAsync(
            "friendships",
            CustomWebApplicationFactory.TestUserId,
            Arg.Any<string?>(),
            1,
            Arg.Any<Expression<Func<Friendship, bool>>?>())
            .Returns((new List<Friendship>(), (string?)null));

        var request = new CreateThoughtRequest
        {
            Content = "Nice!",
            TargetUserId = "stranger",
            TargetType = "item",
            TargetId = "item-1"
        };

        var response = await _client.PostAsJsonAsync("/api/thoughts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateThought_AsFriend_ReturnsOk()
    {
        var friendship = new Friendship
        {
            UserId = CustomWebApplicationFactory.TestUserId,
            FriendId = "friend-1",
            Status = FriendshipStatus.Accepted
        };

        _factory.CosmosDb.QueryAsync(
            "friendships",
            CustomWebApplicationFactory.TestUserId,
            Arg.Any<string?>(),
            1,
            Arg.Any<Expression<Func<Friendship, bool>>?>())
            .Returns((new List<Friendship> { friendship }, (string?)null));

        _factory.CosmosDb.GetAsync<Item>("items", "item-1", "friend-1")
            .Returns(new Item { Id = "item-1", Name = "Test Item", UserId = "friend-1" });

        _factory.CosmosDb.CreateAsync(
            "thoughts",
            Arg.Any<Thought>(),
            Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Thought>(1));

        var request = new CreateThoughtRequest
        {
            Content = "This is great!",
            TargetUserId = "friend-1",
            TargetType = "item",
            TargetId = "item-1"
        };

        var response = await _client.PostAsJsonAsync("/api/thoughts", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteThought_NotAuthor_ReturnsNotFound()
    {
        _factory.CosmosDb.QueryCrossPartitionAsync<Thought>(
            "thoughts",
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>>(),
            1)
            .Returns(new List<Thought>());

        var response = await _client.DeleteAsync("/api/thoughts/nonexistent-id");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyThoughts_ReturnsThoughts()
    {
        var thoughts = new List<Thought>
        {
            new() { Id = "t1", AuthorId = CustomWebApplicationFactory.TestUserId, Content = "Loved it!" }
        };

        _factory.CosmosDb.QueryCrossPartitionAsync<Thought>(
            "thoughts",
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>>(),
            100)
            .Returns(thoughts);

        var response = await _client.GetAsync("/api/thoughts/mine");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetThoughtsOnMyItems_ReturnsThoughts()
    {
        var thoughts = new List<Thought>
        {
            new() { Id = "t1", AuthorId = "friend-1", TargetUserId = CustomWebApplicationFactory.TestUserId, Content = "Nice!" }
        };

        _factory.CosmosDb.QueryAsync(
            "thoughts",
            CustomWebApplicationFactory.TestUserId,
            Arg.Any<string?>(),
            100,
            Arg.Any<Expression<Func<Thought, bool>>?>())
            .Returns((thoughts, (string?)null));

        var response = await _client.GetAsync("/api/thoughts/on-my-items");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
