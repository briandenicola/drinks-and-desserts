using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

// Capture submission
public class CreateCaptureRequest
{
    [JsonPropertyName("userNote")]
    [StringLength(1000)]
    public string? UserNote { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }

    [JsonPropertyName("photos")]
    [Required]
    [MinLength(1)]
    [MaxLength(10)]
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
    [StringLength(200)]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    [StringLength(50)]
    public string? Type { get; set; }

    [JsonPropertyName("brand")]
    [StringLength(200)]
    public string? Brand { get; set; }

    [JsonPropertyName("category")]
    [StringLength(100)]
    public string? Category { get; set; }

    [JsonPropertyName("venue")]
    public VenueInfo? Venue { get; set; }

    [JsonPropertyName("userRating")]
    [Range(0, 5)]
    public double? UserRating { get; set; }

    [JsonPropertyName("userNotes")]
    [StringLength(2000)]
    public string? UserNotes { get; set; }

    [JsonPropertyName("journalEntry")]
    [StringLength(2000)]
    public string? JournalEntry { get; set; }

    [JsonPropertyName("tags")]
    [MaxLength(20)]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("status")]
    [StringLength(50)]
    public string? Status { get; set; }
}

// Wishlist
public class CreateWishlistRequest
{
    [JsonPropertyName("name")]
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("brand")]
    [StringLength(200)]
    public string? Brand { get; set; }

    [JsonPropertyName("notes")]
    [StringLength(2000)]
    public string? Notes { get; set; }

    [JsonPropertyName("venueName")]
    [StringLength(200)]
    public string? VenueName { get; set; }

    [JsonPropertyName("tags")]
    [MaxLength(20)]
    public List<string>? Tags { get; set; }
}

public class CreateWishlistFromUrlRequest
{
    [JsonPropertyName("url")]
    [Required]
    [StringLength(2048)]
    [Url]
    public string Url { get; set; } = string.Empty;
}

// User update
public class UpdateUserRequest
{
    [JsonPropertyName("displayName")]
    [StringLength(100)]
    public string? DisplayName { get; set; }

    [JsonPropertyName("preferences")]
    public UserPreferences? Preferences { get; set; }
}

// Admin
public class UpdateUserRoleRequest
{
    [JsonPropertyName("role")]
    [Required]
    [RegularExpression("^(user|admin)$", ErrorMessage = "Role must be 'user' or 'admin'")]
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
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    [Required]
    [StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}

public class LoginRequest
{
    [JsonPropertyName("email")]
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("user")]
    public User User { get; set; } = null!;
}

public class ChangePasswordRequest
{
    [JsonPropertyName("currentPassword")]
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [JsonPropertyName("newPassword")]
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class EntraTokenRequest
{
    [JsonPropertyName("accessToken")]
    [Required]
    public string AccessToken { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [JsonPropertyName("accessToken")]
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }
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
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class UpdatePromptRequest
{
    [JsonPropertyName("content")]
    [Required]
    [StringLength(10000)]
    public string Content { get; set; } = string.Empty;
}

public class CreateApiKeyRequest
{
    [JsonPropertyName("name")]
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class AddPhotoRequest
{
    [JsonPropertyName("blobUrl")]
    [Required]
    [StringLength(2048)]
    public string BlobUrl { get; set; } = string.Empty;
}

public class RemovePhotoRequest
{
    [JsonPropertyName("blobUrl")]
    [Required]
    [StringLength(2048)]
    public string BlobUrl { get; set; } = string.Empty;
}

// Venue
public class CreateVenueRequest
{
    [JsonPropertyName("name")]
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    [StringLength(500)]
    public string? Address { get; set; }

    [JsonPropertyName("website")]
    [StringLength(2048)]
    [Url]
    public string? Website { get; set; }

    [JsonPropertyName("type")]
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = VenueType.Restaurant;

    [JsonPropertyName("rating")]
    [Range(0, 5)]
    public double? Rating { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }
}

public class UpdateVenueRequest
{
    [JsonPropertyName("name")]
    [StringLength(200)]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    [StringLength(500)]
    public string? Address { get; set; }

    [JsonPropertyName("website")]
    [StringLength(2048)]
    public string? Website { get; set; }

    [JsonPropertyName("type")]
    [StringLength(50)]
    public string? Type { get; set; }

    [JsonPropertyName("rating")]
    [Range(0, 5)]
    public double? Rating { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }
}
