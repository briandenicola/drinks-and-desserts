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
        // Use a relative base URL so the browser resolves against its own origin.
        // This avoids mixed-content errors when the web is served over HTTPS
        // and the API is internal HTTP behind an nginx reverse proxy.
        _baseUrl = config["LocalStorage:BaseUrl"] ?? "/uploads";
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

    public async Task<string> UploadAsync(string userId, string fileName, Stream content)
    {
        var ext = Path.GetExtension(fileName);
        var blobName = $"{userId}/{DateTime.UtcNow:yyyy-MM-dd}/{Guid.NewGuid()}{ext}";
        var localPath = Path.Combine(_storagePath, blobName.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

        using var fileStream = new FileStream(localPath, FileMode.Create);
        await content.CopyToAsync(fileStream);

        _logger.LogInformation("File uploaded locally for user {UserId}: path={Path}", userId, blobName);

        return $"{_baseUrl}/{blobName}";
    }

    public Task DeleteBlobAsync(string blobUrl)
    {
        try
        {
            var relativePath = ExtractRelativePath(blobUrl);
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

    public async Task<byte[]?> DownloadAsync(string blobUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var relativePath = ExtractRelativePath(blobUrl);
            var fullPath = Path.Combine(_storagePath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(fullPath))
            {
                return await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }

            _logger.LogWarning("Local blob not found: {Path}", fullPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download local blob: {Url}", blobUrl);
            return null;
        }
    }

    /// <summary>
    /// Extracts the storage-relative path from either a full URL or a relative path.
    /// Handles both "http://host:port/uploads/user/file.jpg" and "/uploads/user/file.jpg".
    /// </summary>
    private static string ExtractRelativePath(string blobUrl)
    {
        var uploadsIndex = blobUrl.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
        if (uploadsIndex >= 0)
            return blobUrl[(uploadsIndex + "/uploads/".Length)..];

        return blobUrl;
    }

    public string StoragePath => _storagePath;
}
