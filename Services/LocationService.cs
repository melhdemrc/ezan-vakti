using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

#if WINDOWS
using Windows.Devices.Geolocation;
#endif

namespace EzanVakti.Services;

/// <summary>
/// Status of location detection attempt
/// </summary>
public enum LocationStatus
{
    Success,
    AccessDenied,
    Timeout,
    NotSupported,
    Failed
}

/// <summary>
/// Result of a location detection attempt
/// </summary>
public class LocationResult
{
    public LocationStatus Status { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }
    public string? Source { get; set; }

    public string DisplayName => $"{Latitude:F4}, {Longitude:F4}";
    
    public string DisplayWithAccuracy => Accuracy.HasValue 
        ? $"{DisplayName} ({Accuracy:F0}m)" 
        : DisplayName;
}

/// <summary>
/// Service for getting user's location via multiple methods:
/// 1. Windows Location Service (GPS/WiFi/IP) - Windows 10+ only
/// 2. IP-based geolocation fallback - cross-platform
/// </summary>
public static class LocationService
{
    private static readonly HttpClient _httpClient = new() 
    { 
        Timeout = TimeSpan.FromSeconds(10) 
    };

    /// <summary>
    /// Gets location using the best available method.
    /// Tries Windows Location Service first, falls back to IP geolocation.
    /// </summary>
    public static async Task<LocationResult?> GetLocationAsync(CancellationToken cancellationToken = default)
    {
        // Try Windows Location Service first (more accurate)
        var windowsResult = await GetWindowsLocationAsync(cancellationToken);
        if (windowsResult != null && windowsResult.Status == LocationStatus.Success)
        {
            return windowsResult;
        }

        // Fallback to IP-based geolocation
        var ipResult = await GetIpLocationAsync(cancellationToken);
        return ipResult;
    }

    /// <summary>
    /// Gets location from Windows Location Service (GPS/WiFi/IP)
    /// Only available on Windows 10+
    /// </summary>
    public static async Task<LocationResult?> GetWindowsLocationAsync(CancellationToken cancellationToken = default)
    {
#if WINDOWS
        try
        {
            // Use a timeout to prevent indefinite waiting
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            // Request access to location
            var accessStatus = await Geolocator.RequestAccessAsync().AsTask(cts.Token);
            
            if (accessStatus != GeolocationAccessStatus.Allowed)
            {
                System.Diagnostics.Debug.WriteLine($"Location access denied: {accessStatus}");
                return new LocationResult 
                { 
                    Status = LocationStatus.AccessDenied 
                };
            }

            // Get location with timeout
            var geolocator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default
            };

            var position = await geolocator.GetGeopositionAsync().AsTask(cts.Token);
            
            return new LocationResult
            {
                Status = LocationStatus.Success,
                Latitude = position.Coordinate.Point.Position.Latitude,
                Longitude = position.Coordinate.Point.Position.Longitude,
                Accuracy = position.Coordinate.Accuracy,
                Source = "Windows"
            };
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Windows location request timed out");
            return new LocationResult { Status = LocationStatus.Timeout };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Windows location error: {ex.Message}");
            return new LocationResult { Status = LocationStatus.Failed };
        }
#else
        // Not running on Windows
        return new LocationResult { Status = LocationStatus.NotSupported };
#endif
    }

    /// <summary>
    /// Gets approximate location from IP address using ip-api.com
    /// Works on all platforms but less accurate than GPS
    /// </summary>
    public static async Task<LocationResult?> GetIpLocationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // ip-api.com is free for non-commercial use, no API key required
            var response = await _httpClient.GetAsync(
                "http://ip-api.com/json/?fields=status,lat,lon,city,country", 
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new LocationResult { Status = LocationStatus.Failed };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var ipData = JsonSerializer.Deserialize<IpApiResponse>(json);

            if (ipData?.Status == "success" && ipData.Lat.HasValue && ipData.Lon.HasValue)
            {
                return new LocationResult
                {
                    Status = LocationStatus.Success,
                    Latitude = ipData.Lat.Value,
                    Longitude = ipData.Lon.Value,
                    Accuracy = null, // IP geolocation doesn't provide accuracy
                    Source = "IP"
                };
            }

            return new LocationResult { Status = LocationStatus.Failed };
        }
        catch (OperationCanceledException)
        {
            return new LocationResult { Status = LocationStatus.Timeout };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"IP location error: {ex.Message}");
            return new LocationResult { Status = LocationStatus.Failed };
        }
    }
}

/// <summary>
/// Response model for ip-api.com
/// </summary>
internal class IpApiResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("lat")]
    public double? Lat { get; set; }
    
    [JsonPropertyName("lon")]
    public double? Lon { get; set; }
    
    [JsonPropertyName("city")]
    public string? City { get; set; }
    
    [JsonPropertyName("country")]
    public string? Country { get; set; }
}
