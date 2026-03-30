using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface IAgentService
{
    Task ProcessCaptureAsync(Capture capture);
}

public class AgentService : IAgentService
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IPromptService _promptService;
    private readonly ILogger<AgentService> _logger;
    private readonly IConfiguration _config;
    private readonly PersistentAgentsClient? _agentsClient;

    public AgentService(ICosmosDbService cosmosDb, IPromptService promptService, ILogger<AgentService> logger, IConfiguration config)
    {
        _cosmosDb = cosmosDb;
        _promptService = promptService;
        _logger = logger;
        _config = config;

        var endpoint = config["AiFoundry:Endpoint"];
        if (!string.IsNullOrEmpty(endpoint))
        {
            _agentsClient = new PersistentAgentsClient(endpoint, new DefaultAzureCredential());
        }
    }

    public async Task ProcessCaptureAsync(Capture capture)
    {
        using var activity = Diagnostics.Agent.StartActivity("ProcessCapture");
        activity?.SetTag("capture.id", capture.Id);
        activity?.SetTag("capture.user_id", capture.UserId);
        activity?.SetTag("capture.photo_count", capture.Photos.Count);

        _logger.LogInformation(
            "Processing capture {CaptureId} for user {UserId} with {PhotoCount} photos, note length {NoteLength}, hasLocation={HasLocation}",
            capture.Id, capture.UserId, capture.Photos.Count,
            capture.UserNote?.Length ?? 0, capture.Location != null);

        try
        {
            capture.Status = CaptureStatus.Processing;
            capture.UpdatedAt = DateTime.UtcNow;
            await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);
            _logger.LogDebug("Capture {CaptureId} status set to Processing", capture.Id);

            List<Item> items;

            if (_agentsClient != null)
            {
                _logger.LogInformation("AI Foundry client available — processing capture {CaptureId} with agent", capture.Id);
                items = await ProcessWithAiFoundryAsync(capture);
            }
            else
            {
                _logger.LogWarning("AI Foundry not configured — using local extraction fallback for capture {CaptureId}", capture.Id);
                items = await ProcessWithLocalExtractionAsync(capture);
            }

            activity?.SetTag("items.count", items.Count);

            _logger.LogDebug("Persisting {ItemCount} extracted items for capture {CaptureId}", items.Count, capture.Id);
            foreach (var item in items)
            {
                await _cosmosDb.CreateAsync("items", item, item.PartitionKey);
                capture.ItemIds.Add(item.Id);
                _logger.LogDebug("Persisted item {ItemId} (type={ItemType}, name={ItemName}) for capture {CaptureId}",
                    item.Id, item.Type, item.Name, capture.Id);
            }

            capture.Status = CaptureStatus.Completed;
            capture.UpdatedAt = DateTime.UtcNow;
            await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);

            _logger.LogInformation(
                "Capture {CaptureId} processing completed successfully: created {ItemCount} items, types=[{ItemTypes}]",
                capture.Id, items.Count, string.Join(", ", items.Select(i => i.Type).Distinct()));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to process capture {CaptureId} for user {UserId}: {ErrorMessage}",
                capture.Id, capture.UserId, ex.Message);
            capture.Status = CaptureStatus.Failed;
            capture.ProcessingError = ex.Message;
            capture.UpdatedAt = DateTime.UtcNow;
            await _cosmosDb.UpsertAsync("captures", capture, capture.PartitionKey);
        }
    }

    private async Task<List<Item>> ProcessWithAiFoundryAsync(Capture capture)
    {
        using var activity = Diagnostics.Agent.StartActivity("ProcessWithAiFoundry");
        activity?.SetTag("capture.id", capture.Id);

        var admin = _agentsClient!.Administration;

        var prompt = await _promptService.GetAsync(PromptIds.AgentInstructions);
        var instructions = prompt?.Content ?? DefaultPrompts.AgentInstructions;
        _logger.LogDebug("Loaded agent instructions for capture {CaptureId}: promptFound={PromptFound}, instructionLength={InstructionLength}",
            capture.Id, prompt != null, instructions.Length);

        var modelDeployment = _config["AiFoundry:ModelDeployment"] ?? "gpt-4o";
        activity?.SetTag("agent.model", modelDeployment);

        _logger.LogInformation("Creating AI agent for capture {CaptureId} with model={Model}, deployment=whiskey-and-smokes-capture-processor",
            capture.Id, modelDeployment);

        var agent = await admin.CreateAgentAsync(
            model: modelDeployment,
            name: "whiskey-and-smokes-capture-processor",
            instructions: instructions);
        _logger.LogDebug("Agent created: agentId={AgentId} for capture {CaptureId}", agent.Value.Id, capture.Id);

        var thread = await _agentsClient.Threads.CreateThreadAsync();
        _logger.LogDebug("Thread created: threadId={ThreadId} for capture {CaptureId}", thread.Value.Id, capture.Id);

        var contentBlocks = new List<MessageInputContentBlock>();

        if (!string.IsNullOrEmpty(capture.UserNote))
        {
            contentBlocks.Add(new MessageInputTextBlock($"User note: {capture.UserNote}"));
        }

        foreach (var photoUrl in capture.Photos)
        {
            contentBlocks.Add(new MessageInputImageUriBlock(new MessageImageUriParam(photoUrl)));
        }

        if (capture.Location != null)
        {
            contentBlocks.Add(new MessageInputTextBlock(
                $"GPS Location: {capture.Location.Latitude}, {capture.Location.Longitude}"));
        }

        if (contentBlocks.Count == 0)
        {
            contentBlocks.Add(new MessageInputTextBlock(
                "No photos or notes were provided. Create a placeholder item."));
        }

        _logger.LogInformation(
            "Sending message to agent for capture {CaptureId}: {BlockCount} content blocks ({PhotoCount} photos, noteLength={NoteLength}, hasLocation={HasLocation})",
            capture.Id, contentBlocks.Count, capture.Photos.Count, capture.UserNote?.Length ?? 0, capture.Location != null);

        await _agentsClient.Messages.CreateMessageAsync(
            thread.Value.Id,
            MessageRole.User,
            contentBlocks);

        var run = await _agentsClient.Runs.CreateRunAsync(
            thread.Value.Id, agent.Value.Id);
        _logger.LogDebug("Agent run started: runId={RunId}, threadId={ThreadId} for capture {CaptureId}",
            run.Value.Id, thread.Value.Id, capture.Id);

        var previousStatus = run.Value.Status.ToString();
        do
        {
            await Task.Delay(1000);
            run = await _agentsClient.Runs.GetRunAsync(thread.Value.Id, run.Value.Id);
            var currentStatus = run.Value.Status.ToString();
            if (currentStatus != previousStatus)
            {
                _logger.LogDebug("Agent run status changed: {PreviousStatus} -> {CurrentStatus} for capture {CaptureId}, runId={RunId}",
                    previousStatus, currentStatus, capture.Id, run.Value.Id);
                previousStatus = currentStatus;
            }
        } while (run.Value.Status == RunStatus.InProgress
              || run.Value.Status == RunStatus.Queued);

        activity?.SetTag("agent.run_status", run.Value.Status.ToString());

        if (run.Value.Status != RunStatus.Completed)
        {
            _logger.LogError("Agent run failed with status {Status} for capture {CaptureId}, runId={RunId} — falling back to local extraction",
                run.Value.Status, capture.Id, run.Value.Id);
            return await ProcessWithLocalExtractionAsync(capture);
        }

        _logger.LogDebug("Agent run completed successfully for capture {CaptureId}, runId={RunId} — retrieving response",
            capture.Id, run.Value.Id);

        PersistentThreadMessage? assistantMessage = null;
        await foreach (var message in _agentsClient.Messages.GetMessagesAsync(thread.Value.Id))
        {
            if (message.Role == MessageRole.Agent)
            {
                assistantMessage = message;
                break;
            }
        }

        if (assistantMessage == null)
        {
            _logger.LogWarning("No assistant message found in thread {ThreadId} for capture {CaptureId} — falling back to local extraction",
                thread.Value.Id, capture.Id);
            return await ProcessWithLocalExtractionAsync(capture);
        }

        var responseText = string.Join("", assistantMessage.ContentItems
            .OfType<MessageTextContent>()
            .Select(c => c.Text));

        _logger.LogDebug("Agent raw response for capture {CaptureId}: length={ResponseLength}, preview={ResponsePreview}",
            capture.Id, responseText.Length, responseText.Length > 200 ? responseText[..200] + "..." : responseText);

        _logger.LogDebug("Cleaning up agent {AgentId} for capture {CaptureId}", agent.Value.Id, capture.Id);
        await admin.DeleteAgentAsync(agent.Value.Id);

        return ParseAgentResponse(responseText, capture);
    }

    private List<Item> ParseAgentResponse(string responseText, Capture capture)
    {
        try
        {
            var jsonText = responseText;
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                jsonText = responseText[jsonStart..(jsonEnd + 1)];
            }

            _logger.LogDebug("Parsing agent JSON response for capture {CaptureId}: extractedJsonLength={JsonLength}",
                capture.Id, jsonText.Length);

            var parsed = JsonSerializer.Deserialize<List<AgentItemResult>>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed == null || parsed.Count == 0)
            {
                _logger.LogWarning("Agent returned empty/null parsed result for capture {CaptureId} — falling back to local extraction",
                    capture.Id);
                return ProcessWithLocalExtractionFallback(capture);
            }

            _logger.LogInformation("Agent response parsed successfully for capture {CaptureId}: {ParsedCount} items extracted, types=[{Types}]",
                capture.Id, parsed.Count, string.Join(", ", parsed.Select(p => p.Type ?? "null").Distinct()));

            return parsed.Select(p => new Item
            {
                UserId = capture.UserId,
                CaptureId = capture.Id,
                Type = NormalizeType(p.Type),
                Name = p.Name ?? "Unknown Item",
                Brand = p.Brand,
                Category = p.Category,
                Details = p.Details != null ? JsonSerializer.SerializeToElement(p.Details) : null,
                Venue = p.Venue != null ? new VenueInfo
                {
                    Name = p.Venue.Name ?? "Unknown Venue",
                    Address = p.Venue.Address
                } : null,
                PhotoUrls = capture.Photos,
                AiConfidence = p.Confidence ?? 0.8,
                AiSummary = p.Summary,
                Tags = p.Tags ?? [],
                Status = ItemStatus.AiDraft
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse agent response for capture {CaptureId}: {ErrorMessage} — falling back to local extraction",
                capture.Id, ex.Message);
            return ProcessWithLocalExtractionFallback(capture);
        }
    }

    private List<Item> ProcessWithLocalExtractionFallback(Capture capture)
    {
        _logger.LogDebug("Triggering synchronous local extraction fallback for capture {CaptureId}", capture.Id);
        return ProcessWithLocalExtractionAsync(capture).GetAwaiter().GetResult();
    }

    private Task<List<Item>> ProcessWithLocalExtractionAsync(Capture capture)
    {
        _logger.LogInformation("Running local extraction for capture {CaptureId}: noteLength={NoteLength}, photoCount={PhotoCount}",
            capture.Id, capture.UserNote?.Trim().Length ?? 0, capture.Photos.Count);

        var items = new List<Item>();
        var note = capture.UserNote?.Trim() ?? "";

        if (string.IsNullOrEmpty(note) && capture.Photos.Count == 0)
        {
            _logger.LogDebug("Empty capture {CaptureId} — creating placeholder item", capture.Id);
            items.Add(CreatePlaceholderItem(capture, "Empty Capture", "No photos or notes provided."));
            return Task.FromResult(items);
        }

        var extracted = ExtractItemsFromNote(note, capture);
        if (extracted.Count > 0)
        {
            _logger.LogInformation("Local extraction for capture {CaptureId}: matched {ExtractedCount} items from note keywords",
                capture.Id, extracted.Count);
            items.AddRange(extracted);
        }
        else if (capture.Photos.Count > 0 || !string.IsNullOrEmpty(note))
        {
            var type = GuessTypeFromNote(note);
            items.Add(new Item
            {
                UserId = capture.UserId,
                CaptureId = capture.Id,
                Type = type,
                Name = !string.IsNullOrEmpty(note) ? TruncateForName(note) : "Photo Capture",
                PhotoUrls = capture.Photos,
                AiConfidence = 0.3,
                AiSummary = !string.IsNullOrEmpty(note)
                    ? $"Captured with note: \"{note}\". AI analysis pending — configure AI Foundry for full extraction."
                    : "Photo captured. AI analysis pending — configure AI Foundry for full extraction.",
                Venue = capture.Location != null ? new VenueInfo
                {
                    Name = $"Location: {capture.Location.Latitude:F4}, {capture.Location.Longitude:F4}"
                } : null,
                Tags = [],
                Status = ItemStatus.AiDraft
            });
        }

        return Task.FromResult(items);
    }

    private List<Item> ExtractItemsFromNote(string note, Capture capture)
    {
        var items = new List<Item>();
        var lowerNote = note.ToLowerInvariant();

        var patterns = new Dictionary<string, (string type, string[] keywords)>
        {
            ["whiskey"] = ("whiskey", ["whiskey", "whisky", "bourbon", "scotch", "rye", "single malt", "highland", "speyside", "islay",
                "lagavulin", "macallan", "glenfiddich", "jameson", "maker's mark", "buffalo trace", "woodford",
                "bulleit", "wild turkey", "jack daniel", "johnny walker", "laphroaig", "ardbeg", "oban",
                "glenlivet", "balvenie", "talisker", "yamazaki", "hibiki", "blanton", "eagle rare",
                "knob creek", "four roses", "elijah craig", "weller", "pappy"]),
            ["wine"] = ("wine", ["wine", "cabernet", "merlot", "pinot noir", "pinot grigio", "chardonnay",
                "sauvignon blanc", "riesling", "malbec", "syrah", "shiraz", "zinfandel", "tempranillo",
                "sangiovese", "rosé", "prosecco", "champagne", "cava", "bordeaux", "burgundy", "barolo",
                "rioja", "chianti", "port", "sherry"]),
            ["cocktail"] = ("cocktail", ["cocktail", "old fashioned", "manhattan", "martini", "negroni", "margarita",
                "daiquiri", "mojito", "cosmopolitan", "mai tai", "whiskey sour", "gin and tonic",
                "bloody mary", "paloma", "espresso martini", "aperol spritz", "sazerac",
                "mint julep", "sidecar", "gimlet", "tom collins", "moscow mule", "last word",
                "paper plane", "penicillin", "boulevardier"]),
            ["cigar"] = ("cigar", ["cigar", "cohiba", "montecristo", "partagas", "romeo y julieta", "padron",
                "arturo fuente", "davidoff", "oliva", "my father", "liga privada", "opus x",
                "ashton", "rocky patel", "perdomo", "macanudo", "punch", "hoyo de monterrey",
                "robusto", "torpedo", "churchill", "corona", "toro", "maduro", "connecticut"])
        };

        var matchedTypes = new HashSet<string>();

        foreach (var (category, (type, keywords)) in patterns)
        {
            foreach (var keyword in keywords)
            {
                if (lowerNote.Contains(keyword) && matchedTypes.Add(category))
                {
                    var matchedKeyword = keywords
                        .Where(k => k.Length > 3 && lowerNote.Contains(k))
                        .OrderByDescending(k => k.Length)
                        .FirstOrDefault() ?? keyword;

                    var name = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                        .ToTitleCase(matchedKeyword);

                    items.Add(new Item
                    {
                        UserId = capture.UserId,
                        CaptureId = capture.Id,
                        Type = type,
                        Name = name,
                        PhotoUrls = capture.Photos,
                        AiConfidence = 0.5,
                        AiSummary = $"Extracted from note: \"{note}\". Matched keyword: {matchedKeyword}. Configure AI Foundry for richer analysis.",
                        Venue = capture.Location != null ? new VenueInfo
                        {
                            Name = $"Location: {capture.Location.Latitude:F4}, {capture.Location.Longitude:F4}"
                        } : null,
                        Tags = [],
                        Status = ItemStatus.AiDraft
                    });
                    break;
                }
            }
        }

        return items;
    }

    private static string GuessTypeFromNote(string note)
    {
        var lower = note.ToLowerInvariant();
        if (lower.Contains("cigar") || lower.Contains("smoke") || lower.Contains("puff")) return ItemType.Cigar;
        if (lower.Contains("wine") || lower.Contains("cabernet") || lower.Contains("merlot") || lower.Contains("chardonnay")) return ItemType.Wine;
        if (lower.Contains("cocktail") || lower.Contains("mixed") || lower.Contains("old fashioned") || lower.Contains("martini")) return ItemType.Cocktail;
        if (lower.Contains("whiskey") || lower.Contains("whisky") || lower.Contains("bourbon") || lower.Contains("scotch")) return ItemType.Whiskey;
        return "unknown";
    }

    private static string TruncateForName(string text)
    {
        if (text.Length <= 60) return text;
        return text[..57] + "...";
    }

    private static Item CreatePlaceholderItem(Capture capture, string name, string summary)
    {
        return new Item
        {
            UserId = capture.UserId,
            CaptureId = capture.Id,
            Type = "unknown",
            Name = name,
            AiSummary = summary,
            PhotoUrls = capture.Photos,
            AiConfidence = 0.0,
            Status = ItemStatus.AiDraft
        };
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
}

// DTO for parsing agent JSON responses
internal class AgentItemResult
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, object>? Details { get; set; }
    public AgentVenueResult? Venue { get; set; }
    public double? Confidence { get; set; }
    public string? Summary { get; set; }
    public List<string>? Tags { get; set; }
}

internal class AgentVenueResult
{
    public string? Name { get; set; }
    public string? Address { get; set; }
}
