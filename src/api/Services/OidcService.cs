using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface IOidcService
{
    Task<List<OidcPublicProviderDto>> ListPublicProvidersAsync();
    Task<OidcStartLoginResult> StartLoginAsync(string providerId, string? redirectPath, string? callbackPath, string requestOrigin);
    Task<AuthResponse> CompleteLoginCallbackAsync(string providerId, string? code, string? state, string requestOrigin);
    Task<OidcStartLoginResult> StartLinkAsync(string providerId, string userId, string? redirectPath, string? callbackPath, string requestOrigin);
    Task<OidcLinkCallbackResult> CompleteLinkCallbackAsync(string providerId, string userId, string? code, string? state, string requestOrigin);
    Task<List<OidcLinkedIdentityDto>> ListLinkedIdentitiesAsync(string userId);
    Task UnlinkIdentityAsync(string identityId, string userId);
    Task<List<OidcAdminProviderDto>> ListAdminProvidersAsync();
    Task<OidcAdminProviderDto> CreateAdminProviderAsync(OidcAdminProviderInput input);
    Task<OidcAdminProviderDto> UpdateAdminProviderAsync(string providerId, OidcAdminProviderInput input);
    Task DeleteAdminProviderAsync(string providerId);
    Task<OidcProviderTestResult> TestAdminProviderAsync(string providerId);
}

public class OidcException : Exception
{
    public OidcError Error { get; }

    public OidcException(OidcError error, string? message = null, Exception? inner = null)
        : base(message ?? error.ToString(), inner)
    {
        Error = error;
    }
}

public enum OidcError
{
    ProviderNotFound,
    ProviderInvalid,
    ProviderDuplicate,
    ProviderInUse,
    ProviderDiscovery,
    ProviderConfiguration,
    ProviderDenied,
    ProviderSecretMissing,
    ProviderDisabled,
    InvalidRedirect,
    InvalidState,
    ValidationFailed,
    CodeExchangeFailed,
    IdentityNotLinked,
    IdentityNotFound,
    IdentityAlreadyLinked,
    AccountConflict,
    NoUsableSignInMethod,
    TokenIssueFailed
}

public class OidcService : IOidcService
{
    private const string ProvidersContainer = "oidc-providers";
    private const string StatesContainer = "oidc-auth-states";
    private const string IdentitiesContainer = "external-identities";
    private const string UsersContainer = "users";
    private static readonly Regex ProviderNamePattern = new("^[a-z0-9][a-z0-9_-]{1,99}$", RegexOptions.Compiled);
    private static readonly TimeSpan AuthStateTtl = TimeSpan.FromMinutes(10);

    private readonly ICosmosDbService _cosmosDb;
    private readonly IAuthService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OidcService> _logger;

    public OidcService(
        ICosmosDbService cosmosDb,
        IAuthService authService,
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        ILogger<OidcService> logger)
    {
        _cosmosDb = cosmosDb;
        _authService = authService;
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<OidcPublicProviderDto>> ListPublicProvidersAsync()
    {
        var providers = await ListProvidersAsync();
        return providers
            .Where(p => p.Enabled)
            .OrderBy(p => p.DisplayName)
            .Select(p => new OidcPublicProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                DisplayName = p.DisplayName,
                ProviderType = p.ProviderType
            })
            .ToList();
    }

    public Task<OidcStartLoginResult> StartLoginAsync(string providerId, string? redirectPath, string? callbackPath, string requestOrigin) =>
        StartFlowAsync(providerId, null, OidcFlowType.Login, redirectPath, callbackPath, requestOrigin);

    public Task<OidcStartLoginResult> StartLinkAsync(string providerId, string userId, string? redirectPath, string? callbackPath, string requestOrigin) =>
        StartFlowAsync(providerId, userId, OidcFlowType.Link, redirectPath, callbackPath, requestOrigin);

