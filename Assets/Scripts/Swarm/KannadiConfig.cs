using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Kept separate from Choice's schema so numeric locust gains do not change other experiments.
[JsonConverter(typeof(StringEnumConverter))]
public enum RandomInitialHeadingDistribution { Continuous, Cardinal }

[JsonConverter(typeof(StringEnumConverter))]
public enum SwarmSpawnType { Random, Grid }

[Serializable]
public class KannadiConfig : SwarmConfig
{
    public string populationMode = "Mirror";
    public string kannadiModelPath;
    public SceneObject[] objects = Array.Empty<SceneObject>();
    public SunConfig sun;
    public HeadingReferenceConfig headingReference;
    public bool resetPositionOnStart = true;
    public bool resetRotationOnStart = true;
    public bool randomInitialRotation;
    public RandomInitialHeadingDistribution initialHeadingDistribution = RandomInitialHeadingDistribution.Continuous;
    public ColorConfig uniformSkyColor;
    public int? numberOfRings;
    public float? hexRadius;
    public float? spacing; // Legacy name; hexRadius takes precedence.
    public string kannadiTilePrefab;
    public string skyboxPath; // An explicit horizontal panorama takes precedence over nightSky.
    public NightSkyConfig nightSky; // Omitted preserves the existing scene background.
    public bool autopilotEnabled = false;
    public float autopilotSpeed = 10f;
    public ColorConfig backgroundColor;
    public VRConfig[] vrConfigs;
    [JsonConverter(typeof(LocustGainConverter))] public float? closedLoopPosition;
    [JsonConverter(typeof(LocustGainConverter))] public float? closedLoopOrientation;
    // Shared start poses used by nested standalone Swarm experiments. Per-VR poses take precedence.
    public Vector3Config initialPosition { get; set; }
    public Vector3Config initialRotation { get; set; }
    [JsonIgnore] public bool SwarmEnabled { get; private set; } = true;

    public static KannadiConfig Load(Dictionary<string, object> parameters) => Load(parameters, false);
    public static KannadiConfig LoadSwarm(Dictionary<string, object> parameters) => Load(parameters, true);

