using System.Text.Json.Serialization;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// One-time data fix: items edited before the edit form started promoting drafts are stuck
/// in "ai-draft". Marks every existing draft as reviewed, then records a marker in the
/// "settings" container so later startups leave newly captured drafts alone.
/// </summary>
public static class AiDraftResetMigration
{
    private const string ItemsContainer = "items";
    private const string SettingsContainer = "settings";
    private const string NeedsReviewTag = "needs-review";
    private const int BatchSize = 500;
    private const int MaxBatches = 200;

    public static async Task RunOnceAsync(ICosmosDbService db, ILogger logger)
    {
        try
        {
            var marker = await db.GetAsync<AiDraftResetMarker>(
                SettingsContainer, AiDraftResetMarker.DocumentId, AiDraftResetMarker.PartitionKeyValue);
            if (marker != null) return;

            var reset = 0;
            for (var batch = 0; batch < MaxBatches; batch++)
            {
                // Updated items no longer match, so each pass picks up the next set.
                var drafts = await db.QueryCrossPartitionAsync<Item>(
                    ItemsContainer,
                    $"SELECT * FROM c WHERE c.status = '{ItemStatus.AiDraft}'",
                    maxItems: BatchSize);
                if (drafts.Count == 0) break;

                foreach (var item in drafts)
                {
                    item.Status = ItemStatus.Reviewed;
                    item.Tags.Remove(NeedsReviewTag);
                    // UpdatedAt is left alone so the sweep does not reorder "recently updated" lists.
                    await db.UpsertAsync(ItemsContainer, item, item.PartitionKey);
                    reset++;
                }
            }

            var done = new AiDraftResetMarker { ItemsReset = reset };
            await db.UpsertAsync(SettingsContainer, done, done.PartitionKey);
            logger.LogInformation("AI draft reset complete: {Count} items marked reviewed", reset);
        }
        catch (Exception ex)
        {
            // No marker is written on failure, so the next startup retries.
            logger.LogWarning(ex, "AI draft reset failed; it will be retried on next startup");
        }
    }
}

public class AiDraftResetMarker
{
    public const string DocumentId = "migration-ai-draft-reset";
    public const string PartitionKeyValue = "settings";

    [JsonPropertyName("id")]
    public string Id { get; set; } = DocumentId;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = PartitionKeyValue;

    [JsonPropertyName("itemsReset")]
    public int ItemsReset { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
