using System.Net;

namespace WhiskeyAndSmokes.Api.Services;

public interface IPushoverService
{
    Task SendAsync(string appToken, string userKey, string title, string message, string? url = null, bool playSound = true);
}

public class PushoverService : IPushoverService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PushoverService> _logger;

    private const string PushoverApiUrl = "https://api.pushover.net/1/messages.json";

    public PushoverService(IHttpClientFactory httpClientFactory, ILogger<PushoverService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(string appToken, string userKey, string title, string message, string? url = null, bool playSound = true)
    {
        if (string.IsNullOrEmpty(appToken))
        {
            _logger.LogWarning("Pushover app token not configured for user, skipping push notification");
            return;
        }

        if (string.IsNullOrEmpty(userKey))
        {
            _logger.LogWarning("Pushover user key is empty, skipping push notification");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();

            var formData = new Dictionary<string, string>
            {
                ["token"] = appToken,
                ["user"] = userKey,
                ["title"] = WebUtility.HtmlEncode(title),
                ["message"] = message,
                ["html"] = "1",
            };

            if (!playSound)
                formData["sound"] = "none";

            if (!string.IsNullOrEmpty(url))
                formData["url"] = url;

            var response = await client.PostAsync(PushoverApiUrl, new FormUrlEncodedContent(formData));

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Pushover notification sent: {Title}", title);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Pushover API returned {StatusCode}: {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Pushover notification: {Title}", title);
        }
    }
}

public class NullPushoverService : IPushoverService
{
    public Task SendAsync(string appToken, string userKey, string title, string message, string? url = null, bool playSound = true)
        => Task.CompletedTask;
}
