using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ClearExtensions;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class VenuesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static string TestUserId => CustomWebApplicationFactory.TestUserId;

    public VenuesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListVenues_ReturnsOk()
    {
        _factory.CosmosDb.ClearSubstitute();

        var venues = new List<Venue>
        {
            new() { Id = "v1", UserId = TestUserId, Name = "The Pub", Type = VenueType.Bar },
            new() { Id = "v2", UserId = TestUserId, Name = "Chez Louis", Type = VenueType.Restaurant },
        };

        _factory.CosmosDb.QueryAsync<Venue>(
            "venues",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Venue, bool>>?>())
            .Returns((venues, (string?)null));

        var response = await _client.GetAsync("/api/venues");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<Venue>>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListVenues_WithTypeFilter_ReturnsOk()
    {
        _factory.CosmosDb.ClearSubstitute();

        var venues = new List<Venue>
        {
            new() { Id = "v1", UserId = TestUserId, Name = "The Pub", Type = VenueType.Bar },
        };

        _factory.CosmosDb.QueryAsync<Venue>(
            "venues",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Venue, bool>>?>())
            .Returns((venues, (string?)null));

        var response = await _client.GetAsync("/api/venues?type=bar");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<Venue>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task ListVenues_WithSearch_FiltersBeforePaging()
    {
        _factory.CosmosDb.ClearSubstitute();

        var allVenues = Enumerable.Range(1, 30)
            .Select(i => new Venue { Id = $"filler-{i}", UserId = TestUserId, Name = $"Venue {i}", Type = VenueType.Bar })
            .Append(new Venue { Id = "venue-1845", UserId = TestUserId, Name = "1845 Steakhouse", Type = VenueType.Restaurant })
            .ToList();

        _factory.CosmosDb.QueryAsync<Venue>(
            "venues",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Venue, bool>>?>(),
            Arg.Any<Expression<Func<Venue, object>>?>(),
            Arg.Any<bool>())
            .Returns(callInfo =>
            {
                var maxItems = callInfo.ArgAt<int>(3);
                var predicate = callInfo.ArgAt<Expression<Func<Venue, bool>>?>(4);
                var filtered = predicate == null ? allVenues : allVenues.Where(predicate.Compile()).ToList();
                return (filtered.Take(maxItems).ToList(), (string?)null);
            });

        var response = await _client.GetAsync("/api/venues?search=1845&pageSize=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<Venue>>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle().Which.Id.Should().Be("venue-1845");
        await _factory.CosmosDb.Received(1).QueryAsync<Venue>(
            "venues",
            TestUserId,
            Arg.Any<string?>(),
            5,
            Arg.Any<Expression<Func<Venue, bool>>?>(),
            Arg.Any<Expression<Func<Venue, object>>?>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task GetVenueItems_WithSearch_FiltersLinkedItemsBeforePaging()
    {
        _factory.CosmosDb.ClearSubstitute();

        var venue = new Venue { Id = "v1", UserId = TestUserId, Name = "1845", Type = VenueType.Restaurant };
        var allItems = Enumerable.Range(1, 30)
            .Select(i => new Item
            {
                Id = $"filler-{i}",
                UserId = TestUserId,
                Name = $"Filler {i}",
                Type = ItemType.Whiskey,
                Status = ItemStatus.Reviewed,
                Venue = new VenueInfo { VenueId = "v1", Name = "1845" }
            })
            .Append(new Item
            {
                Id = "steak-1",
                UserId = TestUserId,
                Name = "Steak Dinner",
                Type = ItemType.Custom,
                Status = ItemStatus.Reviewed,
                Venue = new VenueInfo { VenueId = "v1", Name = "1845" }
            })
            .Append(new Item
            {
                Id = "other-steak",
                UserId = TestUserId,
                Name = "Steak Elsewhere",
                Type = ItemType.Custom,
                Status = ItemStatus.Reviewed,
                Venue = new VenueInfo { VenueId = "other", Name = "Other Venue" }
            })
            .ToList();

        _factory.CosmosDb.GetAsync<Venue>("venues", "v1", TestUserId)
            .Returns(venue);
        _factory.CosmosDb.QueryAsync<Item>(
            "items",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Item, bool>>?>(),
            Arg.Any<Expression<Func<Item, object>>?>(),
            Arg.Any<bool>())
            .Returns(callInfo =>
            {
                var maxItems = callInfo.ArgAt<int>(3);
                var predicate = callInfo.ArgAt<Expression<Func<Item, bool>>?>(4);
                var filtered = predicate == null ? allItems : allItems.Where(predicate.Compile()).ToList();
                return (filtered.Take(maxItems).ToList(), (string?)null);
            });

        var response = await _client.GetAsync("/api/venues/v1/items?search=steak&pageSize=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<Item>>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle().Which.Id.Should().Be("steak-1");
    }

    [Fact]
    public async Task GetVenue_ReturnsOk()
    {
        _factory.CosmosDb.ClearSubstitute();

        var venue = new Venue { Id = "v1", UserId = TestUserId, Name = "The Pub", Type = VenueType.Bar };

        _factory.CosmosDb.GetAsync<Venue>("venues", "v1", TestUserId)
            .Returns(venue);

        var response = await _client.GetAsync("/api/venues/v1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Venue>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("The Pub");
    }

    [Fact]
    public async Task GetVenue_NotFound_Returns404()
    {
        _factory.CosmosDb.ClearSubstitute();

        _factory.CosmosDb.GetAsync<Venue>("venues", "missing", TestUserId)
            .Returns((Venue?)null);

        var response = await _client.GetAsync("/api/venues/missing");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateVenue_ReturnsCreated()
    {
        _factory.CosmosDb.ClearSubstitute();

        _factory.CosmosDb.CreateAsync("venues", Arg.Any<Venue>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Venue>(1));

        var request = new CreateVenueRequest
        {
            Name = "New Bar",
            Type = VenueType.Bar,
        };

        var response = await _client.PostAsJsonAsync("/api/venues", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await _factory.CosmosDb.Received(1).CreateAsync("venues", Arg.Any<Venue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateVenue_CafeType_ReturnsCreated()
    {
        _factory.CosmosDb.ClearSubstitute();

        _factory.CosmosDb.CreateAsync("venues", Arg.Any<Venue>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Venue>(1));

        var request = new CreateVenueRequest
        {
            Name = "Blue Bottle Coffee",
            Type = VenueType.Cafe,
        };

        var response = await _client.PostAsJsonAsync("/api/venues", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await _factory.CosmosDb.Received(1).CreateAsync("venues", Arg.Any<Venue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateVenue_InvalidType_Returns400()
    {
        _factory.CosmosDb.ClearSubstitute();

        var request = new CreateVenueRequest
        {
            Name = "Bad Venue",
            Type = "invalid",
        };

        var response = await _client.PostAsJsonAsync("/api/venues", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateVenue_WithoutLabels_DefaultsToToTry()
    {
        _factory.CosmosDb.ClearSubstitute();

        Venue? capturedVenue = null;
        _factory.CosmosDb.CreateAsync("venues", Arg.Any<Venue>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                capturedVenue = callInfo.ArgAt<Venue>(1);
                return capturedVenue;
            });

        var request = new CreateVenueRequest
        {
            Name = "New Bar",
            Type = VenueType.Bar,
        };

        var response = await _client.PostAsJsonAsync("/api/venues", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        capturedVenue.Should().NotBeNull();
        capturedVenue!.Labels.Should().ContainSingle().Which.Should().Be("to-try");
    }

    [Fact]
    public async Task CreateVenue_WithCustomLabels_UsesProvidedLabels()
    {
        _factory.CosmosDb.ClearSubstitute();

        Venue? capturedVenue = null;
        _factory.CosmosDb.CreateAsync("venues", Arg.Any<Venue>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                capturedVenue = callInfo.ArgAt<Venue>(1);
                return capturedVenue;
            });

        var request = new CreateVenueRequest
        {
            Name = "Fancy Restaurant",
            Type = VenueType.Restaurant,
            Labels = ["fine dining", "date night"],
        };

        var response = await _client.PostAsJsonAsync("/api/venues", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        capturedVenue.Should().NotBeNull();
        capturedVenue!.Labels.Should().HaveCount(2);
        capturedVenue.Labels.Should().Contain("fine dining");
        capturedVenue.Labels.Should().Contain("date night");
        capturedVenue.Labels.Should().NotContain("to-try");
    }

    [Fact]
    public async Task CreateVenue_WithEmptyLabels_DefaultsToToTry()
    {
        _factory.CosmosDb.ClearSubstitute();

        Venue? capturedVenue = null;
        _factory.CosmosDb.CreateAsync("venues", Arg.Any<Venue>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                capturedVenue = callInfo.ArgAt<Venue>(1);
                return capturedVenue;
            });

        var request = new CreateVenueRequest
        {
            Name = "Empty Labels Bar",
            Type = VenueType.Bar,
            Labels = [],
        };

        var response = await _client.PostAsJsonAsync("/api/venues", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        capturedVenue.Should().NotBeNull();
        capturedVenue!.Labels.Should().ContainSingle().Which.Should().Be("to-try");
    }

    [Fact]
    public async Task CreateVenue_WithWhitespaceOnlyLabels_DefaultsToToTry()
    {
        _factory.CosmosDb.ClearSubstitute();

        Venue? capturedVenue = null;
        _factory.CosmosDb.CreateAsync("venues", Arg.Any<Venue>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                capturedVenue = callInfo.ArgAt<Venue>(1);
                return capturedVenue;
            });

        var request = new CreateVenueRequest
        {
            Name = "Whitespace Labels Bar",
            Type = VenueType.Bar,
            Labels = ["  ", "\t", ""],
        };

        var response = await _client.PostAsJsonAsync("/api/venues", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        capturedVenue.Should().NotBeNull();
        capturedVenue!.Labels.Should().ContainSingle().Which.Should().Be("to-try");
    }

    [Fact]
    public async Task UpdateVenue_ReturnsOk()
    {
        _factory.CosmosDb.ClearSubstitute();

        var existing = new Venue { Id = "v1", UserId = TestUserId, Name = "Old Name", Type = VenueType.Bar };

        _factory.CosmosDb.GetAsync<Venue>("venues", "v1", TestUserId)
            .Returns(existing);

        _factory.CosmosDb.UpsertAsync("venues", Arg.Any<Venue>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Venue>(1));

        var update = new UpdateVenueRequest { Name = "New Name" };

        var response = await _client.PutAsJsonAsync("/api/venues/v1", update);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Venue>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateVenue_NotFound_Returns404()
    {
        _factory.CosmosDb.ClearSubstitute();

        _factory.CosmosDb.GetAsync<Venue>("venues", "missing", TestUserId)
            .Returns((Venue?)null);

        var update = new UpdateVenueRequest { Name = "Doesn't matter" };

        var response = await _client.PutAsJsonAsync("/api/venues/missing", update);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteVenue_ReturnsNoContent()
    {
        _factory.CosmosDb.ClearSubstitute();
        _factory.BlobStorage.ClearSubstitute();

        var venue = new Venue
        {
            Id = "v1",
            UserId = TestUserId,
            Name = "To Delete",
            Type = VenueType.Bar,
            PhotoUrls = ["https://blob.storage/test-user-id/photo1.jpg", "https://blob.storage/test-user-id/photo2.jpg"]
        };

        _factory.CosmosDb.GetAsync<Venue>("venues", "v1", TestUserId)
            .Returns(venue);

        var response = await _client.DeleteAsync("/api/venues/v1");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.BlobStorage.Received(2).DeleteBlobAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteVenue_NotFound_ReturnsNoContent()
    {
        _factory.CosmosDb.ClearSubstitute();

        _factory.CosmosDb.GetAsync<Venue>("venues", "gone", TestUserId)
            .Returns((Venue?)null);

        var response = await _client.DeleteAsync("/api/venues/gone");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddPhoto_ValidatesOwnership()
    {
        _factory.CosmosDb.ClearSubstitute();

        var venue = new Venue { Id = "v1", UserId = TestUserId, Name = "Venue", Type = VenueType.Bar };

        _factory.CosmosDb.GetAsync<Venue>("venues", "v1", TestUserId)
            .Returns(venue);

        var request = new AddPhotoRequest { BlobUrl = "https://blob.storage/other-user/photo.jpg" };

        var response = await _client.PostAsJsonAsync("/api/venues/v1/photos", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddPhoto_ReturnsOk()
    {
        _factory.CosmosDb.ClearSubstitute();

        var venue = new Venue { Id = "v1", UserId = TestUserId, Name = "Venue", Type = VenueType.Bar };

        _factory.CosmosDb.GetAsync<Venue>("venues", "v1", TestUserId)
            .Returns(venue);

        _factory.CosmosDb.UpsertAsync("venues", Arg.Any<Venue>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Venue>(1));

        var blobUrl = $"https://blob.storage/{TestUserId}/2025/01/01/photo.jpg";
        var request = new AddPhotoRequest { BlobUrl = blobUrl };

        var response = await _client.PostAsJsonAsync("/api/venues/v1/photos", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemovePhoto_ReturnsOk()
    {
        _factory.CosmosDb.ClearSubstitute();
        _factory.BlobStorage.ClearSubstitute();

        var photoUrl = $"https://blob.storage/{TestUserId}/2025/01/01/photo.jpg";
        var venue = new Venue
        {
            Id = "v1",
            UserId = TestUserId,
            Name = "Venue",
            Type = VenueType.Bar,
            PhotoUrls = [photoUrl]
        };

        _factory.CosmosDb.GetAsync<Venue>("venues", "v1", TestUserId)
            .Returns(venue);

        _factory.CosmosDb.UpsertAsync("venues", Arg.Any<Venue>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Venue>(1));

        var request = new RemovePhotoRequest { BlobUrl = photoUrl };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/venues/v1/photos")
        {
            Content = JsonContent.Create(request)
        };

        var response = await _client.SendAsync(httpRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.BlobStorage.Received(1).DeleteBlobAsync(photoUrl);
    }

    [Fact]
    public async Task ShareVenue_WhenFriends_CreatesNotificationForRecipient()
    {
        _factory.NotificationService.ClearReceivedCalls();

        var venue = new Venue
        {
            Id = "share-venue-1",
            UserId = TestUserId,
            Name = "Rare Books Bar",
            Type = VenueType.Bar
        };

        _factory.CosmosDb.GetAsync<Venue>("venues", "share-venue-1", TestUserId)
            .Returns(venue);

        var friendship = new Friendship
        {
            Id = "fs-1",
            UserId = TestUserId,
            FriendId = "friend-1",
            FriendDisplayName = "Bob",
            Status = FriendshipStatus.Accepted
        };

        _factory.CosmosDb.QueryAsync(
            "friendships",
            TestUserId,
            Arg.Any<string?>(),
            1,
            Arg.Any<Expression<Func<Friendship, bool>>?>())
            .Returns((new List<Friendship> { friendship }, (string?)null));

        var response = await _client.PostAsJsonAsync("/api/venues/share-venue-1/share", new ShareRequest { FriendId = "friend-1" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.NotificationService.Received(1).CreateAsync(Arg.Is<Notification>(n =>
            n.UserId == "friend-1" &&
            n.Type == NotificationType.VenueShared &&
            n.SourceUserId == TestUserId &&
            n.ReferenceType == "venue" &&
            n.ReferenceId == "share-venue-1"));
    }

    [Fact]
    public async Task ShareVenue_WhenNotFriends_Returns403()
    {
        var venue = new Venue
        {
            Id = "share-venue-2",
            UserId = TestUserId,
            Name = "Rare Books Bar",
            Type = VenueType.Bar
        };

        _factory.CosmosDb.GetAsync<Venue>("venues", "share-venue-2", TestUserId)
            .Returns(venue);

        _factory.CosmosDb.QueryAsync(
            "friendships",
            TestUserId,
            Arg.Any<string?>(),
            1,
            Arg.Any<Expression<Func<Friendship, bool>>?>())
            .Returns((new List<Friendship>(), (string?)null));

        var response = await _client.PostAsJsonAsync("/api/venues/share-venue-2/share", new ShareRequest { FriendId = "stranger-id" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
