using Azure.AI.Projects;
using Azure.Identity;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// Background service that validates Foundry agents are registered and ready on startup.
/// Logs warnings if agents are missing but does not prevent the app from starting.
/// </summary>
public class AgentValidationService : IHostedService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AgentValidationService> _logger;

    public static readonly string[] RequiredAgents =
    [
        "whiskey-smokes-vision-analyst",
        "whiskey-smokes-domain-expert",
        "whiskey-smokes-data-curator",
    ];

    public AgentValidationService(IConfiguration config, ILogger<AgentValidationService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var endpoint = _config["AiFoundry:ProjectEndpoint"];
        if (string.IsNullOrEmpty(endpoint))
        {
            _logger.LogWarning(
                "AiFoundry:ProjectEndpoint not configured — skipping agent validation. " +
                "The app will use local extraction fallback.");
            return;
        }

        _logger.LogInformation("Validating Foundry agents at {Endpoint}...", endpoint);

        try
        {
            var credential = new ChainedTokenCredential(
                new AzureCliCredential(),
                new EnvironmentCredential(),
                new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned));

            var client = new AIProjectClient(new Uri(endpoint), credential);
            var agents = client.Agents;

            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await foreach (var agent in agents.GetAgentsAsync(cancellationToken: cancellationToken))
            {
                if (agent.Name != null)
                    found.Add(agent.Name);
            }

            var missing = RequiredAgents.Where(name => !found.Contains(name)).ToList();

            if (missing.Count == 0)
            {
                _logger.LogInformation(
                    "✅ All {Count} Foundry agents validated and ready: [{Agents}]",
                    RequiredAgents.Length,
                    string.Join(", ", RequiredAgents));
            }
            else
            {
                _logger.LogWarning(
                    "⚠️ {MissingCount}/{TotalCount} Foundry agents are MISSING: [{Missing}]. " +
                    "Run 'task agent:init' to create them. The app will fall back to local extraction.",
                    missing.Count,
                    RequiredAgents.Length,
                    string.Join(", ", missing));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not validate Foundry agents: {Error}. " +
                "The app will fall back to local extraction.",
                ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
