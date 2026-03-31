using System.Linq.Expressions;
using System.Text.Json;
using LiteDB;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace WhiskeyAndSmokes.Api.Services;

public class LiteDbService : ICosmosDbService, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILogger<LiteDbService> _logger;

    public LiteDbService(IConfiguration config, ILogger<LiteDbService> logger)
    {
        var dbPath = config["LiteDb:Path"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "whiskey-and-smokes", "whiskey-and-smokes.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _db = new LiteDatabase(dbPath);
        _logger = logger;
        _logger.LogInformation("LiteDB initialized at {Path}", dbPath);
    }

    private ILiteCollection<BsonDocument> GetCollection(string containerName) =>
        _db.GetCollection(containerName);

    private static T Deserialize<T>(BsonDocument doc)
    {
        var json = JsonSerializer.Serialize(BsonToDict(doc));
        return JsonSerializer.Deserialize<T>(json)!;
    }

    private static BsonDocument Serialize<T>(T item)
    {
        var json = JsonSerializer.Serialize(item);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var doc = DictToBson(dict);

        // Use "id" field as LiteDB _id for direct lookups
        if (doc.ContainsKey("id"))
        {
            doc["_id"] = doc["id"];
        }

        return doc;
    }

    private static Dictionary<string, object?> BsonToDict(BsonDocument doc)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var kv in doc)
        {
            if (kv.Key == "_id") continue;
            dict[kv.Key] = BsonToObject(kv.Value);
        }
        return dict;
    }

    private static object? BsonToObject(BsonValue val)
    {
        return val.Type switch
        {
            BsonType.Null => null,
            BsonType.Boolean => val.AsBoolean,
            BsonType.Int32 => val.AsInt32,
            BsonType.Int64 => val.AsInt64,
            BsonType.Double => val.AsDouble,
            BsonType.Decimal => val.AsDecimal,
            BsonType.String => val.AsString,
            BsonType.DateTime => val.AsDateTime.ToString("o"),
            BsonType.Array => val.AsArray.Select(BsonToObject).ToList(),
            BsonType.Document => BsonToDict(val.AsDocument),
            _ => val.ToString()
        };
    }

    private static BsonDocument DictToBson(Dictionary<string, JsonElement> dict)
    {
        var doc = new BsonDocument();
        foreach (var kv in dict)
        {
            doc[kv.Key] = JsonElementToBson(kv.Value);
        }
        return doc;
    }

    private static BsonValue JsonElementToBson(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => BsonValue.Null,
            JsonValueKind.True => new BsonValue(true),
            JsonValueKind.False => new BsonValue(false),
            JsonValueKind.String => el.TryGetDateTime(out var dt) ? new BsonValue(dt) : new BsonValue(el.GetString()),
            JsonValueKind.Number => el.TryGetInt32(out var i) ? new BsonValue(i)
                : el.TryGetInt64(out var l) ? new BsonValue(l)
                : new BsonValue(el.GetDouble()),
            JsonValueKind.Array => new BsonArray(el.EnumerateArray().Select(JsonElementToBson)),
            JsonValueKind.Object => DictToBson(el.Deserialize<Dictionary<string, JsonElement>>()!),
            _ => new BsonValue(el.ToString())
        };
    }

    public Task<T?> GetAsync<T>(string containerName, string id, string partitionKey)
    {
        var col = GetCollection(containerName);
        var doc = col.FindById(id);
        return Task.FromResult(doc != null ? Deserialize<T>(doc) : default);
    }

    public Task<T> CreateAsync<T>(string containerName, T item, string partitionKey)
    {
        var col = GetCollection(containerName);
        var doc = Serialize(item);
        col.Insert(doc);
        return Task.FromResult(item);
    }

    public Task<T> UpsertAsync<T>(string containerName, T item, string partitionKey)
    {
        var col = GetCollection(containerName);
        var doc = Serialize(item);
        col.Upsert(doc);
        return Task.FromResult(item);
    }

    public Task DeleteAsync(string containerName, string id, string partitionKey)
    {
        var col = GetCollection(containerName);
        col.Delete(id);
        return Task.CompletedTask;
    }

    public Task<(List<T> Items, string? ContinuationToken)> QueryAsync<T>(
        string containerName,
        string partitionKey,
        string? continuationToken = null,
        int maxItems = 25,
        Expression<Func<T, bool>>? predicate = null)
    {
        var col = GetCollection(containerName);

        var allDocs = col.Find(Query.EQ("partitionKey", partitionKey)).ToList();
        var items = allDocs.Select(Deserialize<T>).ToList();

        if (predicate != null)
        {
            items = items.AsQueryable().Where(predicate).ToList();
        }

        // Simple pagination via offset encoded in continuation token
        int skip = 0;
        if (!string.IsNullOrEmpty(continuationToken) && int.TryParse(continuationToken, out var offset))
        {
            skip = offset;
        }

        var page = items.Skip(skip).Take(maxItems).ToList();
        string? nextToken = skip + maxItems < items.Count ? (skip + maxItems).ToString() : null;

        return Task.FromResult((page, nextToken));
    }

    public Task<List<T>> QueryCrossPartitionAsync<T>(string containerName, string query, int maxItems = 100)
    {
        var col = GetCollection(containerName);

        // Handle COUNT queries (used by AuthController for first-user check)
        if (query.Contains("COUNT", StringComparison.OrdinalIgnoreCase))
        {
            var count = col.Count();
            var countJson = JsonSerializer.SerializeToElement(count);
            return Task.FromResult(new List<T> { JsonSerializer.Deserialize<T>(countJson.GetRawText())! });
        }

        // For WHERE clause queries — parse simple "field = 'value'" patterns
        if (query.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
        {
            var whereMatch = System.Text.RegularExpressions.Regex.Match(
                query, @"WHERE\s+c\.(\w+)\s*=\s*'([^']*)'", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (whereMatch.Success)
            {
                var field = whereMatch.Groups[1].Value;
                var value = whereMatch.Groups[2].Value;
                var docs = col.Find(Query.EQ(field, value)).Take(maxItems).ToList();
                return Task.FromResult(docs.Select(Deserialize<T>).ToList());
            }
        }

        // Default: return all documents
        var allDocs = col.FindAll().Take(maxItems).ToList();
        var items = allDocs.Select(Deserialize<T>).ToList();

        // Handle ORDER BY DESC on createdAt (common pattern)
        if (query.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase) &&
            query.Contains("DESC", StringComparison.OrdinalIgnoreCase))
        {
            items.Reverse();
        }

        return Task.FromResult(items);
    }

    public Task<List<T>> QueryCrossPartitionAsync<T>(string containerName, string query, IDictionary<string, object> parameters, int maxItems = 100)
    {
        // LiteDB doesn't support parameterized Cosmos SQL — extract parameter values and
        // substitute them into the simple regex parser used by the non-parameterized overload.
        var resolved = query;
        foreach (var (key, value) in parameters)
        {
            resolved = resolved.Replace(key, $"'{value}'");
        }
        return QueryCrossPartitionAsync<T>(containerName, resolved, maxItems);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
