using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Services;

public class AiModelSettingsServiceTests
{
    private static AiModelSettingsService Create(ICosmosDbService cosmos, FakeTimeProvider time, string vision = "", string reasoning = "")
    {
        var options = Options.Create(new AiFoundryOptions { Models = new AiFoundryModelOptions { Vision = vision, Reasoning = reasoning } });
        return new AiModelSettingsService(cosmos, options, time, NullLogger<AiModelSettingsService>.Instance);
    }

    [Fact]
    public void Resolve_AdminValuesWinPerField_BlankDefersToFallback()
    {
        var fallback = new AiModelSelection("env-vision", "env-reasoning", AiModelSelection.SourceEnvironment);

        var resolved = AiModelSettingsService.Resolve(new AppAiSettings { VisionModel = " admin-vision ", ReasoningModel = "   " }, fallback);

        resolved.VisionModel.Should().Be("admin-vision");
        resolved.ReasoningModel.Should().Be("env-reasoning");
        resolved.Source.Should().Be(AiModelSelection.SourceAdmin);
        resolved.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void Resolve_NothingAnywhere_IsNotConfigured()
    {
        var resolved = AiModelSettingsService.Resolve(null, AiModelSelection.Empty);

        resolved.IsConfigured.Should().BeFalse();
        resolved.Source.Should().Be(AiModelSelection.SourceNone);
    }

    [Fact]
    public void Fallback_ComesFromOptionsWithNoBuiltInDefaults()
    {
        var cosmos = Substitute.For<ICosmosDbService>();

        Create(cosmos, new FakeTimeProvider()).Fallback.Should().Be(AiModelSelection.Empty);
        Create(cosmos, new FakeTimeProvider(), "v", "r").Fallback.Should().Be(new AiModelSelection("v", "r", AiModelSelection.SourceEnvironment));
    }

    [Fact]
    public async Task GetEffective_CachesStoreReadsAndRefreshesAfterTtl()
    {
        var cosmos = Substitute.For<ICosmosDbService>();
        cosmos.GetAsync<AppAiSettingsDocument>("settings", AppAiSettingsDocument.DocumentId, AppAiSettingsDocument.PartitionKeyValue)
            .Returns(new AppAiSettingsDocument { Settings = new AppAiSettings { VisionModel = "stored-vision", ReasoningModel = "stored-reasoning" } });
        var time = new FakeTimeProvider();
        var service = Create(cosmos, time, "env-vision", "env-reasoning");

        var first = await service.GetEffectiveAsync();
        var second = await service.GetEffectiveAsync();
        time.Advance(AiModelSettingsService.CacheTtl + TimeSpan.FromSeconds(1));
        var third = await service.GetEffectiveAsync();

        first.VisionModel.Should().Be("stored-vision");
        second.Should().Be(first);
        third.Should().Be(first);
        await cosmos.Received(2).GetAsync<AppAiSettingsDocument>("settings", AppAiSettingsDocument.DocumentId, AppAiSettingsDocument.PartitionKeyValue);
    }

    [Fact]
    public async Task GetEffective_WhenStoreFails_KeepsLastKnownValues()
    {
        var cosmos = Substitute.For<ICosmosDbService>();
        cosmos.GetAsync<AppAiSettingsDocument>("settings", AppAiSettingsDocument.DocumentId, AppAiSettingsDocument.PartitionKeyValue)
            .Returns(
                _ => new AppAiSettingsDocument { Settings = new AppAiSettings { VisionModel = "stored-vision" } },
                _ => throw new InvalidOperationException("cosmos down"));
        var time = new FakeTimeProvider();
        var service = Create(cosmos, time, "env-vision", "env-reasoning");

        var before = await service.GetEffectiveAsync();
        time.Advance(AiModelSettingsService.CacheTtl + TimeSpan.FromSeconds(1));
        var after = await service.GetEffectiveAsync();

        before.VisionModel.Should().Be("stored-vision");
        after.VisionModel.Should().Be("stored-vision");
        after.ReasoningModel.Should().Be("env-reasoning");
    }

    [Fact]
    public async Task Save_NormalizesAndTakesEffectImmediately()
    {
        var cosmos = Substitute.For<ICosmosDbService>();
        cosmos.GetAsync<AppAiSettingsDocument>("settings", AppAiSettingsDocument.DocumentId, AppAiSettingsDocument.PartitionKeyValue)
            .Returns((AppAiSettingsDocument?)null);
        cosmos.UpsertAsync("settings", Arg.Any<AppAiSettingsDocument>(), AppAiSettingsDocument.PartitionKeyValue)
            .Returns(call => call.ArgAt<AppAiSettingsDocument>(1));
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
        var service = Create(cosmos, time, "env-vision", "env-reasoning");

        (await service.GetEffectiveAsync()).Source.Should().Be(AiModelSelection.SourceEnvironment);

        var saved = await service.SaveAsync(new AppAiSettings { VisionModel = " new-vision ", ReasoningModel = "" }, "admin-1");
        var effective = await service.GetEffectiveAsync();

        saved.Settings.VisionModel.Should().Be("new-vision");
        saved.Settings.ReasoningModel.Should().BeNull();
        saved.UpdatedBy.Should().Be("admin-1");
        saved.UpdatedAt.Should().Be(time.GetUtcNow().UtcDateTime);
        effective.VisionModel.Should().Be("new-vision");
        effective.ReasoningModel.Should().Be("env-reasoning");
        effective.Source.Should().Be(AiModelSelection.SourceAdmin);
        // The save primes the cache, so no extra store read is needed.
        await cosmos.Received(1).GetAsync<AppAiSettingsDocument>("settings", AppAiSettingsDocument.DocumentId, AppAiSettingsDocument.PartitionKeyValue);
    }

    [Theory]
    [InlineData("gpt-5.1", "gpt-5.1")]
    [InlineData("  my_deploy-01  ", "my_deploy-01")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void ModelName_AcceptsDeploymentNamesAndBlank(string? input, string? expected)
    {
        AiModelName.TryNormalize(input, out var normalized).Should().BeNull();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("gpt 4o")]
    [InlineData("-leading-dash")]
    [InlineData("has/slash")]
    [InlineData("semi;colon")]
    public void ModelName_RejectsImplausibleNames(string input)
    {
        AiModelName.TryNormalize(input, out _).Should().NotBeNull();
    }

    [Fact]
    public void ModelName_RejectsOverlongNames()
    {
        AiModelName.TryNormalize(new string('a', AiModelName.MaxLength + 1), out _).Should().NotBeNull();
    }

    [Fact]
    public void Catalog_EmbedsInstructionsForEveryAgent()
    {
        foreach (var spec in FoundryAgentCatalog.Agents)
        {
            FoundryAgentCatalog.LoadInstructions(spec.PromptFile).Should().NotBeNullOrWhiteSpace(spec.Name);
        }
        FoundryAgentCatalog.Agents.Should().ContainSingle(a => a.Role == AgentModelRole.Vision);
    }
}
