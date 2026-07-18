using WhiskeyAndSmokes.Api.Models;

namespace WhiskeyAndSmokes.Api.Services;

public interface IRecommendationService
{
    /// <summary>
    /// Get AI-powered recommendations based on user's rating history
    /// </summary>
    Task<RecommendationResponse> GetRecommendationsAsync(string userId, RecommendationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Build a user's rating profile for recommendations
    /// </summary>
    Task<UserRatingProfile> BuildUserProfileAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract menu items from a photo
    /// </summary>
    Task<List<string>> ExtractMenuItemsAsync(string photoUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save a generated recommendation thread (request + results) so the user can revisit it later
    /// </summary>
    Task<RecommendationThread> SaveRecommendationThreadAsync(string userId, RecommendationRequest request, RecommendationResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    /// List a user's saved recommendation threads, most recent first
    /// </summary>
    Task<List<RecommendationThread>> GetRecommendationThreadsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single saved recommendation thread
    /// </summary>
    Task<RecommendationThread?> GetRecommendationThreadAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a saved recommendation thread
    /// </summary>
    Task DeleteRecommendationThreadAsync(string userId, string id, CancellationToken cancellationToken = default);
}
