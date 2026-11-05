using System;
using UnityEngine;

/// <summary>One timestamp for every piece of the rendered Unity frame.</summary>
public static class RecordedFrameClock
{
    private static int frame = -1;
    private static DateTime timestamp;
    public static double MonotonicSeconds => Time.unscaledTimeAsDouble;
    public static DateTime Now
    {
        get { if(frame!=Time.frameCount){frame=Time.frameCount;timestamp=DateTime.Now;}return timestamp; }
    }
}
