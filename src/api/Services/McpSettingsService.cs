using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// Tracks whether the native MCP (Model Context Protocol) endpoint is enabled.
/// Default-off, admin-gated, persisted to the "settings" container.
/// Mirrors the pattern used by <see cref="DynamicLogLevelService"/>.
/// </summary>
public class McpSettingsService
{
    private volatile bool _enabled;

    public bool IsEnabled => _enabled;

    public void ApplySettings(McpSettings settings) => _enabled = settings.Enabled;

    public McpSettings GetSettings() => new() { Enabled = _enabled };

    public async Task LoadFromStoreAsync(ICosmosDbService db)
    {
        try
        {
            var stored = await db.GetAsync<McpSettingsDocument>("settings", McpSettingsDocument.DocumentId, McpSettingsDocument.PartitionKeyValue);
            if (stored != null)
                ApplySettings(stored.Settings);
        }
        catch
        {
            // First run or missing — default to disabled
        }
    }

    public async Task<McpSettings> SaveToStoreAsync(ICosmosDbService db, bool enabled, string? updatedBy = null)
    {
        ApplySettings(new McpSettings { Enabled = enabled });

        var doc = new McpSettingsDocument
        {
            Settings = GetSettings(),
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };

        await db.UpsertAsync("settings", doc, doc.PartitionKey);
        return doc.Settings;
    }
}

public class McpSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;
}

public class McpSettingsDocument
{
    public const string DocumentId = "mcp-settings";
    public const string PartitionKeyValue = "settings";

    [JsonPropertyName("id")]
    public string Id { get; set; } = DocumentId;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = PartitionKeyValue;

    [JsonPropertyName("settings")]
    public McpSettings Settings { get; set; } = new();

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedBy")]
    public string? UpdatedBy { get; set; }
}
