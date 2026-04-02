using Azure.AI.OpenAI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using WhiskeyAndSmokes.Api.Agents.Executors;
using WhiskeyAndSmokes.Api.Agents.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Agents;

/// <summary>
/// Builds the capture analysis workflow graph:
///   CaptureInput → VisionAnalyst → DomainExpert → DataCurator → Switch
///                                       ↑                         │
///                                       └──── (reject) ───────────┘
///                                                                  │
///                                              (approve) → Output ─┘
/// </summary>
public static class CaptureWorkflow
{
    public static Workflow Build(
        IConfiguration config,
        IPromptService promptService,
        IBlobStorageService blobService,
        ILoggerFactory loggerFactory)
    {
        var endpoint = config["AiFoundry:ProjectEndpoint"]
            ?? throw new InvalidOperationException("AiFoundry:ProjectEndpoint is required for the capture workflow.");

        var credential = CredentialFactory.Create();

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), credential);

        var visionModel = config["AiFoundry:Models:Vision"] ?? "gpt-4o";
        var reasoningModel = config["AiFoundry:Models:Reasoning"] ?? "gpt-5.1-mini";

        IChatClient visionChatClient = azureClient.GetChatClient(visionModel).AsIChatClient();
        IChatClient reasoningChatClient = azureClient.GetChatClient(reasoningModel).AsIChatClient();

        var vision = new VisionExecutor(
            visionChatClient,
            promptService,
            blobService,
            loggerFactory.CreateLogger<VisionExecutor>());

        var expert = new ExpertExecutor(
            reasoningChatClient,
            promptService,
            loggerFactory.CreateLogger<ExpertExecutor>());

        var curator = new CuratorExecutor(
            reasoningChatClient,
            promptService,
            loggerFactory.CreateLogger<CuratorExecutor>());

        // Passthrough executor that receives approved results for output
        var output = new OutputCollector();

        var workflow = new WorkflowBuilder(vision)
            .WithName("CaptureAnalysis")
            .WithDescription("Analyzes captured photos of drinks and cigars using a multi-agent pipeline")
            .AddEdge(vision, expert)
            .AddEdge(expert, curator)
            .AddSwitch(curator, sb => sb
                .AddCase<CuratorDecision>(d => d != null && d.IsApproved, [output])
                .WithDefault([expert]))
            .WithOutputFrom(output)
            .Build();

        return workflow;
    }
}

/// <summary>
/// Simple passthrough executor that receives approved CuratorDecisions and yields them as workflow output.
/// </summary>
internal sealed class OutputCollector : Executor<CuratorDecision, CuratorDecision>
{
    public OutputCollector() : base("OutputCollector") { }

    public override ValueTask<CuratorDecision> HandleAsync(
        CuratorDecision input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(input);
}
