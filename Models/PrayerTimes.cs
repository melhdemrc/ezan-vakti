namespace EzanVakti.Models;

/// <summary>
/// Represents the daily prayer times
/// </summary>
public class PrayerTimes
{
    public DateTime Date { get; set; }
    public TimeOnly Fajr { get; set; }      // İmsak
    public TimeOnly Sunrise { get; set; }   // Güneş
    public TimeOnly Dhuhr { get; set; }     // Öğle
    public TimeOnly Asr { get; set; }       // İkindi
    public TimeOnly Maghrib { get; set; }   // Akşam
    public TimeOnly Isha { get; set; }      // Yatsı

    /// <summary>
    /// Gets all prayer times as a dictionary for easy iteration
    /// </summary>
    public Dictionary<PrayerType, TimeOnly> GetAllPrayers()
    {
        return new Dictionary<PrayerType, TimeOnly>
        {
            { PrayerType.Fajr, Fajr },
            { PrayerType.Sunrise, Sunrise },
            { PrayerType.Dhuhr, Dhuhr },
            { PrayerType.Asr, Asr },
            { PrayerType.Maghrib, Maghrib },
            { PrayerType.Isha, Isha }
        };
    }
}

/// <summary>
/// Prayer type enumeration with display names
/// </summary>
public enum PrayerType
{
    Fajr,       // İmsak
    Sunrise,    // Güneş
    Dhuhr,      // Öğle
    Asr,        // İkindi
    Maghrib,    // Akşam
    Isha        // Yatsı
}

/// <summary>
/// Extension methods for PrayerType
/// </summary>
public static class PrayerTypeExtensions
{
    public static string GetDisplayName(this PrayerType prayer)
    {
        return prayer switch
        {
            PrayerType.Fajr => "İmsak",
            PrayerType.Sunrise => "Güneş",
            PrayerType.Dhuhr => "Öğle",
            PrayerType.Asr => "İkindi",
            PrayerType.Maghrib => "Akşam",
            PrayerType.Isha => "Yatsı",
            _ => prayer.ToString()
        };
    }
}

/// <summary>
/// Represents information about the next prayer
/// </summary>
public class NextPrayerInfo
{
    public PrayerType Prayer { get; set; }
    public DateTime PrayerTime { get; set; }
    public TimeSpan TimeRemaining { get; set; }
    public bool IsNextDay { get; set; }

    public string PrayerName => Prayer.GetDisplayName();
    
    public string FormattedTimeRemaining
    {
        get
        {
            if (TimeRemaining.TotalHours >= 1)
            {
                return $"{(int)TimeRemaining.TotalHours:D2}:{TimeRemaining.Minutes:D2}:{TimeRemaining.Seconds:D2}";
            }
            return $"{TimeRemaining.Minutes:D2}:{TimeRemaining.Seconds:D2}";
        }
    }
}
