using System.Globalization;
using System.Text.RegularExpressions;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface ISearchService
{
    Task<SearchResponse> SearchAsync(string userId, SearchRequest request);
}

public class SearchService : ISearchService
{
    private const int PageSize = 100;
    private static readonly string[] BeverageTypes =
    [
        ItemType.Whiskey, ItemType.Wine, ItemType.Cocktail, ItemType.Vodka, ItemType.Gin,
        ItemType.Espresso, ItemType.Latte, ItemType.Cappuccino, ItemType.ColdBrew, ItemType.PourOver, ItemType.Coffee
    ];

    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["january"] = 1, ["jan"] = 1,
        ["february"] = 2, ["feb"] = 2,
        ["march"] = 3, ["mar"] = 3,
        ["april"] = 4, ["apr"] = 4,
        ["may"] = 5,
        ["june"] = 6, ["jun"] = 6,
        ["july"] = 7, ["jul"] = 7,
        ["august"] = 8, ["aug"] = 8,
        ["september"] = 9, ["sep"] = 9, ["sept"] = 9,
        ["october"] = 10, ["oct"] = 10,
        ["november"] = 11, ["nov"] = 11,
        ["december"] = 12, ["dec"] = 12
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "from", "back", "really", "good", "great", "might", "maybe",
        "had", "have", "was", "were", "but", "cant", "can't", "cannot", "remember", "anything", "about",
        "at", "in", "on", "it", "its", "for", "my", "me", "a", "an", "of", "to", "i"
    };

    private readonly ICosmosDbService _cosmosDb;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SearchService> _logger;

    public SearchService(ICosmosDbService cosmosDb, TimeProvider timeProvider, ILogger<SearchService> logger)
    {
        _cosmosDb = cosmosDb;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SearchResponse> SearchAsync(string userId, SearchRequest request)
    {
        var limit = Math.Clamp(request.Limit ?? 20, 1, 50);
        var interpretation = Interpret(request.Query);

        var itemsTask = QueryAllAsync<Item>("items", userId, i => i.Status != ItemStatus.Wishlist);
        var venuesTask = QueryAllAsync<Venue>("venues", userId, null);
        await Task.WhenAll(itemsTask, venuesTask);

        var venues = venuesTask.Result;
        var venuesById = venues.Where(v => !string.IsNullOrWhiteSpace(v.Id)).ToDictionary(v => v.Id, StringComparer.OrdinalIgnoreCase);
        var venuesByName = venues
            .Where(v => !string.IsNullOrWhiteSpace(v.Name))
            .GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var results = new List<SearchResult>();

        foreach (var item in itemsTask.Result)
        {
            var linkedVenue = ResolveVenue(item, venuesById, venuesByName);
            var (score, reasons) = ScoreItem(item, linkedVenue, interpretation);
            if (score > 0)
            {
                results.Add(new SearchResult
                {
                    Kind = "item",
                    Item = item,
                    Score = Math.Round(score, 3),
                    Reasons = reasons
                });
            }
        }

        foreach (var venue in venues)
        {
            var (score, reasons) = ScoreVenue(venue, interpretation);
            if (score > 0)
            {
                results.Add(new SearchResult
                {
                    Kind = "venue",
                    Venue = venue,
                    Score = Math.Round(score, 3),
                    Reasons = reasons
                });
            }
        }

        var ordered = results
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Item?.UserRating ?? r.Venue?.Rating ?? 0)
            .ThenByDescending(r => r.Item?.CreatedAt ?? r.Venue?.CreatedAt ?? DateTime.MinValue)
            .Take(limit)
            .ToList();

        _logger.LogInformation("Search returned {Count} results for user {UserId} with query hints: terms={Terms}, types={Types}, venues={Venues}",
            ordered.Count, userId, string.Join(",", interpretation.Terms), string.Join(",", interpretation.ItemTypes), string.Join(",", interpretation.VenueHints));

        return new SearchResponse
        {
            InterpretedQuery = interpretation,
            Results = ordered
        };
    }

    private SearchInterpretation Interpret(string query)
    {
        var normalized = Normalize(query);
        var interpretation = new SearchInterpretation
        {
            DateRange = ExtractDateRange(normalized)
        };

        foreach (var type in ItemType.All)
        {
            if (normalized.Contains(type, StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(type.Replace("-", " "), StringComparison.OrdinalIgnoreCase))
            {
                interpretation.ItemTypes.Add(type);
            }
        }

        if (Regex.IsMatch(normalized, @"\b(drink|cocktail|beverage|pour|coffee)\b", RegexOptions.IgnoreCase))
        {
            interpretation.ItemTypes = interpretation.ItemTypes
                .Concat(BeverageTypes)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        foreach (var venueType in VenueType.All)
        {
            if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(venueType)}\b", RegexOptions.IgnoreCase))
            {
                interpretation.VenueHints.Add(venueType);
            }
        }

        if (Regex.IsMatch(normalized, @"\b(good|great|excellent|amazing|best|favorite|favourite|loved|delicious|tasty|memorable)\b", RegexOptions.IgnoreCase))
        {
            interpretation.QualityHints.Add("highly rated or positive notes");
        }

        interpretation.Terms = Regex.Matches(normalized, "[a-z0-9][a-z0-9-]{2,}")
            .Select(m => m.Value)
            .Where(t => !StopWords.Contains(t) && !Months.ContainsKey(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        return interpretation;
    }

    private SearchDateRange? ExtractDateRange(string normalized)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (normalized.Contains("this month", StringComparison.OrdinalIgnoreCase))
        {
            return MonthRange(now.Year, now.Month, "this month");
        }

        if (normalized.Contains("last month", StringComparison.OrdinalIgnoreCase))
        {
            var lastMonth = now.AddMonths(-1);
            return MonthRange(lastMonth.Year, lastMonth.Month, "last month");
        }

        foreach (var (name, month) in Months)
        {
            if (!Regex.IsMatch(normalized, $@"\b{Regex.Escape(name)}\b", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var yearMatch = Regex.Match(normalized, $@"\b{Regex.Escape(name)}\b\s+(?<year>20\d{{2}})", RegexOptions.IgnoreCase);
            var year = yearMatch.Success ? int.Parse(yearMatch.Groups["year"].Value) : now.Year;
            if (!yearMatch.Success && month > now.Month)
            {
                year--;
            }

            return MonthRange(year, month, CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month));
        }

        return null;
    }

    private static SearchDateRange MonthRange(int year, int month, string label)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new SearchDateRange
        {
            Start = start,
            End = start.AddMonths(1).AddTicks(-1),
            Label = label
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

    private static Venue? ResolveVenue(Item item, Dictionary<string, Venue> venuesById, Dictionary<string, Venue> venuesByName)
    {
        if (item.Venue?.VenueId != null && venuesById.TryGetValue(item.Venue.VenueId, out var venueById))
        {
            return venueById;
        }

        return item.Venue?.Name != null && venuesByName.TryGetValue(item.Venue.Name, out var venueByName)
            ? venueByName
            : null;
    }

    private static (double Score, List<string> Reasons) ScoreItem(Item item, Venue? venue, SearchInterpretation interpretation)
    {
        var score = 0.0;
        var reasons = new List<string>();

        if (interpretation.DateRange != null && IsWithin(item.CreatedAt, interpretation.DateRange))
        {
            score += 0.35;
            reasons.Add($"Added in {interpretation.DateRange.Label}");
        }

        if (interpretation.ItemTypes.Contains(item.Type, StringComparer.OrdinalIgnoreCase))
        {
            score += 0.2;
            reasons.Add($"Matches {item.Type} drink clue");
        }

        if (interpretation.VenueHints.Count > 0 && VenueMatches(item.Venue, venue, interpretation.VenueHints))
        {
            score += 0.25;
            reasons.Add($"Venue matches {string.Join(", ", interpretation.VenueHints)} clue");
        }

        if (interpretation.QualityHints.Count > 0)
        {
            if (item.UserRating >= 4.5)
            {
                score += 0.2;
                reasons.Add($"Rated {item.UserRating:0.#} stars");
            }
            else if (item.UserRating >= 4)
            {
                score += 0.15;
                reasons.Add($"Rated {item.UserRating:0.#} stars");
            }

            if (ContainsPositiveLanguage(BuildItemText(item)))
            {
                score += 0.1;
                reasons.Add("Notes sound positive");
            }
        }

        var termMatches = CountTermMatches(BuildItemText(item), interpretation.Terms);
        if (termMatches > 0)
        {
            score += Math.Min(0.2, termMatches * 0.05);
            reasons.Add($"Matches {termMatches} search term{(termMatches == 1 ? "" : "s")}");
        }

        return (score, reasons);
    }

    private static (double Score, List<string> Reasons) ScoreVenue(Venue venue, SearchInterpretation interpretation)
    {
        var score = 0.0;
        var reasons = new List<string>();

        if (interpretation.DateRange != null && IsWithin(venue.CreatedAt, interpretation.DateRange))
        {
            score += 0.2;
            reasons.Add($"Added in {interpretation.DateRange.Label}");
        }

        if (interpretation.VenueHints.Count > 0 && VenueMatches(null, venue, interpretation.VenueHints))
        {
            score += 0.35;
            reasons.Add($"Matches {string.Join(", ", interpretation.VenueHints)} venue clue");
        }

        if (interpretation.QualityHints.Count > 0 && venue.Rating >= 4)
        {
            score += venue.Rating >= 4.5 ? 0.2 : 0.15;
            reasons.Add($"Venue rated {venue.Rating:0.#} stars");
        }

        var termMatches = CountTermMatches(BuildVenueText(venue), interpretation.Terms);
        if (termMatches > 0)
        {
            score += Math.Min(0.2, termMatches * 0.05);
            reasons.Add($"Matches {termMatches} search term{(termMatches == 1 ? "" : "s")}");
        }

        return (score, reasons);
    }

    private static bool IsWithin(DateTime date, SearchDateRange range)
    {
        var utc = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return utc >= range.Start && utc <= range.End;
    }

    private static bool VenueMatches(VenueInfo? itemVenue, Venue? venue, List<string> hints)
    {
        var venueLabels = (IEnumerable<string>?)venue?.Labels ?? Array.Empty<string>();
        var text = Normalize(string.Join(' ', itemVenue?.Name, itemVenue?.Address, venue?.Name, venue?.Address, venue?.Type, string.Join(' ', venueLabels)));
        return hints.Any(h => text.Contains(h, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountTermMatches(string text, List<string> terms)
    {
        var normalized = Normalize(text);
        return terms.Count(t => normalized.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsPositiveLanguage(string text) =>
        Regex.IsMatch(text, @"\b(good|great|excellent|amazing|best|favorite|favourite|loved|delicious|tasty|memorable|balanced|smooth)\b", RegexOptions.IgnoreCase);

    private static string BuildItemText(Item item) =>
        string.Join(' ',
            item.Name,
            item.Brand,
            item.Type,
            item.Category,
            item.Venue?.Name,
            item.Venue?.Address,
            item.UserNotes,
            item.AiSummary,
            string.Join(' ', item.Tags),
            string.Join(' ', item.Journal.Select(j => j.Text)));

    private static string BuildVenueText(Venue venue) =>
        string.Join(' ',
            venue.Name,
            venue.Type,
            venue.Address,
            venue.Website,
            string.Join(' ', venue.Labels));

    private static string Normalize(string value) => value.ToLowerInvariant().Replace("’", "'");
}
