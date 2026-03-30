using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using WhiskeyAndSmokes.Api;

namespace WhiskeyAndSmokes.Api.Services;

public interface IBlobStorageService
{
    Task<(string UploadUrl, string BlobUrl)> GenerateUploadUrlAsync(string userId, string fileName);
    Task DeleteBlobAsync(string blobUrl);
}

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(BlobServiceClient blobServiceClient, IConfiguration config, ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _containerName = config["BlobStorage:ContainerName"] ?? "captures";
        _logger = logger;
    }

    public async Task<(string UploadUrl, string BlobUrl)> GenerateUploadUrlAsync(string userId, string fileName)
    {
        using var activity = Diagnostics.Storage.StartActivity("Blob.GenerateUploadUrl");
        activity?.SetTag("blob.container", _containerName);

        _logger.LogDebug("Generating upload URL for user {UserId}, fileName={FileName}, container={Container}",
            userId, fileName, _containerName);

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();

        var blobName = $"{userId}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var blobClient = containerClient.GetBlobClient(blobName);

        activity?.SetTag("blob.path", blobName);

        // Generate user delegation SAS
        var userDelegationKey = await _blobServiceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(30));

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var sasToken = sasBuilder.ToSasQueryParameters(userDelegationKey, _blobServiceClient.AccountName);
        var uploadUrl = $"{blobClient.Uri}?{sasToken}";

        _logger.LogInformation(
            "Upload URL generated for user {UserId}: container={Container}, blobPath={BlobPath}, sasExpiresMinutes=30",
            userId, _containerName, blobName);

        return (uploadUrl, blobClient.Uri.ToString());
    }

    public async Task DeleteBlobAsync(string blobUrl)
    {
        using var activity = Diagnostics.Storage.StartActivity("Blob.Delete");
        activity?.SetTag("blob.container", _containerName);

        var uri = new Uri(blobUrl);
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobName = string.Join("/", uri.Segments.Skip(2)).TrimStart('/');

        activity?.SetTag("blob.path", blobName);
        _logger.LogDebug("Deleting blob: container={Container}, blobPath={BlobPath}", _containerName, blobName);

        await containerClient.DeleteBlobIfExistsAsync(blobName);

        _logger.LogInformation("Blob deleted: container={Container}, blobPath={BlobPath}", _containerName, blobName);
    }
}
