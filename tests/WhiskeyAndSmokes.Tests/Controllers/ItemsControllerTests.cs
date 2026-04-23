using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class ItemsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private const string TestUserId = CustomWebApplicationFactory.TestUserId;

    public ItemsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListItems_ReturnsOk()
    {
        var items = new List<Item>
        {
            new() { Id = "item-1", UserId = TestUserId, Name = "Lagavulin 16", Type = ItemType.Whiskey, Status = ItemStatus.Reviewed },
            new() { Id = "item-2", UserId = TestUserId, Name = "Opus X", Type = ItemType.Cigar, Status = ItemStatus.Reviewed }
        };

        _factory.CosmosDb.QueryAsync<Item>(
            "items",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Item, bool>>?>())
            .Returns((items, (string?)null));

        var response = await _client.GetAsync("/api/items");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<Item>>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListItems_WithTypeFilter_ReturnsOk()
    {
        var items = new List<Item>
        {
            new() { Id = "item-1", UserId = TestUserId, Name = "Lagavulin 16", Type = ItemType.Whiskey, Status = ItemStatus.Reviewed }
        };

        _factory.CosmosDb.QueryAsync<Item>(
            "items",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Item, bool>>?>())
            .Returns((items, (string?)null));

        var response = await _client.GetAsync("/api/items?type=whiskey");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<Item>>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetItem_ReturnsOk()
    {
        var item = new Item
        {
            Id = "item-get-1",
            UserId = TestUserId,
            Name = "Macallan 18",
            Type = ItemType.Whiskey,
            Status = ItemStatus.Reviewed
        };

        _factory.CosmosDb.GetAsync<Item>("items", "item-get-1", TestUserId)
            .Returns(item);

        var response = await _client.GetAsync("/api/items/item-get-1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Item>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Macallan 18");
    }

    [Fact]
    public async Task GetItem_NotFound_Returns404()
    {
        _factory.CosmosDb.GetAsync<Item>("items", "nonexistent", TestUserId)
            .Returns((Item?)null);

        var response = await _client.GetAsync("/api/items/nonexistent");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateItem_ReturnsOk()
    {
        var existing = new Item
        {
            Id = "item-upd-1",
            UserId = TestUserId,
            Name = "Old Name",
            Type = ItemType.Whiskey,
            Status = ItemStatus.Reviewed
        };

        _factory.CosmosDb.GetAsync<Item>("items", "item-upd-1", TestUserId)
            .Returns(existing);

        _factory.CosmosDb.UpsertAsync("items", Arg.Any<Item>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Item>(1));

        var request = new UpdateItemRequest { Name = "Updated Name", UserRating = 4.5 };
        var response = await _client.PutAsJsonAsync("/api/items/item-upd-1", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Item>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateItem_NotFound_Returns404()
    {
        _factory.CosmosDb.GetAsync<Item>("items", "missing-upd", TestUserId)
            .Returns((Item?)null);

        var request = new UpdateItemRequest { Name = "Won't Work" };
        var response = await _client.PutAsJsonAsync("/api/items/missing-upd", request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteItem_ReturnsNoContent()
    {
        var capture = new Capture
        {
            Id = "cap-1",
            UserId = TestUserId,
            ItemIds = new List<string> { "item-del-1", "item-del-2" },
            Status = CaptureStatus.Completed
        };

        var item = new Item
        {
            Id = "item-del-1",
            UserId = TestUserId,
            Name = "Delete Me",
            Type = ItemType.Whiskey,
            CaptureId = "cap-1",
            Status = ItemStatus.Reviewed
        };

        _factory.CosmosDb.GetAsync<Item>("items", "item-del-1", TestUserId)
            .Returns(item);

        _factory.CosmosDb.GetAsync<Capture>("captures", "cap-1", TestUserId)
            .Returns(capture);

        _factory.CosmosDb.UpsertAsync("captures", Arg.Any<Capture>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Capture>(1));

        var response = await _client.DeleteAsync("/api/items/item-del-1");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteItem_NotFound_ReturnsNoContent()
    {
        _factory.CosmosDb.GetAsync<Item>("items", "no-item", TestUserId)
            .Returns((Item?)null);

        var response = await _client.DeleteAsync("/api/items/no-item");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateWishlistItem_ReturnsCreated()
    {
        _factory.CosmosDb.CreateAsync("items", Arg.Any<Item>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Item>(1));

        var request = new CreateWishlistRequest
        {
            Name = "Yamazaki 12",
            Type = ItemType.Whiskey,
            Brand = "Suntory"
        };

        var response = await _client.PostAsJsonAsync("/api/items/wishlist", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<Item>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Yamazaki 12");
        body.Status.Should().Be(ItemStatus.Wishlist);
    }

    [Fact]
    public async Task CreateWishlistItem_MissingName_Returns400()
    {
        var request = new CreateWishlistRequest
        {
            Name = "",
            Type = ItemType.Whiskey
        };

        var response = await _client.PostAsJsonAsync("/api/items/wishlist", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConvertWishlistItem_ReturnsOk()
    {
        var item = new Item
        {
            Id = "wish-1",
            UserId = TestUserId,
            Name = "Wishlist Whiskey",
            Type = ItemType.Whiskey,
            Status = ItemStatus.Wishlist
        };

        _factory.CosmosDb.GetAsync<Item>("items", "wish-1", TestUserId)
            .Returns(item);

        _factory.CosmosDb.UpsertAsync("items", Arg.Any<Item>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Item>(1));

        var response = await _client.PostAsync("/api/items/wish-1/convert", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Item>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(ItemStatus.Reviewed);
    }

    [Fact]
    public async Task ConvertWishlistItem_NotWishlist_Returns400()
    {
        var item = new Item
        {
            Id = "coll-1",
            UserId = TestUserId,
            Name = "Collection Item",
            Type = ItemType.Whiskey,
            Status = ItemStatus.Reviewed
        };

        _factory.CosmosDb.GetAsync<Item>("items", "coll-1", TestUserId)
            .Returns(item);

        var response = await _client.PostAsync("/api/items/coll-1/convert", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddPhoto_ValidatesOwnership()
    {
        var item = new Item
        {
            Id = "photo-item-1",
            UserId = TestUserId,
            Name = "Photo Item",
            Type = ItemType.Whiskey,
            Status = ItemStatus.Reviewed
        };

        _factory.CosmosDb.GetAsync<Item>("items", "photo-item-1", TestUserId)
            .Returns(item);

        // BlobUrl does NOT contain the userId — should fail ownership validation
        var request = new AddPhotoRequest { BlobUrl = "https://storage.blob.core.windows.net/photos/other-user/2025/01/01/photo.jpg" };
        var response = await _client.PostAsJsonAsync("/api/items/photo-item-1/photos", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddPhoto_ReturnsOk()
    {
        var item = new Item
        {
            Id = "photo-item-2",
            UserId = TestUserId,
            Name = "Photo Item OK",
            Type = ItemType.Whiskey,
            Status = ItemStatus.Reviewed,
            PhotoUrls = new List<string>()
        };

        _factory.CosmosDb.GetAsync<Item>("items", "photo-item-2", TestUserId)
            .Returns(item);

        _factory.CosmosDb.UpsertAsync("items", Arg.Any<Item>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Item>(1));

        // BlobUrl contains the userId — should pass ownership validation
        var request = new AddPhotoRequest { BlobUrl = $"https://storage.blob.core.windows.net/photos/{TestUserId}/2025/01/01/photo.jpg" };
        var response = await _client.PostAsJsonAsync("/api/items/photo-item-2/photos", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Item>();
        body.Should().NotBeNull();
        body!.PhotoUrls.Should().Contain(request.BlobUrl);
    }
}
