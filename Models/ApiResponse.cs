using System.Text.Json.Serialization;

namespace EzanVakti.Models;

/// <summary>
/// Root response from Aladhan API
/// </summary>
public class AladhanResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public AladhanData? Data { get; set; }
}

/// <summary>
/// Response for Calendar/Monthly requests
/// </summary>
public class AladhanCalendarResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<AladhanData>? Data { get; set; }
}

public class AladhanData
{
    [JsonPropertyName("timings")]
    public AladhanTimings? Timings { get; set; }

    [JsonPropertyName("date")]
    public AladhanDate? Date { get; set; }
}

public class AladhanTimings
{
    [JsonPropertyName("Fajr")]
    public string Fajr { get; set; } = string.Empty;

    [JsonPropertyName("Sunrise")]
    public string Sunrise { get; set; } = string.Empty;

    [JsonPropertyName("Dhuhr")]
    public string Dhuhr { get; set; } = string.Empty;

    [JsonPropertyName("Asr")]
    public string Asr { get; set; } = string.Empty;

    [JsonPropertyName("Maghrib")]
    public string Maghrib { get; set; } = string.Empty;

    [JsonPropertyName("Isha")]
    public string Isha { get; set; } = string.Empty;

    [JsonPropertyName("Imsak")]
    public string Imsak { get; set; } = string.Empty;

    [JsonPropertyName("Midnight")]
    public string Midnight { get; set; } = string.Empty;
}

public class AladhanDate
{
    [JsonPropertyName("readable")]
    public string Readable { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("gregorian")]
    public AladhanGregorian? Gregorian { get; set; }
}

public class AladhanGregorian
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("day")]
    public string Day { get; set; } = string.Empty;

    [JsonPropertyName("month")]
    public AladhanMonth? Month { get; set; }

    [JsonPropertyName("year")]
    public string Year { get; set; } = string.Empty;
}

public class AladhanMonth
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("en")]
    public string En { get; set; } = string.Empty;
}
