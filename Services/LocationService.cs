using Windows.Devices.Geolocation;

namespace EzanVakti.Services;

/// <summary>
/// Service for getting user's location via Windows Location Service
/// </summary>
public static class LocationService
{
    /// <summary>
    /// Gets location from Windows Location Service (GPS/WiFi/IP)
    /// </summary>
    public static async Task<LocationResult?> GetWindowsLocationAsync()
    {
        try
        {
            // Request access to location
            var accessStatus = await Geolocator.RequestAccessAsync();
            
            if (accessStatus != GeolocationAccessStatus.Allowed)
            {
                System.Diagnostics.Debug.WriteLine($"Location access denied: {accessStatus}");
                return null;
            }

            // Get location
            var geolocator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default
            };

            var position = await geolocator.GetGeopositionAsync();
            
            return new LocationResult
            {
                Latitude = position.Coordinate.Point.Position.Latitude,
                Longitude = position.Coordinate.Point.Position.Longitude,
                Accuracy = position.Coordinate.Accuracy
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Windows location error: {ex.Message}");
            return null;
        }
    }
}

public class LocationResult
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }

    public string DisplayName => $"{Latitude:F4}°, {Longitude:F4}°";
    
    public string DisplayWithAccuracy => Accuracy.HasValue 
        ? $"{DisplayName} (±{Accuracy:F0}m)" 
        : DisplayName;
}
