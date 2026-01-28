using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using EzanVakti.Models;
using System.IO;

namespace EzanVakti.Services;

/// <summary>
/// Service for fetching and calculating prayer times
/// Uses Aladhan API with coordinates for precise calculation
/// </summary>
public sealed class PrayerService : IDisposable
{
    private static readonly Lazy<PrayerService> _instance = new(() => new PrayerService());
    public static PrayerService Instance => _instance.Value;

    private readonly JsonSerializerOptions _jsonOptions;
    private Dictionary<string, PrayerTimes> _monthlyCache = new();
    private string _currentCacheKey = string.Empty;

    // Use HTTPS for better connectivity
    private const string API_BY_CITY_MONTHLY = "https://api.aladhan.com/v1/calendarByCity";
    private const string API_BY_COORDS_MONTHLY = "https://api.aladhan.com/v1/calendar";
    private const string API_BY_CITY_DAILY = "https://api.aladhan.com/v1/timingsByCity";
    private const string API_BY_COORDS_DAILY = "https://api.aladhan.com/v1/timings";
    private const int DIYANET_METHOD = 13;

    public event Action<PrayerTimes>? PrayerTimesUpdated;

    private PrayerService()
    {
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task<PrayerTimes?> GetPrayerTimesAsync(DateTime date)
    {
        var config = ConfigService.Instance.Config;
        string cityKey = config.City;
        if (config.Latitude.HasValue && config.Longitude.HasValue && config.LocationName == "Windows Konum")
        {
            cityKey = $"gps_{config.Latitude:F2}_{config.Longitude:F2}";
        }
        
        var cacheKey = $"{cityKey}_{date.Month}_{date.Year}";
        var dateKey = date.ToString("yyyy-MM-dd");

        // 1. Check memory cache
        if (_monthlyCache.ContainsKey(cacheKey) && _monthlyCache.TryGetValue(dateKey, out var memTimes))
        {
            return memTimes;
        }

        // 2. Check/Load local file cache
        if (_currentCacheKey != cacheKey)
        {
            if (await LoadLocalCacheAsync(cacheKey))
            {
                if (_monthlyCache.TryGetValue(dateKey, out var fileTimes))
                    return fileTimes;
            }
        }

        // 3. Fetch from API (Monthly)
        if (await FetchAndCacheMonthlyAsync(config, date))
        {
            if (_monthlyCache.TryGetValue(dateKey, out var newTimes))
            {
                PrayerTimesUpdated?.Invoke(newTimes);
                return newTimes;
            }
        }

        // 4. FALLBACK: Fetch Single Day (Daily)
        // If monthly fails, try to get just today's data to keep app running
        var dailyTimes = await FetchDailyAsync(config, date);
        if (dailyTimes != null)
        {
            // Add to cache temporarily (but don't save entire file to avoid corrupting monthly cache with single day)
            _monthlyCache[dateKey] = dailyTimes;
            PrayerTimesUpdated?.Invoke(dailyTimes);
            return dailyTimes;
        }

        return null;
    }

    public async Task<PrayerTimes?> GetTodayPrayerTimesAsync()
    {
        return await GetPrayerTimesAsync(DateTime.Today);
    }

    public async Task<NextPrayerInfo?> GetNextPrayerAsync()
    {
        var now = DateTime.Now;
        var today = await GetPrayerTimesAsync(now.Date);
        if (today == null) return null;

        var currentTime = TimeOnly.FromDateTime(now);
        var prayers = today.GetAllPrayers();
        
        foreach (var prayer in prayers)
        {
            if (prayer.Value > currentTime)
            {
                var prayerDateTime = now.Date.Add(prayer.Value.ToTimeSpan());
                return new NextPrayerInfo
                {
                    Prayer = prayer.Key,
                    PrayerTime = prayerDateTime,
                    TimeRemaining = prayerDateTime - now,
                    IsNextDay = false
                };
            }
        }

        var tomorrow = await GetPrayerTimesAsync(now.Date.AddDays(1));
        if (tomorrow != null)
        {
            var tomorrowFajr = now.Date.AddDays(1).Add(tomorrow.Fajr.ToTimeSpan());
            return new NextPrayerInfo
            {
                Prayer = PrayerType.Fajr,
                PrayerTime = tomorrowFajr,
                TimeRemaining = tomorrowFajr - now,
                IsNextDay = true
            };
        }

        return null;
    }

    private async Task<bool> LoadLocalCacheAsync(string cacheKey)
    {
        try
        {
            var fileName = GetCacheFileName(cacheKey);
            if (File.Exists(fileName))
            {
                var json = await File.ReadAllTextAsync(fileName);
                var items = JsonSerializer.Deserialize<Dictionary<string, PrayerTimes>>(json);
                if (items != null)
                {
                    _monthlyCache = items;
                    _currentCacheKey = cacheKey;
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private async Task<bool> FetchAndCacheMonthlyAsync(AppConfig config, DateTime date)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            
            string url;
            if (config.Latitude.HasValue && config.Longitude.HasValue && config.LocationName == "Windows Konum")
            {
                url = $"{API_BY_COORDS_MONTHLY}?latitude={config.Latitude.Value.ToString(CultureInfo.InvariantCulture)}&longitude={config.Longitude.Value.ToString(CultureInfo.InvariantCulture)}&method={DIYANET_METHOD}&month={date.Month}&year={date.Year}";
            }
            else
            {
                url = $"{API_BY_CITY_MONTHLY}?city={Uri.EscapeDataString(config.City)}&country={Uri.EscapeDataString(config.Country)}&method={DIYANET_METHOD}&month={date.Month}&year={date.Year}";
            }

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<AladhanCalendarResponse>(json, _jsonOptions);

            if (apiResponse?.Data == null) return false;

            var newCache = new Dictionary<string, PrayerTimes>();
            string[] dateFormats = { "dd-MM-yyyy", "d-M-yyyy", "dd-M-yyyy", "d-MM-yyyy" };

            foreach (var item in apiResponse.Data)
            {
                if (item.Timings != null && item.Date?.Gregorian?.Date != null)
                {
                    if (DateTime.TryParseExact(item.Date.Gregorian.Date, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var itemDate))
                    {
                        var times = CreatePrayerTimes(itemDate, item.Timings);
                        newCache[itemDate.ToString("yyyy-MM-dd")] = times;
                    }
                }
            }

            if (newCache.Any())
            {
                _monthlyCache = newCache;
                
                string cityKey = config.City;
                if (config.Latitude.HasValue && config.Longitude.HasValue && config.LocationName == "Windows Konum")
                {
                    cityKey = $"gps_{config.Latitude:F2}_{config.Longitude:F2}";
                }
                _currentCacheKey = $"{cityKey}_{date.Month}_{date.Year}";

                var cacheJson = JsonSerializer.Serialize(_monthlyCache);
                await File.WriteAllTextAsync(GetCacheFileName(_currentCacheKey), cacheJson);
                
                CleanupOldCaches();
                return true;
            }
        }
        catch (Exception)
        {
            // Silent fail for monthly, will try daily fallback
        }
        return false;
    }

    private async Task<PrayerTimes?> FetchDailyAsync(AppConfig config, DateTime date)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            string url;
            var dateStr = date.ToString("dd-MM-yyyy");

            if (config.Latitude.HasValue && config.Longitude.HasValue && config.LocationName == "Windows Konum")
            {
                var timestamp = ((DateTimeOffset)date).ToUnixTimeSeconds();
                url = $"{API_BY_COORDS_DAILY}/{timestamp}?latitude={config.Latitude.Value.ToString(CultureInfo.InvariantCulture)}&longitude={config.Longitude.Value.ToString(CultureInfo.InvariantCulture)}&method={DIYANET_METHOD}";
            }
            else
            {
                url = $"{API_BY_CITY_DAILY}?city={Uri.EscapeDataString(config.City)}&country={Uri.EscapeDataString(config.Country)}&method={DIYANET_METHOD}&date={dateStr}";
            }

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<AladhanResponse>(json, _jsonOptions);

            if (apiResponse?.Data?.Timings != null)
            {
                return CreatePrayerTimes(date, apiResponse.Data.Timings);
            }
        }
        catch (Exception ex)
        {
             MessageBox.Show($"Veri çekme hatası: {ex.Message}", "Bağlantı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        return null;
    }

    private PrayerTimes CreatePrayerTimes(DateTime date, AladhanTimings timings)
    {
        return new PrayerTimes
        {
            Date = date,
            Fajr = ParseTime(timings.Fajr),
            Sunrise = ParseTime(timings.Sunrise),
            Dhuhr = ParseTime(timings.Dhuhr),
            Asr = ParseTime(timings.Asr),
            Maghrib = ParseTime(timings.Maghrib),
            Isha = ParseTime(timings.Isha)
        };
    }

    private string GetCacheFileName(string key)
    {
        string safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"cache_{safeKey}.json");
    }

    private void CleanupOldCaches()
    {
        try
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var files = Directory.GetFiles(dir, "cache_*.json");
            foreach (var file in files)
            {
                if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-60))
                {
                    File.Delete(file);
                }
            }
        }
        catch { }
    }

    private static TimeOnly ParseTime(string timeStr)
    {
        var cleanTime = timeStr.Split(' ')[0].Trim();
        if (TimeOnly.TryParseExact(cleanTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return time;
        if (TimeOnly.TryParseExact(cleanTime, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
            return time;
        return TimeOnly.MinValue;
    }

    public void ClearCache()
    {
        _monthlyCache.Clear();
        _currentCacheKey = string.Empty;
        try 
        {
             var dir = AppDomain.CurrentDomain.BaseDirectory;
             var files = Directory.GetFiles(dir, "cache_*.json");
             foreach (var f in files) File.Delete(f);
        }
        catch { }
    }

    public void Dispose() { }
}
