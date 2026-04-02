using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using WhiskeyAndSmokes.Api.Agents.Executors;
using WhiskeyAndSmokes.Api.Agents.Models;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Agents;

/// <summary>
/// IAgentService implementation using Microsoft Foundry Agent Service (AIProjectClient).
/// Calls pre-registered Foundry agents via the Responses API for text-based agents
/// and uses the project-scoped ChatClient for vision (multimodal/image) analysis.
/// Falls back to local keyword extraction when Foundry is not configured.
/// </summary>
public class WorkflowAgentService : IAgentService
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IPromptService _promptService;
    private readonly IBlobStorageService _blobService;
    private readonly ILogger<WorkflowAgentService> _logger;
    private readonly AiFoundryOptions _foundryOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly bool _isFoundryConfigured;

    private const int MaxRefinements = 2;

    private const string VisionAgentName = "whiskey-smokes-vision-analyst";
    private const string ExpertAgentName = "whiskey-smokes-domain-expert";
    private const string CuratorAgentName = "whiskey-smokes-data-curator";
    private const string NoteAgentName = "whiskey-smokes-note-analyst";

    public WorkflowAgentService(
        ICosmosDbService cosmosDb,
        IPromptService promptService,
        IBlobStorageService blobService,
        ILogger<WorkflowAgentService> logger,
        IOptions<AiFoundryOptions> foundryOptions,
        ILoggerFactory loggerFactory)
    {
        _cosmosDb = cosmosDb;
        _promptService = promptService;
        _blobService = blobService;
        _logger = logger;
        _foundryOptions = foundryOptions.Value;
        _loggerFactory = loggerFactory;
        _isFoundryConfigured = !string.IsNullOrEmpty(_foundryOptions.ProjectEndpoint);
    }

    public async Task ProcessCaptureAsync(Capture capture)
    {
        using var activity = Diagnostics.Agent.StartActivity("ProcessCapture");
        activity?.SetTag("capture.id", capture.Id);
        activity?.SetTag("capture.user_id", capture.UserId);
        activity?.SetTag("capture.photo_count", capture.Photos.Count);

        _logger.LogInformation(
            "Processing capture {CaptureId} for user {UserId} with {PhotoCount} photos, noteLength={NoteLength}",
            capture.Id, capture.UserId, capture.Photos.Count, capture.UserNote?.Length ?? 0);

        try
        {
            capture.Status = CaptureStatus.Processing;
            capture.WorkflowSteps = [];
            capture.UpdatedAt = DateTime.UtcNow;
            await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);

            List<Item> items;

            if (_isFoundryConfigured)
            {
                _logger.LogInformation("Running multi-agent workflow for capture {CaptureId}", capture.Id);
                items = await RunWorkflowAsync(capture);
                capture.ProcessedBy = ProcessingSource.AiFoundry;
            }
            else
            {
                _logger.LogWarning("AI Foundry not configured — using local extraction for capture {CaptureId}", capture.Id);
                await LogStep(capture, "S01", "Local Extraction", WorkflowStepStatus.Running, "AI Foundry not configured — using keyword matching");
                items = LocalExtraction.Process(capture, _logger);
                capture.ProcessedBy = ProcessingSource.LocalExtraction;
                await LogStep(capture, "S01", "Local Extraction", WorkflowStepStatus.Complete,
                    $"Extracted {items.Count} item(s) from note keywords");
            }

            activity?.SetTag("items.count", items.Count);

            foreach (var item in items)
            {
                await _cosmosDb.CreateAsync("items", item, item.PartitionKey);
                capture.ItemIds.Add(item.Id);
            }

            capture.Status = CaptureStatus.Completed;
            capture.UpdatedAt = DateTime.UtcNow;
            await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);

            _logger.LogInformation(
                "Capture {CaptureId} completed: {ItemCount} items, types=[{Types}]",
                capture.Id, items.Count, string.Join(", ", items.Select(i => i.Type).Distinct()));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to process capture {CaptureId}: {Error}", capture.Id, ex.Message);

            await LogStep(capture, "ERR", "Error", WorkflowStepStatus.Error, ex.Message);

            try
            {
                _logger.LogWarning("Attempting local extraction fallback for capture {CaptureId}", capture.Id);
                var fallbackItems = LocalExtraction.Process(capture, _logger);

                foreach (var item in fallbackItems)
                {
                    await _cosmosDb.CreateAsync("items", item, item.PartitionKey);
                    capture.ItemIds.Add(item.Id);
                }

                capture.Status = CaptureStatus.Completed;
                capture.ProcessedBy = ProcessingSource.AiFallback;
                capture.ProcessingError = $"AI workflow failed ({ex.Message}), used local extraction";
                capture.UpdatedAt = DateTime.UtcNow;
                await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Local extraction fallback also failed for capture {CaptureId}", capture.Id);
                capture.Status = CaptureStatus.Failed;
                capture.ProcessingError = ex.Message;
                capture.UpdatedAt = DateTime.UtcNow;
                await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);
            }
        }
    }

    private async Task<List<Item>> RunWorkflowAsync(Capture capture)
    {
        using var activity = Diagnostics.Agent.StartActivity("RunWorkflow");
        activity?.SetTag("capture.id", capture.Id);

        var endpoint = _foundryOptions.ProjectEndpoint;
        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("AiFoundry:ProjectEndpoint is required");

        var credential = CredentialFactory.Create();

        var projectClient = new AIProjectClient(new Uri(endpoint), credential);

        var visionModel = _foundryOptions.Models.Vision;
        var reasoningModel = _foundryOptions.Models.Reasoning;

        _logger.LogInformation("Connected to Foundry project at {Endpoint} for capture {CaptureId}",
            endpoint, capture.Id);

        var input = new CaptureInput
        {
            CaptureId = capture.Id,
            UserId = capture.UserId,
            PhotoUrls = capture.Photos,
            UserNote = capture.UserNote,
            Location = capture.Location != null
                ? new GeoCoordinate { Latitude = capture.Location.Latitude, Longitude = capture.Location.Longitude }
                : null
        };

        // ── Step 1: Vision Analyst + Note Analyst (run in parallel) ──
        await LogStep(capture, "S01", "Vision Analyst", WorkflowStepStatus.Running,
            $"Analyzing {capture.Photos.Count} photo(s) with {visionModel}...");

        IChatClient visionChatClient = projectClient.OpenAI.GetChatClient(visionModel).AsIChatClient();
        var vision = new VisionExecutor(
            visionChatClient, _promptService, _blobService,
            _loggerFactory.CreateLogger<VisionExecutor>());

        // Start vision and note analysis concurrently
        var visionTask = vision.HandleAsync(input, null!, CancellationToken.None).AsTask();

        NoteAnalysis? noteAnalysis = null;
        Task<NoteAnalysis?>? noteTask = null;
        if (!string.IsNullOrWhiteSpace(capture.UserNote))
        {
            await LogStep(capture, "S01b", "Note Analyst", WorkflowStepStatus.Running,
                "Extracting venue, sentiment, and occasion from user notes...");
            noteTask = RunNoteAnalystAsync(projectClient, capture);
        }

        var visionResult = await visionTask;

        if (noteTask != null)
        {
            noteAnalysis = await noteTask;
            var noteInfo = noteAnalysis != null
                ? $"venue={noteAnalysis.Venue?.Name ?? "none"}, rating={noteAnalysis.SuggestedRating?.ToString() ?? "none"}, occasion={noteAnalysis.Occasion ?? "none"}"
                : "no results";
            await LogStep(capture, "S01b", "Note Analyst", WorkflowStepStatus.Complete, noteInfo);
        }

        var visionSummary = visionResult.Description.Length > 300
            ? visionResult.Description[..300] + "..."
            : visionResult.Description;
        await LogStep(capture, "S01", "Vision Analyst", WorkflowStepStatus.Complete,
            visionSummary, visionResult.Description);

        // ── Step 2: Domain Expert (Foundry agent via Responses API) ──
        // Prompt is baked into the Foundry agent at init time (see AgentInitiator/Prompts/).
        // To change the prompt, edit the file and re-run agent:init.
        await LogStep(capture, "S02", "Domain Expert", WorkflowStepStatus.Running,
            $"Identifying products via Foundry agent...");

        var expertPrompt = BuildExpertPrompt(visionResult);
        var expertText = await CallFoundryAgentAsync(projectClient, ExpertAgentName, expertPrompt, "S02");

        var expertSummary = expertText.Length > 300
            ? expertText[..300] + "..."
            : expertText;
        await LogStep(capture, "S02", "Domain Expert", WorkflowStepStatus.Complete,
            expertSummary, expertText);

        var expertAnalysis = expertText;

        // ── Step 3: Data Curator (Foundry agent with retry loop) ──
        CuratorDecision? decision = null;
        var refinements = 0;

        while (refinements <= MaxRefinements)
        {
            var stepLabel = refinements == 0 ? "Data Curator" : $"Data Curator (refinement #{refinements})";
            await LogStep(capture, "S03", stepLabel, WorkflowStepStatus.Running,
                refinements == 0
                    ? "Structuring results via Foundry agent..."
                    : "Refining based on curator feedback...");

            var curatorPrompt = $"Expert analysis to structure:\n\n{expertAnalysis}";
            var curatorText = await CallFoundryAgentAsync(projectClient, CuratorAgentName, curatorPrompt, "S03");

            decision = ParseCuratorResponse(curatorText, capture.Id);

            if (decision.IsApproved)
            {
                var itemNames = decision.Items?.Select(i => i.Name).ToList() ?? [];
                await LogStep(capture, "S03", stepLabel, WorkflowStepStatus.Complete,
                    $"Approved {decision.Items?.Count ?? 0} item(s): {string.Join(", ", itemNames)}",
                    JsonSerializer.Serialize(decision.Items, new JsonSerializerOptions { WriteIndented = true }));
                break;
            }

            await LogStep(capture, "S03", stepLabel, WorkflowStepStatus.Complete,
                $"Rejected: {decision.Reason}");

            refinements++;
            if (refinements > MaxRefinements)
            {
                _logger.LogWarning("Max refinements reached for capture {CaptureId} — auto-approving", capture.Id);
                decision = new CuratorDecision { Decision = "approve", Items = decision.Items ?? [] };
                await LogStep(capture, "S03", "Data Curator", WorkflowStepStatus.Complete,
                    "Max refinements reached — auto-approved");
                break;
            }

            // Re-run expert with rejection feedback via Foundry agent
            await LogStep(capture, "S02", $"Domain Expert (revision #{refinements})", WorkflowStepStatus.Running,
                $"Revising based on feedback: {decision.Reason}");

            var refinementPrompt = BuildRefinementPrompt(visionResult.Description, decision);
            expertAnalysis = await CallFoundryAgentAsync(projectClient, ExpertAgentName, refinementPrompt, "S02");

            await LogStep(capture, "S02", $"Domain Expert (revision #{refinements})", WorkflowStepStatus.Complete,
                expertAnalysis.Length > 300 ? expertAnalysis[..300] + "..." : expertAnalysis);
        }

        if (decision?.IsApproved == true && decision.Items != null && decision.Items.Count > 0)
        {
            return ConvertToItems(decision.Items, capture, noteAnalysis);
        }

        _logger.LogWarning("Workflow did not produce approved items for capture {CaptureId} — falling back", capture.Id);
        await LogStep(capture, "S04", "Fallback", WorkflowStepStatus.Complete,
            "AI workflow produced no items — used local keyword extraction");
        return LocalExtraction.Process(capture, _logger);
    }

    private async Task LogStep(Capture capture, string stepId, string agentName, string status,
        string summary, string? detail = null)
    {
        capture.WorkflowSteps.Add(new WorkflowStep
        {
            StepId = stepId,
            AgentName = agentName,
            Status = status,
            Summary = summary,
            Detail = detail,
            Timestamp = DateTime.UtcNow
        });
        capture.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist workflow step {StepId} for capture {CaptureId}", stepId, capture.Id);
        }
    }

    private static List<Item> ConvertToItems(List<CuratorItemResult> curatorItems, Capture capture, NoteAnalysis? noteAnalysis = null)
    {
        return curatorItems.Select(p =>
        {
            // Prefer curator venue, fall back to note analyst venue
            VenueInfo? venue = null;
            if (p.Venue != null)
            {
                venue = new VenueInfo { Name = p.Venue.Name ?? "Unknown Venue", Address = p.Venue.Address };
            }
            else if (noteAnalysis?.Venue?.Name != null)
            {
                venue = new VenueInfo { Name = noteAnalysis.Venue.Name, Address = noteAnalysis.Venue.Address };
            }

            // Build tags — include occasion from note analyst if available
            var tags = p.Tags ?? [];
            if (!string.IsNullOrWhiteSpace(noteAnalysis?.Occasion))
            {
                var occasionTag = noteAnalysis.Occasion.ToLowerInvariant().Trim();
                if (!tags.Contains(occasionTag))
                    tags = [.. tags, occasionTag];
            }

            return new Item
            {
                UserId = capture.UserId,
                CaptureId = capture.Id,
                Type = NormalizeType(p.Type),
                Name = p.Name ?? "Unknown Item",
                Brand = p.Brand,
                Category = p.Category,
                Details = p.Details != null ? JsonSerializer.SerializeToElement(p.Details) : null,
                Venue = venue,
                PhotoUrls = capture.Photos,
                AiConfidence = p.Confidence ?? 0.8,
                AiSummary = p.Summary,
                UserRating = noteAnalysis?.SuggestedRating,
                Journal = LocalExtraction.InitialJournal(capture.UserNote),
                Tags = tags,
                Status = ItemStatus.AiDraft,
                ProcessedBy = ProcessingSource.AiFoundry
            };
        }).ToList();
    }

    private static string NormalizeType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "whiskey" or "whisky" or "bourbon" or "scotch" or "rye" => ItemType.Whiskey,
            "wine" or "red wine" or "white wine" or "rosé" => ItemType.Wine,
            "cocktail" or "mixed drink" => ItemType.Cocktail,
            "cigar" => ItemType.Cigar,
            _ => type?.ToLowerInvariant() ?? "unknown"
        };
    }

    /// <summary>
    /// Calls a Foundry agent by name using the Project Responses API.
    /// The agent's system prompt (instructions) is baked in at init time via AgentInitiator.
    /// To change an agent's prompt, edit the file in AgentInitiator/Prompts/ and re-run agent:init.
    /// </summary>
    private async Task<string> CallFoundryAgentAsync(
        AIProjectClient projectClient, string agentName, string prompt, string stepId)
    {
        using var agentActivity = Diagnostics.Agent.StartActivity($"InvokeAgent_{stepId}");
        agentActivity?.SetTag("gen_ai.agent.name", agentName);
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("{Step}: Calling Foundry agent '{Agent}'...", stepId, agentName);

        var agentRef = new AgentReference(agentName, "1");
        var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentRef);
        var response = await responsesClient.CreateResponseAsync(prompt);

        sw.Stop();
        var text = response.Value.GetOutputText() ?? "(empty response)";

        _logger.LogInformation("{Step}: Agent '{Agent}' responded in {Duration}ms ({Chars} chars)",
            stepId, agentName, sw.ElapsedMilliseconds, text.Length);

        agentActivity?.SetTag("agent.response.length", text.Length);
        agentActivity?.SetTag("agent.call.duration_ms", sw.ElapsedMilliseconds);

        return text;
    }

    private async Task<NoteAnalysis?> RunNoteAnalystAsync(AIProjectClient projectClient, Capture capture)
    {
        try
        {
            var prompt = $"User's note: \"{capture.UserNote}\"";
            if (capture.Location != null)
                prompt += $"\nGPS coordinates: {capture.Location.Latitude}, {capture.Location.Longitude}";

            var responseText = await CallFoundryAgentAsync(projectClient, NoteAgentName, prompt, "S01b");

            var jsonText = responseText.Trim();
            if (jsonText.StartsWith("```"))
            {
                var firstNewline = jsonText.IndexOf('\n');
                var lastFence = jsonText.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                    jsonText = jsonText[(firstNewline + 1)..lastFence].Trim();
            }

            return JsonSerializer.Deserialize<NoteAnalysis>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Note Analyst failed for capture {CaptureId} — continuing without note analysis", capture.Id);
            return null;
        }
    }

    private static string BuildExpertPrompt(VisionDescription vision)
    {
        return $"""
            Here is what the vision analyst observed in the photos:

            {vision.Description}

            {(vision.UserNote != null ? $"User's note: {vision.UserNote}" : "")}
            {(vision.Location != null ? $"GPS Location: {vision.Location.Latitude}, {vision.Location.Longitude}" : "")}

            Please identify each item and provide your expert analysis.
            """;
    }

    private static string BuildRefinementPrompt(string visionContext, CuratorDecision rejection)
    {
        return $"""
            The data curator rejected your previous analysis with this feedback:
            {rejection.Reason}

            Original vision description for reference:
            {visionContext}

            Please refine your analysis addressing the curator's feedback.
            """;
    }

    private CuratorDecision ParseCuratorResponse(string responseText, string captureId)
    {
        try
        {
            var jsonText = responseText.Trim();

            if (jsonText.StartsWith("```"))
            {
                var firstNewline = jsonText.IndexOf('\n');
                var lastFence = jsonText.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                    jsonText = jsonText[(firstNewline + 1)..lastFence].Trim();
            }

            var decision = JsonSerializer.Deserialize<CuratorDecision>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (decision != null) return decision;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse curator JSON for capture {CaptureId}: {Error}", captureId, ex.Message);
        }

        return new CuratorDecision
        {
            Decision = "approve",
            Items = ExtractBestEffortItems(responseText)
        };
    }

    private List<CuratorItemResult>? ExtractBestEffortItems(string text)
    {
        try
        {
            var arrayStart = text.IndexOf('[');
            var arrayEnd = text.LastIndexOf(']');
            if (arrayStart >= 0 && arrayEnd > arrayStart)
            {
                var arrayJson = text[arrayStart..(arrayEnd + 1)];
                return JsonSerializer.Deserialize<List<CuratorItemResult>>(arrayJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to extract best-effort items from curator response");
        }
        return null;
    }
}
