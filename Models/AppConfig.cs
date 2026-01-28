using System.Text.Json.Serialization;

namespace EzanVakti.Models;

/// <summary>
/// Application configuration with coordinate support
/// </summary>
public class AppConfig
{
    [JsonPropertyName("city")]
    public string City { get; set; } = "İstanbul";

    [JsonPropertyName("country")]
    public string Country { get; set; } = "Turkey";

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("locationName")]
    public string? LocationName { get; set; }

    [JsonPropertyName("lastFetchDate")]
    public DateTime? LastFetchDate { get; set; }

    [JsonPropertyName("cachedPrayerTimes")]
    public CachedPrayerTimes? CachedPrayerTimes { get; set; }
}

public class CachedPrayerTimes
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("fajr")]
    public string Fajr { get; set; } = string.Empty;

    [JsonPropertyName("sunrise")]
    public string Sunrise { get; set; } = string.Empty;

    [JsonPropertyName("dhuhr")]
    public string Dhuhr { get; set; } = string.Empty;

    [JsonPropertyName("asr")]
    public string Asr { get; set; } = string.Empty;

    [JsonPropertyName("maghrib")]
    public string Maghrib { get; set; } = string.Empty;

    [JsonPropertyName("isha")]
    public string Isha { get; set; } = string.Empty;
}

/// <summary>
/// All 81 Turkish provinces with coordinates (proper Turkish characters)
/// </summary>
public static class TurkishCities
{
    public static readonly CityInfo[] AllCities = new[]
    {
        new CityInfo("Adana", 37.0000, 35.3213),
        new CityInfo("Adıyaman", 37.7648, 38.2786),
        new CityInfo("Afyonkarahisar", 38.7507, 30.5567),
        new CityInfo("Ağrı", 39.7191, 43.0503),
        new CityInfo("Aksaray", 38.3687, 34.0370),
        new CityInfo("Amasya", 40.6499, 35.8353),
        new CityInfo("Ankara", 39.9334, 32.8597),
        new CityInfo("Antalya", 36.8969, 30.7133),
        new CityInfo("Ardahan", 41.1105, 42.7022),
        new CityInfo("Artvin", 41.1828, 41.8183),
        new CityInfo("Aydın", 37.8560, 27.8416),
        new CityInfo("Balıkesir", 39.6484, 27.8826),
        new CityInfo("Bartın", 41.6344, 32.3375),
        new CityInfo("Batman", 37.8812, 41.1351),
        new CityInfo("Bayburt", 40.2552, 40.2249),
        new CityInfo("Bilecik", 40.0567, 30.0665),
        new CityInfo("Bingöl", 38.8854, 40.4966),
        new CityInfo("Bitlis", 38.3938, 42.1232),
        new CityInfo("Bolu", 40.7355, 31.6061),
        new CityInfo("Burdur", 37.7203, 30.2905),
        new CityInfo("Bursa", 40.1826, 29.0665),
        new CityInfo("Çanakkale", 40.1553, 26.4142),
        new CityInfo("Çankırı", 40.6013, 33.6134),
        new CityInfo("Çorum", 40.5506, 34.9556),
        new CityInfo("Denizli", 37.7765, 29.0864),
        new CityInfo("Diyarbakır", 37.9144, 40.2306),
        new CityInfo("Düzce", 40.8438, 31.1565),
        new CityInfo("Edirne", 41.6818, 26.5623),
        new CityInfo("Elazığ", 38.6810, 39.2264),
        new CityInfo("Erzincan", 39.7500, 39.5000),
        new CityInfo("Erzurum", 39.9000, 41.2700),
        new CityInfo("Eskişehir", 39.7767, 30.5206),
        new CityInfo("Gaziantep", 37.0662, 37.3833),
        new CityInfo("Giresun", 40.9128, 38.3895),
        new CityInfo("Gümüşhane", 40.4386, 39.5086),
        new CityInfo("Hakkari", 37.5833, 43.7333),
        new CityInfo("Hatay", 36.4018, 36.3498),
        new CityInfo("Iğdır", 39.9237, 44.0450),
        new CityInfo("Isparta", 37.7648, 30.5566),
        new CityInfo("İstanbul", 41.0082, 28.9784),
        new CityInfo("İzmir", 38.4237, 27.1428),
        new CityInfo("Kahramanmaraş", 37.5858, 36.9371),
        new CityInfo("Karabük", 41.2061, 32.6204),
        new CityInfo("Karaman", 37.1759, 33.2287),
        new CityInfo("Kars", 40.6167, 43.1000),
        new CityInfo("Kastamonu", 41.3887, 33.7827),
        new CityInfo("Kayseri", 38.7312, 35.4787),
        new CityInfo("Kilis", 36.7184, 37.1212),
        new CityInfo("Kırıkkale", 39.8468, 33.5153),
        new CityInfo("Kırklareli", 41.7333, 27.2167),
        new CityInfo("Kırşehir", 39.1425, 34.1709),
        new CityInfo("Kocaeli", 40.8533, 29.8815),
        new CityInfo("Konya", 37.8667, 32.4833),
        new CityInfo("Kütahya", 39.4167, 29.9833),
        new CityInfo("Malatya", 38.3552, 38.3095),
        new CityInfo("Manisa", 38.6191, 27.4289),
        new CityInfo("Mardin", 37.3212, 40.7245),
        new CityInfo("Mersin", 36.8121, 34.6415),
        new CityInfo("Muğla", 37.2153, 28.3636),
        new CityInfo("Muş", 38.9462, 41.7539),
        new CityInfo("Nevşehir", 38.6939, 34.6857),
        new CityInfo("Niğde", 37.9667, 34.6833),
        new CityInfo("Ordu", 40.9839, 37.8764),
        new CityInfo("Osmaniye", 37.0742, 36.2478),
        new CityInfo("Rize", 41.0201, 40.5234),
        new CityInfo("Sakarya", 40.6940, 30.4358),
        new CityInfo("Samsun", 41.2928, 36.3313),
        new CityInfo("Şanlıurfa", 37.1674, 38.7955),
        new CityInfo("Siirt", 37.9333, 41.9500),
        new CityInfo("Sinop", 42.0231, 35.1531),
        new CityInfo("Şırnak", 37.4187, 42.4918),
        new CityInfo("Sivas", 39.7477, 37.0179),
        new CityInfo("Tekirdağ", 40.9833, 27.5167),
        new CityInfo("Tokat", 40.3167, 36.5500),
        new CityInfo("Trabzon", 41.0015, 39.7178),
        new CityInfo("Tunceli", 39.3074, 39.4388),
        new CityInfo("Uşak", 38.6823, 29.4082),
        new CityInfo("Van", 38.4891, 43.4089),
        new CityInfo("Yalova", 40.6500, 29.2667),
        new CityInfo("Yozgat", 39.8181, 34.8147),
        new CityInfo("Zonguldak", 41.4564, 31.7987)
    };

    public static IEnumerable<CityInfo> GetAllCities() => AllCities;
    
    public static CityInfo? GetCity(string name) => 
        AllCities.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public class CityInfo
{
    public string Name { get; }
    public double Latitude { get; }
    public double Longitude { get; }

    public CityInfo(string name, double lat, double lon)
    {
        Name = name;
        Latitude = lat;
        Longitude = lon;
    }
}
