using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class FriendsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public FriendsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListFriends_ReturnsAcceptedFriends()
    {
        var friends = new List<Friendship>
        {
            new() { Id = "f1", UserId = CustomWebApplicationFactory.TestUserId, FriendId = "friend-1", FriendDisplayName = "Alice", Status = FriendshipStatus.Accepted }
        };

        _factory.CosmosDb.QueryAsync(
            "friendships",
            CustomWebApplicationFactory.TestUserId,
            Arg.Any<string?>(),
            100,
            Arg.Any<Expression<Func<Friendship, bool>>?>())
            .Returns((friends, (string?)null));

        var response = await _client.GetAsync("/api/friends");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<Friendship>>();
        body.Should().HaveCount(1);
        body![0].FriendDisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task ListRequests_ReturnsSentAndReceived()
    {
        var requests = new List<Friendship>
        {
            new() { Id = "f1", UserId = CustomWebApplicationFactory.TestUserId, FriendId = "u2", Status = FriendshipStatus.PendingSent },
            new() { Id = "f2", UserId = CustomWebApplicationFactory.TestUserId, FriendId = "u3", Status = FriendshipStatus.PendingReceived },
        };

        _factory.CosmosDb.QueryAsync(
            "friendships",
            CustomWebApplicationFactory.TestUserId,
            Arg.Any<string?>(),
            100,
            Arg.Any<Expression<Func<Friendship, bool>>?>())
            .Returns((requests, (string?)null));

        var response = await _client.GetAsync("/api/friends/requests");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateInvite_ReturnsInviteWithCode()
    {
        _factory.CosmosDb.CreateAsync(
            "friend-invites",
            Arg.Any<FriendInvite>(),
            Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<FriendInvite>(1));

        var response = await _client.PostAsync("/api/friends/invite", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<FriendInvite>();
        body.Should().NotBeNull();
        body!.Id.Should().HaveLength(8);
        body.UserId.Should().Be(CustomWebApplicationFactory.TestUserId);
    }

    [Fact]
    public async Task JoinViaInvite_WithOwnInvite_ReturnsBadRequest()
    {
        var invite = new FriendInvite
        {
            Id = "TESTCODE",
            UserId = CustomWebApplicationFactory.TestUserId,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _factory.CosmosDb.GetAsync<FriendInvite>("friend-invites", "TESTCODE", "TESTCODE")
            .Returns(invite);

        var response = await _client.PostAsync("/api/friends/join/TESTCODE", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JoinViaInvite_WithExpiredInvite_ReturnsNotFound()
    {
        var invite = new FriendInvite
        {
            Id = "EXPIRED1",
            UserId = "other-user",
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };

        _factory.CosmosDb.GetAsync<FriendInvite>("friend-invites", "EXPIRED1", "EXPIRED1")
            .Returns(invite);

        var response = await _client.PostAsync("/api/friends/join/EXPIRED1", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveFriend_WithValidFriendship_ReturnsNoContent()
    {
        var friendship = new Friendship
        {
            Id = "fs-1",
            UserId = CustomWebApplicationFactory.TestUserId,
            FriendId = "friend-1",
            Status = FriendshipStatus.Accepted
        };

        _factory.CosmosDb.GetAsync<Friendship>("friendships", "fs-1", CustomWebApplicationFactory.TestUserId)
            .Returns(friendship);

        _factory.CosmosDb.QueryAsync(
            "friendships",
            "friend-1",
            Arg.Any<string?>(),
            1,
            Arg.Any<Expression<Func<Friendship, bool>>?>())
            .Returns((new List<Friendship>(), (string?)null));

        var response = await _client.DeleteAsync("/api/friends/fs-1");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveFriend_NotOwner_ReturnsNotFound()
    {
        _factory.CosmosDb.GetAsync<Friendship>("friendships", "fs-other", CustomWebApplicationFactory.TestUserId)
            .Returns((Friendship?)null);

        var response = await _client.DeleteAsync("/api/friends/fs-other");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFriendItems_NotFriends_ReturnsForbid()
    {
        _factory.CosmosDb.QueryAsync(
            "friendships",
            CustomWebApplicationFactory.TestUserId,
            Arg.Any<string?>(),
            1,
            Arg.Any<Expression<Func<Friendship, bool>>?>())
            .Returns((new List<Friendship>(), (string?)null));

        var response = await _client.GetAsync("/api/friends/stranger-id/items");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFriendItems_AreFriends_ReturnsItems()
    {
        var friendship = new Friendship
        {
            Id = "fs-1",
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

        var items = new List<Item>
        {
            new() { Id = "item-1", Name = "Test Whiskey", UserId = "friend-1" }
        };

        _factory.CosmosDb.QueryAsync<Item>(
            "items",
            "friend-1",
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Item, bool>>?>())
            .Returns((items, (string?)null));

        var response = await _client.GetAsync("/api/friends/friend-1/items");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
