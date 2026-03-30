namespace WhiskeyAndSmokes.Api.Services;

public class LocalBlobStorageService : IBlobStorageService
{
    private readonly string _storagePath;
    private readonly string _baseUrl;
    private readonly ILogger<LocalBlobStorageService> _logger;

    public LocalBlobStorageService(IConfiguration config, ILogger<LocalBlobStorageService> logger)
    {
        _storagePath = config["LocalStorage:Path"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "whiskey-and-smokes", "uploads");
        _baseUrl = config["LocalStorage:BaseUrl"] ?? "http://localhost:5062/uploads";
        _logger = logger;

        Directory.CreateDirectory(_storagePath);
        _logger.LogInformation("Local blob storage initialized at {Path}", _storagePath);
    }

    public Task<(string UploadUrl, string BlobUrl)> GenerateUploadUrlAsync(string userId, string fileName)
    {
        var ext = Path.GetExtension(fileName);
        var blobName = $"{userId}/{DateTime.UtcNow:yyyy-MM-dd}/{Guid.NewGuid()}{ext}";
        var localPath = Path.Combine(_storagePath, blobName.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

        var uploadUrl = $"{_baseUrl}/direct-upload?path={Uri.EscapeDataString(blobName)}";
        var blobUrl = $"{_baseUrl}/{blobName}";

        return Task.FromResult((uploadUrl, blobUrl));
    }

    public Task DeleteBlobAsync(string blobUrl)
    {
        try
        {
            var uri = new Uri(blobUrl);
            var relativePath = uri.AbsolutePath.Replace("/uploads/", "");
            var fullPath = Path.Combine(_storagePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete local blob: {Url}", blobUrl);
        }
        return Task.CompletedTask;
    }

    public string StoragePath => _storagePath;
}
