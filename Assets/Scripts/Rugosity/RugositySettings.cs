using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

[JsonConverter(typeof(StringEnumConverter))]
public enum RugosityHeightMode { AboveGround, Absolute }
[JsonConverter(typeof(StringEnumConverter))]
public enum RugosityOrientationMode { WorldUp, SurfaceNormal }

// Complete Inspector state. JSON patches use nullable fields so omission preserves scene tuning.
[Serializable]
public class RugositySettings
{
    public bool enabled = true;
    public string visualSurface = "RS";
    public string collisionSurface = "RS";
    public string heightSurface = ""; // Empty inherits collisionSurface.
    public string normalSurface = ""; // Empty inherits collisionSurface.
    public RugosityHeightMode heightMode = RugosityHeightMode.AboveGround;
    public RugosityOrientationMode orientationMode = RugosityOrientationMode.SurfaceNormal;
    public float centimetersPerUnit = 1;
    public float clearanceCm = 1;
    public float absoluteHeight = 1; // World Y, in scene units.
    public float normalRadiusCm = .5f;
    public int normalSamples = 5;
    public float tiltResponse = 0; // 0 = exact; otherwise inverse seconds.
}

[Serializable]
public class RugosityConfig
{
    public bool? enabled;
    public string visualSurface, collisionSurface, heightSurface, normalSurface;
    public RugosityHeightMode? heightMode;
    public RugosityOrientationMode? orientationMode;
    public float? centimetersPerUnit, clearanceCm, absoluteHeight, normalRadiusCm, tiltResponse;
    public int? normalSamples;
}
