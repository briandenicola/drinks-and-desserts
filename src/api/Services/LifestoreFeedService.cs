using System.Text;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// The read-only feed the household life store reads in full on each poll: every item and venue
/// the key's owner has, one container after the other, in the order storage returns them. It
/// changes nothing. Photos, capture links, AI processing internals and friends' thoughts are left
/// out: the life store hands what it reads to agents, and thoughts are other people's words.
/// </summary>
public interface ILifestoreFeedService
{
    Task<LifestoreFeedPage> PageAsync(string userId, string? token, int limit, CancellationToken ct = default);
}

public class LifestoreFeedService(ICosmosDbService cosmosDb) : ILifestoreFeedService
{
    public const string Schema = "drinksanddesserts.lifestore.records/1";
    public const int DefaultLimit = 100;
    public const int MaxLimit = 200;

    // Kinds are read in this order; the token says which one a page is in.
    private static readonly string[] Kinds = ["item", "venue"];

    public async Task<LifestoreFeedPage> PageAsync(string userId, string? token, int limit, CancellationToken ct = default)
    {
        var (kind, continuation) = DecodeToken(token);
        limit = Math.Clamp(limit <= 0 ? DefaultLimit : limit, 1, MaxLimit);

        List<LifestoreRecord> records;
        string? next;
        try
        {
            (records, next) = kind == "item"
                ? await ReadAsync<Item>("items", userId, continuation, limit, ToRecord)
                : await ReadAsync<Venue>("venues", userId, continuation, limit, ToRecord);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // A continuation Cosmos does not recognise: one this feed did not give out.
            throw new LifestoreTokenException();
        }

        string? nextToken = null;
        if (next is not null)
        {
            nextToken = EncodeToken(kind, next);
        }
        else if (Array.IndexOf(Kinds, kind) + 1 < Kinds.Length)
        {
            nextToken = EncodeToken(Kinds[Array.IndexOf(Kinds, kind) + 1], null);
        }

        return new LifestoreFeedPage(Schema, records, nextToken);
    }

    private async Task<(List<LifestoreRecord>, string?)> ReadAsync<T>(
        string container, string userId, string? continuation, int limit, Func<T, LifestoreRecord> map)
    {
        var (items, next) = await cosmosDb.QueryAsync<T>(container, userId, continuation, limit);
        return (items.Select(map).ToList(), next);
    }

    private static LifestoreRecord ToRecord(Item item) => new("item", item.Id, item.UpdatedAt, new LifestoreItem(
        item.Id, item.Type, item.Name, item.Brand, item.Category, item.Details, item.Venue,
        item.UserRating, item.UserNotes, item.AiSummary, item.Journal, item.Tags, item.Status,
        item.SourceAttribution?.SourceDisplayName, item.CreatedAt, item.UpdatedAt));

    private static LifestoreRecord ToRecord(Venue venue) => new("venue", venue.Id, venue.UpdatedAt, new LifestoreVenue(
        venue.Id, venue.Name, venue.Address, venue.Website, venue.Type, venue.Rating, venue.Location,
        venue.Labels, venue.Status, venue.SourceAttribution?.SourceDisplayName, venue.CreatedAt, venue.UpdatedAt));

    private record Token(string K, string? C);

    private static string EncodeToken(string kind, string? continuation)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new Token(kind, continuation));
        return Convert.ToBase64String(json).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static (string Kind, string? Continuation) DecodeToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return (Kinds[0], null);
        }

        try
        {
            var base64 = token.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var decoded = JsonSerializer.Deserialize<Token>(Convert.FromBase64String(base64));
            if (decoded is not null && Kinds.Contains(decoded.K))
            {
                return (decoded.K, decoded.C);
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException or DecoderFallbackException)
        {
            // Falls through to the refusal below: the token is not one this feed gave out.
        }

        throw new LifestoreTokenException();
    }
}

public class LifestoreTokenException()
    : Exception("The page token is not one this feed gave out. Start again without a page.");

public record LifestoreFeedPage(string Schema, IReadOnlyList<LifestoreRecord> Records, string? Next);

public record LifestoreRecord(string Kind, string Id, DateTime UpdatedAt, object Data);

public record LifestoreItem(
    string Id, string Type, string Name, string? Brand, string? Category, JsonElement? Details, VenueInfo? Venue,
    double? Rating, string? Notes, string? AiSummary, List<JournalEntry> Journal, List<string> Tags, string Status,
    string? SharedBy, DateTime CreatedAt, DateTime UpdatedAt);

public record LifestoreVenue(
    string Id, string Name, string? Address, string? Website, string Type, double? Rating, GeoLocation? Location,
    List<string> Labels, string Status, string? SharedBy, DateTime CreatedAt, DateTime UpdatedAt);
