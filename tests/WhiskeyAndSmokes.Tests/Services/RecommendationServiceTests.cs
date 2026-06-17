using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Services;

public class RecommendationServiceTests
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<RecommendationService> _logger;
    private readonly IOptions<AiFoundryOptions> _foundryOptions;
    private readonly RecommendationService _service;

    public RecommendationServiceTests()
    {
        _cosmosDb = Substitute.For<ICosmosDbService>();
        _blobStorage = Substitute.For<IBlobStorageService>();
        _logger = Substitute.For<ILogger<RecommendationService>>();
        
        // Configure foundry options to be unconfigured (so we don't try to call AI)
        _foundryOptions = Substitute.For<IOptions<AiFoundryOptions>>();
        _foundryOptions.Value.Returns(new AiFoundryOptions());
        
        _service = new RecommendationService(_cosmosDb, _blobStorage, _logger, _foundryOptions);
    }

    [Fact]
    public async Task BuildUserProfile_WithNullItemType_NormalizesToCustom()
    {
        // Arrange
        var userId = "test-user";
        var items = new List<Item>
        {
            new()
            {
                Id = "item-1",
                UserId = userId,
                Name = "Legacy Item 1",
                Type = null!, // Null type - the bug scenario
                UserRating = 4.5
            },
            new()
            {
                Id = "item-2",
                UserId = userId,
                Name = "Legacy Item 2",
                Type = "", // Blank type - also should be normalized
                UserRating = 4.0
            },
            new()
            {
                Id = "item-3",
                UserId = userId,
                Name = "Normal Whiskey",
                Type = ItemType.Whiskey,
                UserRating = 5.0
            }
        };

        _cosmosDb.QueryAsync<Item>("items", userId, Arg.Any<string?>())
            .Returns((items, (string?)null));

        // Act - this used to throw ArgumentNullException when Type was null
        var profile = await _service.BuildUserProfileAsync(userId);

        // Assert
        profile.Should().NotBeNull();
        profile.UserId.Should().Be(userId);
        profile.TotalRatedItems.Should().Be(3);
        
        // Verify the null/blank types are grouped under Custom
        profile.ItemTypePreferences.Should().ContainKey(ItemType.Custom);
        profile.ItemTypePreferences[ItemType.Custom].Count.Should().Be(2);
        
        // Verify normal types are preserved
        profile.ItemTypePreferences.Should().ContainKey(ItemType.Whiskey);
        profile.ItemTypePreferences[ItemType.Whiskey].Count.Should().Be(1);
        
        // Verify top rated items have normalized types
        var topRatedCustomItems = profile.TopRatedItems.Where(i => i.Type == ItemType.Custom).ToList();
        topRatedCustomItems.Should().HaveCount(2);
        topRatedCustomItems.Should().Contain(i => i.Name == "Legacy Item 1");
        topRatedCustomItems.Should().Contain(i => i.Name == "Legacy Item 2");
    }

    [Fact]
    public async Task BuildUserProfile_WithOnlyNullTypes_DoesNotThrow()
    {
        // Arrange
        var userId = "test-user";
        var items = new List<Item>
        {
            new()
            {
                Id = "item-1",
                UserId = userId,
                Name = "Legacy Item 1",
                Type = null!, // All null types
                UserRating = 4.5
            },
            new()
            {
                Id = "item-2",
                UserId = userId,
                Name = "Legacy Item 2",
                Type = null!,
                UserRating = 4.0
            }
        };

        _cosmosDb.QueryAsync<Item>("items", userId, Arg.Any<string?>())
            .Returns((items, (string?)null));

        // Act
        var profile = await _service.BuildUserProfileAsync(userId);

        // Assert
        profile.Should().NotBeNull();
        profile.ItemTypePreferences.Should().ContainKey(ItemType.Custom);
        profile.ItemTypePreferences[ItemType.Custom].Count.Should().Be(2);
        profile.ItemTypePreferences[ItemType.Custom].TopRated.Should().Contain("Legacy Item 1");
        profile.ItemTypePreferences[ItemType.Custom].TopRated.Should().Contain("Legacy Item 2");
    }

    [Fact]
    public async Task BuildUserProfile_WithNoRatedItems_ReturnsEmptyProfile()
    {
        // Arrange
        var userId = "test-user";
        var items = new List<Item>
        {
            new()
            {
                Id = "item-1",
                UserId = userId,
                Name = "Unrated Item",
                Type = ItemType.Whiskey,
                UserRating = null // No rating
            }
        };

        _cosmosDb.QueryAsync<Item>("items", userId, Arg.Any<string?>())
            .Returns((items, (string?)null));

        // Act
        var profile = await _service.BuildUserProfileAsync(userId);

        // Assert
        profile.Should().NotBeNull();
        profile.TotalRatedItems.Should().Be(0);
        profile.ItemTypePreferences.Should().BeEmpty();
        profile.TopRatedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildUserProfile_WithMixedTypes_GroupsCorrectly()
    {
        // Arrange
        var userId = "test-user";
        var items = new List<Item>
        {
            new()
            {
                Id = "item-1",
                UserId = userId,
                Name = "Whiskey 1",
                Type = ItemType.Whiskey,
                UserRating = 4.5
            },
            new()
            {
                Id = "item-2",
                UserId = userId,
                Name = "Whiskey 2",
                Type = ItemType.Whiskey,
                UserRating = 4.0
            },
            new()
            {
                Id = "item-3",
                UserId = userId,
                Name = "Cigar 1",
                Type = ItemType.Cigar,
                UserRating = 5.0
            },
            new()
            {
                Id = "item-4",
                UserId = userId,
                Name = "Legacy Item",
                Type = null!,
                UserRating = 3.5
            }
        };

        _cosmosDb.QueryAsync<Item>("items", userId, Arg.Any<string?>())
            .Returns((items, (string?)null));

        // Act
        var profile = await _service.BuildUserProfileAsync(userId);

        // Assert
        profile.Should().NotBeNull();
        profile.TotalRatedItems.Should().Be(4);
        
        profile.ItemTypePreferences.Should().HaveCount(3);
        profile.ItemTypePreferences[ItemType.Whiskey].Count.Should().Be(2);
        profile.ItemTypePreferences[ItemType.Cigar].Count.Should().Be(1);
        profile.ItemTypePreferences[ItemType.Custom].Count.Should().Be(1);
        
        // Verify average ratings per type
        profile.ItemTypePreferences[ItemType.Whiskey].AverageRating.Should().Be(4.25);
        profile.ItemTypePreferences[ItemType.Cigar].AverageRating.Should().Be(5.0);
        profile.ItemTypePreferences[ItemType.Custom].AverageRating.Should().Be(3.5);
    }
}
