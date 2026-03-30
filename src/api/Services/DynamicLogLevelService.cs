using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// Manages runtime-configurable log levels per category, persisted to the database.
/// Created before DI so the logging filter lambda can capture it.
/// </summary>
public class DynamicLogLevelService
{
    private readonly ConcurrentDictionary<string, LogLevel> _categoryLevels = new();
    private LogLevel _defaultLevel = LogLevel.Information;

    public LogLevel GetMinimumLevel(string? category)
    {
        if (string.IsNullOrEmpty(category))
            return _defaultLevel;

        // Exact match first
        if (_categoryLevels.TryGetValue(category, out var level))
            return level;

        // Walk up the namespace hierarchy: "WhiskeyAndSmokes.Api.Controllers.AuthController" → "WhiskeyAndSmokes.Api.Controllers" → "WhiskeyAndSmokes.Api" → "WhiskeyAndSmokes"
        var lastDot = category.LastIndexOf('.');
        while (lastDot > 0)
        {
            var parent = category[..lastDot];
            if (_categoryLevels.TryGetValue(parent, out level))
                return level;
            lastDot = parent.LastIndexOf('.');
        }

        return _defaultLevel;
    }

    public bool IsEnabled(string? category, LogLevel logLevel)
    {
        return logLevel >= GetMinimumLevel(category);
    }

    public LoggingSettings GetSettings()
    {
        return new LoggingSettings
        {
            DefaultLevel = _defaultLevel.ToString(),
            CategoryLevels = _categoryLevels.ToDictionary(kv => kv.Key, kv => kv.Value.ToString())
        };
    }

    public void ApplySettings(LoggingSettings settings)
    {
        if (Enum.TryParse<LogLevel>(settings.DefaultLevel, true, out var defaultLevel))
            _defaultLevel = defaultLevel;

        _categoryLevels.Clear();
        foreach (var (category, levelStr) in settings.CategoryLevels)
        {
            if (Enum.TryParse<LogLevel>(levelStr, true, out var level))
                _categoryLevels[category] = level;
        }
    }

    public async Task LoadFromStoreAsync(ICosmosDbService db)
    {
        try
        {
            var stored = await db.GetAsync<LoggingSettingsDocument>("settings", "logging-settings", "settings");
            if (stored != null)
            {
                ApplySettings(stored.Settings);
            }
        }
        catch
        {
            // First run or missing — use defaults
        }
    }

    public async Task SaveToStoreAsync(ICosmosDbService db, LoggingSettings settings, string? updatedBy = null)
    {
        ApplySettings(settings);

        var doc = new LoggingSettingsDocument
        {
            Settings = GetSettings(),
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };

        await db.UpsertAsync("settings", doc, doc.PartitionKey);
    }
}

public class LoggingSettings
{
    [JsonPropertyName("defaultLevel")]
    public string DefaultLevel { get; set; } = "Information";

    [JsonPropertyName("categoryLevels")]
    public Dictionary<string, string> CategoryLevels { get; set; } = new();

    /// <summary>Standard categories exposed in the admin UI</summary>
    public static readonly Dictionary<string, string> DefaultCategories = new()
    {
        ["Default"] = "Information",
        ["Microsoft.AspNetCore"] = "Warning",
        ["Microsoft.Azure.Cosmos"] = "Warning",
        ["Azure.Identity"] = "Warning",
        ["Azure.Core"] = "Warning",
        ["System.Net.Http"] = "Warning",
        ["OpenTelemetry"] = "Warning",
        ["WhiskeyAndSmokes.Api"] = "Debug",
        ["WhiskeyAndSmokes.Api.Controllers"] = "Debug",
        ["WhiskeyAndSmokes.Api.Services"] = "Debug",
    };
}

public class LoggingSettingsDocument
{
    [JsonPropertyName("id")]
    public string Id => "logging-settings";

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => "settings";

    [JsonPropertyName("settings")]
    public LoggingSettings Settings { get; set; } = new();

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedBy")]
    public string? UpdatedBy { get; set; }
}
