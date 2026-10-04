using System;
using System.Globalization;

/// <summary>Observer and clock settings for a stars-only celestial stimulus.</summary>
[Serializable]
public sealed class NightSkyConfig
{
    public bool enabled = true;
    public double latitude = 47.6896; // University of Konstanz, Universitaetsstrasse 10.
    public double longitude = 9.1881;  // East positive.
    public string timeZoneId = "Europe/Berlin";
    public double? utcOffsetHours;   // Preferred fixed offset: 11 = UTC+11; 5.5 = UTC+05:30.
    public int? utcOffsetMinutes;
    public string date;              // yyyy-MM-dd; absent means today's date in the selected zone.
    public string localTime = "now"; // now or HH:mm[:ss]; absent/blank also means now.
    public bool advanceWithRealTime = true;
    public int updateIntervalMinutes = 30;
    public bool roundToInterval = false; // Start at the exact requested instant unless explicitly rounded.
    public float northYawDegrees;    // Geographic north clockwise from Unity +Z.
    public float exposure = 1;
    public float faintDetailCutoff;  // Linear source brightness; zero preserves all detail (not magnitude).
    public bool maskBelowHorizon = true;
    public int imageWidth = 4096;

    public void Validate()
    {
        if (!Finite(latitude) || latitude < -90 || latitude > 90 ||
            !Finite(longitude) || longitude < -180 || longitude > 180)
            throw new ArgumentException("nightSky latitude/longitude must be finite degrees in [-90,90]/[-180,180].");
        if (updateIntervalMinutes < 1 || updateIntervalMinutes > 60)
            throw new ArgumentException("nightSky updateIntervalMinutes must be between 1 and 60.");
        if (!Finite(northYawDegrees) || !Finite(exposure) || exposure <= 0 || exposure > 100)
            throw new ArgumentException("nightSky northYawDegrees must be finite and exposure must be in (0,100].");
        if (imageWidth != 1024 && imageWidth != 2048 && imageWidth != 4096)
            throw new ArgumentException("nightSky imageWidth must be 1024, 2048 or 4096 (height is half width).");
        if (utcOffsetMinutes.HasValue && Math.Abs((long)utcOffsetMinutes.Value) > 840)
            throw new ArgumentException("nightSky utcOffsetMinutes must be in [-840,840].");
        if (utcOffsetHours.HasValue && (!Finite(utcOffsetHours.Value) || Math.Abs(utcOffsetHours.Value) > 14 ||
            Math.Abs(utcOffsetHours.Value * 60 - Math.Round(utcOffsetHours.Value * 60)) > 1e-7))
            throw new ArgumentException("nightSky utcOffsetHours must be in [-14,14], in whole-minute increments (e.g. 5.5).");
        if (utcOffsetHours.HasValue && utcOffsetMinutes.HasValue)
            throw new ArgumentException("Supply only one of nightSky utcOffsetHours and utcOffsetMinutes.");
        if (!Finite(faintDetailCutoff) || faintDetailCutoff < 0 || faintDetailCutoff > 1)
            throw new ArgumentException("nightSky faintDetailCutoff must be in [0,1].");
    }

    public DateTimeOffset ResolveStartUtc(DateTimeOffset now)
    {
        Validate();
        // No calendar override: use the actual instant, independent of the computer's zone.
        bool currentClock = string.IsNullOrWhiteSpace(localTime) || string.Equals(localTime.Trim(), "now", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(date) && currentClock) return now.ToUniversalTime();
        TimeSpan? offset = FixedOffset;
        TimeZoneInfo zone = offset.HasValue ? null : FindTimeZone(timeZoneId);
        DateTime localNow = offset.HasValue
            ? now.ToOffset(offset.Value).DateTime
            : TimeZoneInfo.ConvertTime(now, zone).DateTime;
        DateTime day = string.IsNullOrWhiteSpace(date) ? localNow.Date
            : DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None);
        TimeSpan clock = currentClock ? localNow.TimeOfDay
            : DateTime.ParseExact(localTime, new[] { "HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF" },
                CultureInfo.InvariantCulture, DateTimeStyles.None).TimeOfDay;
        DateTime local = DateTime.SpecifyKind(day.Date + clock, DateTimeKind.Unspecified);
        if (offset.HasValue)
            return new DateTimeOffset(local, offset.Value).ToUniversalTime();
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new ArgumentException("nightSky local time is skipped or repeated by daylight saving. Supply utcOffsetHours to specify the intended instant.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    public DateTimeOffset SampleUtc(DateTimeOffset instant)
    {
        long interval = TimeSpan.TicksPerMinute * updateIntervalMinutes;
        long ticks = instant.UtcDateTime.Ticks;
        return new DateTimeOffset(ticks - ticks % interval + (ticks % interval >= interval / 2 ? interval : 0), TimeSpan.Zero);
    }

    public DateTimeOffset ObserverLocalTime(DateTimeOffset instant)
    {
        Validate();
        TimeSpan? offset = FixedOffset;
        return offset.HasValue
            ? instant.ToOffset(offset.Value)
            : TimeZoneInfo.ConvertTime(instant, FindTimeZone(timeZoneId));
    }

    private TimeSpan? FixedOffset => utcOffsetHours.HasValue
        ? TimeSpan.FromMinutes(Math.Round(utcOffsetHours.Value * 60))
        : utcOffsetMinutes.HasValue ? TimeSpan.FromMinutes(utcOffsetMinutes.Value) : (TimeSpan?)null;

    private static TimeZoneInfo FindTimeZone(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("nightSky requires utcOffsetHours or timeZoneId for local calendar inputs.");
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException)
        {
            // Unity Mono on Windows uses Windows IDs; Unix uses IANA IDs.
            if (id == "Europe/Berlin") return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
            if (id == "W. Europe Standard Time") return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
            if (id == "Australia/Sydney") return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
            if (id == "AUS Eastern Standard Time") return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
            throw new ArgumentException("Unknown nightSky timeZoneId '" + id + "'. Use utcOffsetHours or an OS-supported zone ID.");
        }
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
