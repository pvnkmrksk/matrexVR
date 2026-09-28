using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Kept separate from Choice's schema so numeric locust gains do not change other experiments.
[Serializable]
public class KannadiConfig
{
    public int? numberOfRings;
    public float? hexRadius;
    public float? spacing; // Legacy name; hexRadius takes precedence.
    public string kannadiTilePrefab;
    public float boundaryLengthX = 200f;
    public float boundaryLengthZ = 200f;
    public bool periodicBoundary = true;
    public bool animateOnMove;
    public float animationSpeedThreshold = 0.5f;
    public ColorConfig backgroundColor;
    public VRConfig[] vrConfigs;
    [JsonConverter(typeof(LocustGainConverter))] public float? closedLoopPosition;
    [JsonConverter(typeof(LocustGainConverter))] public float? closedLoopOrientation;

    public static KannadiConfig Load(Dictionary<string, object> parameters)
    {
        JObject json = new JObject();
        if (parameters != null && parameters.TryGetValue("configFile", out object file))
            json = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, file.ToString())));
        // Inline sequence parameters override the referenced experiment file.
        if (parameters != null)
            json.Merge(JObject.FromObject(parameters), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        if (json["animationSpeedThreshold"] == null && json["animationNoiseThreshold"] != null)
            json["animationSpeedThreshold"] = json["animationNoiseThreshold"];
        KannadiConfig config = json.ToObject<KannadiConfig>();
        config.Validate();
        return config;
    }

    public void Validate()
    {
        float radius = hexRadius ?? spacing ?? 10f;
        if (!Finite(radius) || radius <= 0) throw new ArgumentException("Kannadi hexRadius/spacing must be positive and finite.");
        if (numberOfRings > 100) throw new ArgumentException("Kannadi numberOfRings exceeds the supported limit of 100.");
        if (!Finite(boundaryLengthX) || !Finite(boundaryLengthZ) || boundaryLengthX <= 0 || boundaryLengthZ <= 0)
            throw new ArgumentException("Kannadi boundary dimensions must be positive and finite.");
        if (!Finite(animationSpeedThreshold) || animationSpeedThreshold < 0)
            throw new ArgumentException("animationSpeedThreshold must be finite and nonnegative (world units per second).");
        ValidateGain(closedLoopPosition); ValidateGain(closedLoopOrientation);
        var ids = new HashSet<int>();
        if (vrConfigs == null) return;
        foreach (VRConfig vr in vrConfigs)
        {
            if (vr == null || vr.vrIndex < 1 || vr.vrIndex > 4 || !ids.Add(vr.vrIndex))
                throw new ArgumentException("vrConfigs must contain unique vrIndex values from 1 to 4.");
            if (vr.watchIndex.HasValue && (vr.watchIndex < 1 || vr.watchIndex > 4))
                throw new ArgumentException("watchIndex must be from 1 to 4, or omitted to see the other animals.");
            ValidateGain(vr.closedLoopPosition); ValidateGain(vr.closedLoopOrientation);
        }
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static void ValidateGain(float? gain)
    {
        if (gain.HasValue && (!Finite(gain.Value) || gain < 0))
            throw new ArgumentException("Closed-loop gains must be finite and nonnegative.");
    }
}

[Serializable]
public class VRConfig
{
    public int vrIndex;
    public Vector3Config initialPosition;
    public Vector3Config initialRotation;
    public int? watchIndex;
    [JsonConverter(typeof(LocustGainConverter))] public float? closedLoopPosition;
    [JsonConverter(typeof(LocustGainConverter))] public float? closedLoopOrientation;
}

[Serializable]
public class Vector3Config
{
    public float x, y, z;
    public Vector3 ToVector3() => new Vector3(x, y, z);
}

public class LocustGainConverter : JsonConverter
{
    public override bool CanConvert(Type type) => type == typeof(float) || type == typeof(float?);
    public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        if (reader.TokenType == JsonToken.Boolean) return (bool)reader.Value ? 1f : 0f;
        if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer)
            return Convert.ToSingle(reader.Value, System.Globalization.CultureInfo.InvariantCulture);
        throw new JsonSerializationException("Closed-loop gain must be a number or boolean.");
    }
    public override bool CanWrite => false;
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();
}
