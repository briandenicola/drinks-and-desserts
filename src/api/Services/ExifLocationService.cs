using MetadataExtractor.Formats.Exif;

namespace WhiskeyAndSmokes.Api.Services;

/// <summary>
/// Extracts GPS coordinates from photo EXIF metadata.
/// Used as a fallback when the client does not provide a location.
/// </summary>
public class ExifLocationService
{
    private readonly IBlobStorageService _blobService;
    private readonly ILogger<ExifLocationService> _logger;

    public ExifLocationService(IBlobStorageService blobService, ILogger<ExifLocationService> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to extract GPS coordinates from the first photo that contains EXIF location data.
    /// Returns null if no photos contain GPS data or if extraction fails.
    /// </summary>
    public async Task<Models.GeoLocation?> TryExtractLocationAsync(
        IReadOnlyList<string> photoUrls,
        CancellationToken cancellationToken = default)
    {
        foreach (var url in photoUrls)
        {
            try
            {
                var bytes = await _blobService.DownloadAsync(url, cancellationToken);
                if (bytes == null || bytes.Length == 0)
                    continue;

                var location = ExtractGpsFromBytes(bytes);
                if (location != null)
                {
                    _logger.LogInformation(
                        "Extracted EXIF GPS location ({Lat}, {Lon}) from photo {Url}",
                        location.Latitude, location.Longitude, url);
                    return location;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read EXIF data from {Url}", url);
            }
        }

        _logger.LogDebug("No EXIF GPS data found in {Count} photo(s)", photoUrls.Count);
        return null;
    }

    private static Models.GeoLocation? ExtractGpsFromBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return ExtractGpsFromStream(stream);
    }

    public static Models.GeoLocation? ExtractGpsFromStream(Stream stream)
    {
        var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(stream);

        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        if (gps == null)
            return null;

        MetadataExtractor.GeoLocation? coords = gps.GetGeoLocation();
        if (coords == null)
            return null;

        return new Models.GeoLocation
        {
            Latitude = coords.Value.Latitude,
            Longitude = coords.Value.Longitude
        };
    }
}
