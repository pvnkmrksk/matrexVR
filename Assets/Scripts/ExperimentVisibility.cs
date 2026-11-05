using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class VisibilityMatrixConfig
{
    public bool enabled;
    public bool[][] rows;
}

/// <summary>Optional observer-to-VR population visibility. Other world layers retain their authored masks.</summary>
public static class ExperimentVisibility
{
    public const string Field = "visibilityMatrix";
    private static ConditionalWeakTable<JObject, VisibilityMatrixConfig> applied =
        new ConditionalWeakTable<JObject, VisibilityMatrixConfig>();
    private sealed class CameraState { public int OriginalPopulationBits; public bool Applied; }
    private static ConditionalWeakTable<Camera, CameraState> cameraStates = new ConditionalWeakTable<Camera, CameraState>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        applied = new ConditionalWeakTable<JObject, VisibilityMatrixConfig>();
        cameraStates = new ConditionalWeakTable<Camera, CameraState>();
    }

    public static JObject Default(int dimension = RuntimeRigRegistry.MaximumRigCount)
    {
        if (dimension < 1 || dimension > RuntimeRigRegistry.MaximumRigCount)
            throw new ArgumentOutOfRangeException(nameof(dimension));
        return new JObject { ["enabled"] = false, ["rows"] = new JArray(
            Enumerable.Range(0, dimension).Select(row => new JArray(
                Enumerable.Range(0, dimension).Select(column => column != row)))) };
    }

    private static VisibilityMatrixConfig Read(JObject configuration)
    {
        var value = configuration?[Field];
        if (value == null || value.Type == JTokenType.Null) return new VisibilityMatrixConfig();
        if (!(value is JObject settings)) throw new ArgumentException("visibilityMatrix must contain enabled and rows.");
        if (settings["enabled"] != null && settings["enabled"].Type != JTokenType.Boolean)
            throw new ArgumentException("Visibility matrix enabled must be a checkbox/boolean value.");
        if ((bool?)settings["enabled"] != true) return new VisibilityMatrixConfig();
        if (!(settings["rows"] is JArray rows) || rows.Count < 1 || rows.Count > RuntimeRigRegistry.MaximumRigCount)
            throw new ArgumentException("Visibility matrix needs 1 to 16 square rows, indexed by VR number.");
        var result = new VisibilityMatrixConfig { enabled = true, rows = new bool[rows.Count][] };
        for (int row = 0; row < rows.Count; row++)
        {
            if (!(rows[row] is JArray columns) || columns.Count != rows.Count ||
                columns.Any(cell => cell.Type != JTokenType.Boolean))
                throw new ArgumentException("Visibility matrix must be square, using true/false for every cell.");
            result.rows[row] = columns.Values<bool>().ToArray();
        }
        return result;
    }

    public static void Validate(JObject configuration, int largestRigIndex = 0)
    {
        var settings = Read(configuration);
        if (settings.enabled && settings.rows.Length < largestRigIndex)
            throw new ArgumentException("Visibility matrix has no row/column for VR" + largestRigIndex + ". Resize it or disable the matrix.");
    }

    public static int CameraMask(JObject configuration, string observer, int original)
    {
        if (configuration?[Field] == null) return original;
        // Effective presentation payloads remain immutable while applied. A weak
        // cache keeps camera updates allocation-free without retaining old trials.
        var settings = applied.GetValue(configuration, Read);
        if (!settings.enabled) return original;
        int index = RuntimeRigRegistry.ParseIndex(observer) - 1;
        if (index < 0 || index >= settings.rows.Length)
            throw new ArgumentException("Visibility matrix has no observer row for " + observer + ".");
        int visible = 0;
        for (int column = 0; column < settings.rows.Length; column++)
            if (settings.rows[index][column]) visible |= 1 << RuntimeRigRegistry.StimulusLayer(column + 1);
        return (original & ~RuntimeRigRegistry.StimulusMask) | visible;
    }

    public static void Apply(Scene scene, JObject configuration)
    {
        Restore(scene);
        var rigs = ReplayExecutionContext.Rigs(scene);
        Validate(configuration, rigs.Select(rig => RuntimeRigRegistry.ParseIndex(rig.name)).DefaultIfEmpty(0).Max());
        foreach (var rig in rigs)
            foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
            {
                int original = camera.cullingMask;
                int next = CameraMask(configuration, rig.name, original);
                if (next == original) continue;
                var state = cameraStates.GetValue(camera, _ => new CameraState());
                state.OriginalPopulationBits = original & RuntimeRigRegistry.StimulusMask;
                state.Applied = true;
                camera.cullingMask = next;
            }
    }

    /// <summary>Restore before the original controller configures the next phase's default cameras.</summary>
    public static void Restore(Scene scene)
    {
        foreach (var rig in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ClosedLoop>(true)))
            foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
                if (cameraStates.TryGetValue(camera, out var state) && state.Applied)
                {
                    camera.cullingMask = (camera.cullingMask & ~RuntimeRigRegistry.StimulusMask) | state.OriginalPopulationBits;
                    state.Applied = false;
                }
    }
}
