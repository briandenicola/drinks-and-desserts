using System.Diagnostics;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Linq.Expressions;
using WhiskeyAndSmokes.Api;

namespace WhiskeyAndSmokes.Api.Services;

public interface ICosmosDbService
{
    Task<T?> GetAsync<T>(string containerName, string id, string partitionKey);
    Task<T> CreateAsync<T>(string containerName, T item, string partitionKey);
    Task<T> UpsertAsync<T>(string containerName, T item, string partitionKey);
    Task DeleteAsync(string containerName, string id, string partitionKey);
    Task<(List<T> Items, string? ContinuationToken)> QueryAsync<T>(
        string containerName,
        string partitionKey,
        string? continuationToken = null,
        int maxItems = 25,
        Expression<Func<T, bool>>? predicate = null);
    Task<List<T>> QueryCrossPartitionAsync<T>(string containerName, string query, int maxItems = 100);
    Task<List<T>> QueryCrossPartitionAsync<T>(string containerName, string query, IDictionary<string, object> parameters, int maxItems = 100);
}

public class CosmosDbService : ICosmosDbService
{
    private readonly CosmosClient _client;
    private readonly Database _database;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(CosmosClient client, IConfiguration config, ILogger<CosmosDbService> logger)
    {
        _client = client;
        _database = _client.GetDatabase(config["CosmosDb:DatabaseName"] ?? "whiskey-and-smokes");
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string containerName, string id, string partitionKey)
    {
        using var activity = Diagnostics.Storage.StartActivity("CosmosDb.Get");
        activity?.SetTag("db.container", containerName);
        activity?.SetTag("db.operation", "Get");
        activity?.SetTag("db.item_id", id);

        _logger.LogDebug("CosmosDb GET: container={Container}, id={Id}, partitionKey={PartitionKey}",
            containerName, id, partitionKey);

        var container = _database.GetContainer(containerName);
        try
        {
            var response = await container.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
            _logger.LogDebug("CosmosDb GET success: container={Container}, id={Id}, RU={RequestCharge}",
                containerName, id, response.RequestCharge);
            activity?.SetTag("db.request_charge", response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("CosmosDb GET not found: container={Container}, id={Id}, RU={RequestCharge}",
                containerName, id, ex.RequestCharge);
            activity?.SetTag("db.request_charge", ex.RequestCharge);
            return default;
        }
    }

    public async Task<T> CreateAsync<T>(string containerName, T item, string partitionKey)
    {
        using var activity = Diagnostics.Storage.StartActivity("CosmosDb.Create");
        activity?.SetTag("db.container", containerName);
        activity?.SetTag("db.operation", "Create");

        _logger.LogDebug("CosmosDb CREATE: container={Container}, partitionKey={PartitionKey}",
            containerName, partitionKey);

        var container = _database.GetContainer(containerName);
        var response = await container.CreateItemAsync(item, new PartitionKey(partitionKey));

        _logger.LogDebug("CosmosDb CREATE success: container={Container}, partitionKey={PartitionKey}, RU={RequestCharge}",
            containerName, partitionKey, response.RequestCharge);
        activity?.SetTag("db.request_charge", response.RequestCharge);

        return response.Resource;
    }

    public async Task<T> UpsertAsync<T>(string containerName, T item, string partitionKey)
    {
        using var activity = Diagnostics.Storage.StartActivity("CosmosDb.Upsert");
        activity?.SetTag("db.container", containerName);
        activity?.SetTag("db.operation", "Upsert");

        _logger.LogDebug("CosmosDb UPSERT: container={Container}, partitionKey={PartitionKey}",
            containerName, partitionKey);

        var container = _database.GetContainer(containerName);
        var response = await container.UpsertItemAsync(item, new PartitionKey(partitionKey));

        _logger.LogDebug("CosmosDb UPSERT success: container={Container}, partitionKey={PartitionKey}, RU={RequestCharge}",
            containerName, partitionKey, response.RequestCharge);
        activity?.SetTag("db.request_charge", response.RequestCharge);

        return response.Resource;
    }

    public async Task DeleteAsync(string containerName, string id, string partitionKey)
    {
        using var activity = Diagnostics.Storage.StartActivity("CosmosDb.Delete");
        activity?.SetTag("db.container", containerName);
        activity?.SetTag("db.operation", "Delete");
        activity?.SetTag("db.item_id", id);

        _logger.LogDebug("CosmosDb DELETE: container={Container}, id={Id}, partitionKey={PartitionKey}",
            containerName, id, partitionKey);

        var container = _database.GetContainer(containerName);
        await container.DeleteItemAsync<object>(id, new PartitionKey(partitionKey));

        _logger.LogInformation("CosmosDb DELETE success: container={Container}, id={Id}", containerName, id);
    }

    public async Task<(List<T> Items, string? ContinuationToken)> QueryAsync<T>(
        string containerName,
        string partitionKey,
        string? continuationToken = null,
        int maxItems = 25,
        Expression<Func<T, bool>>? predicate = null)
    {
        using var activity = Diagnostics.Storage.StartActivity("CosmosDb.Query");
        activity?.SetTag("db.container", containerName);
        activity?.SetTag("db.operation", "Query");

        _logger.LogDebug("CosmosDb QUERY: container={Container}, partitionKey={PartitionKey}, maxItems={MaxItems}, hasPredicate={HasPredicate}, hasContinuation={HasContinuation}",
            containerName, partitionKey, maxItems, predicate != null, continuationToken != null);

        var container = _database.GetContainer(containerName);
        var queryable = container.GetItemLinqQueryable<T>(
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(partitionKey),
                MaxItemCount = maxItems
            },
            continuationToken: continuationToken);

        if (predicate != null)
            queryable = (IOrderedQueryable<T>)queryable.Where(predicate);

        var iterator = queryable.ToFeedIterator();
        var results = new List<T>();
        string? nextToken = null;
        double totalRu = 0;

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
            nextToken = response.ContinuationToken;
            totalRu += response.RequestCharge;
        }

