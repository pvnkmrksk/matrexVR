using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Kept separate from Choice's schema so numeric locust gains do not change other experiments.
[Serializable]
public class KannadiConfig : SwarmConfig
{
    public HeadingReferenceConfig headingReference;
    public bool resetPositionOnStart = true;
    public bool resetRotationOnStart = true;
    public bool randomInitialRotation;
    public ColorConfig uniformSkyColor;
    public int? numberOfRings;
    public float? hexRadius;
    public float? spacing; // Legacy name; hexRadius takes precedence.
    public string kannadiTilePrefab;
    public bool periodicBoundary = true;
    public string skyboxPath; // An explicit horizontal panorama takes precedence over nightSky.
    public NightSkyConfig nightSky; // Omitted preserves the existing scene background.
    public bool autopilotEnabled = false;
    public float autopilotSpeed = 10f;
    public ColorConfig backgroundColor;
    public VRConfig[] vrConfigs;
    [JsonConverter(typeof(LocustGainConverter))] public float? closedLoopPosition;
    [JsonConverter(typeof(LocustGainConverter))] public float? closedLoopOrientation;

    public static KannadiConfig Load(Dictionary<string, object> parameters)
    {
        JObject json = new JObject();
        if (ExperimentPhases.IsPhase(parameters))
        {
            KannadiConfig phase = ExperimentPhases.Read(parameters).ToObject<KannadiConfig>();
            phase.Validate(); return phase;
        }
        if (parameters != null && parameters.TryGetValue("configFile", out object file))
            json = JObject.Parse(File.ReadAllText(ExperimentConfigFiles.Resolve(file.ToString())));
        // Inline sequence parameters override the referenced experiment file.
        if (parameters != null)
        {
            JObject inline = JObject.FromObject(parameters);
            // Autopilot belongs to the referenced individual config file;
            // sequence-level duplicates cannot override it.
            inline.Remove("autopilotEnabled");
            inline.Remove("autopilotSpeed");
            inline.Remove("headingReference"); // Experiment-file only; never a sequence override.
            json.Merge(inline, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        }
        if (json["animationSpeedThreshold"] == null && json["animationNoiseThreshold"] != null)
            json["animationSpeedThreshold"] = json["animationNoiseThreshold"];
        KannadiConfig config = json.ToObject<KannadiConfig>();
        config.Validate();
        return config;
    }

    public void Validate()
    {
        headingReference?.Validate();
        float radius = hexRadius ?? spacing ?? 10f;
        if (!Finite(radius) || radius <= 0) throw new ArgumentException("Kannadi hexRadius/spacing must be positive and finite.");
        if (numberOfRings > 100) throw new ArgumentException("Kannadi numberOfRings exceeds the supported limit of 100.");
        ValidateSwarm();
        if (string.IsNullOrWhiteSpace(skyboxPath) && nightSky != null && nightSky.enabled) nightSky.Validate();
        if (!Finite(autopilotSpeed) || autopilotSpeed < 0)
            throw new ArgumentException("autopilotSpeed must be finite and nonnegative (world units per second).");
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

[Serializable]
public class BogongVisualConfig
{
    // World keeps normal perspective. Angular keeps the apparent diameter in degrees.
    public string sizeMode = "World";
    public float size = 0.5f;
    public float angularSizeDegrees = 1f;
    public ColorConfig color = new ColorConfig { r = 0.12f, g = 0.12f, b = 0.12f, a = 1f };
    public float metallic; // Legacy input; flat Bogong patches ignore material lighting fields.
    public float smoothness = 0.25f;
    public float flickerFrequencyHz;
    public float flickerDutyCycle = 0.5f;

    public void Validate()
    {
        if (!string.Equals(sizeMode, "World", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sizeMode, "Angular", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Bogong sizeMode must be World or Angular.");
        if (!Finite(size) || size <= 0 || !Finite(angularSizeDegrees) || angularSizeDegrees <= 0 || angularSizeDegrees >= 180)
            throw new ArgumentException("Bogong size must be positive and finite; angularSizeDegrees must be in (0,180).");
        if (!Finite(metallic) || metallic < 0 || metallic > 1 || !Finite(smoothness) || smoothness < 0 || smoothness > 1)
            throw new ArgumentException("Bogong metallic and smoothness must be in [0,1].");
        if (!Finite(flickerFrequencyHz) || flickerFrequencyHz < 0 || !Finite(flickerDutyCycle) || flickerDutyCycle < 0 || flickerDutyCycle > 1)
            throw new ArgumentException("Bogong flicker settings are invalid.");
        if (color == null) throw new ArgumentException("Bogong color must be supplied.");
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
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

[Serializable]
public class SwarmConfig
{
    public bool useHeadingReference = false;
    // With relative spawning or an available, opted-in heading reference, spawnCenter is an offset from the rig.
    public bool spawnRelativeToAnimal = false;
    public bool followAnimalPosition = false;
    // Swarm settings belong to the individual experiment file. Nullable values
    // preserve scene defaults when an older config omits a setting.
    public int? numberOfLocusts;
    public float? density;
    public float? spawnAreaSize;
    public float? mu;
    public float? kappa;
    public float? locustSpeed;
    // "2D" is the historical X/Z swarm. "3D" enables a sampled Y volume and Y wrapping.
    public string dimension = "2D";
    public Vector3Config spawnCenter;
    public Vector3Config spawnVolumeSize;
    public float? muElevation;
    public float? kappaElevation;
    public float? boundaryHeight;
    public bool wrapY;
    public string agentVisual = "ScenePrefab";
    public BogongVisualConfig bogongVisual;
    public float boundaryLengthX = 200f;
    public float boundaryLengthZ = 200f;
    public bool animateOnMove;
    public float animationSpeedThreshold = 0.5f;
    public void ValidateSwarm()
    {
        if (!Finite(boundaryLengthX) || !Finite(boundaryLengthZ) || boundaryLengthX <= 0 || boundaryLengthZ <= 0)
            throw new ArgumentException("Boundary dimensions must be positive and finite.");
        if (!Finite(animationSpeedThreshold) || animationSpeedThreshold < 0)
            throw new ArgumentException("animationSpeedThreshold must be finite and nonnegative (world units per second).");
        if (numberOfLocusts.HasValue && numberOfLocusts.Value < 0)
            throw new ArgumentException("numberOfLocusts must be nonnegative.");
        if (density.HasValue && (!Finite(density.Value) || density.Value < 0))
            throw new ArgumentException("density must be finite and nonnegative agents per world-unit area/volume.");
        if (spawnAreaSize.HasValue && (!Finite(spawnAreaSize.Value) || spawnAreaSize.Value <= 0))
            throw new ArgumentException("spawnAreaSize must be positive and finite.");
        if (mu.HasValue && !Finite(mu.Value))
            throw new ArgumentException("mu must be finite degrees.");
        if (kappa.HasValue && (!Finite(kappa.Value) || kappa.Value < 0))
            throw new ArgumentException("kappa must be finite and nonnegative.");
        if (locustSpeed.HasValue && (!Finite(locustSpeed.Value) || locustSpeed.Value < 0))
            throw new ArgumentException("locustSpeed must be finite and nonnegative world units per second.");
        if (!string.Equals(dimension, "2D", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(dimension, "3D", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("dimension must be 2D or 3D.");
        ValidateVector(spawnCenter, "spawnCenter", false);
        ValidateVector(spawnVolumeSize, "spawnVolumeSize", true);
        if (muElevation.HasValue && !Finite(muElevation.Value))
            throw new ArgumentException("muElevation must be finite degrees.");
        if (kappaElevation.HasValue && (!Finite(kappaElevation.Value) || kappaElevation.Value < 0))
            throw new ArgumentException("kappaElevation must be finite and nonnegative.");
        if (boundaryHeight.HasValue && (!Finite(boundaryHeight.Value) || boundaryHeight.Value <= 0))
            throw new ArgumentException("boundaryHeight must be positive and finite when supplied.");
        if (!string.Equals(agentVisual, "ScenePrefab", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(agentVisual, "Bogong", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("agentVisual must be ScenePrefab or Bogong.");
        bogongVisual?.Validate();
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static void ValidateVector(Vector3Config value, string name, bool positive)
    {
        if (value == null) return;
        if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z) ||
            (positive && (value.x <= 0 || value.y <= 0 || value.z <= 0)))
            throw new ArgumentException(name + " must contain finite" + (positive ? " positive" : "") + " components.");
    }
}