    private async Task<OidcStartLoginResult> StartFlowAsync(
        string providerId,
        string? userId,
        OidcFlowType flowType,
        string? redirectPath,
        string? callbackPath,
        string requestOrigin)
    {
        var provider = await EnabledProviderAsync(providerId);
        var normalizedRedirectPath = NormalizeRedirectPath(redirectPath);
        if (!IsSafeRelativeRedirectPath(normalizedRedirectPath))
        {
            throw new OidcException(OidcError.InvalidRedirect);
        }

        var normalizedCallbackPath = NormalizeCallbackPath(callbackPath, FlowCallbackPath(provider, flowType));
        if (!IsSafeRelativeCallbackPath(normalizedCallbackPath))
        {
            throw new OidcException(OidcError.InvalidRedirect);
        }

        var discovery = await DiscoverAsync(provider);
        var redirectUri = AbsoluteUrl(requestOrigin, normalizedCallbackPath);
        var state = SecureRandomUrlToken(32);
        var nonce = SecureRandomUrlToken(32);
        var verifier = SecureRandomUrlToken(64);
        var expiresAt = UtcNow().Add(AuthStateTtl);
        var stateHash = HashSecret(state);

        var authState = new OidcAuthState
        {
            Id = stateHash,
            StateHash = stateHash,
            ProviderId = provider.Id,
            FlowType = flowType,
            UserId = userId,
            PkceVerifier = verifier,
            NonceHash = HashSecret(nonce),
            RedirectPath = normalizedRedirectPath,
            RedirectUri = redirectUri,
            ExpiresAt = expiresAt
        };
        await _cosmosDb.UpsertAsync(StatesContainer, authState, authState.PartitionKey);

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = provider.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = string.Join(' ', provider.Scopes),
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = PkceChallenge(verifier),
            ["code_challenge_method"] = "S256"
        };

