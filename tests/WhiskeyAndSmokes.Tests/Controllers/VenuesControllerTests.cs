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
}
