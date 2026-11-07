using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// Singleton that holds the current Foundry connectivity status.
/// Updated by AgentValidationService on startup and by admin test requests.
/// </summary>
public class FoundryStatusService
{
    private readonly object _lock = new();
    private FoundryStatus _status = new();

    public FoundryStatus GetStatus()
    {
        lock (_lock) return _status;
    }

    public void Update(Action<FoundryStatus> mutate)
    {
        lock (_lock)
        {
            mutate(_status);
            _status.CheckedAt = DateTime.UtcNow;
        }
    }

    public void Reset(FoundryStatus status)
    {
        status.CheckedAt = DateTime.UtcNow;
        lock (_lock) _status = status;
    }
}

public class FoundryStatus
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("projectEndpoint")]
    public string ProjectEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("visionModel")]
    public string VisionModel { get; set; } = string.Empty;

    [JsonPropertyName("reasoningModel")]
    public string ReasoningModel { get; set; } = string.Empty;

    /// <summary>Where the effective models came from: "admin", "environment" or "none".</summary>
    [JsonPropertyName("modelSource")]
    public string ModelSource { get; set; } = AiModelSelection.SourceNone;

    [JsonPropertyName("isEndpointConfigured")]
    public bool IsEndpointConfigured { get; set; }

    [JsonPropertyName("isProjectConfigured")]
    public bool IsProjectConfigured { get; set; }

    [JsonPropertyName("agentValidation")]
    public AgentValidationResult AgentValidation { get; set; } = new();

    [JsonPropertyName("connectivityTest")]
    public ConnectivityTestResult? ConnectivityTest { get; set; }

    [JsonPropertyName("lastAgentSync")]
    public AgentSyncResult? LastAgentSync { get; set; }

    [JsonPropertyName("checkedAt")]
    public DateTime CheckedAt { get; set; }
}

public class AgentValidationResult
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "not_checked";

    [JsonPropertyName("foundAgents")]
    public List<string> FoundAgents { get; set; } = [];

    [JsonPropertyName("missingAgents")]
    public List<string> MissingAgents { get; set; } = [];

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class ConnectivityTestResult
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "not_tested";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("latencyMs")]
    public long? LatencyMs { get; set; }

    [JsonPropertyName("testedAt")]
    public DateTime TestedAt { get; set; }
}
