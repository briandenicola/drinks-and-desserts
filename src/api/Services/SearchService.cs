using System.Text.RegularExpressions;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface ISearchService
{
    Task<SearchResponse> SearchAsync(string userId, SearchRequest request);
}

public class SearchService : ISearchService
{
    private readonly ISearchIntentInterpreter _intentInterpreter;
    private readonly ISearchCandidateProvider _candidateProvider;
    private readonly ILogger<SearchService> _logger;

    public SearchService(ISearchIntentInterpreter intentInterpreter, ISearchCandidateProvider candidateProvider, ILogger<SearchService> logger)
    {
        _intentInterpreter = intentInterpreter;
        _candidateProvider = candidateProvider;
        _logger = logger;
    }

    public async Task<SearchResponse> SearchAsync(string userId, SearchRequest request)
    {
        var limit = Math.Clamp(request.Limit ?? 20, 1, 50);
        var interpretation = await _intentInterpreter.InterpretAsync(request.Query);

        var candidates = await _candidateProvider.GetCandidatesAsync(userId, interpretation);
        var venues = candidates.Venues;
        var venuesById = venues.Where(v => !string.IsNullOrWhiteSpace(v.Id)).ToDictionary(v => v.Id, StringComparer.OrdinalIgnoreCase);
        var venuesByName = venues
            .Where(v => !string.IsNullOrWhiteSpace(v.Name))
            .GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var results = new List<SearchResult>();

        foreach (var item in candidates.Items)
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
