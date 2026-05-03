using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class User
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("authProvider")]
    public string AuthProvider { get; set; } = "local";

    [JsonPropertyName("entraObjectId")]
    public string? EntraObjectId { get; set; }

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; } = false;

    [JsonPropertyName("preferences")]
    public UserPreferences Preferences { get; set; } = new();

    [JsonPropertyName("apiKeys")]
    public List<ApiKey> ApiKeys { get; set; } = [];

    [JsonPropertyName("refreshTokens")]
    public List<RefreshToken> RefreshTokens { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => Id;

    public User Sanitized()
    {
        return new User
        {
            Id = Id,
            DisplayName = DisplayName,
            Email = Email,
            PasswordHash = "",
            Role = Role,
            AuthProvider = AuthProvider,
            IsDisabled = IsDisabled,
            Preferences = new UserPreferences
            {
                FavoriteTypes = Preferences.FavoriteTypes,
                DefaultView = Preferences.DefaultView,
                CollectionSort = Preferences.CollectionSort,
                CollectionFilter = Preferences.CollectionFilter,
                PushoverAppToken = !string.IsNullOrEmpty(Preferences.PushoverAppToken) ? "****" : null,
                PushoverUserKey = !string.IsNullOrEmpty(Preferences.PushoverUserKey) ? "****" : null,
                PushoverEnabled = Preferences.PushoverEnabled,
                PushoverSound = Preferences.PushoverSound,
            },
            ApiKeys = [],
            RefreshTokens = [],
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    public User SanitizedForOwner()
    {
        return new User
        {
            Id = Id,
            DisplayName = DisplayName,
            Email = Email,
            PasswordHash = "",
            Role = Role,
            AuthProvider = AuthProvider,
            IsDisabled = IsDisabled,
            Preferences = Preferences,
            ApiKeys = [],
            RefreshTokens = [],
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}

public class UserPreferences
{
    [JsonPropertyName("favoriteTypes")]
    public List<string> FavoriteTypes { get; set; } = [];

    [JsonPropertyName("defaultView")]
    public string DefaultView { get; set; } = "recent";

    [JsonPropertyName("collectionSort")]
    public string CollectionSort { get; set; } = "rating";

    [JsonPropertyName("collectionFilter")]
    public string? CollectionFilter { get; set; }

    [JsonPropertyName("pushoverAppToken")]
    public string? PushoverAppToken { get; set; }

    [JsonPropertyName("pushoverUserKey")]
    public string? PushoverUserKey { get; set; }

    [JsonPropertyName("pushoverEnabled")]
    public bool PushoverEnabled { get; set; }

    [JsonPropertyName("pushoverSound")]
    public bool PushoverSound { get; set; } = true;
}

public class ApiKey
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;

    [JsonPropertyName("keyHash")]
    public string KeyHash { get; set; } = string.Empty;

    [JsonPropertyName("isRevoked")]
    public bool IsRevoked { get; set; } = false;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }
}

public class ApiKeyResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }

    [JsonPropertyName("isRevoked")]
    public bool IsRevoked { get; set; }
}

public class CreateApiKeyResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class RefreshToken
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}