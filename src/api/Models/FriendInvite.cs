using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class FriendInvite
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = GenerateCode();

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("userDisplayName")]
    public string UserDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    [JsonPropertyName("maxUses")]
    public int MaxUses { get; set; } = 1;

    [JsonPropertyName("usedBy")]
    public List<string> UsedBy { get; set; } = [];

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => Id;

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[8];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}
