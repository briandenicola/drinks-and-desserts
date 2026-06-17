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
    [Fact]
    public async Task BuildUserProfile_UsesCustomType_WhenRatedItemTypeIsMissing()
    {
        const string userId = "test-user-id";
        var cosmosDb = Substitute.For<ICosmosDbService>();
        var blobStorage = Substitute.For<IBlobStorageService>();
        var logger = Substitute.For<ILogger<RecommendationService>>();
        var options = Options.Create(new AiFoundryOptions());

        cosmosDb
            .QueryAsync<Item>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<System.Linq.Expressions.Expression<Func<Item, bool>>?>(), Arg.Any<System.Linq.Expressions.Expression<Func<Item, object>>?>(), Arg.Any<bool>())
            .Returns((new List<Item>
            {
                new()
                {
                    Id = "item-1",
                    UserId = userId,
                    Name = "Legacy Item",
                    Type = null!,
                    UserRating = 4.5
                }
            }, null));

        var service = new RecommendationService(cosmosDb, blobStorage, logger, options);

        var profile = await service.BuildUserProfileAsync(userId);

        profile.TotalRatedItems.Should().Be(1);
        profile.ItemTypePreferences.Should().ContainKey(ItemType.Custom);
        profile.TopRatedItems[0].Type.Should().Be(ItemType.Custom);
    }
}
