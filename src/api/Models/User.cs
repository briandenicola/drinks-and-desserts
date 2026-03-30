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

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; } = false;

    [JsonPropertyName("preferences")]
    public UserPreferences Preferences { get; set; } = new();

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
            IsDisabled = IsDisabled,
            Preferences = Preferences,
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
}
