using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface ISearchCandidateProvider
{
    Task<SearchCandidates> GetCandidatesAsync(string userId, SearchInterpretation interpretation, CancellationToken cancellationToken = default);
}

public class SearchCandidates
{
    public List<Item> Items { get; set; } = [];
    public List<Venue> Venues { get; set; } = [];
}

public class CosmosSearchCandidateProvider : ISearchCandidateProvider
{
    private const int PageSize = 100;
    private readonly ICosmosDbService _cosmosDb;

    public CosmosSearchCandidateProvider(ICosmosDbService cosmosDb)
    {
        _cosmosDb = cosmosDb;
    }

    public async Task<SearchCandidates> GetCandidatesAsync(string userId, SearchInterpretation interpretation, CancellationToken cancellationToken = default)
    {
        var itemsTask = QueryAllAsync<Item>("items", userId, i => i.Status != ItemStatus.Wishlist);
        var venuesTask = QueryAllAsync<Venue>("venues", userId, null);
        await Task.WhenAll(itemsTask, venuesTask);

        return new SearchCandidates
        {
            Items = itemsTask.Result,
            Venues = venuesTask.Result
        };
    }

    private async Task<List<T>> QueryAllAsync<T>(
        string containerName,
        string userId,
        System.Linq.Expressions.Expression<Func<T, bool>>? predicate)
    {
        var all = new List<T>();
        string? token = null;
        do
        {
            var (items, nextToken) = await _cosmosDb.QueryAsync<T>(
                containerName,
                userId,
                token,
                PageSize,
                predicate);
            all.AddRange(items);
            token = nextToken;
        } while (token != null);

        return all;
    }
}