        return new OidcStartLoginResult
        {
            AuthorizationUrl = AppendQuery(discovery.AuthorizationEndpoint, query),
            ExpiresAt = expiresAt
        };
    }

    public async Task<AuthResponse> CompleteLoginCallbackAsync(string providerId, string? code, string? state, string requestOrigin)
    {
        var provider = await EnabledProviderAsync(providerId);
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            throw new OidcException(OidcError.InvalidState);
        }

        var consumed = await ConsumeStateAsync(state, provider.Id, OidcFlowType.Login);
        var claims = await ExchangeAndValidateAsync(provider, consumed, code, requestOrigin);
        var email = claims.Email?.Trim().ToLowerInvariant();

        var identity = await FindExternalIdentityAsync(provider.Id, claims.Issuer, claims.Subject);
        if (identity == null)
        {
            if (!string.IsNullOrEmpty(email) && claims.EmailVerified)
            {
                var matchingUser = await _authService.FindByEmailAsync(email);
                if (matchingUser != null)
                {
                    throw new OidcException(OidcError.AccountConflict);
                }
            }

            throw new OidcException(OidcError.IdentityNotLinked);
        }

        var user = await _cosmosDb.GetAsync<User>(UsersContainer, identity.UserId, identity.UserId);
        if (user == null || user.IsDisabled)
        {
            throw new OidcException(OidcError.IdentityNotLinked);
        }

        identity.LastLoginAt = UtcNow();
        identity.UpdatedAt = UtcNow();
        await _cosmosDb.UpsertAsync(IdentitiesContainer, identity, identity.PartitionKey);

        try
        {
            var response = await _authService.GenerateTokenWithRefreshAsync(user);
            response.User = user.Sanitized();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue OIDC app session for user {UserId}", user.Id);
            throw new OidcException(OidcError.TokenIssueFailed, inner: ex);
        }
    }

    public async Task<OidcLinkCallbackResult> CompleteLinkCallbackAsync(string providerId, string userId, string? code, string? state, string requestOrigin)
    {
        var provider = await EnabledProviderAsync(providerId);
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            throw new OidcException(OidcError.InvalidState);
        }

        var consumed = await ConsumeStateAsync(state, provider.Id, OidcFlowType.Link);
        if (string.IsNullOrWhiteSpace(consumed.UserId) ||
            !string.Equals(consumed.UserId, userId, StringComparison.Ordinal))
        {
            throw new OidcException(OidcError.InvalidState);
        }

        var user = await _cosmosDb.GetAsync<User>(UsersContainer, consumed.UserId, consumed.UserId);
        if (user == null)
        {
            throw new OidcException(OidcError.InvalidState);
        }

        var claims = await ExchangeAndValidateAsync(provider, consumed, code, requestOrigin);
        var email = claims.Email?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(email) && claims.EmailVerified)
        {
            var existingEmailUser = await _authService.FindByEmailAsync(email);
            if (existingEmailUser != null && existingEmailUser.Id != user.Id)
            {
                throw new OidcException(OidcError.AccountConflict);
            }
        }

        var existing = await FindExternalIdentityAsync(provider.Id, claims.Issuer, claims.Subject);
        if (existing != null)
        {
            if (existing.UserId != user.Id)
            {
                throw new OidcException(OidcError.IdentityAlreadyLinked);
            }

            existing.ProviderDisplayName = provider.DisplayName;
            return new OidcLinkCallbackResult { Identity = ToLinkedIdentityDto(existing) };
        }

        var identity = new ExternalIdentity
        {
            UserId = user.Id,
            ProviderId = provider.Id,
            ProviderDisplayName = provider.DisplayName,
            Issuer = claims.Issuer,
            Subject = claims.Subject,
            Email = email,
            EmailVerified = claims.EmailVerified,
            DisplayName = claims.Name?.Trim()
        };

        await _cosmosDb.CreateAsync(IdentitiesContainer, identity, identity.PartitionKey);
        return new OidcLinkCallbackResult { Identity = ToLinkedIdentityDto(identity) };
    }

    public async Task<List<OidcLinkedIdentityDto>> ListLinkedIdentitiesAsync(string userId)
    {
        var identities = await ListIdentitiesAsync();
        return identities
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.ProviderDisplayName)
            .Select(ToLinkedIdentityDto)
            .ToList();
    }

    public async Task UnlinkIdentityAsync(string identityId, string userId)
    {
        var identity = (await ListIdentitiesAsync()).FirstOrDefault(i => i.Id == identityId && i.UserId == userId);
        if (identity == null)
        {
            throw new OidcException(OidcError.IdentityNotFound);
        }

        var user = await _cosmosDb.GetAsync<User>(UsersContainer, userId, userId);
        if (user == null)
        {
            throw new OidcException(OidcError.IdentityNotFound);
        }

        var linkedCount = (await ListIdentitiesAsync()).Count(i => i.UserId == userId);
        if (string.IsNullOrWhiteSpace(user.PasswordHash) && linkedCount <= 1)
        {
            throw new OidcException(OidcError.NoUsableSignInMethod);
        }

        await _cosmosDb.DeleteAsync(IdentitiesContainer, identity.Id, identity.PartitionKey);
    }

    public async Task<List<OidcAdminProviderDto>> ListAdminProvidersAsync()
    {
        var providers = await ListProvidersAsync();
        return providers.OrderBy(p => p.DisplayName).Select(ToAdminDto).ToList();
    }

    public async Task<OidcAdminProviderDto> CreateAdminProviderAsync(OidcAdminProviderInput input)
    {
        var provider = ProviderFromInput(input, null);
        if (string.IsNullOrWhiteSpace(input.ClientSecret))
        {
            throw new OidcException(OidcError.ProviderSecretMissing);
        }

        await EnsureProviderUniqueAsync(provider.Name, provider.IssuerUrl, provider.ClientId, null);
        provider.CallbackPath = string.IsNullOrWhiteSpace(provider.CallbackPath)
            ? DefaultCallbackPath(provider.Id)
            : provider.CallbackPath;
        await _cosmosDb.CreateAsync(ProvidersContainer, provider, provider.PartitionKey);
        return ToAdminDto(provider);
    }

    public async Task<OidcAdminProviderDto> UpdateAdminProviderAsync(string providerId, OidcAdminProviderInput input)
    {
        var existing = await GetProviderAsync(providerId);
        var updated = ProviderFromInput(input, existing);
        updated.Id = existing.Id;
        updated.CreatedAt = existing.CreatedAt;
        updated.LastTestedAt = existing.LastTestedAt;
        updated.LastTestStatus = existing.LastTestStatus;
        updated.LastTestMessage = existing.LastTestMessage;
        updated.CallbackPath = string.IsNullOrWhiteSpace(updated.CallbackPath)
            ? DefaultCallbackPath(existing.Id)
            : updated.CallbackPath;

        if (IsRedactedSecretValue(input.ClientSecret))
        {
            updated.ClientSecret = existing.ClientSecret;
        }

        await EnsureProviderUniqueAsync(updated.Name, updated.IssuerUrl, updated.ClientId, existing.Id);
        await _cosmosDb.UpsertAsync(ProvidersContainer, updated, updated.PartitionKey);
        return ToAdminDto(updated);
    }

    public async Task DeleteAdminProviderAsync(string providerId)
    {
        var provider = await GetProviderAsync(providerId);
        var linked = (await ListIdentitiesAsync()).Any(i => i.ProviderId == provider.Id);
        if (linked)
        {
            throw new OidcException(OidcError.ProviderInUse);
        }

        await _cosmosDb.DeleteAsync(ProvidersContainer, provider.Id, provider.PartitionKey);
    }

    public async Task<OidcProviderTestResult> TestAdminProviderAsync(string providerId)
    {
        var provider = await GetProviderAsync(providerId);
        var result = await TestProviderDiscoveryAsync(provider);
        provider.LastTestedAt = UtcNow();
        provider.LastTestStatus = result.Available ? OidcProviderTestStatus.Ok : OidcProviderTestStatus.Failed;
        provider.LastTestMessage = result.Message;
        provider.UpdatedAt = UtcNow();
        await _cosmosDb.UpsertAsync(ProvidersContainer, provider, provider.PartitionKey);

        if (!result.Available)
        {
            throw new OidcException(OidcError.ProviderDiscovery, result.Message);
        }

        return result;
    }

    private async Task<OidcAuthState> ConsumeStateAsync(string state, string providerId, OidcFlowType expectedFlow)
    {
        var stateHash = HashSecret(state);
        var authState = await _cosmosDb.GetAsync<OidcAuthState>(StatesContainer, stateHash, stateHash);
        if (authState == null ||
            authState.ProviderId != providerId ||
            authState.FlowType != expectedFlow ||
            authState.ConsumedAt != null ||
            authState.ExpiresAt <= UtcNow())
        {
            throw new OidcException(OidcError.InvalidState);
        }

        authState.ConsumedAt = UtcNow();
        await _cosmosDb.UpsertAsync(StatesContainer, authState, authState.PartitionKey);
        return authState;
    }

    private async Task<OidcClaims> ExchangeAndValidateAsync(OidcProvider provider, OidcAuthState state, string code, string requestOrigin)
    {
        var discovery = await DiscoverAsync(provider);
        var redirectUri = string.IsNullOrWhiteSpace(state.RedirectUri)
            ? AbsoluteUrl(requestOrigin, FlowCallbackPath(provider, state.FlowType))
            : state.RedirectUri;

        using var request = new HttpRequestMessage(HttpMethod.Post, discovery.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = provider.ClientId,
                ["client_secret"] = provider.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = state.PkceVerifier
            })
        };

        var client = _httpClientFactory.CreateClient("oidc");
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("OIDC token exchange failed for provider {ProviderId}: {Status} {Body}",
                provider.Id, response.StatusCode, body);
            throw new OidcException(OidcError.CodeExchangeFailed, TokenExchangeDetail(body, response.StatusCode));
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<OidcTokenResponse>();
        if (string.IsNullOrWhiteSpace(tokenResponse?.IdToken))
        {
            throw new OidcException(OidcError.ValidationFailed);
        }

        var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{provider.IssuerUrl.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());
        var configuration = await configurationManager.GetConfigurationAsync(CancellationToken.None);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration.Issuer,
            ValidateAudience = true,
            ValidAudience = provider.ClientId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(tokenResponse.IdToken, validationParameters, out _);
            var claims = OidcClaims.FromPrincipal(principal, configuration.Issuer);
            if (string.IsNullOrWhiteSpace(claims.Subject) || HashSecret(claims.Nonce ?? "") != state.NonceHash)
            {
                throw new OidcException(OidcError.ValidationFailed);
            }

            if (provider.RequireVerifiedEmail && (string.IsNullOrWhiteSpace(claims.Email) || !claims.EmailVerified))
            {
                throw new OidcException(OidcError.ValidationFailed);
            }

            claims.Email = claims.Email?.Trim().ToLowerInvariant();
            return claims;
        }
        catch (SecurityTokenException ex)
        {
            throw new OidcException(OidcError.ValidationFailed, inner: ex);
        }
    }

    private async Task<OidcDiscoveryDocument> DiscoverAsync(OidcProvider provider)
    {
        ValidateProviderForRuntime(provider);
        var client = _httpClientFactory.CreateClient("oidc");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var metadataUrl = $"{provider.IssuerUrl.TrimEnd('/')}/.well-known/openid-configuration";
        var metadata = await client.GetFromJsonAsync<OidcDiscoveryDocument>(metadataUrl, cts.Token);
        if (metadata == null ||
            string.IsNullOrWhiteSpace(metadata.Issuer) ||
            string.IsNullOrWhiteSpace(metadata.AuthorizationEndpoint) ||
            string.IsNullOrWhiteSpace(metadata.TokenEndpoint) ||
            string.IsNullOrWhiteSpace(metadata.JwksUri) ||
            metadata.AuthorizationEndpoint == metadata.TokenEndpoint)
        {
            throw new OidcException(OidcError.ProviderConfiguration, "OIDC provider discovery failed");
        }

        return metadata;
    }

    private async Task<OidcProviderTestResult> TestProviderDiscoveryAsync(OidcProvider provider)
    {
        try
        {
            var metadata = await DiscoverAsync(provider);
            return new OidcProviderTestResult
            {
                Available = true,
                Message = "Discovery succeeded",
                Issuer = metadata.Issuer,
                AuthorizationEndpoint = metadata.AuthorizationEndpoint,
                TokenEndpoint = metadata.TokenEndpoint
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OIDC discovery failed for provider {ProviderId}", provider.Id);
            return new OidcProviderTestResult { Available = false, Message = "Discovery failed" };
        }
    }

    private async Task<OidcProvider> EnabledProviderAsync(string providerId)
    {
        var provider = await GetProviderAsync(providerId);
        if (!provider.Enabled)
        {
            throw new OidcException(OidcError.ProviderDisabled);
        }

        return provider;
    }

    private async Task<OidcProvider> GetProviderAsync(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new OidcException(OidcError.ProviderNotFound);
        }

        var provider = await _cosmosDb.GetAsync<OidcProvider>(ProvidersContainer, providerId, providerId);
        if (provider == null)
        {
            throw new OidcException(OidcError.ProviderNotFound);
        }

        return provider;
    }

    private async Task<List<OidcProvider>> ListProvidersAsync() =>
        await _cosmosDb.QueryCrossPartitionAsync<OidcProvider>(ProvidersContainer, "SELECT * FROM c", maxItems: 1000);

    private async Task<List<ExternalIdentity>> ListIdentitiesAsync() =>
        await _cosmosDb.QueryCrossPartitionAsync<ExternalIdentity>(IdentitiesContainer, "SELECT * FROM c", maxItems: 1000);

    private async Task<ExternalIdentity?> FindExternalIdentityAsync(string providerId, string issuer, string subject)
    {
        var identities = await ListIdentitiesAsync();
        return identities.FirstOrDefault(i => i.ProviderId == providerId && i.Issuer == issuer && i.Subject == subject);
    }

    private async Task EnsureProviderUniqueAsync(string name, string issuerUrl, string clientId, string? currentId)
    {
        var providers = await ListProvidersAsync();
        if (providers.Any(p =>
                p.Id != currentId &&
                (p.Name == name || (p.IssuerUrl == issuerUrl && p.ClientId == clientId))))
        {
            throw new OidcException(OidcError.ProviderDuplicate);
        }
    }

    private OidcProvider ProviderFromInput(OidcAdminProviderInput input, OidcProvider? existing)
    {
        var provider = existing == null
            ? new OidcProvider { Enabled = false, RequireVerifiedEmail = true }
            : new OidcProvider
            {
                Id = existing.Id,
                CreatedAt = existing.CreatedAt,
                ClientSecret = existing.ClientSecret,
                Enabled = existing.Enabled,
                RequireVerifiedEmail = existing.RequireVerifiedEmail
            };

        provider.Name = input.Name.Trim().ToLowerInvariant();
        provider.DisplayName = input.DisplayName.Trim();
        provider.ProviderType = input.ProviderType;
        provider.Enabled = input.Enabled ?? provider.Enabled;
        provider.IssuerUrl = input.IssuerUrl.Trim().TrimEnd('/');
        provider.ClientId = input.ClientId.Trim();
        if (!string.IsNullOrWhiteSpace(input.ClientSecret))
        {
            provider.ClientSecret = input.ClientSecret.Trim();
        }
        provider.Scopes = NormalizeScopes(input.Scopes);
        provider.CallbackPath = input.CallbackPath?.Trim() ?? existing?.CallbackPath ?? string.Empty;
        provider.RequireVerifiedEmail = input.RequireVerifiedEmail ?? provider.RequireVerifiedEmail;
        provider.UpdatedAt = UtcNow();
        ValidateProvider(provider);
        return provider;
    }

    private static void ValidateProviderForRuntime(OidcProvider provider)
    {
        ValidateProvider(provider);
        if (string.IsNullOrWhiteSpace(provider.ClientSecret))
        {
            throw new OidcException(OidcError.ProviderSecretMissing);
        }
        if (string.IsNullOrWhiteSpace(provider.CallbackPath))
        {
            throw new OidcException(OidcError.ProviderInvalid);
        }
    }

    private static void ValidateProvider(OidcProvider provider)
    {
        if (!ProviderNamePattern.IsMatch(provider.Name) ||
            string.IsNullOrWhiteSpace(provider.DisplayName) ||
            provider.DisplayName.Length > 150 ||
            !Enum.IsDefined(provider.ProviderType) ||
            string.IsNullOrWhiteSpace(provider.ClientId) ||
            provider.Scopes.Count == 0 ||
            !provider.Scopes.Contains("openid", StringComparer.Ordinal))
        {
            throw new OidcException(OidcError.ProviderInvalid);
        }

        ValidateIssuerUrl(provider.IssuerUrl);
        if (!string.IsNullOrWhiteSpace(provider.CallbackPath) && !IsSafeRelativeCallbackPath(provider.CallbackPath))
        {
            throw new OidcException(OidcError.ProviderInvalid);
        }
    }

    private static void ValidateIssuerUrl(string rawUrl)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && IsLocalhost(uri.Host))))
        {
            throw new OidcException(OidcError.ProviderInvalid);
        }
    }

    private static bool IsLocalhost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);

    private static List<string> NormalizeScopes(IEnumerable<string>? scopes)
    {
        var normalized = (scopes ?? [])
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!normalized.Contains("openid", StringComparer.Ordinal))
        {
            normalized.Insert(0, "openid");
        }
        if (normalized.Count == 1)
        {
            normalized.AddRange(["profile", "email"]);
        }
        return normalized;
    }

    private static bool IsRedactedSecretValue(string? secret)
    {
        var normalized = secret?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ||
            normalized is "configured" or "redacted" or "<redacted>" or "********";
    }

    private static bool IsSafeRelativeCallbackPath(string path)
    {
        if (!path.StartsWith('/') || path.StartsWith("//") || path.Contains('\\') || path.Contains('?') || path.Contains('#'))
        {
            return false;
        }
        return Uri.TryCreate(path, UriKind.Relative, out _);
    }

    private static bool IsSafeRelativeRedirectPath(string path)
    {
        if (!path.StartsWith('/') || path.StartsWith("//") || path.Contains('\\') || path.Contains('#'))
        {
            return false;
        }
        return Uri.TryCreate(path, UriKind.Relative, out _);
    }

    private static string NormalizeRedirectPath(string? path) => string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
    private static string NormalizeCallbackPath(string? path, string fallback) => string.IsNullOrWhiteSpace(path) ? fallback : path.Trim();
    private static string DefaultCallbackPath(string providerId) => $"/api/auth/oidc/{providerId}/callback";
    private static string DefaultLinkCallbackPath(string providerId) => $"/api/auth/oidc/{providerId}/link/callback";
    private static string FlowCallbackPath(OidcProvider provider, OidcFlowType flowType) =>
        flowType == OidcFlowType.Link ? DefaultLinkCallbackPath(provider.Id) : provider.CallbackPath;

    private static string AbsoluteUrl(string origin, string path) => origin.TrimEnd('/') + path;

    private static string SecureRandomUrlToken(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Base64UrlEncoder.Encode(bytes);
    }

    private static string HashSecret(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string PkceChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncoder.Encode(hash);
    }

    private static string AppendQuery(string url, Dictionary<string, string?> values)
    {
        var builder = new StringBuilder(url);
        builder.Append(url.Contains('?') ? '&' : '?');
        builder.Append(string.Join('&', values
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")));
        return builder.ToString();
    }

    private static OidcAdminProviderDto ToAdminDto(OidcProvider provider) => new()
    {
        Id = provider.Id,
        Name = provider.Name,
        DisplayName = provider.DisplayName,
        ProviderType = provider.ProviderType,
        Enabled = provider.Enabled,
        IssuerUrl = provider.IssuerUrl,
        ClientId = provider.ClientId,
        ClientSecretConfigured = !string.IsNullOrWhiteSpace(provider.ClientSecret),
        Scopes = provider.Scopes,
        CallbackPath = provider.CallbackPath,
        RequireVerifiedEmail = provider.RequireVerifiedEmail,
        LastTestedAt = provider.LastTestedAt,
        LastTestStatus = provider.LastTestStatus,
        LastTestMessage = provider.LastTestMessage,
        CreatedAt = provider.CreatedAt,
        UpdatedAt = provider.UpdatedAt
    };

    private static OidcLinkedIdentityDto ToLinkedIdentityDto(ExternalIdentity identity) => new()
    {
        Id = identity.Id,
        ProviderId = identity.ProviderId,
        ProviderDisplayName = identity.ProviderDisplayName,
        Issuer = identity.Issuer,
        SubjectPreview = SubjectPreview(identity.Subject),
        Email = identity.Email,
        EmailVerified = identity.EmailVerified,
        CreatedAt = identity.CreatedAt,
        LastLoginAt = identity.LastLoginAt
    };

    private static string SubjectPreview(string subject) =>
        string.IsNullOrWhiteSpace(subject) || subject.Length <= 8 ? subject : subject[..8] + "...";

    private static string TokenExchangeDetail(string body, HttpStatusCode statusCode)
    {
        var raw = body.ToLowerInvariant();
        return raw switch
        {
            var s when s.Contains("aadsts7000215") || s.Contains("invalid client secret") =>
                "provider rejected the client secret; for Entra, paste the client secret Value, not the Secret ID",
            var s when s.Contains("redirect_uri") || s.Contains("reply url") || s.Contains("aadsts50011") =>
                "provider rejected the redirect URI; confirm the exact callback URL is registered",
            var s when s.Contains("expired") || s.Contains("already redeemed") || s.Contains("invalid_grant") =>
                "provider rejected the authorization code as expired, reused, or invalid",
            _ => $"provider token endpoint returned {(int)statusCode}"
        };
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private sealed class OidcDiscoveryDocument
    {
        [JsonPropertyName("issuer")]
        public string Issuer { get; set; } = string.Empty;

        [JsonPropertyName("authorization_endpoint")]
        public string AuthorizationEndpoint { get; set; } = string.Empty;

        [JsonPropertyName("token_endpoint")]
        public string TokenEndpoint { get; set; } = string.Empty;

        [JsonPropertyName("jwks_uri")]
        public string JwksUri { get; set; } = string.Empty;
    }

    private sealed class OidcTokenResponse
    {
        [JsonPropertyName("id_token")]
        public string IdToken { get; set; } = string.Empty;
    }

    private sealed class OidcClaims
    {
        public string Issuer { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Nonce { get; set; }
        public string? Email { get; set; }
        public bool EmailVerified { get; set; }
        public string? Name { get; set; }

        public static OidcClaims FromPrincipal(ClaimsPrincipal principal, string issuer)
        {
            var emailVerified = principal.FindFirst("email_verified")?.Value;
            return new OidcClaims
            {
                Issuer = issuer,
                Subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                Nonce = principal.FindFirstValue("nonce"),
                Email = principal.FindFirstValue(JwtRegisteredClaimNames.Email) ?? principal.FindFirstValue(ClaimTypes.Email),
                EmailVerified = string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase),
                Name = principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name)
            };
        }
    }
}
