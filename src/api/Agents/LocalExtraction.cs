using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Agents;

/// <summary>
/// Keyword-based local extraction fallback when AI Foundry is not configured.
/// Matches known product names and categories from user notes.
/// </summary>
internal static class LocalExtraction
{
    public static List<Item> Process(Capture capture, ILogger logger)
    {
        logger.LogInformation("Running local extraction for capture {CaptureId}: noteLength={NoteLength}, photoCount={PhotoCount}",
            capture.Id, capture.UserNote?.Trim().Length ?? 0, capture.Photos.Count);

        var items = new List<Item>();
        var note = capture.UserNote?.Trim() ?? "";

        if (string.IsNullOrEmpty(note) && capture.Photos.Count == 0)
        {
            items.Add(CreatePlaceholder(capture, "Empty Capture", "No photos or notes provided."));
            return items;
        }

        var extracted = ExtractFromNote(note, capture);
        if (extracted.Count > 0)
        {
            logger.LogInformation("Local extraction matched {Count} items from note keywords", extracted.Count);
            items.AddRange(extracted);
        }
        else if (capture.Photos.Count > 0 || !string.IsNullOrEmpty(note))
        {
            var type = GuessType(note);
            var venue = ExtractVenue(note, capture);
            var rating = !string.IsNullOrEmpty(note) ? EstimateRating(note) : null;
            items.Add(new Item
            {
                UserId = capture.UserId,
                CaptureId = capture.Id,
                Type = type,
                Name = !string.IsNullOrEmpty(note) ? Truncate(note, 60) : "Photo Capture",
                PhotoUrls = capture.Photos,
                AiConfidence = 0.3,
                AiSummary = !string.IsNullOrEmpty(note)
                    ? $"Captured with note: \"{note}\". AI analysis pending — configure AI Foundry for full extraction."
                    : "Photo captured. AI analysis pending — configure AI Foundry for full extraction.",
                Venue = venue,
                UserRating = rating,
                Journal = InitialJournal(note),
                Tags = [],
                Status = ItemStatus.AiDraft,
                ProcessedBy = ProcessingSource.LocalExtraction
            });
        }

        return items;
    }

    internal static List<JournalEntry> InitialJournal(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return [];
        return [new JournalEntry { Text = note.Trim(), Date = DateTime.UtcNow, Source = "capture" }];
    }

    private static List<Item> ExtractFromNote(string note, Capture capture)
    {
        var items = new List<Item>();
        var lowerNote = note.ToLowerInvariant();
        var venue = ExtractVenue(note, capture);
        var rating = EstimateRating(note);

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
                        Venue = venue,
                        UserRating = rating,
                        Journal = InitialJournal(note),
                        Tags = [],
                        Status = ItemStatus.AiDraft,
                        ProcessedBy = ProcessingSource.LocalExtraction
                    });
                    break;
                }
            }
        }

        return items;
    }

    private static VenueInfo? ExtractVenue(string note, Capture capture)
    {
        // Simple pattern matching as offline fallback — the Note Analyst agent
        // handles this properly when AI Foundry is available
        var lower = note.ToLowerInvariant();
        foreach (var prep in new[] { " at ", " from " })
        {
            var idx = lower.LastIndexOf(prep);
            if (idx < 0) continue;

            var after = note[(idx + prep.Length)..].Trim();
            var endIdx = after.IndexOfAny(['.', '!', '\n']);
            var candidate = (endIdx >= 0 ? after[..endIdx] : after).TrimEnd(',', ';', ':').Trim();

            if (candidate.Length >= 3 && (char.IsUpper(candidate[0]) ||
                candidate.StartsWith("the ", StringComparison.OrdinalIgnoreCase)))
            {
                return new VenueInfo { Name = candidate.Length > 80 ? candidate[..80] : candidate };
            }
        }

        if (capture.Location != null)
        {
            return new VenueInfo
            {
                Name = $"Location: {capture.Location.Latitude:F4}, {capture.Location.Longitude:F4}"
            };
        }

        return null;
    }

    private static int? EstimateRating(string note)
    {
        // Simple sentiment fallback — the Note Analyst agent handles this properly
        var lower = note.ToLowerInvariant();
        if (new[] { "amazing", "incredible", "best", "perfect", "phenomenal" }.Any(lower.Contains)) return 5;
        if (new[] { "great", "excellent", "fantastic", "loved", "delicious" }.Any(lower.Contains)) return 4;
        if (new[] { "good", "nice", "decent", "solid" }.Any(lower.Contains)) return 3;
        if (new[] { "disappointing", "mediocre", "underwhelming" }.Any(lower.Contains)) return 2;
        if (new[] { "terrible", "awful", "worst", "disgusting" }.Any(lower.Contains)) return 1;
        return null;
    }

    private static string GuessType(string note)
    {
        var lower = note.ToLowerInvariant();
        if (lower.Contains("cigar") || lower.Contains("smoke") || lower.Contains("puff")) return ItemType.Cigar;
        if (lower.Contains("wine") || lower.Contains("cabernet") || lower.Contains("merlot") || lower.Contains("chardonnay")) return ItemType.Wine;
        if (lower.Contains("cocktail") || lower.Contains("mixed") || lower.Contains("old fashioned") || lower.Contains("martini")) return ItemType.Cocktail;
        if (lower.Contains("whiskey") || lower.Contains("whisky") || lower.Contains("bourbon") || lower.Contains("scotch")) return ItemType.Whiskey;
        if (lower.Contains("bar") || lower.Contains("restaurant") || lower.Contains("lounge") || lower.Contains("venue")) return ItemType.Venue;
        return ItemType.Custom;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    private static Item CreatePlaceholder(Capture capture, string name, string summary)
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
            Status = ItemStatus.AiDraft,
            ProcessedBy = ProcessingSource.LocalExtraction
        };
    }
}
