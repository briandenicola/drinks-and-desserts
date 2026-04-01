using System.Text;
using System.Threading.Channels;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Agents;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Dynamic log level service — created before DI so the filter lambda can capture it
var logLevelService = new DynamicLogLevelService();

builder.Logging.AddFilter((provider, category, logLevel) =>
    logLevelService.IsEnabled(category, logLevel));

builder.AddServiceDefaults();

// Bind strongly-typed options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.Configure<EntraIdOptions>(builder.Configuration.GetSection(EntraIdOptions.Section));
builder.Services.Configure<AiFoundryOptions>(builder.Configuration.GetSection(AiFoundryOptions.Section));
builder.Services.Configure<CosmosDbOptions>(builder.Configuration.GetSection(CosmosDbOptions.Section));
builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection(BlobStorageOptions.Section));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrEmpty(jwtOptions.Secret) && builder.Environment.IsDevelopment())
{
    jwtOptions.Secret = "dev-secret-key-change-in-production-min-32-chars!!";
}

var entraOptions = builder.Configuration.GetSection(EntraIdOptions.Section).Get<EntraIdOptions>() ?? new EntraIdOptions();

// Authentication — local JWT + optional Entra ID
var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "MultiAuth";
        options.DefaultChallengeScheme = "MultiAuth";
    })
    .AddJwtBearer("LocalJwt", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

if (entraOptions.IsConfigured)
{
    authBuilder.AddJwtBearer("EntraId", options =>
    {
        options.Authority = $"{entraOptions.Instance}{entraOptions.TenantId}/v2.0";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{entraOptions.Instance}{entraOptions.TenantId}/v2.0",
            ValidateAudience = true,
            ValidAudience = !string.IsNullOrEmpty(entraOptions.Audience)
                ? entraOptions.Audience
                : entraOptions.ClientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });
}

// Policy scheme that tries both — Entra first (if configured), then local
builder.Services.AddAuthentication()
    .AddPolicyScheme("MultiAuth", "Local JWT or Entra ID", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return "LocalJwt";

            // Entra ID tokens are standard JWTs from login.microsoftonline.com
            // Local tokens use our custom issuer
            var token = authHeader["Bearer ".Length..];
            try
            {
                // Quick peek at the issuer claim without full validation
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);
                    var issuer = jwt.Issuer;
                    if (entraOptions.IsConfigured &&
                        issuer.Contains("login.microsoftonline.com", StringComparison.OrdinalIgnoreCase))
                    {
                        return "EntraId";
                    }
                }
            }
            catch
            {
                // If we can't read the token, fall through to local
            }

            return "LocalJwt";
        };
    });

// Authorization policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin")
              .RequireAuthenticatedUser());

// Storage services — conditional on provider setting or environment
// Storage:Provider = "local" | "azure" | "auto" (default)
var storageProvider = builder.Configuration["Storage:Provider"]?.ToLowerInvariant() ?? "auto";
var useLocalStorage = storageProvider == "local" ||
    (storageProvider == "auto" &&
     builder.Environment.IsDevelopment() &&
     string.IsNullOrEmpty(builder.Configuration["CosmosDb:Endpoint"]) &&
     string.IsNullOrEmpty(builder.Configuration["CosmosDb:ConnectionString"]));

if (useLocalStorage)
{
    // Local development: LiteDB + filesystem
    builder.Services.AddSingleton<ICosmosDbService, LiteDbService>();
    builder.Services.AddSingleton<IBlobStorageService, LocalBlobStorageService>();
}
else
{
    // Azure: CosmosDB + Blob Storage
    var cosmosEndpoint = builder.Configuration["CosmosDb:Endpoint"];
    if (!string.IsNullOrEmpty(cosmosEndpoint))
    {
        builder.Services.AddSingleton(sp =>
        {
            var options = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            };
            return new CosmosClient(cosmosEndpoint, new DefaultAzureCredential(), options);
        });
    }
    else
    {
        builder.Services.AddSingleton(sp =>
        {
            var connString = builder.Configuration["CosmosDb:ConnectionString"]
                ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            return new CosmosClient(connString, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                HttpClientFactory = () => new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                })
            });
        });
    }
    builder.Services.AddSingleton<ICosmosDbService, CosmosDbService>();

    var blobEndpoint = builder.Configuration["BlobStorage:Endpoint"];
    if (!string.IsNullOrEmpty(blobEndpoint))
    {
        builder.Services.AddSingleton(new BlobServiceClient(new Uri(blobEndpoint), new DefaultAzureCredential()));
    }
    else
    {
        var blobConnString = builder.Configuration["BlobStorage:ConnectionString"]
            ?? "UseDevelopmentStorage=true";
        builder.Services.AddSingleton(new BlobServiceClient(blobConnString));
    }
    builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
}

// Auth & AI services
builder.Services.AddSingleton(logLevelService);
builder.Services.AddSingleton<FoundryStatusService>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IPromptService, PromptService>();
builder.Services.AddSingleton<IAgentService, WorkflowAgentService>();
builder.Services.AddHostedService<AgentValidationService>();

// Background capture processing queue
builder.Services.AddSingleton(Channel.CreateUnbounded<Capture>(new UnboundedChannelOptions
{
    SingleReader = true
}));
builder.Services.AddHostedService<CaptureProcessingService>();

// Dependency health checks
if (!useLocalStorage)
{
    builder.Services.AddHealthChecks()
        .AddCheck<CosmosDbHealthCheck>("cosmosdb", tags: ["ready"])
        .AddCheck<BlobStorageHealthCheck>("blob-storage", tags: ["ready"]);
}
builder.Services.AddHealthChecks()
    .AddCheck<FoundryHealthCheck>("ai-foundry", tags: ["ready"]);

// API
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Initialize CosmosDB containers (only when using Cosmos)
if (!useLocalStorage)
{
    await InitializeCosmosDb(app);
}

// Seed default prompts
var promptService = app.Services.GetRequiredService<IPromptService>();
await promptService.SeedDefaultsAsync();

// Load persisted log level settings
var cosmosDb = app.Services.GetRequiredService<ICosmosDbService>();
await logLevelService.LoadFromStoreAsync(cosmosDb);

app.Logger.LogInformation("Whiskey & Smokes API starting — log levels loaded from store");
app.Logger.LogInformation("Storage provider: {Provider} ({Backend})",
    storageProvider, useLocalStorage ? "LiteDB + local filesystem" : "CosmosDB + Azure Blob Storage");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task InitializeCosmosDb(WebApplication app)
{
    try
    {
        var client = app.Services.GetRequiredService<CosmosClient>();
        var config = app.Services.GetRequiredService<IConfiguration>();
        var dbName = config["CosmosDb:DatabaseName"] ?? "whiskey-and-smokes";

        var database = await client.CreateDatabaseIfNotExistsAsync(dbName);

        string[] containers = ["users", "captures", "items", "venues"];
        string[] partitionKeys = ["/partitionKey", "/partitionKey", "/partitionKey", "/partitionKey"];

        for (int i = 0; i < containers.Length; i++)
        {
            await database.Database.CreateContainerIfNotExistsAsync(containers[i], partitionKeys[i]);
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not initialize CosmosDB. Will retry on first request.");
    }
}
