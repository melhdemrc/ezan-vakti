using System.IO;
using System.Text.Json;
using EzanVakti.Models;

namespace EzanVakti.Services;

/// <summary>
/// Singleton service for managing application configuration
/// </summary>
public sealed class ConfigService
{
    private static readonly Lazy<ConfigService> _instance = new(() => new ConfigService());
    public static ConfigService Instance => _instance.Value;

    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private AppConfig _config;

    public AppConfig Config => _config;
    public event Action? ConfigChanged;

    private ConfigService()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        _config = new AppConfig();
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
                if (config != null) _config = config;
            }
        }
        catch
        {
            _config = new AppConfig();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_config, _jsonOptions);
            await File.WriteAllTextAsync(_configPath, json);
            ConfigChanged?.Invoke();
        }
        catch { }
    }

    /// <summary>
    /// Update location using city coordinates
    /// </summary>
    public void UpdateCity(string cityName)
    {
        var city = TurkishCities.GetCity(cityName);
        if (city != null)
        {
            _config.City = city.Name;
            _config.Latitude = city.Latitude;
            _config.Longitude = city.Longitude;
            _config.LocationName = city.Name;
        }
        else
        {
            _config.City = cityName;
            _config.Latitude = null;
            _config.Longitude = null;
        }
        
        // Clear cache when location changes
        _config.CachedPrayerTimes = null;
        _config.LastFetchDate = null;
    }

    /// <summary>
    /// Update location using exact coordinates
    /// </summary>
    public void UpdateCoordinates(double lat, double lon, string? locationName = null)
    {
        _config.Latitude = lat;
        _config.Longitude = lon;
        _config.LocationName = locationName ?? $"{lat:F4}, {lon:F4}";
        _config.CachedPrayerTimes = null;
        _config.LastFetchDate = null;
    }

    public void UpdateCache(PrayerTimes prayerTimes)
    {
        _config.LastFetchDate = DateTime.Today;
        _config.CachedPrayerTimes = new CachedPrayerTimes
        {
            Date = prayerTimes.Date,
            Fajr = prayerTimes.Fajr.ToString("HH:mm"),
            Sunrise = prayerTimes.Sunrise.ToString("HH:mm"),
            Dhuhr = prayerTimes.Dhuhr.ToString("HH:mm"),
            Asr = prayerTimes.Asr.ToString("HH:mm"),
            Maghrib = prayerTimes.Maghrib.ToString("HH:mm"),
            Isha = prayerTimes.Isha.ToString("HH:mm")
        };
    }

    public PrayerTimes? GetCachedPrayerTimes()
    {
        if (_config.CachedPrayerTimes == null || _config.LastFetchDate?.Date != DateTime.Today)
            return null;

        try
        {
            return new PrayerTimes
            {
                Date = _config.CachedPrayerTimes.Date,
                Fajr = TimeOnly.Parse(_config.CachedPrayerTimes.Fajr),
                Sunrise = TimeOnly.Parse(_config.CachedPrayerTimes.Sunrise),
                Dhuhr = TimeOnly.Parse(_config.CachedPrayerTimes.Dhuhr),
                Asr = TimeOnly.Parse(_config.CachedPrayerTimes.Asr),
                Maghrib = TimeOnly.Parse(_config.CachedPrayerTimes.Maghrib),
                Isha = TimeOnly.Parse(_config.CachedPrayerTimes.Isha)
            };
        }
        catch { return null; }
    }

    public string GetLocationDisplay()
    {
        if (!string.IsNullOrEmpty(_config.LocationName))
            return _config.LocationName;
        if (_config.Latitude.HasValue && _config.Longitude.HasValue)
            return $"{_config.Latitude:F2}°, {_config.Longitude:F2}°";
        return _config.City;
    }
}