        activity?.SetTag("db.item_count", results.Count);
        activity?.SetTag("db.request_charge", totalRu);
        _logger.LogDebug("CosmosDb QUERY result: container={Container}, partitionKey={PartitionKey}, itemCount={ItemCount}, RU={RequestCharge}, hasMore={HasMore}",
            containerName, partitionKey, results.Count, totalRu, nextToken != null);

        return (results, nextToken);
    }

    public async Task<List<T>> QueryCrossPartitionAsync<T>(string containerName, string query, int maxItems = 100)
    {
        using var activity = Diagnostics.Storage.StartActivity("CosmosDb.QueryCrossPartition");
        activity?.SetTag("db.container", containerName);
        activity?.SetTag("db.operation", "QueryCrossPartition");

        var queryPreview = query.Length > 200 ? query[..200] + "..." : query;
        _logger.LogDebug("CosmosDb CROSS-PARTITION QUERY: container={Container}, maxItems={MaxItems}, query={Query}",
            containerName, maxItems, queryPreview);

        var container = _database.GetContainer(containerName);
        var queryDef = new QueryDefinition(query);
        var iterator = container.GetItemQueryIterator<T>(queryDef, requestOptions: new QueryRequestOptions { MaxItemCount = maxItems });
        var results = new List<T>();
        double totalRu = 0;

        while (iterator.HasMoreResults && results.Count < maxItems)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
            totalRu += response.RequestCharge;
        }

        activity?.SetTag("db.item_count", results.Count);
        activity?.SetTag("db.request_charge", totalRu);
        _logger.LogDebug("CosmosDb CROSS-PARTITION QUERY result: container={Container}, itemCount={ItemCount}, totalRU={RequestCharge}",
            containerName, results.Count, totalRu);

        return results;
    }

    public async Task<List<T>> QueryCrossPartitionAsync<T>(string containerName, string query, IDictionary<string, object> parameters, int maxItems = 100)
    {
        using var activity = Diagnostics.Storage.StartActivity("CosmosDb.QueryCrossPartition");
        activity?.SetTag("db.container", containerName);
        activity?.SetTag("db.operation", "QueryCrossPartition");

        _logger.LogDebug("CosmosDb PARAMETERIZED CROSS-PARTITION QUERY: container={Container}, maxItems={MaxItems}",
            containerName, maxItems);

        var container = _database.GetContainer(containerName);
        var queryDef = new QueryDefinition(query);
        foreach (var (key, value) in parameters)
        {
            queryDef = queryDef.WithParameter(key, value);
        }
        var iterator = container.GetItemQueryIterator<T>(queryDef, requestOptions: new QueryRequestOptions { MaxItemCount = maxItems });
        var results = new List<T>();
        double totalRu = 0;

        while (iterator.HasMoreResults && results.Count < maxItems)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
            totalRu += response.RequestCharge;
        }

        activity?.SetTag("db.item_count", results.Count);
        activity?.SetTag("db.request_charge", totalRu);

        return results;
    }
}
