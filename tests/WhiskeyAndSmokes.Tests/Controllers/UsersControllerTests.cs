using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class UsersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static string TestUserId => CustomWebApplicationFactory.TestUserId;

    public UsersControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private User CreateTestUser(Action<User>? configure = null)
    {
        var user = new User
        {
            Id = TestUserId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashed-password",
            Role = "user",
            ApiKeys = []
        };
        configure?.Invoke(user);
        return user;
    }

    [Fact]
    public async Task GetMe_ReturnsOk()
    {
        var user = CreateTestUser();
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);

        var response = await _client.GetAsync("/api/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<User>();
        body.Should().NotBeNull();
        body!.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public async Task GetMe_NotFound_Returns404()
    {
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns((User?)null);

        var response = await _client.GetAsync("/api/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateMe_ReturnsOk()
    {
        var user = CreateTestUser();
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);
        _factory.CosmosDb.UpsertAsync("users", Arg.Any<User>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<User>(1));

        var request = new UpdateUserRequest
        {
            DisplayName = "Updated Name",
            Preferences = new UserPreferences
            {
                CollectionSort = "type",
                CollectionSortDirection = "asc",
                VenueSort = "type",
                VenueSortDirection = "asc",
                VenueFilter = "restaurant"
            }
        };
        var response = await _client.PutAsJsonAsync("/api/users/me", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<User>();
        body.Should().NotBeNull();
        body!.DisplayName.Should().Be("Updated Name");
        body.Preferences.CollectionSort.Should().Be("type");
        body.Preferences.CollectionSortDirection.Should().Be("asc");
        body.Preferences.VenueSort.Should().Be("type");
        body.Preferences.VenueSortDirection.Should().Be("asc");
        body.Preferences.VenueFilter.Should().Be("restaurant");
    }

    [Fact]
    public async Task ChangePassword_ReturnsOk()
    {
        var user = CreateTestUser();
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);
        _factory.AuthService.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _factory.AuthService.HashPassword(Arg.Any<string>()).Returns("new-hashed");
        _factory.CosmosDb.UpsertAsync("users", Arg.Any<User>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<User>(1));

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword1",
            NewPassword = "NewPassword1"
        };
        var response = await _client.PutAsJsonAsync("/api/users/me/password", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns400()
    {
        var user = CreateTestUser();
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);
        _factory.AuthService.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword",
            NewPassword = "NewPassword1"
        };
        var response = await _client.PutAsJsonAsync("/api/users/me/password", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_TooShort_Returns400()
    {
        var user = CreateTestUser();
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);
        _factory.AuthService.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword1",
            NewPassword = "short"
        };
        var response = await _client.PutAsJsonAsync("/api/users/me/password", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListApiKeys_ReturnsOk()
    {
        var user = CreateTestUser(u => u.ApiKeys =
        [
            new ApiKey { Id = "key-1", Name = "My Key", Prefix = "ws_test123...", KeyHash = "hash1" },
            new ApiKey { Id = "key-2", Name = "Other Key", Prefix = "ws_other12...", KeyHash = "hash2" }
        ]);
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);

        var response = await _client.GetAsync("/api/users/me/api-keys");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ApiKeyResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateApiKey_ReturnsOk()
    {
        var user = CreateTestUser(u => u.ApiKeys = []);
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);
        _factory.CosmosDb.UpsertAsync("users", Arg.Any<User>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<User>(1));

        var request = new CreateApiKeyRequest { Name = "Test Key" };
        var response = await _client.PostAsJsonAsync("/api/users/me/api-keys", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        body.Should().NotBeNull();
        body!.Key.Should().NotBeNullOrEmpty();
        body.Name.Should().Be("Test Key");
    }

    [Fact]
    public async Task CreateApiKey_MaxKeysReached_Returns400()
    {
        var user = CreateTestUser(u => u.ApiKeys = Enumerable.Range(1, 5)
            .Select(i => new ApiKey { Id = $"key-{i}", Name = $"Key {i}", KeyHash = $"hash{i}" })
            .ToList());
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);

        var request = new CreateApiKeyRequest { Name = "One Too Many" };
        var response = await _client.PostAsJsonAsync("/api/users/me/api-keys", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateApiKey_DefaultsToReadCapabilityOnly()
    {
        var user = CreateTestUser(u => u.ApiKeys = []);
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);
        _factory.CosmosDb.UpsertAsync("users", Arg.Any<User>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<User>(1));

        var request = new CreateApiKeyRequest { Name = "Read Only Key" };
        var response = await _client.PostAsJsonAsync("/api/users/me/api-keys", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        body.Should().NotBeNull();
        body!.Capabilities.Should().BeEquivalentTo([ApiKeyCapability.Read]);
    }

    [Fact]
    public async Task CreateApiKey_RequestingAgentic_GrantsBothCapabilities()
    {
        var user = CreateTestUser(u => u.ApiKeys = []);
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);
        _factory.CosmosDb.UpsertAsync("users", Arg.Any<User>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<User>(1));

        var request = new CreateApiKeyRequest { Name = "Agentic Key", Capabilities = [ApiKeyCapability.Agentic] };
        var response = await _client.PostAsJsonAsync("/api/users/me/api-keys", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        body.Should().NotBeNull();
        body!.Capabilities.Should().BeEquivalentTo([ApiKeyCapability.Read, ApiKeyCapability.Agentic]);
    }

    [Fact]
    public async Task CreateApiKey_InvalidCapability_Returns400()
    {
        var user = CreateTestUser(u => u.ApiKeys = []);
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);

        var request = new CreateApiKeyRequest { Name = "Bad Key", Capabilities = ["admin"] };
        var response = await _client.PostAsJsonAsync("/api/users/me/api-keys", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListApiKeys_LegacyKeyWithoutCapabilities_ShowsReadAndWrite()
    {
        var user = CreateTestUser(u => u.ApiKeys =
        [
            new ApiKey { Id = "legacy-key", Name = "Legacy", Prefix = "ws_legacy...", KeyHash = "hash", Capabilities = [] }
        ]);
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);

        var response = await _client.GetAsync("/api/users/me/api-keys");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ApiKeyResponse>>();
        body.Should().NotBeNull();
        // It could always capture, and keeps that until its access is changed.
        body!.Single(k => k.Id == "legacy-key").Capabilities.Should().Equal(ApiKeyCapability.Read, ApiKeyCapability.Write);
    }

    [Fact]
    public async Task RevokeApiKey_ReturnsOk()
    {
        var user = CreateTestUser(u => u.ApiKeys =
        [
            new ApiKey { Id = "key-to-revoke", Name = "Revokable", KeyHash = "hash" }
        ]);
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);
        _factory.CosmosDb.UpsertAsync("users", Arg.Any<User>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<User>(1));

        var response = await _client.DeleteAsync("/api/users/me/api-keys/key-to-revoke");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // Keys made before write was its own option could always capture, so they keep it.
    [Fact]
    public void OlderKeysKeepWrite_NewKeysGetWhatWasPicked()
    {
        new ApiKey { Capabilities = [ApiKeyCapability.Read] }.Effective().Should().Equal("read", "write");
        new ApiKey { Capabilities = ["read", "agentic"] }.Effective().Should().Equal("read", "write", "agentic");
        new ApiKey { Capabilities = [ApiKeyCapability.Read], AccessSet = true }.Effective().Should().Equal("read");
        new ApiKey { Capabilities = ["agentic", "write"], AccessSet = true }.Effective().Should().Equal("read", "write", "agentic");
    }

    [Fact]
    public async Task UpdateApiKey_ChangesAccessInPlace()
    {
        var user = CreateTestUser(u => u.ApiKeys = [new ApiKey { Id = "key-1", Name = "Shortcut", Prefix = "ws_abc...", KeyHash = "hash1" }]);
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);
        _factory.CosmosDb.UpsertAsync("users", Arg.Any<User>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<User>(1));

        var response = await _client.PatchAsJsonAsync("/api/users/me/api-keys/key-1", new UpdateApiKeyRequest { Capabilities = ["read"] });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiKeyResponse>();
        body!.Capabilities.Should().Equal("read");
        user.ApiKeys[0].AccessSet.Should().BeTrue();
        user.ApiKeys[0].KeyHash.Should().Be("hash1", "the key itself stays the same");

        response = await _client.PatchAsJsonAsync("/api/users/me/api-keys/key-1", new UpdateApiKeyRequest { Capabilities = ["read", "write"] });
        (await response.Content.ReadFromJsonAsync<ApiKeyResponse>())!.Capabilities.Should().Equal("read", "write");

        (await _client.PatchAsJsonAsync("/api/users/me/api-keys/key-1", new UpdateApiKeyRequest { Capabilities = ["admin"] }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.PatchAsJsonAsync("/api/users/me/api-keys/nope", new UpdateApiKeyRequest { Capabilities = ["read"] }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateApiKey_WithWrite()
    {
        var user = CreateTestUser(u => u.ApiKeys = []);
        _factory.CosmosDb.GetAsync<User>("users", TestUserId, TestUserId).Returns(user);
        _factory.CosmosDb.UpsertAsync("users", Arg.Any<User>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<User>(1));

        var response = await _client.PostAsJsonAsync("/api/users/me/api-keys", new CreateApiKeyRequest { Name = "Shortcut", Capabilities = ["read", "write"] });
        (await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>())!.Capabilities.Should().Equal("read", "write");
        user.ApiKeys[0].AccessSet.Should().BeTrue();
    }

    // A capture adds an item: a Read key can't, a Read + write key can.
    [Theory]
    [InlineData("read", HttpStatusCode.Forbidden)]
    [InlineData("read,agentic", HttpStatusCode.Forbidden)]
    [InlineData("read,write", HttpStatusCode.BadRequest)] // past the check; no image sent
    public async Task Capture_NeedsWrite(string capabilities, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/external/capture")
        {
            Content = new MultipartFormDataContent { { new StringContent("a note"), "note" } },
        };
        request.Headers.Add("X-Test-ApiKeyCapabilities", capabilities);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(expected);
        if (expected == HttpStatusCode.Forbidden)
            (await response.Content.ReadAsStringAsync()).Should().Contain("Read + write");
    }
}
