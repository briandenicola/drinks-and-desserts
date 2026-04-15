using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public ICosmosDbService CosmosDb { get; } = Substitute.For<ICosmosDbService>();
    public IBlobStorageService BlobStorage { get; } = Substitute.For<IBlobStorageService>();
    public IAuthService AuthService { get; } = Substitute.For<IAuthService>();
    public IPromptService PromptService { get; } = Substitute.For<IPromptService>();
    public IAgentService AgentService { get; } = Substitute.For<IAgentService>();
    public INotificationService NotificationService { get; } = Substitute.For<INotificationService>();

    public const string TestUserId = "test-user-id";
    public const string TestUserEmail = "test@example.com";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "local",
                ["Jwt:Secret"] = "test-secret-key-that-is-at-least-32-characters-long-for-hmac",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpiryMinutes"] = "60",
                ["BlobStorage:LocalPath"] = Path.Combine(Path.GetTempPath(), "ws-tests-blobs"),
                ["Database:LocalPath"] = Path.Combine(Path.GetTempPath(), "ws-tests.db"),
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove hosted services to prevent background work during tests
            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                .ToList();
            foreach (var descriptor in hostedServiceDescriptors)
            {
                services.Remove(descriptor);
            }

            // Replace real services with mocks
            ReplaceService<ICosmosDbService>(services, CosmosDb);
            ReplaceService<IBlobStorageService>(services, BlobStorage);
            ReplaceService<IAuthService>(services, AuthService);
            ReplaceService<IPromptService>(services, PromptService);
            ReplaceService<IAgentService>(services, AgentService);
            ReplaceService<INotificationService>(services, NotificationService);

            // Ensure bounded channel is available
            var channelDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Channel<Capture>));
            if (channelDescriptor == null)
            {
                services.AddSingleton(Channel.CreateBounded<Capture>(new BoundedChannelOptions(10)
                {
                    SingleReader = true,
                    FullMode = BoundedChannelFullMode.Wait
                }));
            }

            // Override authentication: remove all existing auth and replace with test scheme
            var authDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("Authentication") == true ||
                            d.ImplementationType?.FullName?.Contains("Authentication") == true)
                .ToList();
            foreach (var d in authDescriptors) services.Remove(d);

            // Remove existing IConfigureOptions<AuthenticationOptions> registrations
            var authOptionDescriptors = services
                .Where(d => d.ServiceType.IsGenericType &&
                            d.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
                            d.ServiceType.GenericTypeArguments[0] == typeof(AuthenticationOptions))
                .ToList();
            foreach (var d in authOptionDescriptors) services.Remove(d);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
                options.DefaultScheme = "TestScheme";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", null)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("MultiAuth", null)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("ApiKey", null);

            services.AddAuthorizationBuilder()
                .AddPolicy("AdminOnly", policy =>
                    policy.RequireRole("admin").RequireAuthenticatedUser());
        });
    }

    private static void ReplaceService<T>(IServiceCollection services, T implementation) where T : class
    {
        var existing = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var descriptor in existing)
        {
            services.Remove(descriptor);
        }
        services.AddSingleton(implementation);
    }
}

/// <summary>
/// Authentication handler that auto-authenticates all requests for testing.
/// Set the "X-Test-UserId" and "X-Test-Role" headers to customize claims.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Allow anonymous endpoints to work by checking for opt-out header
        if (Request.Headers.ContainsKey("X-Test-Anonymous"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers["X-Test-UserId"].FirstOrDefault()
            ?? CustomWebApplicationFactory.TestUserId;
        var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "user";
        var email = Request.Headers["X-Test-Email"].FirstOrDefault()
            ?? CustomWebApplicationFactory.TestUserEmail;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim("sub", userId),
        };

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
