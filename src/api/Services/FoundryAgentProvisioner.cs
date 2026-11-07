using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Microsoft.Extensions.Options;

namespace WhiskeyAndSmokes.Api.Services;

public enum AgentModelRole
{
    Vision,
    Reasoning
}

/// <summary>A Foundry agent the app depends on: its name, which model role it runs on, and its instructions file.</summary>
public sealed record FoundryAgentSpec(string Name, string Description, AgentModelRole Role, string PromptFile);

/// <summary>
/// Single source of truth for the Foundry agents the app calls by name.
/// Models are NOT listed here on purpose: they come from <see cref="IAiModelSettingsService"/> at sync time.
/// </summary>
public static class FoundryAgentCatalog
{
    public static readonly FoundryAgentSpec[] Agents =
    [
        new("dd-vision-analyst",
            "Vision Analyst — examines photos, describes visible items",
            AgentModelRole.Vision, "vision-analyst.md"),
        new("dd-domain-expert",
            "Domain Expert — identifies products, adds expert knowledge",
            AgentModelRole.Reasoning, "domain-expert.md"),
        new("dd-data-curator",
            "Data Curator — structures JSON output, validates quality",
            AgentModelRole.Reasoning, "data-curator.md"),
        new("dd-note-analyst",
            "Note Analyst — extracts venue, sentiment rating, and occasion from user notes",
            AgentModelRole.Reasoning, "note-analyst.md"),
        new("dd-wishlist-url-extractor",
            "Wishlist URL Extractor — extracts product details from webpage content",
            AgentModelRole.Reasoning, "wishlist-url-extractor.md"),
        new("dd-venue-url-extractor",
            "Venue URL Extractor — extracts venue details from Apple Maps or venue webpage content",
            AgentModelRole.Reasoning, "venue-url-extractor.md"),
    ];

    public static string[] Names { get; } = Agents.Select(a => a.Name).ToArray();

    /// <summary>Reads an agent's instructions from the prompt files embedded in the API assembly.</summary>
    public static string LoadInstructions(string promptFile)
    {
        var resourceName = $"AgentPrompts/{promptFile}";
        using var stream = typeof(FoundryAgentCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded agent prompt not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}

public class AgentSyncResult
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "not_run";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("visionModel")]
    public string VisionModel { get; set; } = string.Empty;

    [JsonPropertyName("reasoningModel")]
    public string ReasoningModel { get; set; } = string.Empty;

    [JsonPropertyName("synced")]
    public List<string> Synced { get; set; } = [];

    [JsonPropertyName("failed")]
    public List<string> Failed { get; set; } = [];

    [JsonPropertyName("syncedAt")]
    public DateTime SyncedAt { get; set; }
}

/// <summary>
/// (Re)creates the app's Foundry agents so that each one runs on the currently selected model.
/// Foundry binds the model at agent-version creation time, so changing the model in the app
/// requires re-provisioning the agents; this does the same work as the AgentInitiator CLI.
/// </summary>
public interface IFoundryAgentProvisioner
{
    Task<AgentSyncResult> SyncAsync(AiModelSelection models, CancellationToken cancellationToken = default);
}

public class FoundryAgentProvisioner : IFoundryAgentProvisioner
{
    private readonly AiFoundryOptions _foundryOptions;
    private readonly FoundryStatusService _foundryStatus;
    private readonly ILogger<FoundryAgentProvisioner> _logger;

    public FoundryAgentProvisioner(
        IOptions<AiFoundryOptions> foundryOptions,
        FoundryStatusService foundryStatus,
        ILogger<FoundryAgentProvisioner> logger)
    {
        _foundryOptions = foundryOptions.Value;
        _foundryStatus = foundryStatus;
        _logger = logger;
    }

