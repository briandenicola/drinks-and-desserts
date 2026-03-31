using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace WhiskeyAndSmokes.Api.Services;

public class CosmosDbHealthCheck : IHealthCheck
{
    private readonly CosmosClient _client;
    private readonly string _databaseName;

    public CosmosDbHealthCheck(CosmosClient client, IOptions<CosmosDbOptions> options)
    {
        _client = client;
        _databaseName = options.Value.DatabaseName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _client.GetDatabase(_databaseName);
            await database.ReadAsync(cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("CosmosDB is reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("CosmosDB is unreachable", ex);
        }
    }
}

public class BlobStorageHealthCheck : IHealthCheck
{
    private readonly Azure.Storage.Blobs.BlobServiceClient _client;

    public BlobStorageHealthCheck(Azure.Storage.Blobs.BlobServiceClient client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetPropertiesAsync(cancellationToken);
            return HealthCheckResult.Healthy("Blob Storage is reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Blob Storage is unreachable", ex);
        }
    }
}

public class FoundryHealthCheck : IHealthCheck
{
    private readonly FoundryStatusService _foundryStatus;

    public FoundryHealthCheck(FoundryStatusService foundryStatus)
    {
        _foundryStatus = foundryStatus;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var status = _foundryStatus.GetStatus();
        if (!status.IsProjectConfigured)
            return Task.FromResult(HealthCheckResult.Degraded("AI Foundry is not configured — local extraction fallback active"));

        if (status.AgentValidation.Status == "valid")
            return Task.FromResult(HealthCheckResult.Healthy($"AI Foundry agents validated at {status.CheckedAt:u}"));

        return Task.FromResult(HealthCheckResult.Unhealthy(
            $"AI Foundry agents not validated: {status.AgentValidation.Error ?? status.AgentValidation.Status}"));
    }
}
