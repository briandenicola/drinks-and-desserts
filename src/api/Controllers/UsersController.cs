using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;
using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IAuthService _authService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<UsersController> _logger;
    private const string ContainerName = "users";

    public UsersController(ICosmosDbService cosmosDb, IAuthService authService, IBlobStorageService blobStorage, ILogger<UsersController> logger)
    {
        _cosmosDb = cosmosDb;
        _authService = authService;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    [HttpGet("me")]
    public async Task<ActionResult<User>> GetCurrentUser()
    {
        using var activity = Diagnostics.Auth.StartActivity("UserGetProfile");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);
        _logger.LogDebug("Getting profile for user {UserId}", userId);

        var user = await _cosmosDb.GetAsync<User>(ContainerName, userId, userId);
        if (user == null)
        {
            _logger.LogWarning("Profile not found for user {UserId}", userId);
            return NotFound();
        }

        _logger.LogInformation("Retrieved profile for user {UserId}", userId);
        return Ok(user.Sanitized());
    }

    [HttpPut("me")]
    public async Task<ActionResult<User>> UpdateCurrentUser([FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var activity = Diagnostics.Auth.StartActivity("UserUpdateProfile");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);
        _logger.LogDebug("Updating profile for user {UserId}", userId);

        var user = await _cosmosDb.GetAsync<User>(ContainerName, userId, userId);
        if (user == null)
        {
            _logger.LogWarning("Profile not found for update, user {UserId}", userId);
            return NotFound();
        }

        if (request.DisplayName != null) user.DisplayName = request.DisplayName;
        if (request.Preferences != null) user.Preferences = request.Preferences;
        user.UpdatedAt = DateTime.UtcNow;

        user = await _cosmosDb.UpsertAsync(ContainerName, user, user.PartitionKey);

        _logger.LogInformation("Updated profile for user {UserId}", userId);
        return Ok(user.Sanitized());
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var activity = Diagnostics.Auth.StartActivity("UserChangePassword");
        _logger.LogDebug("Password change attempt");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            _logger.LogWarning("Password change failed: new password too short or empty");
            return BadRequest(new { message = "New password must be at least 8 characters" });
        }

        var userId = GetUserId();
        activity?.SetTag("user.id", userId);

        var user = await _cosmosDb.GetAsync<User>(ContainerName, userId, userId);
        if (user == null)
        {
            _logger.LogWarning("Password change failed: user {UserId} not found", userId);
            return NotFound();
        }

        if (!_authService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Password change failed for user {UserId}: current password incorrect", userId);
            return BadRequest(new { message = "Current password is incorrect" });
        }

        user.PasswordHash = _authService.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _cosmosDb.UpsertAsync(ContainerName, user, user.PartitionKey);

        // Revoke all refresh tokens so other sessions must re-authenticate
        await _authService.RevokeAllRefreshTokensAsync(user.Id);

        _logger.LogInformation("Password changed successfully for user {UserId}", userId);
        return Ok(new { message = "Password changed successfully" });
    }

    // API Key Management
    [HttpGet("me/api-keys")]
    public async Task<ActionResult<List<ApiKeyResponse>>> ListApiKeys()
    {
        var userId = GetUserId();
        var user = await _cosmosDb.GetAsync<User>(ContainerName, userId, userId);
        if (user == null) return NotFound();

        var keys = user.ApiKeys.Select(k => new ApiKeyResponse
        {
            Id = k.Id,
            Name = k.Name,
            Prefix = k.Prefix,
            CreatedAt = k.CreatedAt,
            LastUsedAt = k.LastUsedAt,
            IsRevoked = k.IsRevoked,
        }).ToList();

        return Ok(keys);
    }

    [HttpPost("me/api-keys")]
    public async Task<ActionResult<CreateApiKeyResponse>> CreateApiKey([FromBody] CreateApiKeyRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();
        var user = await _cosmosDb.GetAsync<User>(ContainerName, userId, userId);
        if (user == null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Key name is required" });

        var activeKeys = user.ApiKeys.Count(k => !k.IsRevoked);
        if (activeKeys >= 5)
            return BadRequest(new { message = "Maximum of 5 active API keys allowed" });

        var rawKey = ApiKeyAuthHandler.GenerateKey();
        var keyHash = ApiKeyAuthHandler.HashKey(rawKey);
        var prefix = rawKey[..10] + "...";

        var apiKey = new ApiKey
        {
            Name = request.Name,
            Prefix = prefix,
            KeyHash = keyHash,
        };

        user.ApiKeys.Add(apiKey);
        user.UpdatedAt = DateTime.UtcNow;
        await _cosmosDb.UpsertAsync(ContainerName, user, user.PartitionKey);

        _logger.LogInformation("API key created for user {UserId}: keyId={KeyId}, name={Name}",
            userId, apiKey.Id, request.Name);

        return Ok(new CreateApiKeyResponse
        {
            Id = apiKey.Id,
            Name = apiKey.Name,
            Key = rawKey,
            Prefix = prefix,
            CreatedAt = apiKey.CreatedAt,
        });
    }

    [HttpDelete("me/api-keys/{keyId}")]
    public async Task<IActionResult> RevokeApiKey(string keyId)
    {
        var userId = GetUserId();
        var user = await _cosmosDb.GetAsync<User>(ContainerName, userId, userId);
        if (user == null) return NotFound();

        var apiKey = user.ApiKeys.FirstOrDefault(k => k.Id == keyId);
        if (apiKey == null) return NotFound();

        apiKey.IsRevoked = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _cosmosDb.UpsertAsync(ContainerName, user, user.PartitionKey);

        _logger.LogInformation("API key revoked for user {UserId}: keyId={KeyId}", userId, keyId);
        return NoContent();
    }

    [HttpGet("me/stats")]
    public async Task<ActionResult<UserStats>> GetStats()
    {
        using var activity = Diagnostics.Auth.StartActivity("UserGetStats");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);

        var user = await _cosmosDb.GetAsync<User>(ContainerName, userId, userId);
        var allItems = await QueryAllAsync<Item>("items", userId);
        var allCaptures = await QueryAllAsync<Capture>("captures", userId);

        var dayNames = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-5);

        var stats = new UserStats
        {
            Overview = new OverviewStats
            {
                TotalItems = allItems.Count,
                TotalCaptures = allCaptures.Count,
                TotalRated = allItems.Count(i => i.UserRating.HasValue && i.UserRating > 0),
                UniqueBrands = allItems.Where(i => !string.IsNullOrEmpty(i.Brand)).Select(i => i.Brand!).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                UniqueVenues = allItems.Where(i => i.Venue != null && !string.IsNullOrEmpty(i.Venue.Name)).Select(i => i.Venue!.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                MemberSince = user?.CreatedAt ?? now,
            },

            TypeBreakdown = allItems
                .GroupBy(i => i.Type)
                .Select(g => new TypeCount { Type = g.Key, Count = g.Count() })
                .OrderByDescending(t => t.Count)
                .ToList(),

            AvgRatingByType = allItems
                .Where(i => i.UserRating.HasValue && i.UserRating > 0)
                .GroupBy(i => i.Type)
                .Select(g => new TypeRating
                {
                    Type = g.Key,
                    AvgRating = Math.Round(g.Average(i => i.UserRating!.Value), 1),
                    Count = g.Count(),
                })
                .OrderByDescending(t => t.AvgRating)
                .ToList(),

            TopRated = allItems
                .Where(i => i.UserRating.HasValue && i.UserRating > 0)
                .OrderByDescending(i => i.UserRating)
                .ThenByDescending(i => i.UpdatedAt)
                .Take(5)
                .Select(i => new TopItem
                {
                    Id = i.Id,
                    Name = i.Name,
                    Type = i.Type,
                    Brand = i.Brand,
                    Rating = i.UserRating!.Value,
                    PhotoUrl = i.PhotoUrls.FirstOrDefault(),
                })
                .ToList(),

            TopVenues = allItems
                .Where(i => i.Venue != null && !string.IsNullOrEmpty(i.Venue.Name))
                .GroupBy(i => i.Venue!.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new VenueCount { Name = g.Key, Count = g.Count() })
                .OrderByDescending(v => v.Count)
                .Take(5)
                .ToList(),

            TopTags = allItems
                .SelectMany(i => i.Tags)
                .Where(t => !string.IsNullOrEmpty(t))
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TagCount { Tag = g.Key, Count = g.Count() })
                .OrderByDescending(t => t.Count)
                .Take(10)
                .ToList(),

            ActivityByDay = Enumerable.Range(0, 7)
                .Select(d => new DayActivity
                {
                    Day = dayNames[d],
                    Count = allCaptures.Count(c => (int)c.CreatedAt.DayOfWeek == d),
                })
                .ToList(),

            MonthlyTrend = Enumerable.Range(0, 6)
                .Select(i => sixMonthsAgo.AddMonths(i))
                .Select(m => new MonthlyCount
                {
                    Month = m.ToString("MMM yyyy"),
                    Count = allItems.Count(i => i.CreatedAt.Year == m.Year && i.CreatedAt.Month == m.Month),
                })
                .ToList(),

            RatingTrend = Enumerable.Range(0, 6)
                .Select(i => sixMonthsAgo.AddMonths(i))
                .Select(m =>
                {
                    var rated = allItems.Where(i => i.CreatedAt.Year == m.Year && i.CreatedAt.Month == m.Month && i.UserRating.HasValue && i.UserRating > 0).ToList();
                    return new MonthlyRating
                    {
                        Month = m.ToString("MMM yyyy"),
                        AvgRating = rated.Count > 0 ? Math.Round(rated.Average(i => i.UserRating!.Value), 1) : 0,
                        Count = rated.Count,
                    };
                })
                .ToList(),
        };

        _logger.LogInformation("Generated stats for user {UserId}: {ItemCount} items, {CaptureCount} captures", userId, allItems.Count, allCaptures.Count);
        return Ok(stats);
    }

    private static readonly HashSet<string> DrinkTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ItemType.Whiskey, ItemType.Wine, ItemType.Cocktail, ItemType.Vodka, ItemType.Gin
    };

    private static string ClassifyItem(Item item) =>
        item.Type.Equals(ItemType.Dessert, StringComparison.OrdinalIgnoreCase) ? "dessert" :
        DrinkTypes.Contains(item.Type) ? "drink" : "drink";

    [HttpGet("me/dashboard")]
    public async Task<ActionResult<DashboardStats>> GetDashboard()
    {
        using var activity = Diagnostics.Auth.StartActivity("UserGetDashboard");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);

        var allItems = await QueryAllAsync<Item>("items", userId);
        var allCaptures = await QueryAllAsync<Capture>("captures", userId);
        var allVenues = await QueryAllAsync<Venue>("venues", userId);

        var collectionItems = allItems.Where(i => i.Status != ItemStatus.Wishlist).ToList();
        var wishlistItems = allItems.Where(i => i.Status == ItemStatus.Wishlist).ToList();
        var drinkItems = collectionItems.Where(i => ClassifyItem(i) == "drink").ToList();
        var dessertItems = collectionItems.Where(i => ClassifyItem(i) == "dessert").ToList();

        var ratedDrinks = drinkItems.Where(i => i.UserRating.HasValue && i.UserRating > 0).ToList();
        var ratedDesserts = dessertItems.Where(i => i.UserRating.HasValue && i.UserRating > 0).ToList();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var thisMonthItems = collectionItems.Where(i => i.CreatedAt >= monthStart).ToList();
        var thisMonthVenues = thisMonthItems
            .Where(i => i.Venue != null && !string.IsNullOrEmpty(i.Venue.Name))
            .Select(i => i.Venue!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var stats = new DashboardStats
        {
            Summary = new DashboardSummary
            {
                TotalItems = collectionItems.Count,
                DrinkCount = drinkItems.Count,
                DessertCount = dessertItems.Count,
                AvgDrinkRating = ratedDrinks.Count > 0 ? Math.Round(ratedDrinks.Average(i => i.UserRating!.Value), 1) : 0,
                AvgDessertRating = ratedDesserts.Count > 0 ? Math.Round(ratedDesserts.Average(i => i.UserRating!.Value), 1) : 0,
                WishlistSize = wishlistItems.Count,
                TotalVenues = allVenues.Count,
            },
            ThisMonth = new MonthlySnapshot
            {
                NewItemsCaptured = thisMonthItems.Count,
                VenuesVisited = thisMonthVenues,
                WishlistConversions = 0, // No conversion tracking yet
                Month = now.ToString("yyyy-MM"),
            },
            RecentActivity = allCaptures
                .OrderByDescending(c => c.CreatedAt)
                .Take(20)
                .Select(c =>
                {
                    var captureItems = allItems.Where(i => i.CaptureId == c.Id).ToList();
                    var venueName = captureItems
                        .Where(i => i.Venue != null && !string.IsNullOrEmpty(i.Venue.Name))
                        .Select(i => i.Venue!.Name)
                        .FirstOrDefault();

                    return new Models.RecentActivity
                    {
                        CaptureId = c.Id,
                        Status = c.Status switch
                        {
                            CaptureStatus.Completed => "complete",
                            CaptureStatus.Failed => "failed",
                            _ => "processing",
                        },
                        ThumbnailUrls = c.Photos.Take(3).ToList(),
                        ItemCount = captureItems.Count,
                        CreatedAt = c.CreatedAt,
                        VenueName = venueName,
                    };
                })
                .ToList(),
        };

        _logger.LogInformation("Generated dashboard for user {UserId}: {ItemCount} items, {CaptureCount} captures, {VenueCount} venues",
            userId, collectionItems.Count, allCaptures.Count, allVenues.Count);
        return Ok(stats);
    }

    [HttpGet("me/stats/ratings-distribution")]
    public async Task<ActionResult<RatingDistribution>> GetRatingsDistribution()
    {
        using var activity = Diagnostics.Auth.StartActivity("UserGetRatingsDistribution");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);

        var allItems = await QueryAllAsync<Item>("items", userId);

        var buckets = allItems
            .Where(i => i.Status != ItemStatus.Wishlist && i.UserRating.HasValue && i.UserRating > 0)
            .GroupBy(i => new { Rating = Math.Round(i.UserRating!.Value * 2, MidpointRounding.AwayFromZero) / 2, Category = ClassifyItem(i) })
            .Select(g => new RatingBucket
            {
                Rating = g.Key.Rating,
                Category = g.Key.Category,
                Count = g.Count(),
            })
            .OrderBy(b => b.Rating)
            .ThenBy(b => b.Category)
            .ToList();

        _logger.LogInformation("Generated ratings distribution for user {UserId}: {BucketCount} buckets", userId, buckets.Count);
        return Ok(new RatingDistribution { Buckets = buckets });
    }

    [HttpGet("me/export")]
    public async Task<IActionResult> ExportData(CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.Auth.StartActivity("UserExportData");
        var userId = GetUserId();
        activity?.SetTag("user.id", userId);
        _logger.LogInformation("Starting data export for user {UserId}", userId);

        var allItems = await QueryAllAsync<Item>("items", userId);
        var allCaptures = await QueryAllAsync<Capture>("captures", userId);

        var imageUrls = new HashSet<string>();
        foreach (var item in allItems)
            foreach (var url in item.PhotoUrls)
                imageUrls.Add(url);
        foreach (var capture in allCaptures)
            foreach (var url in capture.Photos)
                imageUrls.Add(url);

        _logger.LogInformation("Export for user {UserId}: {ItemCount} items, {CaptureCount} captures, {ImageCount} images",
            userId, allItems.Count, allCaptures.Count, imageUrls.Count);

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var itemsEntry = archive.CreateEntry("data/items.json");
            await using (var stream = itemsEntry.Open())
                await JsonSerializer.SerializeAsync(stream, allItems, jsonOptions, cancellationToken);

            var capturesEntry = archive.CreateEntry("data/captures.json");
            await using (var stream = capturesEntry.Open())
                await JsonSerializer.SerializeAsync(stream, allCaptures, jsonOptions, cancellationToken);

            foreach (var url in imageUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var imageBytes = await _blobStorage.DownloadAsync(url, cancellationToken);
                if (imageBytes != null)
                {
                    var imagePath = ExtractImagePath(url);
                    var imageEntry = archive.CreateEntry($"images/{imagePath}", CompressionLevel.NoCompression);
                    await using var stream = imageEntry.Open();
                    await stream.WriteAsync(imageBytes, cancellationToken);
                }
            }
        }

        memoryStream.Position = 0;
        var fileName = $"whiskey-and-smokes-export-{DateTime.UtcNow:yyyy-MM-dd}.zip";
        return File(memoryStream.ToArray(), "application/zip", fileName);
    }

    private async Task<List<T>> QueryAllAsync<T>(string containerName, string partitionKey)
    {
        var all = new List<T>();
        string? token = null;
        do
        {
            var (items, nextToken) = await _cosmosDb.QueryAsync<T>(containerName, partitionKey, token, 100);
            all.AddRange(items);
            token = nextToken;
        } while (token != null);
        return all;
    }

    private static string ExtractImagePath(string url)
    {
        // URLs: /uploads/{userId}/{date}/{file} or https://host/container/{userId}/{date}/{file}
        var uploadsIdx = url.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
        if (uploadsIdx >= 0)
        {
            var path = url[(uploadsIdx + "/uploads/".Length)..];
            // Strip userId prefix — keep date/file
            var firstSlash = path.IndexOf('/');
            return firstSlash >= 0 ? path[(firstSlash + 1)..] : path;
        }

        // Azure blob URL — take last two segments (date/file)
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2
            ? string.Join("/", segments[^2..])
            : segments[^1];
    }
}
