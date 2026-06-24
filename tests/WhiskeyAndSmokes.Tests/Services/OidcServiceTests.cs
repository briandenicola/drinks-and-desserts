using System.Linq.Expressions;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Services;

public class OidcServiceTests
{
    [Fact]
    public async Task CreateAdminProvider_DefaultsCallbackAndDoesNotExposeSecret()
    {
        var store = new InMemoryCosmosDbService();
        var sut = CreateService(store);

        var provider = await sut.CreateAdminProviderAsync(new OidcAdminProviderInput
        {
            Name = "Pocket_ID",
            DisplayName = "Pocket ID",
            ProviderType = OidcProviderType.PocketId,
            IssuerUrl = "https://id.example.com/",
            ClientId = "client",
            ClientSecret = "secret",
            Scopes = []
        });

        provider.Name.Should().Be("pocket_id");
        provider.ProviderType.Should().Be(OidcProviderType.PocketId);
        provider.Scopes.Should().ContainInOrder("openid", "profile", "email");
        provider.CallbackPath.Should().Be($"/api/auth/oidc/{provider.Id}/callback");
        provider.ClientSecretConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task StartLogin_WithEnabledProvider_CreatesPkceStateAndAuthorizationUrl()
    {
        var store = new InMemoryCosmosDbService();
        var sut = CreateService(store);
        var provider = await CreateProviderAsync(sut);

        var result = await sut.StartLoginAsync(provider.Id, "/dashboard?tab=home", null, "https://app.example.com");

        result.AuthorizationUrl.Should().StartWith("https://id.example.com/authorize?");
        result.AuthorizationUrl.Should().Contain("response_type=code");
        result.AuthorizationUrl.Should().Contain("code_challenge_method=S256");
        result.AuthorizationUrl.Should().Contain(Uri.EscapeDataString($"/api/auth/oidc/{provider.Id}/callback"));
        var states = await store.QueryCrossPartitionAsync<OidcAuthState>("oidc-auth-states", "SELECT * FROM c");
        states.Should().ContainSingle();
        states[0].PkceVerifier.Should().NotBeNullOrWhiteSpace();
        states[0].NonceHash.Should().HaveLength(64);
        states[0].RedirectPath.Should().Be("/dashboard?tab=home");
    }

    [Fact]
    public async Task StartLogin_WithUnsafeRedirect_ReturnsInvalidRedirect()
    {
        var store = new InMemoryCosmosDbService();
        var sut = CreateService(store);
        var provider = await CreateProviderAsync(sut);

        var act = () => sut.StartLoginAsync(provider.Id, "https://evil.example.com", null, "https://app.example.com");

        await act.Should().ThrowAsync<OidcException>()
            .Where(ex => ex.Error == OidcError.InvalidRedirect);
    }

    [Fact]
    public async Task UnlinkIdentity_WhenLastOidcOnlySignInMethod_ReturnsConflict()
    {
        var store = new InMemoryCosmosDbService();
        var sut = CreateService(store);
        var user = new User { Id = "user-1", Email = "user@example.com", PasswordHash = "" };
        var identity = new ExternalIdentity
        {
            Id = "identity-1",
            UserId = user.Id,
            ProviderId = "provider-1",
            Issuer = "https://id.example.com",
            Subject = "subject"
        };
        await store.CreateAsync("users", user, user.PartitionKey);
        await store.CreateAsync("external-identities", identity, identity.PartitionKey);

        var act = () => sut.UnlinkIdentityAsync(identity.Id, user.Id);

        await act.Should().ThrowAsync<OidcException>()
            .Where(ex => ex.Error == OidcError.NoUsableSignInMethod);
    }

    [Fact]
    public async Task CompleteLinkCallback_WhenAuthenticatedUserDoesNotMatchState_ReturnsInvalidState()
    {
        var store = new InMemoryCosmosDbService();
        var sut = CreateService(store);
        var provider = await CreateProviderAsync(sut);
        const string rawState = "raw-state";
        var stateHash = HashSecret(rawState);
        await store.CreateAsync("oidc-auth-states", new OidcAuthState
        {
            Id = stateHash,
            StateHash = stateHash,
            ProviderId = provider.Id,
            FlowType = OidcFlowType.Link,
            UserId = "attacker-user",
            PkceVerifier = "verifier",
            NonceHash = HashSecret("nonce"),
            RedirectPath = "/profile",
            RedirectUri = "https://app.example.com/profile/oidc/link/callback/" + provider.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        }, stateHash);

        var act = () => sut.CompleteLinkCallbackAsync(provider.Id, "victim-user", "code", rawState, "https://app.example.com");

        await act.Should().ThrowAsync<OidcException>()
            .Where(ex => ex.Error == OidcError.InvalidState);
    }

    [Fact]
    public async Task ListPublicProviders_ReturnsOnlyEnabledSafeFields()
    {
        var store = new InMemoryCosmosDbService();
        var sut = CreateService(store);
        var enabled = await CreateProviderAsync(sut, enabled: true);
        await CreateProviderAsync(sut, name: "disabled", enabled: false);

        var providers = await sut.ListPublicProvidersAsync();

        providers.Should().ContainSingle();
        providers[0].Id.Should().Be(enabled.Id);
        providers[0].DisplayName.Should().Be("Pocket ID");
    }

    private static async Task<OidcAdminProviderDto> CreateProviderAsync(
        OidcService sut,
        string name = "pocket",
        bool enabled = true)
    {
        return await sut.CreateAdminProviderAsync(new OidcAdminProviderInput
        {
            Name = name,
            DisplayName = name == "pocket" ? "Pocket ID" : name,
            ProviderType = OidcProviderType.PocketId,
            Enabled = enabled,
            IssuerUrl = "https://id.example.com",
            ClientId = $"client-{name}",
            ClientSecret = "secret",
            Scopes = ["openid", "profile", "email"]
        });
    }

    private static OidcService CreateService(InMemoryCosmosDbService store)
    {
        var auth = Substitute.For<IAuthService>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("oidc").Returns(new HttpClient(new DiscoveryHandler()));
        return new OidcService(
            store,
            auth,
            httpClientFactory,
            TimeProvider.System,
            NullLogger<OidcService>.Instance);
    }

    private static string HashSecret(string value)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class DiscoveryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(new
            {
                issuer = "https://id.example.com",
                authorization_endpoint = "https://id.example.com/authorize",
                token_endpoint = "https://id.example.com/token",
                jwks_uri = "https://id.example.com/jwks"
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }

    private sealed class InMemoryCosmosDbService : ICosmosDbService
    {
        private readonly Dictionary<string, Dictionary<string, object>> _containers = new();

        public Task<T?> GetAsync<T>(string containerName, string id, string partitionKey)
        {
            return Task.FromResult(_containers.TryGetValue(containerName, out var container) &&
                container.TryGetValue(id, out var item)
                    ? (T)item
                    : default);
        }

        public Task<T> CreateAsync<T>(string containerName, T item, string partitionKey)
        {
            var id = GetId(item);
            Container(containerName).Add(id, item!);
            return Task.FromResult(item);
        }

        public Task<T> UpsertAsync<T>(string containerName, T item, string partitionKey)
        {
            var id = GetId(item);
            Container(containerName)[id] = item!;
            return Task.FromResult(item);
        }

        public Task DeleteAsync(string containerName, string id, string partitionKey)
        {
            if (_containers.TryGetValue(containerName, out var container))
            {
                container.Remove(id);
            }
            return Task.CompletedTask;
        }

        public Task<(List<T> Items, string? ContinuationToken)> QueryAsync<T>(
            string containerName,
            string partitionKey,
            string? continuationToken = null,
            int maxItems = 25,
            Expression<Func<T, bool>>? predicate = null,
            Expression<Func<T, object>>? orderBy = null,
            bool orderDescending = false)
        {
            var items = All<T>(containerName);
            if (predicate != null)
            {
                items = items.AsQueryable().Where(predicate).ToList();
            }
            return Task.FromResult((items.Take(maxItems).ToList(), (string?)null));
        }

        public Task<List<T>> QueryCrossPartitionAsync<T>(string containerName, string query, int maxItems = 100) =>
            Task.FromResult(All<T>(containerName).Take(maxItems).ToList());

        public Task<List<T>> QueryCrossPartitionAsync<T>(string containerName, string query, IDictionary<string, object> parameters, int maxItems = 100) =>
            QueryCrossPartitionAsync<T>(containerName, query, maxItems);

        private Dictionary<string, object> Container(string name)
        {
            if (!_containers.TryGetValue(name, out var container))
            {
                container = new Dictionary<string, object>();
                _containers[name] = container;
            }
            return container;
        }

        private List<T> All<T>(string containerName) =>
            _containers.TryGetValue(containerName, out var container)
                ? container.Values.Cast<T>().ToList()
                : [];

        private static string GetId<T>(T item) =>
            item?.GetType().GetProperty("Id")?.GetValue(item)?.ToString()
            ?? throw new InvalidOperationException("Item must have an Id property.");
    }
}
