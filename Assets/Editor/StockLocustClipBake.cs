using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class StockLocustClipBake
{
    private const string PrefabPath = "Assets/Prefabs/Locust/SimulatedLocust.prefab";
    private const string CachePath = "Assets/Resources/NativeLocust/StockWalkCycle.asset";
    private const int Samples = 4096;

    private static string SourceIdentity() =>
        StockLocustClipCache.FormatVersion + ":" + AssetDatabase.GetAssetDependencyHash(PrefabPath);

    internal static void RequireCurrentCache()
    {
        var cache = AssetDatabase.LoadAssetAtPath<StockLocustClipCache>(CachePath);
        string expected = SourceIdentity();
        if (cache != null && cache.Identity == expected) return;

        string current = cache != null ? cache.Identity : "missing";
        throw new BuildFailedException($"The stock locust walk-cycle cache is out of date ({current}, expected {expected}). " +
            "Switch Unity to the intended build target and choose MATREX/Swarm/Bake Stock Locust Walk Cycle, " +
            "or run -executeMethod StockLocustClipBake.Bake with that target, then rebuild.");
    }

    [MenuItem("MATREX/Swarm/Bake Stock Locust Walk Cycle")]
    public static void Bake()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(prefab != null, "The stock SimulatedLocust prefab is missing.");
        var instance = UnityEngine.Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        var cache = ScriptableObject.CreateInstance<StockLocustClipCache>();
        try
        {
            var root = instance.transform;
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = instance.GetComponent<Animator>();
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            Require(controller != null && controller.layers.Length == 1 && controller.parameters.Length == 0,
                "The stock Animator must have exactly one layer and no parameters.");
            var layer = controller.layers[0];
            var machine = layer.stateMachine;
            var state = machine.defaultState;
            var animation = state != null ? state.motion as AnimationClip : null;
            Require(animation != null && animation.isLooping && animation.length > 0 &&
                machine.states.Length == 1 && machine.stateMachines.Length == 0 && machine.behaviours.Length == 0 &&
                machine.anyStateTransitions.Length == 0 && machine.entryTransitions.Length == 0 &&
                state.transitions.Length == 0 && state.behaviours.Length == 0 && animation.events.Length == 0 &&
                !layer.iKPass && !state.iKOnFeet && !state.mirror && state.speed == 1 && state.cycleOffset == 0,
                "The stock Animator must be a single looping clip without transitions, events or IK.");
            Require(skin != null && skin.sharedMesh != null && skin.sharedMesh.isReadable &&
                skin.sharedMesh.blendShapeCount == 0 && skin.bones.Length > 0 &&
                skin.bones.All(bone => bone != null), "The stock locust mesh and skin must be readable.");
            var mesh = skin.sharedMesh;
            var bones = skin.bones;
            var bindPoses = mesh.bindposes;
            var weights = mesh.boneWeights;
            var vertices = mesh.vertices;
            Require(bindPoses.Length == bones.Length && weights.Length == vertices.Length &&
                weights.All(weight => weight.boneIndex0 >= 0 && weight.boneIndex0 < bones.Length &&
                    weight.weight0 > 0 && weight.weight0 >= Mathf.Max(weight.weight1, Mathf.Max(weight.weight2, weight.weight3))),
                "The stock mesh needs matching bind poses and one dominant bone per vertex.");

            int hash = Animator.StringToHash(layer.name + "." + state.name);
            animator.Rebind();
            animator.speed = 1;
            animator.Update(0);
            Require(animator.HasState(0, hash), "Unity could not initialize the stock walk state.");
            CheckRootMotion(animator, root, hash, animation.length);

            var matrices = new StockLocustClipCache.SkinMatrix[Samples * bones.Length];
            float radiusSquared = 0;
            for (int index = 0; index < Samples; index++)
            {
                Seek(animator, root, hash, (float)index / Samples);
                var inverse = root.worldToLocalMatrix;
                int offset = index * bones.Length;
                for (int bone = 0; bone < bones.Length; bone++)
                    matrices[offset + bone] = new StockLocustClipCache.SkinMatrix(inverse * bones[bone].localToWorldMatrix * bindPoses[bone]);
                for (int vertex = 0; vertex < vertices.Length; vertex++)
                {
                    var point = matrices[offset + weights[vertex].boneIndex0].TransformPoint(vertices[vertex]);
                    radiusSquared = Mathf.Max(radiusSquared, point.sqrMagnitude);
                }
            }
            string identity = SourceIdentity();
            float radius = Mathf.Sqrt(radiusSquared);
            cache.Initialize(identity, mesh, animator, Samples, bones.Length, hash, animation.length, radius * 1.001f, matrices);
            var (replayError, quantizationError) = ValidateSamples(cache, prefab, bindPoses, weights, vertices);
            Require(replayError <= radius * .00001f, "Stock walk-cycle playback differs at the recorded phases: " + replayError);

            var saved = AssetDatabase.LoadAssetAtPath<StockLocustClipCache>(CachePath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(cache, CachePath);
                cache = null;
            }
            else
            {
                EditorUtility.CopySerialized(cache, saved);
                EditorUtility.SetDirty(saved);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Stock walk-cycle bake passed. Samples {Samples}, bones {bones.Length}, clip seconds {animation.length:R}, " +
                $"radius {radius:R}, maximum replay vertex error {replayError:R}, " +
                $"maximum subframe quantization displacement {quantizationError:R}, identity {identity}.");
        }
        finally
        {
            if (cache != null) UnityEngine.Object.DestroyImmediate(cache);
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void CheckRootMotion(Animator animator, Transform root, int hash, float length)
    {
        const int steps = 1024;
        const float tolerance = .00001f;
        Seek(animator, root, hash, 0);
        float deltaPosition = 0, rootPosition = 0, deltaRotation = 0, rootRotation = 0;
        for (int step = 0; step <= steps; step++)
        {
            animator.Update(length / steps);
            deltaPosition = Mathf.Max(deltaPosition, animator.deltaPosition.magnitude);
            rootPosition = Mathf.Max(rootPosition, root.position.magnitude);
            deltaRotation = Mathf.Max(deltaRotation, RotationDifference(animator.deltaRotation));
            rootRotation = Mathf.Max(rootRotation, RotationDifference(root.rotation));
        }
        Debug.Log($"Stock walk root motion over a full cycle and seam. Translation delta {deltaPosition:R}, " +
            $"absolute root displacement {rootPosition:R}, rotation delta {deltaRotation:R}, root rotation {rootRotation:R}.");
        Require(Mathf.Max(deltaPosition, rootPosition, deltaRotation, rootRotation) <= tolerance,
            "The stock Animator moves its world root. Preserve this motion before using a pose-only cache.");
    }

    private static float RotationDifference(Quaternion rotation) =>
        new Vector3(rotation.x, rotation.y, rotation.z).magnitude;

    private static void Seek(Animator animator, Transform root, int hash, float phase)
    {
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        animator.Play(hash, 0, phase);
        animator.Update(0);
    }

    private static (float replayError, float quantizationError) ValidateSamples(StockLocustClipCache cache,
        GameObject prefab, Matrix4x4[] bindPoses, BoneWeight[] weights, Vector3[] vertices)
    {
        var instance = UnityEngine.Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var animator = instance.GetComponent<Animator>();
            var root = instance.transform;
            var bones = instance.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            var live = new StockLocustClipCache.SkinMatrix[bones.Length];
            float replayError = 0, quantizationError = 0;
            var random = new System.Random(1997);
            animator.Rebind();
            animator.Update(0);
            Require(cache.SampleIndex(-.0000001) == 0 && cache.SampleIndex(.9999999) == 0 &&
                cache.SampleIndex(1.0000001) == 0, "The stock walk-cycle phase must wrap across the loop seam.");
            for (int sample = 0; sample < 521; sample++)
            {
                float phase = sample == 0 ? .9999999f : sample == 1 ? 0 : (float)random.NextDouble();
                int index = cache.SampleIndex(phase);
                Seek(animator, root, cache.StateHash, cache.SamplePhase(index));
                replayError = Mathf.Max(replayError, Compare(index));
                Seek(animator, root, cache.StateHash, phase);
                quantizationError = Mathf.Max(quantizationError, Compare(index));
            }
            return (replayError, quantizationError);

            float Compare(int index)
            {
                float largest = 0;
                var inverse = root.worldToLocalMatrix;
                for (int bone = 0; bone < bones.Length; bone++)
                    live[bone] = new StockLocustClipCache.SkinMatrix(inverse * bones[bone].localToWorldMatrix * bindPoses[bone]);
                for (int vertex = 0; vertex < vertices.Length; vertex++)
                {
                    int bone = weights[vertex].boneIndex0;
                    var point = vertices[vertex];
                    largest = Mathf.Max(largest, Vector3.Distance(live[bone].TransformPoint(point),
                        cache.MatrixAt(index, bone).TransformPoint(point)));
                }
                return largest;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

public sealed class StockLocustClipBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (PlayerSettings.GetGraphicsAPIs(report.summary.platform).Contains(GraphicsDeviceType.Vulkan))
            StockLocustClipBake.RequireCurrentCache();
    }
}