    private static KannadiConfig Load(Dictionary<string, object> parameters, bool readNestedSwarm)
    {
        JObject json = new JObject();
        if (ExperimentPhases.IsPhase(parameters))
            json = ExperimentPhases.Read(parameters);
        else if (parameters != null && parameters.TryGetValue("configFile", out object file))
            json = JObject.Parse(ExperimentConfigFiles.ReadAllText(file.ToString()));
        // Inline sequence parameters override the referenced experiment file.
        if (parameters != null && !ExperimentPhases.IsPhase(parameters))
        {
            JObject inline = JObject.FromObject(parameters);
            // Autopilot belongs to the referenced individual config file;
            // sequence-level duplicates cannot override it.
            inline.Remove("autopilotEnabled");
            inline.Remove("autopilotSpeed");
            inline.Remove("headingReference"); // Experiment-file only; never a sequence override.
            json.Merge(inline, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        }
        if (ExperimentPhases.IsPhase(parameters))
            foreach (string key in new[] { "populationMode", "worldUnitsPerMetre" })
                if (json[key] == null && parameters.TryGetValue(key, out object value)) json[key] = JToken.FromObject(value);
        bool swarmEnabled = true;
        readNestedSwarm |= string.Equals((string)json["populationMode"], "Swarm", StringComparison.OrdinalIgnoreCase);
        if (readNestedSwarm && json.TryGetValue("swarm", out JToken nested))
        {
            swarmEnabled = nested.Type != JTokenType.Null;
            if (swarmEnabled)
            {
                if (!(nested is JObject swarm)) throw new ArgumentException("swarm must be an object or null.");
                // Only swarm fields move into the inherited schema. Phase-level motion,
                // sky, gains and reset flags retain their own values.
                foreach (var field in typeof(SwarmConfig).GetFields())
                    if (swarm.TryGetValue(field.Name, out JToken value)) json[field.Name] = value.DeepClone();
            }
            json.Remove("swarm");
        }
        if (json["animationSpeedThreshold"] == null && json["animationNoiseThreshold"] != null)
            json["animationSpeedThreshold"] = json["animationNoiseThreshold"];
        KannadiConfig config = json.ToObject<KannadiConfig>();
        config.SwarmEnabled = swarmEnabled;
        config.Validate();
        return config;
    }

    public void Validate()
    {
        if (!string.Equals(populationMode, "Mirror", StringComparison.OrdinalIgnoreCase) && !string.Equals(populationMode, "Swarm", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("populationMode must be Mirror or Swarm.");
        sun?.Validate();
        if (!Enum.IsDefined(typeof(RandomInitialHeadingDistribution), initialHeadingDistribution))
            throw new ArgumentException("initialHeadingDistribution must be Continuous or Cardinal.");
        headingReference?.Validate();
        float radius = hexRadius ?? spacing ?? 10f;
        if (!Finite(radius) || radius <= 0) throw new ArgumentException("Kannadi hexRadius/spacing must be positive and finite.");
        if (numberOfRings > 100) throw new ArgumentException("Kannadi numberOfRings exceeds the supported limit of 100.");
        ValidateSwarm();
        if (HasAgentGroups && (!string.IsNullOrWhiteSpace(kannadiModelPath) ||
            (!string.IsNullOrWhiteSpace(kannadiTilePrefab) && kannadiTilePrefab != "SimulatedLocust")))
            throw new ArgumentException("agentGroups require the built-in SimulatedLocust model.");
        if (string.IsNullOrWhiteSpace(skyboxPath) && nightSky != null && nightSky.enabled) nightSky.Validate();
        if (!Finite(autopilotSpeed) || autopilotSpeed < 0)
            throw new ArgumentException("autopilotSpeed must be finite and nonnegative (world units per second).");
        ValidateGain(closedLoopPosition); ValidateGain(closedLoopOrientation);
        var ids = new HashSet<int>();
        if (vrConfigs == null) return;
        foreach (VRConfig vr in vrConfigs)
        {
            if (vr == null || vr.vrIndex < 1 || vr.vrIndex > RuntimeRigRegistry.MaximumRigCount || !ids.Add(vr.vrIndex))
                throw new ArgumentException("vrConfigs must contain unique vrIndex values from 1 through 16.");
            if (vr.watchIndex.HasValue && (vr.watchIndex < 1 || vr.watchIndex > RuntimeRigRegistry.MaximumRigCount))
                throw new ArgumentException("watchIndex must be a configured rig index from 1 through 16, or omitted to see the other animals.");
            ValidateGain(vr.closedLoopPosition); ValidateGain(vr.closedLoopOrientation);
        }
    }
    // A uniform draw is supplied by the scene so legacy files consume the same one
    // random value per reset. The inclusive upper endpoint remains the fourth cardinal.
    public float SampleInitialYaw(float uniformSample)
    {
        if (float.IsNaN(uniformSample) || float.IsInfinity(uniformSample)) throw new ArgumentException("Initial heading sample must be finite.");
        float sample = Math.Max(0, Math.Min(1, uniformSample));
        return initialHeadingDistribution == RandomInitialHeadingDistribution.Cardinal
            ? Math.Min(3, (int)Math.Floor(sample * 4)) * 90f : sample * 360f;
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
    public string shape = "Dot"; // Moth opts into a low-poly 3D body and GPU wing motion.
    public float bodyLengthMetres = 0.0254f;
    public float bodyDiameterMetres = 0.01f;
    public float wingSpanMetres = 0.04f;
    public float flapFrequencyHz = 30f;
    public float wingOpacity = 0.25f;
    public bool subpixelVisibility = true;
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
        if (!string.Equals(shape, "Dot", StringComparison.OrdinalIgnoreCase) && !string.Equals(shape, "Moth", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Bogong shape must be Dot or Moth.");
        if (!Finite(bodyLengthMetres) || bodyLengthMetres <= 0 || !Finite(bodyDiameterMetres) || bodyDiameterMetres <= 0 ||
            !Finite(wingSpanMetres) || wingSpanMetres <= 0 || !Finite(flapFrequencyHz) || flapFrequencyHz < 0 ||
            !Finite(wingOpacity) || wingOpacity < 0 || wingOpacity > 1)
            throw new ArgumentException("Moth dimensions must be positive; flap frequency nonnegative; wing opacity in [0,1].");
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

[JsonConverter(typeof(StringEnumConverter))]
public enum SwarmAgentAppearance { Stock, Black }

[Serializable]
public class SwarmAgentGroupConfig
{
    public int count = 128;
    public Vector3Config spawnOffset = new Vector3Config();
    public SwarmAgentAppearance appearance;
}

[Serializable]
public class SwarmConfig
{
    internal const float DefaultBoundaryLength = 200f;
    public string modelPath; // Optional embedded GLB visual for generated or recorded agents.
    public ModelAppearanceConfig modelAppearance;
    public string trajectoryFile = ""; // Blank retains the generated swarm.
    public SwarmPlaybackConfig playback = new SwarmPlaybackConfig();
    public float? worldUnitsPerMetre; // Omitted: scenes with active Terrain=1; legacy walking scenes=100.
    public float? nearClipMetres;
    public float? farClipMetres;
    public bool useHeadingReference = false;
    public string headingReferenceId = ""; // Empty retains the existing within-experiment reference.
    public string directionDistribution = "Legacy"; // VonMises opts into the normalized circular sampler.
    // With relative spawning or an available, opted-in heading reference, spawnCenter is an offset from the rig.
    public bool spawnRelativeToAnimal = false;
    public bool followAnimalPosition = false;
    // Swarm settings belong to the individual experiment file. Nullable values
    // preserve scene defaults when an older config omits a setting.
    public int? numberOfLocusts;
    public int? agentCount; // Preferred neutral name; takes precedence over numberOfLocusts.
    public float? density;
    public SwarmSpawnType spawnType = SwarmSpawnType.Random;
    public float? spawnAreaSize;
    public float? mu;
    public float? kappa;
    public float? locustSpeed;
    public float locustSpeedStdDev; // Nonnegative normal distribution, sampled once per generated agent. Zero preserves legacy speed/RNG.
    public float? agentSpeed;
    public float? agentSpeedStdDev;
    public float? locoSpeed; // Historical spelling accepted on input.
    public SwarmAgentGroupConfig[] agentGroups;
    [JsonIgnore] public bool HasAgentGroups => agentGroups != null && agentGroups.Length != 0;
    [JsonIgnore] public int? EffectiveAgentCount => HasAgentGroups
        ? agentGroups.Sum(group => group.count) : agentCount ?? numberOfLocusts;
    [JsonIgnore] public float? EffectiveAgentSpeed => agentSpeed ?? locustSpeed ?? locoSpeed;
    [JsonIgnore] public float EffectiveAgentSpeedStdDev => agentSpeedStdDev ?? locustSpeedStdDev;
    public float? volumeHalfExtentMetres; // Optional generated cube: +/- this physical distance on each axis.
    public bool preserveBoundaryOvershoot = true; // Retained for existing files. Generated swarms always preserve overshoot.
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
    public bool periodicBoundary = true;
    public float? boundaryLengthX;
    public float? boundaryLengthZ;
    public bool animateOnMove;
    public float animationSpeedThreshold = 0.5f;
    public void ValidateSwarm()
    {
        modelAppearance?.Validate();
        if (!string.IsNullOrWhiteSpace(headingReferenceId) && !useHeadingReference)
            throw new ArgumentException("headingReferenceId requires useHeadingReference=true.");
        if (!string.Equals(directionDistribution, "Legacy", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(directionDistribution, "VonMises", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("directionDistribution must be Legacy or VonMises.");
        playback?.Validate();
        if (nearClipMetres.HasValue && (!Finite(nearClipMetres.Value) || nearClipMetres <= 0))
            throw new ArgumentException("nearClipMetres must be positive metres.");
        if (farClipMetres.HasValue && (!Finite(farClipMetres.Value) || farClipMetres <= 0 || farClipMetres <= nearClipMetres))
            throw new ArgumentException("farClipMetres must be positive and greater than nearClipMetres when both are set.");
        if (worldUnitsPerMetre.HasValue && (!Finite(worldUnitsPerMetre.Value) || worldUnitsPerMetre <= 0))
            throw new ArgumentException("worldUnitsPerMetre must be positive.");
        foreach (var length in new[] { boundaryLengthX, boundaryLengthZ })
            if (length.HasValue && (!Finite(length.Value) || length <= 0))
                throw new ArgumentException("Boundary dimensions must be positive and finite.");
        if (!Finite(animationSpeedThreshold) || animationSpeedThreshold < 0)
            throw new ArgumentException("animationSpeedThreshold must be finite and nonnegative (world units per second).");
        if (numberOfLocusts.HasValue && numberOfLocusts.Value < 0)
            throw new ArgumentException("numberOfLocusts must be nonnegative.");
        if (agentCount.HasValue && agentCount.Value < 0)
            throw new ArgumentException("agentCount must be nonnegative.");
        if (density.HasValue && (!Finite(density.Value) || density.Value < 0))
            throw new ArgumentException("density must be finite and nonnegative agents per world-unit area/volume.");
        if (!Enum.IsDefined(typeof(SwarmSpawnType), spawnType))
            throw new ArgumentException("spawnType must be Random or Grid.");
        if (spawnAreaSize.HasValue && (!Finite(spawnAreaSize.Value) || spawnAreaSize.Value <= 0))
            throw new ArgumentException("spawnAreaSize must be positive and finite.");
        if (mu.HasValue && !Finite(mu.Value))
            throw new ArgumentException("mu must be finite degrees.");
        if (kappa.HasValue && (!Finite(kappa.Value) || kappa.Value < 0))
            throw new ArgumentException("kappa must be finite and nonnegative.");
        if (locustSpeed.HasValue && (!Finite(locustSpeed.Value) || locustSpeed.Value < 0))
            throw new ArgumentException("locustSpeed must be finite and nonnegative world units per second.");
        if (!Finite(locustSpeedStdDev) || locustSpeedStdDev < 0)
            throw new ArgumentException("locustSpeedStdDev must be finite and nonnegative world units per second.");
        foreach (var speed in new[] { agentSpeed, agentSpeedStdDev, locoSpeed })
            if (speed.HasValue && (!Finite(speed.Value) || speed.Value < 0))
                throw new ArgumentException("agentSpeed, agentSpeedStdDev and locoSpeed must be finite and nonnegative world units per second.");
        if (volumeHalfExtentMetres.HasValue && (!Finite(volumeHalfExtentMetres.Value) || volumeHalfExtentMetres <= 0))
            throw new ArgumentException("volumeHalfExtentMetres must be positive metres.");
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
        if (!volumeHalfExtentMetres.HasValue || worldUnitsPerMetre.HasValue)
            ValidateSpawnBounds(worldUnitsPerMetre ?? 1);
        if (!string.Equals(agentVisual, "ScenePrefab", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(agentVisual, "Bogong", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("agentVisual must be ScenePrefab or Bogong.");
        bogongVisual?.Validate();
        if (HasAgentGroups)
        {
            if (!string.IsNullOrWhiteSpace(trajectoryFile) || !string.IsNullOrWhiteSpace(modelPath) ||
                !string.Equals(agentVisual, "ScenePrefab", StringComparison.OrdinalIgnoreCase) || modelAppearance?.enabled == true)
                throw new ArgumentException("agentGroups require generated stock locusts and supply their own appearances.");
            long total = 0;
            foreach (var group in agentGroups)
            {
                if (group == null || group.count <= 0 || !Enum.IsDefined(typeof(SwarmAgentAppearance), group.appearance))
                    throw new ArgumentException("Every agent group needs a positive count and a Stock or Black appearance.");
                ValidateVector(group.spawnOffset, "agentGroups spawnOffset", false);
                if (!string.Equals(dimension, "3D", StringComparison.OrdinalIgnoreCase) && (group.spawnOffset?.y ?? 0) != 0)
                    throw new ArgumentException("agentGroups spawnOffset.y must be zero in a 2D swarm.");
                total += group.count;
            }
            if (total > int.MaxValue) throw new ArgumentException("The combined agent group count is too large.");
        }
    }

    internal float? PhysicalArenaSize(float unitsPerMetre) => volumeHalfExtentMetres.HasValue
        ? (float)(2d * volumeHalfExtentMetres.Value * unitsPerMetre) : (float?)null;

    internal void ValidateSpawnBounds(float unitsPerMetre)
    {
        if (!string.IsNullOrWhiteSpace(trajectoryFile)) return;
        float? cubeSide = PhysicalArenaSize(unitsPerMetre);
        if (cubeSide.HasValue && (!Finite(cubeSide.Value) || cubeSide.Value <= 0))
            throw new ArgumentException("volumeHalfExtentMetres must produce a positive finite arena size in scene units.");
        if (!periodicBoundary) return;
        float? x = spawnVolumeSize?.x ?? spawnAreaSize;
        float? z = spawnVolumeSize?.z ?? spawnAreaSize;
        if (x > (boundaryLengthX ?? cubeSide ?? DefaultBoundaryLength) || z > (boundaryLengthZ ?? cubeSide ?? DefaultBoundaryLength))
            throw new ArgumentException("The spawn area must fit inside the periodic X and Z lengths.");
        bool verticalWrap = wrapY && string.Equals(dimension, "3D", StringComparison.OrdinalIgnoreCase);
        if (verticalWrap && (spawnVolumeSize?.y ?? spawnAreaSize) > (boundaryHeight ?? cubeSide))
            throw new ArgumentException("The spawn volume must fit inside the periodic boundary height.");
        if (!HasAgentGroups) return;
        float lengthX = boundaryLengthX ?? cubeSide ?? DefaultBoundaryLength;
        float lengthZ = boundaryLengthZ ?? cubeSide ?? DefaultBoundaryLength;
        float? lengthY = verticalWrap ? boundaryHeight ?? cubeSide : null;
        float implicitSize = Math.Min(cubeSide ?? 0, Math.Min(lengthX, lengthZ));
        if (lengthY.HasValue) implicitSize = Math.Min(implicitSize, lengthY.Value);
        ValidateAgentGroupBounds(new Vector3(x ?? implicitSize, spawnVolumeSize?.y ?? spawnAreaSize ?? implicitSize, z ?? implicitSize),
            lengthX, lengthZ, lengthY);
    }
    internal void ValidateAgentGroupBounds(Vector3 size, float lengthX, float lengthZ, float? lengthY)
    {
        if (!HasAgentGroups || !periodicBoundary) return;
        foreach (var group in agentGroups)
        {
            if (group == null) continue; // ValidateSwarm reports malformed entries.
            Vector3 offset = group.spawnOffset?.ToVector3() ?? Vector3.zero;
            if (size.x + 2d * Math.Abs(offset.x) > lengthX || size.z + 2d * Math.Abs(offset.z) > lengthZ ||
                lengthY.HasValue && size.y + 2d * Math.Abs(offset.y) > lengthY.Value)
                throw new ArgumentException("Every agent group's spawn area and offset must fit inside the shared periodic arena.");
        }
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

[Serializable]
public class SwarmPlaybackConfig
{
    public float dataUnitsPerMetre = 1f;
    public bool loop = true;
    public float speed = 1f;
    public float startTimeSeconds;
    public float maxGapSeconds = 0.1f;
    public float yawOffsetDegrees;
    public bool centerOnAnimal = true;
    public bool periodicBoundary = true;
    // Opt-in stream mode preserves historical recorded-lifetime/periodic playback by default.
    public bool continueAfterTrack = false;
    public float despawnDistanceMetres = 100f;
    public float terminalVelocityWindowSeconds = 0.1f;
    // Null preserves measured terminal speed; an override affects extrapolated tails only.
    public float? continuationSpeedMetresPerSecond;
    // Straight-line incoming extrapolation; zero retains the original first-sample spawn.
    public float preRollSeconds = 0;
    public void Validate()
    {
        if (float.IsNaN(dataUnitsPerMetre) || float.IsInfinity(dataUnitsPerMetre) || dataUnitsPerMetre <= 0 ||
            float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0 ||
            float.IsNaN(startTimeSeconds) || float.IsInfinity(startTimeSeconds) || startTimeSeconds < 0 ||
            float.IsNaN(maxGapSeconds) || float.IsInfinity(maxGapSeconds) || maxGapSeconds <= 0 ||
            float.IsNaN(yawOffsetDegrees) || float.IsInfinity(yawOffsetDegrees) ||
            float.IsNaN(despawnDistanceMetres) || float.IsInfinity(despawnDistanceMetres) || despawnDistanceMetres <= 0 ||
            float.IsNaN(terminalVelocityWindowSeconds) || float.IsInfinity(terminalVelocityWindowSeconds) || terminalVelocityWindowSeconds < 0 ||
            float.IsNaN(preRollSeconds) || float.IsInfinity(preRollSeconds) || preRollSeconds < 0)
            throw new ArgumentException("Invalid swarm playback timing/rotation.");
        if (continuationSpeedMetresPerSecond.HasValue && (float.IsNaN(continuationSpeedMetresPerSecond.Value) ||
            float.IsInfinity(continuationSpeedMetresPerSecond.Value) || continuationSpeedMetresPerSecond <= 0))
            throw new ArgumentException("playback.continuationSpeedMetresPerSecond must be positive and finite, or null for measured speed.");
        if (continueAfterTrack && periodicBoundary)
            throw new ArgumentException("Continuing CSV tracks requires playback.periodicBoundary=false; moths must fly out rather than recycle through the mocap volume.");
        if (preRollSeconds > 0 && !continueAfterTrack)
            throw new ArgumentException("playback.preRollSeconds requires continueAfterTrack=true.");
    }
}