    public async Task<AgentSyncResult> SyncAsync(AiModelSelection models, CancellationToken cancellationToken = default)
    {
        using var activity = Diagnostics.Agent.StartActivity("SyncFoundryAgents");
        var result = new AgentSyncResult
        {
            VisionModel = models.VisionModel,
            ReasoningModel = models.ReasoningModel,
            SyncedAt = DateTime.UtcNow
        };

        if (string.IsNullOrWhiteSpace(_foundryOptions.ProjectEndpoint))
        {
            result.Status = "skipped";
            result.Message = "AiFoundry:ProjectEndpoint is not configured; agents were not synced.";
            Record(result);
            return result;
        }

        if (!models.IsConfigured)
        {
            result.Status = "skipped";
            result.Message = "Both a vision and a reasoning model must be set before agents can be synced.";
            Record(result);
            return result;
        }

        try
        {
            var credential = CredentialFactory.Create();
            var projectClient = new AIProjectClient(new Uri(_foundryOptions.ProjectEndpoint), credential);

            // Delete and recreate so every agent comes back as version "1", which is what the
            // AgentReference(name, "1") call sites expect.
            var names = new HashSet<string>(FoundryAgentCatalog.Names, StringComparer.OrdinalIgnoreCase);
            await foreach (var agent in projectClient.Agents.GetAgentsAsync(cancellationToken: cancellationToken))
            {
                if (agent.Name == null || !names.Contains(agent.Name)) continue;
                try
                {
                    await projectClient.Agents.DeleteAgentAsync(agent.Id, cancellationToken: cancellationToken);
                    _logger.LogInformation("Deleted Foundry agent {Agent} (id={AgentId}) before re-provisioning", agent.Name, agent.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete Foundry agent {Agent}; creation will add a new version", agent.Name);
                }
            }

            foreach (var spec in FoundryAgentCatalog.Agents)
            {
                var model = spec.Role == AgentModelRole.Vision ? models.VisionModel : models.ReasoningModel;
                try
                {
                    var definition = new PromptAgentDefinition(model: model)
                    {
                        Instructions = FoundryAgentCatalog.LoadInstructions(spec.PromptFile),
                    };
                    var options = new AgentVersionCreationOptions(definition) { Description = spec.Description };

                    var version = await projectClient.Agents.CreateAgentVersionAsync(
                        agentName: spec.Name, options: options, cancellationToken: cancellationToken);

                    result.Synced.Add(spec.Name);
                    _logger.LogInformation("Provisioned Foundry agent {Agent} on model {Model} (version={Version})",
                        spec.Name, model, version.Value.Version);
                }
                catch (Exception ex)
                {
                    result.Failed.Add(spec.Name);
                    _logger.LogError(ex, "Failed to provision Foundry agent {Agent} on model {Model}", spec.Name, model);
                }
            }

            result.Status = result.Failed.Count == 0 ? "ok" : result.Synced.Count == 0 ? "error" : "partial";
            result.Message = result.Failed.Count == 0
                ? $"Synced {result.Synced.Count} agent(s): vision={models.VisionModel}, reasoning={models.ReasoningModel}."
                : $"{result.Failed.Count} agent(s) failed to sync: {string.Join(", ", result.Failed)}. Check logs for details.";
        }
        catch (Exception ex)
        {
            result.Status = "error";
            result.Message = "Could not reach Foundry to sync agents. Check logs for details.";
            _logger.LogError(ex, "Foundry agent sync failed");
        }

        Record(result);
        return result;
    }

    private void Record(AgentSyncResult result)
    {
        _foundryStatus.Update(s =>
        {
            s.LastAgentSync = result;
            if (result.Status is "ok" or "partial" or "error")
            {
                s.VisionModel = result.VisionModel;
                s.ReasoningModel = result.ReasoningModel;
                s.AgentValidation.FoundAgents = [.. result.Synced];
                s.AgentValidation.MissingAgents = FoundryAgentCatalog.Names.Where(n => !result.Synced.Contains(n)).ToList();
                s.AgentValidation.Status = result.Failed.Count == 0 && result.Synced.Count > 0 ? "ok"
                    : result.Synced.Count > 0 ? "partial" : "error";
                s.AgentValidation.Error = result.Status == "ok" ? null : result.Message;
            }
        });
    }
}
