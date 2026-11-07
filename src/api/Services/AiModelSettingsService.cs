using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// The model deployment names the app should use right now, and where they came from.
/// </summary>
public sealed record AiModelSelection(string VisionModel, string ReasoningModel, string Source)
{
    /// <summary>At least one value was set by an admin in the app (Admin → Foundry).</summary>
    public const string SourceAdmin = "admin";
    /// <summary>Values came from the AiFoundry__Models__* environment/configuration seed.</summary>
    public const string SourceEnvironment = "environment";
    /// <summary>Nothing configured anywhere — AI features fall back to local extraction.</summary>
    public const string SourceNone = "none";

    public static readonly AiModelSelection Empty = new(string.Empty, string.Empty, SourceNone);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(VisionModel) && !string.IsNullOrWhiteSpace(ReasoningModel);
}

/// <summary>
/// Validates Azure AI Foundry model deployment names entered by admins.
/// </summary>
public static partial class AiModelName
{
    public const int MaxLength = 128;

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex Pattern();

    /// <summary>
    /// Trims the input. Blank input normalizes to <c>null</c> (meaning "use the fallback").
    /// Returns an error message when the value is not a plausible deployment name.
    /// </summary>
    public static string? TryNormalize(string? raw, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        if (normalized == null) return null;
        if (normalized.Length > MaxLength)
            return $"Model deployment names must be at most {MaxLength} characters.";
        if (!Pattern().IsMatch(normalized))
            return "Model deployment names may only contain letters, digits, '.', '_' and '-'.";
        return null;
    }
}

/// <summary>
/// Source of truth for which model deployments the app uses. Admin-set values persisted in
/// Cosmos take precedence over the environment seed (AiFoundry__Models__*), so a model
/// retirement is handled in the app without a code change or an infrastructure redeploy.
/// </summary>
public interface IAiModelSettingsService
{
    /// <summary>Values seeded from configuration/environment. Used when nothing is set in the app.</summary>
    AiModelSelection Fallback { get; }

    /// <summary>The stored admin document, or null when no admin has saved anything yet.</summary>
    Task<AppAiSettingsDocument?> GetStoredAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin values where set, otherwise the fallback.</summary>
    Task<AiModelSelection> GetEffectiveAsync(CancellationToken cancellationToken = default);

    Task<AppAiSettingsDocument> SaveAsync(AppAiSettings settings, string updatedBy, CancellationToken cancellationToken = default);
}

public class AiModelSettingsService : IAiModelSettingsService
{
    public const string ContainerName = "settings";
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly ICosmosDbService _cosmosDb;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AiModelSettingsService> _logger;
    private readonly object _lock = new();
    private AppAiSettingsDocument? _cached;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;
    private bool _hasCache;

    public AiModelSelection Fallback { get; }

    public AiModelSettingsService(
        ICosmosDbService cosmosDb,
        IOptions<AiFoundryOptions> foundryOptions,
        TimeProvider timeProvider,
        ILogger<AiModelSettingsService> logger)
    {
        _cosmosDb = cosmosDb;
        _timeProvider = timeProvider;
        _logger = logger;

        var vision = foundryOptions.Value.Models.Vision?.Trim() ?? string.Empty;
        var reasoning = foundryOptions.Value.Models.Reasoning?.Trim() ?? string.Empty;
        Fallback = new AiModelSelection(
            vision,
            reasoning,
            vision.Length == 0 && reasoning.Length == 0 ? AiModelSelection.SourceNone : AiModelSelection.SourceEnvironment);
    }

    public async Task<AppAiSettingsDocument?> GetStoredAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_lock)
        {
            if (_hasCache && now - _cachedAt < CacheTtl)
                return _cached;
        }

        AppAiSettingsDocument? doc;
        try
        {
            doc = await _cosmosDb.GetAsync<AppAiSettingsDocument>(
                ContainerName, AppAiSettingsDocument.DocumentId, AppAiSettingsDocument.PartitionKeyValue);
        }
        catch (Exception ex)
        {
            // Don't let a transient store failure take AI features down: keep serving the last
            // known value (or the environment fallback) and try again on the next call.
            _logger.LogWarning(ex, "Could not load AI model settings from the store; using last known values");
            lock (_lock) return _cached;
        }

        lock (_lock)
        {
            _cached = doc;
            _cachedAt = now;
            _hasCache = true;
        }
        return doc;
    }

    public async Task<AiModelSelection> GetEffectiveAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredAsync(cancellationToken);
        return Resolve(stored?.Settings, Fallback);
    }

    public async Task<AppAiSettingsDocument> SaveAsync(AppAiSettings settings, string updatedBy, CancellationToken cancellationToken = default)
    {
        var doc = new AppAiSettingsDocument
        {
            Settings = new AppAiSettings
            {
                VisionModel = Normalize(settings.VisionModel),
                ReasoningModel = Normalize(settings.ReasoningModel)
            },
            UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            UpdatedBy = updatedBy
        };

        var saved = await _cosmosDb.UpsertAsync(ContainerName, doc, doc.PartitionKey);

        lock (_lock)
        {
            _cached = saved;
            _cachedAt = _timeProvider.GetUtcNow();
            _hasCache = true;
        }

        _logger.LogInformation("AI model settings saved by {UpdatedBy}: vision={Vision}, reasoning={Reasoning}",
            updatedBy, saved.Settings.VisionModel ?? "(fallback)", saved.Settings.ReasoningModel ?? "(fallback)");
        return saved;
    }

    /// <summary>Admin values win per field; blank admin values defer to the fallback.</summary>
    public static AiModelSelection Resolve(AppAiSettings? stored, AiModelSelection fallback)
    {
        var storedVision = Normalize(stored?.VisionModel);
        var storedReasoning = Normalize(stored?.ReasoningModel);

        var vision = storedVision ?? fallback.VisionModel;
        var reasoning = storedReasoning ?? fallback.ReasoningModel;

        var source = storedVision != null || storedReasoning != null
            ? AiModelSelection.SourceAdmin
            : string.IsNullOrEmpty(vision) && string.IsNullOrEmpty(reasoning)
                ? AiModelSelection.SourceNone
                : AiModelSelection.SourceEnvironment;

        return new AiModelSelection(vision, reasoning, source);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
