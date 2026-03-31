using System.Text.Json.Serialization;

namespace WhiskeyAndSmokes.Api.Models;

public class Capture
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("photos")]
    public List<string> Photos { get; set; } = [];

    [JsonPropertyName("userNote")]
    public string? UserNote { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = CaptureStatus.Pending;

    [JsonPropertyName("processedBy")]
    public string ProcessedBy { get; set; } = ProcessingSource.Pending;

    [JsonPropertyName("itemIds")]
    public List<string> ItemIds { get; set; } = [];

    [JsonPropertyName("workflowSteps")]
    public List<WorkflowStep> WorkflowSteps { get; set; } = [];

    [JsonPropertyName("processingError")]
    public string? ProcessingError { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey => UserId;
}

public class WorkflowStep
{
    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = string.Empty;

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = WorkflowStepStatus.Pending;

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public static class WorkflowStepStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Complete = "complete";
    public const string Error = "error";
}

public class GeoLocation
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
}

public static class CaptureStatus
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public static class ProcessingSource
{
    public const string Pending = "pending";
    public const string AiFoundry = "ai-foundry";
    public const string LocalExtraction = "local-extraction";
    public const string AiFallback = "ai-fallback";
}
