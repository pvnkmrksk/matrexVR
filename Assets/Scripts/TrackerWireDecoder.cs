using System;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>Decode tracker transports without changing the upstream numeric conventions.</summary>
public static class TrackerWireDecoder
{
    public struct Sample
    {
        public Vector3 position, rotation;
        public Quaternion orientation;
        public bool ficTracRow;
        public string format;
    }
    private sealed class LegacyPose
    {
        public float x, y, z, roll, pitch, yaw;
    }
    public static bool IsPayload(string text)
    {
        string value=(text??"").TrimStart();
        return value.StartsWith("{",StringComparison.Ordinal) || value.StartsWith("FT,",StringComparison.Ordinal);
    }
    public static Sample Decode(string text, ClosedLoopMode mode)
    {
        if(string.IsNullOrWhiteSpace(text) || text.Length>32768) throw new FormatException("Tracker messages must contain 1–32768 characters.");
        string value=text.Trim();
        if(value.StartsWith("FT,",StringComparison.Ordinal))
        {
            if(!TrackerBackend.IsWalking(mode)) throw new FormatException("FicTrac FT rows require the HejjéTrac/FicTrac walking backend.");
            string[] fields=value.Split(',');
            if(fields.Length!=26 || fields[0].Trim()!="FT") throw new FormatException("FicTrac messages require FT followed by exactly 25 numeric columns.");
            var numbers=new double[25];
            for(int i=0;i<numbers.Length;i++)
                if(!double.TryParse(fields[i+1].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out numbers[i]) || double.IsNaN(numbers[i]) || double.IsInfinity(numbers[i]))
                    throw new FormatException("FicTrac column "+(i+1)+" must be finite.");
            if(!ulong.TryParse(fields[1].Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out _) || !ulong.TryParse(fields[23].Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out _))
                throw new FormatException("FicTrac frame and sequence columns must be nonnegative integers.");
            float forward=CheckedFloat(numbers[14]),east=CheckedFloat(numbers[15]),heading=CheckedFloat(numbers[16]);
            return new Sample {position=new Vector3(forward,east,0),rotation=new Vector3(0,heading,0),orientation=Quaternion.Euler(0,heading*Mathf.Rad2Deg,0),ficTracRow=true,format="FicTrac FT25"};
        }
        if(!value.StartsWith("{",StringComparison.Ordinal)) throw new FormatException("Expected JSON tracker pose or a FicTrac FT row.");
        var pose=JsonConvert.DeserializeObject<LegacyPose>(value);
        if(pose==null || !Finite(pose.x) || !Finite(pose.y) || !Finite(pose.z) || !Finite(pose.pitch) || !Finite(pose.yaw) || !Finite(pose.roll))
            throw new FormatException("Tracking pose must contain finite numbers.");
        // Kinefly/Rekkefly yaw is deliberately retained in radians for the flight
        // controller. Legacy walking JSON keeps its historical degree quaternion.
        return new Sample {position=new Vector3(pose.x,pose.y,pose.z),rotation=new Vector3(pose.pitch,pose.yaw,pose.roll),orientation=Quaternion.Euler(pose.pitch,pose.yaw,pose.roll),format="JSON pose"};
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float CheckedFloat(double value)
    {
        float result=(float)value;
        if(!Finite(result)) throw new FormatException("Tracker position/heading exceeds the runtime range.");
        return result;
    }
}
