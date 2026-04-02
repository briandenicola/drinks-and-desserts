using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

// Capture submission
public class CreateCaptureRequest
{
    [JsonPropertyName("userNote")]
    public string? UserNote { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }

    [JsonPropertyName("photos")]
    public List<string>? Photos { get; set; }
}

public class CaptureResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("processedBy")]
    public string ProcessedBy { get; set; } = string.Empty;

    [JsonPropertyName("photos")]
    public List<string> Photos { get; set; } = [];

    [JsonPropertyName("userNote")]
    public string? UserNote { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }

    [JsonPropertyName("itemIds")]
    public List<string> ItemIds { get; set; } = [];

    [JsonPropertyName("workflowSteps")]
    public List<WorkflowStep> WorkflowSteps { get; set; } = [];

    [JsonPropertyName("processingError")]
    public string? ProcessingError { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

// Photo upload
public class UploadUrlResponse
{
    [JsonPropertyName("uploadUrl")]
    public string UploadUrl { get; set; } = string.Empty;

    [JsonPropertyName("blobUrl")]
    public string BlobUrl { get; set; } = string.Empty;
}

// Item update
public class UpdateItemRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("venue")]
    public VenueInfo? Venue { get; set; }

    [JsonPropertyName("userRating")]
    public double? UserRating { get; set; }

    [JsonPropertyName("userNotes")]
    public string? UserNotes { get; set; }

    [JsonPropertyName("journalEntry")]
    public string? JournalEntry { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

// Wishlist
public class CreateWishlistRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("venueName")]
    public string? VenueName { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

// User update
public class UpdateUserRequest
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("preferences")]
    public UserPreferences? Preferences { get; set; }
}

// Admin
public class UpdateUserRoleRequest
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";
}

public class PagedResponse<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = [];

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

// Auth
public class RegisterRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public class LoginRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("user")]
    public User User { get; set; } = null!;
}

public class ChangePasswordRequest
{
    [JsonPropertyName("currentPassword")]
    public string CurrentPassword { get; set; } = string.Empty;

    [JsonPropertyName("newPassword")]
    public string NewPassword { get; set; } = string.Empty;
}

public class EntraTokenRequest
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;
}

public class EntraConfigResponse
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("authority")]
    public string Authority { get; set; } = string.Empty;

    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; set; } = [];
}

public class AdminResetPasswordRequest
{
    [JsonPropertyName("newPassword")]
    public string NewPassword { get; set; } = string.Empty;
}

public class UpdatePromptRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class CreateApiKeyRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
