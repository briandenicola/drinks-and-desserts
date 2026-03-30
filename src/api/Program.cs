using System.Text;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Agents;
using WhiskeyAndSmokes.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Dynamic log level service — created before DI so the filter lambda can capture it
var logLevelService = new DynamicLogLevelService();

builder.Logging.AddFilter((provider, category, logLevel) =>
    logLevelService.IsEnabled(category, logLevel));

builder.AddServiceDefaults();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "dev-secret-key-change-in-production-min-32-chars!!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "whiskey-and-smokes";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "whiskey-and-smokes";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

// Authorization policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin")
              .RequireAuthenticatedUser());

// Storage services — conditional on environment
var useLocalStorage = builder.Environment.IsDevelopment() &&
    string.IsNullOrEmpty(builder.Configuration["CosmosDb:Endpoint"]) &&
    string.IsNullOrEmpty(builder.Configuration["CosmosDb:ConnectionString"]);

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
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IPromptService, PromptService>();
builder.Services.AddSingleton<IAgentService, WorkflowAgentService>();
builder.Services.AddHostedService<AgentValidationService>();

// API
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.Logger.LogInformation("Running in Development mode with {Storage}",
        useLocalStorage ? "LiteDB + local filesystem" : "CosmosDB + Azure Blob Storage");
}

app.MapDefaultEndpoints();
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
