using Azure.AI.Projects;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// Background service that validates Foundry agents are registered and ready on startup.
/// Populates FoundryStatusService with connectivity results.
/// Logs warnings if agents are missing but does not prevent the app from starting.
/// </summary>
public class AgentValidationService : IHostedService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AgentValidationService> _logger;
    private readonly FoundryStatusService _foundryStatus;

    public static readonly string[] RequiredAgents =
    [
        "dd-vision-analyst",
        "dd-domain-expert",
        "dd-data-curator",
        "dd-note-analyst",
        "dd-wishlist-url-extractor",
    ];

    public AgentValidationService(
        IConfiguration config,
        ILogger<AgentValidationService> logger,
        FoundryStatusService foundryStatus)
    {
        _config = config;
        _logger = logger;
        _foundryStatus = foundryStatus;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var endpoint = _config["AiFoundry:Endpoint"] ?? "";
        var projectEndpoint = _config["AiFoundry:ProjectEndpoint"] ?? "";
        var visionModel = _config["AiFoundry:Models:Vision"] ?? "gpt-4o";
        var reasoningModel = _config["AiFoundry:Models:Reasoning"] ?? "gpt-5-mini";

        _logger.LogInformation(
            "Foundry configuration: Endpoint={Endpoint}, ProjectEndpoint={ProjectEndpoint}, Vision={Vision}, Reasoning={Reasoning}",
            string.IsNullOrEmpty(endpoint) ? "(not set)" : endpoint,
            string.IsNullOrEmpty(projectEndpoint) ? "(not set)" : projectEndpoint,
            visionModel, reasoningModel);

        _foundryStatus.Update(s =>
        {
            s.Endpoint = endpoint;
            s.ProjectEndpoint = projectEndpoint;
            s.VisionModel = visionModel;
            s.ReasoningModel = reasoningModel;
            s.IsEndpointConfigured = !string.IsNullOrEmpty(endpoint);
            s.IsProjectConfigured = !string.IsNullOrEmpty(projectEndpoint);
        });

        if (string.IsNullOrEmpty(projectEndpoint))
        {
            _logger.LogWarning(
                "AiFoundry:ProjectEndpoint not configured — skipping agent validation. " +
                "The app will use local extraction fallback.");
            _foundryStatus.Update(s =>
            {
                s.AgentValidation.Status = "skipped";
                s.AgentValidation.Error = "ProjectEndpoint not configured";
            });
            return;
        }

        _logger.LogInformation("Validating Foundry agents at {Endpoint}...", projectEndpoint);

        try
        {
            var credential = CredentialFactory.Create();

            var client = new AIProjectClient(new Uri(projectEndpoint), credential);
            var agents = client.Agents;

            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await foreach (var agent in agents.GetAgentsAsync(cancellationToken: cancellationToken))
            {
                if (agent.Name != null)
                    found.Add(agent.Name);
            }

            var missing = RequiredAgents.Where(name => !found.Contains(name)).ToList();
            var foundRequired = RequiredAgents.Where(name => found.Contains(name)).ToList();

            _foundryStatus.Update(s =>
            {
                s.AgentValidation.FoundAgents = foundRequired;
                s.AgentValidation.MissingAgents = missing;
                s.AgentValidation.Status = missing.Count == 0 ? "ok" : "partial";
            });

            if (missing.Count == 0)
            {
                _logger.LogInformation(
                    "All {Count} Foundry agents validated and ready: [{Agents}]",
                    RequiredAgents.Length,
                    string.Join(", ", RequiredAgents));
            }
            else
            {
                _logger.LogWarning(
                    "{MissingCount}/{TotalCount} Foundry agents are MISSING: [{Missing}]. " +
                    "Run 'task agent:init' to create them. The app will fall back to local extraction.",
                    missing.Count,
                    RequiredAgents.Length,
                    string.Join(", ", missing));
            }
        }
        catch (Exception ex)
        {
            var errorCategory = ex switch
            {
                Azure.RequestFailedException => "AzureServiceError",
                HttpRequestException => "ConnectionError",
                TaskCanceledException => "Timeout",
                _ => "UnexpectedError"
            };

            _logger.LogWarning(ex,
                "Could not validate Foundry agents: {ErrorCategory}. " +
                "The app will fall back to local extraction.",
                errorCategory);
            _foundryStatus.Update(s =>
            {
                s.AgentValidation.Status = "error";
                s.AgentValidation.Error = errorCategory;
            });
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
