using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Services;

/// <summary>The life store feed against a real LiteDB file, so paging is the storage's own.</summary>
public sealed class LifestoreFeedServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"lifestore-feed-{Guid.NewGuid():N}.db");
    private readonly LiteDbService _db;

    public LifestoreFeedServiceTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LiteDb:Path"] = _path }).Build();
        _db = new LiteDbService(config, NullLogger<LiteDbService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        File.Delete(_path);
    }

    private async Task SeedAsync(int items, int venues, string userId = "owner")
    {
        for (var i = 1; i <= items; i++)
        {
            await _db.UpsertAsync("items", new Item
            {
                Id = $"{userId}-i{i}", UserId = userId, CaptureId = "cap", Type = ItemType.Whiskey, Name = $"Whiskey {i}",
                PhotoUrls = ["https://blob.example/secret.jpg"], AiConfidence = 0.9, ProcessedBy = "agent",
                Venue = new VenueInfo { VenueId = "v1", Name = "The Pub" }, UserRating = 4.5,
            }, userId);
        }
        for (var v = 1; v <= venues; v++)
        {
            await _db.UpsertAsync("venues", new Venue
            {
                Id = $"{userId}-v{v}", UserId = userId, Name = $"Venue {v}", Type = VenueType.Bar, ProcessingError = "internal detail",
            }, userId);
        }
    }

    private async Task<List<LifestoreRecord>> ReadAllAsync(LifestoreFeedService feed, int limit, string userId = "owner")
    {
        var all = new List<LifestoreRecord>();
        string? token = null;
        for (var pages = 0; pages < 50; pages++)
        {
            var page = await feed.PageAsync(userId, token, limit);
            page.Schema.Should().Be(LifestoreFeedService.Schema);
            all.AddRange(page.Records);
            token = page.Next;
            if (token is null)
                return all;
        }
        throw new InvalidOperationException("the feed did not end");
    }

    [Fact]
    public async Task Lists_every_item_then_every_venue_across_pages()
    {
        await SeedAsync(items: 5, venues: 3);
        await SeedAsync(items: 2, venues: 1, userId: "someone-else");

        var records = await ReadAllAsync(new LifestoreFeedService(_db), limit: 2);

        records.Select(r => $"{r.Kind}:{r.Id}").Should().Equal(
            "item:owner-i1", "item:owner-i2", "item:owner-i3", "item:owner-i4", "item:owner-i5",
            "venue:owner-v1", "venue:owner-v2", "venue:owner-v3");
    }

    [Fact]
    public async Task An_empty_collection_still_ends()
    {
        var records = await ReadAllAsync(new LifestoreFeedService(_db), limit: 10);
        records.Should().BeEmpty();
    }

    [Fact]
    public async Task Records_leave_out_photos_capture_links_and_processing_internals()
    {
        await SeedAsync(items: 1, venues: 1);

        var records = await ReadAllAsync(new LifestoreFeedService(_db), limit: 10);
        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("\"name\":\"Whiskey 1\"").And.Contain("\"rating\":4.5").And.Contain("The Pub");
        foreach (var hidden in new[] { "photoUrls", "secret.jpg", "captureId", "aiConfidence", "processedBy", "processingError", "internal detail", "userId" })
            json.Should().NotContain(hidden);
    }

    [Theory]
    [InlineData("not base64 !!")]
    [InlineData("eyJLIjoiY29pbiIsIkMiOm51bGx9")] // {"K":"coin","C":null}
    [InlineData("bm90IGpzb24")] // "not json"
    public async Task Refuses_a_token_it_did_not_give_out(string token)
    {
        var act = () => new LifestoreFeedService(_db).PageAsync("owner", token, 10);
        await act.Should().ThrowAsync<LifestoreTokenException>().WithMessage("*Start again without a page*");
    }

    [Fact]
    public async Task Limit_is_held_to_what_a_page_may_hold()
    {
        await SeedAsync(items: 3, venues: 0);
        (await new LifestoreFeedService(_db).PageAsync("owner", null, 0)).Records.Should().HaveCount(3);
        (await new LifestoreFeedService(_db).PageAsync("owner", null, 100_000)).Records.Should().HaveCount(3);
    }
}
