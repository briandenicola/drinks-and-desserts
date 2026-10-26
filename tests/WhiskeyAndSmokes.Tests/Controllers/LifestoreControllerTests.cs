using System.Linq.Expressions;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using NSubstitute.ClearExtensions;
using WhiskeyAndSmokes.Api.Controllers;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class LifestoreControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public LifestoreControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void Feed_is_read_with_an_api_key_only()
    {
        var authorize = typeof(LifestoreController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        authorize.AuthenticationSchemes.Should().Be("ApiKey");
        typeof(LifestoreController).GetMethods()
            .SelectMany(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true))
            .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()
            .SelectMany(a => a.HttpMethods).Should().OnlyContain(m => m == "GET");
    }

    [Fact]
    public async Task Feed_returns_the_owner_records_and_is_not_cached()
    {
        _factory.CosmosDb.ClearSubstitute();
        _factory.CosmosDb.QueryAsync<Item>("items", CustomWebApplicationFactory.TestUserId, Arg.Any<string?>(), Arg.Any<int>(),
                Arg.Any<Expression<Func<Item, bool>>?>(), Arg.Any<Expression<Func<Item, object>>?>(), Arg.Any<bool>())
            .Returns((new List<Item> { new() { Id = "i1", UserId = CustomWebApplicationFactory.TestUserId, Name = "Lagavulin 16", Type = ItemType.Whiskey } }, (string?)null));

        var response = await _client.GetAsync("/api/lifestore/records");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("schema").GetString().Should().Be("drinksanddesserts.lifestore.records/1");
        var record = body.RootElement.GetProperty("records")[0];
        record.GetProperty("kind").GetString().Should().Be("item");
        record.GetProperty("data").GetProperty("name").GetString().Should().Be("Lagavulin 16");
        body.RootElement.GetProperty("next").GetString().Should().NotBeNullOrEmpty("venues follow the items");
    }

    [Fact]
    public async Task Feed_refuses_a_bad_token_with_a_plain_message()
    {
        var response = await _client.GetAsync("/api/lifestore/records?page=nope!");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Start again without a page");
    }
}
