using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OidcProviderType
{
    [JsonStringEnumMemberName("entra")]
    Entra,
    [JsonStringEnumMemberName("pocket_id")]
    PocketId,
    [JsonStringEnumMemberName("generic")]
    Generic
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OidcProviderTestStatus
{
    [JsonStringEnumMemberName("unknown")]
    Unknown,
    [JsonStringEnumMemberName("ok")]
    Ok,
    [JsonStringEnumMemberName("failed")]
    Failed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OidcFlowType
{
    [JsonStringEnumMemberName("login")]
    Login,
    [JsonStringEnumMemberName("link")]
    Link
}

public class OidcProvider
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("providerType")]
    public OidcProviderType ProviderType { get; set; } = OidcProviderType.Generic;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("issuerUrl")]
    public string IssuerUrl { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; set; } = ["openid", "profile", "email"];

    [JsonPropertyName("callbackPath")]
    public string CallbackPath { get; set; } = string.Empty;

    [JsonPropertyName("requireVerifiedEmail")]
    public bool RequireVerifiedEmail { get; set; } = true;

    [JsonPropertyName("lastTestedAt")]
    public DateTime? LastTestedAt { get; set; }

    [JsonPropertyName("lastTestStatus")]
    public OidcProviderTestStatus LastTestStatus { get; set; } = OidcProviderTestStatus.Unknown;

    [JsonPropertyName("lastTestMessage")]
    public string? LastTestMessage { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => Id;
}

public class ExternalIdentity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("providerDisplayName")]
    public string ProviderDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("lastLoginAt")]
    public DateTime? LastLoginAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => Id;
}

public class OidcAuthState
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("stateHash")]
    public string StateHash { get; set; } = string.Empty;

    [JsonPropertyName("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("flowType")]
    public OidcFlowType FlowType { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("pkceVerifier")]
    public string PkceVerifier { get; set; } = string.Empty;

    [JsonPropertyName("nonceHash")]
    public string NonceHash { get; set; } = string.Empty;

    [JsonPropertyName("redirectPath")]
    public string RedirectPath { get; set; } = "/";

    [JsonPropertyName("redirectUri")]
    public string RedirectUri { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("consumedAt")]
    public DateTime? ConsumedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => Id;
}

public class OidcAdminProviderInput
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("providerType")]
    public OidcProviderType ProviderType { get; set; } = OidcProviderType.Generic;

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("issuerUrl")]
    [Required]
    public string IssuerUrl { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    [Required]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string? ClientSecret { get; set; }

    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; set; } = [];

    [JsonPropertyName("callbackPath")]
    public string? CallbackPath { get; set; }

    [JsonPropertyName("requireVerifiedEmail")]
    public bool? RequireVerifiedEmail { get; set; }
}

public class OidcAdminProviderDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("providerType")]
    public OidcProviderType ProviderType { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("issuerUrl")]
    public string IssuerUrl { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecretConfigured")]
    public bool ClientSecretConfigured { get; set; }

    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; set; } = [];

    [JsonPropertyName("callbackPath")]
    public string CallbackPath { get; set; } = string.Empty;

    [JsonPropertyName("requireVerifiedEmail")]
    public bool RequireVerifiedEmail { get; set; }

    [JsonPropertyName("lastTestedAt")]
    public DateTime? LastTestedAt { get; set; }

    [JsonPropertyName("lastTestStatus")]
    public OidcProviderTestStatus LastTestStatus { get; set; }

    [JsonPropertyName("lastTestMessage")]
    public string? LastTestMessage { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class OidcPublicProviderDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("providerType")]
    public OidcProviderType ProviderType { get; set; }
}

public class OidcStartLoginRequest
{
    [JsonPropertyName("redirectPath")]
    public string? RedirectPath { get; set; }

    [JsonPropertyName("callbackPath")]
    public string? CallbackPath { get; set; }
}

public class OidcStartLoginResult
{
    [JsonPropertyName("authorizationUrl")]
    public string AuthorizationUrl { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}

public class OidcProviderTestResult
{
    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    [JsonPropertyName("authorizationEndpoint")]
    public string? AuthorizationEndpoint { get; set; }

    [JsonPropertyName("tokenEndpoint")]
    public string? TokenEndpoint { get; set; }
}

public class OidcLinkedIdentityDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("providerDisplayName")]
    public string ProviderDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [JsonPropertyName("subjectPreview")]
    public string SubjectPreview { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("lastLoginAt")]
    public DateTime? LastLoginAt { get; set; }
}

public class OidcLinkCallbackResult
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "OIDC identity linked";

    [JsonPropertyName("identity")]
    public OidcLinkedIdentityDto Identity { get; set; } = new();
}
