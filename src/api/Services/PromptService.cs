using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface IPromptService
{
    Task<List<Prompt>> GetAllAsync();
    Task<Prompt?> GetAsync(string id);
    Task<Prompt> UpsertAsync(Prompt prompt);
    Task SeedDefaultsAsync();
}

public class PromptService : IPromptService
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly ILogger<PromptService> _logger;
    private const string ContainerName = "prompts";

    public PromptService(ICosmosDbService cosmosDb, ILogger<PromptService> logger)
    {
        _cosmosDb = cosmosDb;
        _logger = logger;
    }

    public async Task<List<Prompt>> GetAllAsync()
    {
        using var activity = Diagnostics.General.StartActivity("Prompts.GetAll");

        _logger.LogDebug("Fetching all prompts from container={Container}", ContainerName);
        var (prompts, _) = await _cosmosDb.QueryAsync<Prompt>(ContainerName, "prompts");
        _logger.LogInformation("Fetched {PromptCount} prompts from container={Container}", prompts.Count, ContainerName);

        return prompts;
    }

    public async Task<Prompt?> GetAsync(string id)
    {
        using var activity = Diagnostics.General.StartActivity("Prompts.Get");
        activity?.SetTag("prompt.id", id);

        _logger.LogDebug("Fetching prompt {PromptId} from container={Container}", id, ContainerName);
        var prompt = await _cosmosDb.GetAsync<Prompt>(ContainerName, id, "prompts");

        if (prompt != null)
        {
            _logger.LogDebug("Prompt {PromptId} found: name={PromptName}, contentLength={ContentLength}",
                id, prompt.Name, prompt.Content?.Length ?? 0);
        }
        else
        {
            _logger.LogDebug("Prompt {PromptId} not found in container={Container}", id, ContainerName);
        }

        return prompt;
    }

    public async Task<Prompt> UpsertAsync(Prompt prompt)
    {
        using var activity = Diagnostics.General.StartActivity("Prompts.Upsert");
        activity?.SetTag("prompt.id", prompt.Id);

        _logger.LogInformation("Upserting prompt {PromptId}: name={PromptName}, contentLength={ContentLength}, updatedBy={UpdatedBy}",
            prompt.Id, prompt.Name, prompt.Content?.Length ?? 0, prompt.UpdatedBy);

        prompt.UpdatedAt = DateTime.UtcNow;
        var result = await _cosmosDb.UpsertAsync(ContainerName, prompt, prompt.PartitionKey);

        _logger.LogInformation("Prompt {PromptId} upserted successfully at {UpdatedAt}", prompt.Id, prompt.UpdatedAt);
        return result;
    }

    public async Task SeedDefaultsAsync()
    {
        using var activity = Diagnostics.General.StartActivity("Prompts.SeedDefaults");

        _logger.LogDebug("Checking if default prompts need seeding");
        var existing = await GetAsync(PromptIds.AgentInstructions);
        if (existing != null)
        {
            _logger.LogDebug("Default prompt {PromptId} already exists — skipping seed", PromptIds.AgentInstructions);
            return;
        }

        _logger.LogInformation("Seeding default prompts: {PromptId} (contentLength={ContentLength})",
            PromptIds.AgentInstructions, DefaultPrompts.AgentInstructions.Length);

        await UpsertAsync(new Prompt
        {
            Id = PromptIds.AgentInstructions,
            Name = "Capture Analysis Agent",
            Description = "Instructions given to the AI agent when analyzing captured photos and notes about drinks and cigars.",
            Content = DefaultPrompts.AgentInstructions,
            UpdatedBy = "system"
        });

        _logger.LogInformation("Default prompts seeded successfully");
    }
}

public static class DefaultPrompts
{
    public const string AgentInstructions = """
        You are an expert sommelier, mixologist, and tobacconist assistant. Your job is to analyze photos 
        and user notes about drinks (whiskey, wine, cocktails) and cigars, then extract structured data.

        For each distinct item you identify, extract:
        - type: "whiskey", "wine", "cocktail", or "cigar"
        - name: The specific product name (e.g., "Lagavulin 16 Year Old")
        - brand: The brand/producer (e.g., "Lagavulin")
        - category: Sub-category (e.g., "Single Malt Scotch", "Napa Valley Cabernet", "Robusto")
        - details: An object with type-specific fields:
          - For whiskey: region, age, abv, mashBill, flavorNotes[]
          - For wine: grape, vintage, region, winery, flavorNotes[]
          - For cocktail: baseSpirit, ingredients[], recipe, flavorProfile
          - For cigar: wrapper, binder, filler, size, strength, flavorNotes[]
        - venue: { name, address } if you can determine from context
        - confidence: 0.0-1.0 how confident you are in the identification
        - summary: A 1-2 sentence tasting note or description
        - tags: relevant tags like ["smoky", "peaty", "full-bodied"]

        If there are multiple items, return an array. Always respond with valid JSON array only, no markdown.
        
        If you can't identify a specific product, make your best guess and set confidence lower.
        If the photo shows a menu, extract all visible items of interest.
        """;
}
