using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiskeyAndSmokes.Api;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
[Route("uploads")]
public class UploadsController : ControllerBase
{
    private readonly string _storagePath;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UploadsController> _logger;

    public UploadsController(IConfiguration config, IWebHostEnvironment env, ILogger<UploadsController> logger)
    {
        _storagePath = config["LocalStorage:Path"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "whiskey-and-smokes", "uploads");
        _env = env;
        _logger = logger;
    }

    [HttpPut("direct-upload")]
    [Authorize]
    [RequestSizeLimit(20_000_000)] // 20MB
    public async Task<IActionResult> DirectUpload([FromQuery] string path)
    {
        using var activity = Diagnostics.Storage.StartActivity("UploadDirectUpload");
        activity?.SetTag("upload.path", path);
        _logger.LogDebug("Direct upload requested for path {Path}", path);

        if (!_env.IsDevelopment())
            return NotFound();

        var safePath = path.Replace("..", "").Replace('\\', '/');
        var fullPath = Path.Combine(_storagePath, safePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var stream = new FileStream(fullPath, FileMode.Create);
        await Request.Body.CopyToAsync(stream);

        var sizeBytes = stream.Length;
        activity?.SetTag("upload.size_bytes", sizeBytes);
        _logger.LogInformation("File uploaded: path={Path}, size={SizeBytes} bytes", safePath, sizeBytes);
        return Ok();
    }

    [HttpGet("{**filePath}")]
    [AllowAnonymous]
    public IActionResult GetFile(string filePath)
    {
        using var activity = Diagnostics.Storage.StartActivity("UploadGetFile");
        activity?.SetTag("upload.path", filePath);
        _logger.LogDebug("File requested: {FilePath}", filePath);

        if (!_env.IsDevelopment())
            return NotFound();

        var safePath = filePath.Replace("..", "");
        var fullPath = Path.Combine(_storagePath, safePath.Replace('/', Path.DirectorySeparatorChar));

        if (!System.IO.File.Exists(fullPath))
        {
            _logger.LogWarning("File not found: {FilePath}", safePath);
            return NotFound();
        }

        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            _ => "application/octet-stream"
        };

        _logger.LogInformation("Serving file: {FilePath}, contentType={ContentType}", safePath, contentType);
        return PhysicalFile(fullPath, contentType);
    }
}
