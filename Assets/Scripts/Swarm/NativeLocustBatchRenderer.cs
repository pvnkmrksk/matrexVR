using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

/// <summary>Instancing of stock locusts using a shared clip or live Animator bones.</summary>
[DefaultExecutionOrder(1050)]
public sealed class NativeLocustBatchRenderer : MonoBehaviour
{
    private const int MaxInstances = 256;
    private static readonly bool requested =
        Array.IndexOf(Environment.GetCommandLineArgs(), "--stock-locust-renderers") < 0;
    private static readonly bool verifyGeometry =
        Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-batch-native-locusts") >= 0;
    private static readonly bool useSharedClip = !verifyGeometry &&
        Array.IndexOf(Environment.GetCommandLineArgs(), "--live-stock-locust-animators") < 0;
    private static readonly int BoneData = Shader.PropertyToID("_LocustBones");
    private static readonly int BoneOffset = Shader.PropertyToID("_LocustBoneOffset");
    private static readonly List<NativeLocustBatchRenderer> groups = new List<NativeLocustBatchRenderer>();
    private static Camera[] cameras = Array.Empty<Camera>();
    private static int cameraCount;
    private static int lastFrame = -1;
    private static int submittedFrame = -1;
    private static JobHandle pendingTransforms;
    private static bool transformsPending;

    private SkinnedMeshRenderer[] skins;
    private Transform[] roots;
    private float[] radii;
    private bool[] activeAgents;
    private Bounds[] worldBounds;
    private TransformAccessArray bones;
    private NativeArray<Matrix4x4> bindPoses;
    private NativeArray<SkinMatrix> matrices;
    private GraphicsBuffer gpuMatrices;
    private StockLocustPlayback playback;
    private GraphicsBuffer clipMatrices;
    private TransformAccessArray clipRoots;
    private NativeArray<Matrix4x4> clipRootMatrices;
    private float[] clipOffsets;
    private Mesh mesh;
    private Material material;
    private MaterialPropertyBlock properties;
    private readonly Plane[] frustum = new Plane[6];
    private readonly Matrix4x4[] instances = new Matrix4x4[MaxInstances];
    private readonly float[] offsets = new float[MaxInstances];
    private int boneCount;
    private ReflectionProbeUsage reflectionUsage;
    private bool replacingSkins;
    private bool verifiedGeometry;
    private int preparedFrame = -1;

    private struct SkinMatrix
    {
        public Vector4 x, y, z;
    }

    [BurstCompile(FloatMode = FloatMode.Strict)]
    private struct ReadBones : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<Matrix4x4> bindPoses;
        [WriteOnly] public NativeArray<SkinMatrix> output;

        public void Execute(int index, TransformAccess bone)
        {
            if (!bone.isValid) return;
            Matrix4x4 matrix = bone.localToWorldMatrix * bindPoses[index % bindPoses.Length];
            output[index] = new SkinMatrix { x = matrix.GetRow(0), y = matrix.GetRow(1), z = matrix.GetRow(2) };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Strict)]
    private struct ReadRoots : IJobParallelForTransform
    {
        [WriteOnly] public NativeArray<Matrix4x4> output;

        public void Execute(int index, TransformAccess root)
        {
            if (root.isValid) output[index] = root.localToWorldMatrix;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGroups()
    {
        CompletePendingTransforms();
        groups.Clear();
        lastFrame = submittedFrame = -1;
        cameras = Array.Empty<Camera>();
        cameraCount = 0;
    }

    internal static GameObject TryCreate(Transform parent, int layer, GameObject prefab,
        IReadOnlyList<GameObject> agents, IReadOnlyList<float> initialPhases = null)
    {
        if (!requested || !Application.isPlaying || prefab == null || agents.Count < 32) return null;
        var catalog = Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog");
        if (catalog == null || prefab != catalog.Find("SimulatedLocust")) return null;
        if (!SupportsStockForward())
        {
            if (verifyGeometry)
                Debug.LogWarning("Native locust instancing requires Vulkan, Built-in Forward, one skin weight, and the shadow-free stock lighting profile.");
            return null;
        }
        var host = new GameObject("Instanced stock locusts") { layer = layer };
        host.transform.SetParent(parent, false);
        var batch = host.AddComponent<NativeLocustBatchRenderer>();
        try
        {
            if (batch.Configure(agents, initialPhases))
            {
                host.AddComponent<NativeLocustBatchSubmission>();
                return host;
            }
            if (verifyGeometry)
                Debug.LogWarning("Native locust instancing could not use the stock shader or skinning layout. Using original renderers.");
        }
        catch (Exception error)
        {
            Debug.LogWarning("Native locust instancing reverted to the original renderers: " + error.Message);
        }
        host.SetActive(false);
        Destroy(host);
        return null;
    }

    private static bool SupportsStockForward() =>
        SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan && SystemInfo.graphicsShaderLevel >= 45 &&
        SystemInfo.supportsInstancing && GraphicsSettings.currentRenderPipeline == null &&
        QualitySettings.skinWeights == SkinWeights.OneBone && QualitySettings.shadows == ShadowQuality.Disable &&
        QualitySettings.pixelLightCount == 0 && (LightmapSettings.lightProbes == null || LightmapSettings.lightProbes.count == 0);

    private bool Configure(IReadOnlyList<GameObject> agents, IReadOnlyList<float> initialPhases)
    {
        var template = Resources.Load<Material>("NativeLocust/InstancedStock");
        var first = agents[0].GetComponentInChildren<SkinnedMeshRenderer>();
        if (template == null || template.shader == null || !template.shader.isSupported || first == null ||
            first.sharedMesh == null || !first.sharedMesh.isReadable || first.sharedMesh.subMeshCount != 1 ||
            first.sharedMesh.blendShapeCount != 0 || first.sharedMaterial == null)
            return false;
        var stock = first.sharedMesh;
        Material sourceMaterial = first.sharedMaterial;
        bool black = sourceMaterial == StockLocustAppearance.BlackMaterial;
        if (!black && (sourceMaterial.shader.name != "Standard" || sourceMaterial.shaderKeywords.Length != 0))
            return false;
        reflectionUsage = first.reflectionProbeUsage;
        Matrix4x4[] pose = stock.bindposes;
        BoneWeight[] weights = stock.boneWeights;
        boneCount = pose.Length;
        if (boneCount == 0 || weights.Length != stock.vertexCount) return false;

        skins = new SkinnedMeshRenderer[agents.Count];
        roots = new Transform[agents.Count];
        radii = new float[agents.Count];
        activeAgents = new bool[agents.Count];
        worldBounds = new Bounds[agents.Count];
        for (int i = 0; i < skins.Length; i++)
        {
            var agent = agents[i];
            var skin = agent.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = agent.GetComponent<Animator>();
            if (skin == null || !skin.enabled || skin.sharedMesh != stock || skin.sharedMaterial != sourceMaterial ||
                skin.reflectionProbeUsage != reflectionUsage ||
                skin.HasPropertyBlock() || (skin.quality != SkinQuality.Auto && skin.quality != SkinQuality.Bone1) ||
                animator == null || animator.cullingMode != AnimatorCullingMode.AlwaysAnimate) return false;
            Transform[] currentBones = skin.bones;
            if (currentBones.Length != boneCount || Array.Exists(currentBones, bone => bone == null)) return false;
            skins[i] = skin;
            roots[i] = agent.transform;
            Vector3 scale = skin.transform.lossyScale;
            if (scale.x <= 0 || Mathf.Abs(scale.x - scale.y) > .001f || Mathf.Abs(scale.x - scale.z) > .001f) return false;
            // Use a rotation-independent conservative bound about the root.
            radii[i] = Vector3.Distance(skin.bounds.center, roots[i].position) + skin.bounds.extents.magnitude;
        }

        var boneIds = new List<Vector2>(weights.Length);
        foreach (var weight in weights)
        {
            if (weight.boneIndex0 < 0 || weight.boneIndex0 >= boneCount || weight.weight0 <= 0 ||
                weight.weight0 < Mathf.Max(weight.weight1, Mathf.Max(weight.weight2, weight.weight3))) return false;
            boneIds.Add(new Vector2(weight.boneIndex0, 0));
        }
        mesh = Instantiate(stock);
        mesh.name = "Stock locust instanced geometry";
        mesh.SetUVs(3, boneIds);
        material = new Material(template);
        material.CopyPropertiesFromMaterial(sourceMaterial);
        if (black) material.EnableKeyword("MATREX_LOCUST_BLACK");
        material.enableInstancing = true;
        properties = new MaterialPropertyBlock();
        for (int i = 0; i < instances.Length; i++) instances[i] = Matrix4x4.identity;

        bool cached = TryConfigureClip(agents, initialPhases);
        if (!cached) ConfigureLiveBones(pose);
        replacingSkins = true;
        foreach (var skin in skins) skin.enabled = false;
        groups.Add(this);
        string mode = cached ? $"shared clip, {clipMatrices.count / boneCount} samples" : "live bones";
        Debug.Log($"Native locust instancing active for {skins.Length} agents ({EvidencePosition()}, {mode}, {(black ? "black" : "stock")}).");
        return true;
    }

    private bool TryConfigureClip(IReadOnlyList<GameObject> agents, IReadOnlyList<float> initialPhases)
    {
        if (!useSharedClip || initialPhases == null || initialPhases.Count != agents.Count ||
            !StockLocustClipCache.TryGet(skins[0], agents[0].GetComponent<Animator>(), out var clip) ||
            clip.BoneCount != boneCount || clip.SampleCount <= 0 || (long)clip.SampleCount * boneCount > (1 << 24) ||
            clip.SkinningBuffer == null || !clip.SkinningBuffer.IsValid()) return false;
        playback = StockLocustPlayback.TryCreate(gameObject, clip, agents, initialPhases);
        if (playback == null || !playback.UsesCache)
        {
            playback?.RestoreNative();
            playback = null;
            return false;
        }
        clipMatrices = clip.SkinningBuffer;
        clipRoots = new TransformAccessArray(roots);
        clipRootMatrices = new NativeArray<Matrix4x4>(roots.Length, Allocator.Persistent);
        clipOffsets = new float[roots.Length];
        for (int i = 0; i < roots.Length; i++)
        {
            Vector3 scale = roots[i].lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            radii[i] = Mathf.Max(radii[i], clip.BoundingRadius * maxScale);
        }
        properties.SetBuffer(BoneData, clipMatrices);
        return true;
    }

    private void ConfigureLiveBones(Matrix4x4[] pose)
    {
        var transforms = new Transform[checked(skins.Length * boneCount)];
        for (int i = 0; i < skins.Length; i++)
            Array.Copy(skins[i].bones, 0, transforms, i * boneCount, boneCount);
        bones = new TransformAccessArray(transforms);
        bindPoses = new NativeArray<Matrix4x4>(pose, Allocator.Persistent);
        matrices = new NativeArray<SkinMatrix>(transforms.Length, Allocator.Persistent);
        gpuMatrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, matrices.Length, 48);
        properties.SetBuffer(BoneData, gpuMatrices);
    }

    private void LateUpdate()
    {
        if (!replacingSkins || lastFrame == Time.frameCount) return;
        CompletePendingTransforms();
        lastFrame = Time.frameCount;
        if (!SupportsStockForward())
        {
            foreach (var group in groups) group.FallBack("the rendering quality changed");
            groups.Clear();
            return;
        }

        cameraCount = Camera.allCamerasCount;
        if (cameras.Length < cameraCount) cameras = new Camera[cameraCount];
        Camera.GetAllCameras(cameras);
        for (int i = groups.Count - 1; i >= 0; i--)
        {
            var group = groups[i];
            string reason = group.playback != null && !group.playback.UsesCache ? "shared clip playback stopped" :
                !group.CanRenderVisibleCameras(cameraCount) ? "a visible camera requires an unsupported rendering pass" : null;
            if (reason == null) continue;
            group.FallBack(reason);
            groups.RemoveAt(i);
        }
        try
        {
            foreach (var group in groups)
            {
                JobHandle job = group.playback != null
                    ? new ReadRoots { output = group.clipRootMatrices }.ScheduleReadOnly(group.clipRoots, 64)
                    : new ReadBones { bindPoses = group.bindPoses, output = group.matrices }.ScheduleReadOnly(group.bones, 64);
                pendingTransforms = JobHandle.CombineDependencies(pendingTransforms, job);
                transformsPending = true;
                group.preparedFrame = lastFrame;
            }
            if (transformsPending) JobHandle.ScheduleBatchedJobs();
        }
        catch (Exception error)
        {
            CompletePendingTransforms();
            FallBackGroups(error.Message);
        }
    }

    internal static void SubmitFrame()
    {
        if (lastFrame != Time.frameCount || submittedFrame == lastFrame) return;
        submittedFrame = lastFrame;
        CompletePendingTransforms();
        for (int i = groups.Count - 1; i >= 0; i--)
        {
            var group = groups[i];
            if (group.preparedFrame != lastFrame) continue;
            try { group.Draw(cameraCount); }
            catch (Exception error)
            {
                group.FallBack(error.Message);
                groups.RemoveAt(i);
            }
        }
    }

    private static void CompletePendingTransforms()
    {
        if (!transformsPending) return;
        try { pendingTransforms.Complete(); }
        catch (Exception error) { FallBackGroups(error.Message); }
        finally
        {
            transformsPending = false;
            pendingTransforms = default;
        }
    }

    private static void FallBackGroups(string reason)
    {
        foreach (var group in groups) group.FallBack(reason);
        groups.Clear();
    }

    private bool CanRenderVisibleCameras(int cameraCount)
    {
        int mask = 1 << gameObject.layer;
        for (int i = 0; i < cameraCount; i++)
        {
            var camera = cameras[i];
            if (camera != null && (camera.cullingMask & mask) != 0 &&
                (camera.actualRenderingPath != RenderingPath.Forward || camera.depthTextureMode != DepthTextureMode.None))
                return false;
        }
        return true;
    }

    private void Draw(int cameraCount)
    {
        if (verifyGeometry && !verifiedGeometry)
        {
            verifiedGeometry = true;
            CompareGeometry();
        }
        bool cached = playback != null;
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            activeAgents[i] = root != null && root.gameObject.activeInHierarchy;
            if (!activeAgents[i]) continue;
            Vector3 center;
            if (cached)
            {
                var matrix = clipRootMatrices[i];
                center = new Vector3(matrix.m03, matrix.m13, matrix.m23);
                clipOffsets[i] = playback.SampleIndex(i) * boneCount;
            }
            else center = root.position;
            worldBounds[i] = new Bounds(center, Vector3.one * (2 * radii[i]));
        }
        if (!cached) gpuMatrices.SetData(matrices);
        int layer = gameObject.layer;
        var parameters = new RenderParams(material)
        {
            layer = layer,
            matProps = properties,
            lightProbeUsage = LightProbeUsage.Off,
            reflectionProbeUsage = reflectionUsage,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
        };
        for (int c = 0; c < cameraCount; c++)
        {
            var camera = cameras[c];
            if (camera == null || !camera.isActiveAndEnabled || (camera.cullingMask & (1 << layer)) == 0) continue;
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            parameters.camera = camera;
            DrawInstances(ref parameters, cached);
        }
    }

    private void DrawInstances(ref RenderParams parameters, bool cached)
    {
        int count = 0;
        Bounds bounds = default;
        for (int i = 0; i < roots.Length; i++)
        {
            if (!activeAgents[i] || !GeometryUtility.TestPlanesAABB(frustum, worldBounds[i])) continue;
            if (cached) instances[count] = clipRootMatrices[i];
            offsets[count] = cached ? clipOffsets[i] : i * boneCount;
            if (count == 0) bounds = worldBounds[i]; else bounds.Encapsulate(worldBounds[i]);
            if (++count == MaxInstances)
            {
                Submit(ref parameters, bounds, count);
                count = 0;
            }
        }
        if (count != 0) Submit(ref parameters, bounds, count);
    }

    private void CompareGeometry()
    {
        var skin = skins[0];
        var reference = new Mesh();
        try
        {
            // Unity 6.3 compensates for transform scale in this overload.
            // Apply the complete renderer transform exactly once.
            skin.BakeMesh(reference, true);
            Vector3[] source = skin.sharedMesh.vertices;
            Vector3[] baked = reference.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            var toWorld = skin.transform.localToWorldMatrix;
            if (baked.Length != source.Length)
                throw new InvalidOperationException("BakeMesh vertex count differs from the stock source.");
            float maximum = 0;
            for (int i = 0; i < source.Length; i++)
            {
                var position = new Vector4(source[i].x, source[i].y, source[i].z, 1);
                SkinMatrix skinning = matrices[weights[i].boneIndex0];
                Vector3 world = new Vector3(Vector4.Dot(skinning.x, position), Vector4.Dot(skinning.y, position), Vector4.Dot(skinning.z, position));
                float error = Vector3.Distance(world, toWorld.MultiplyPoint3x4(baked[i]));
                if (float.IsNaN(error) || float.IsInfinity(error))
                    throw new InvalidOperationException("The native locust geometry check produced a nonfinite deviation.");
                maximum = Mathf.Max(maximum, error);
            }
            if (maximum > .001f)
                throw new InvalidOperationException($"Native locust instancing geometry deviation {maximum:G6} exceeds 0.001 scene units.");
            Debug.Log($"Native locust instancing geometry check: layer {gameObject.layer}, {source.Length} vertices, max world deviation {maximum:G6}.");
        }
        finally { Destroy(reference); }
    }

    private void Submit(ref RenderParams parameters, Bounds bounds, int count)
    {
        parameters.worldBounds = bounds;
        properties.SetFloatArray(BoneOffset, offsets);
        Graphics.RenderMeshInstanced(parameters, mesh, 0, instances, count);
    }

    private void RestoreSkins()
    {
        playback?.RestoreNative();
        playback = null;
        if (!replacingSkins) return;
        replacingSkins = false;
        foreach (var skin in skins) if (skin != null) skin.enabled = true;
    }

    private string EvidencePosition() =>
        $"layer {gameObject.layer}, Unity frame {Time.frameCount}, presentation {ReplayPresentationRecorder.CurrentPresentationId ?? "(none)"}";

    private void FallBack(string reason)
    {
        RestoreSkins();
        Debug.LogWarning($"Native locust instancing reverted to the original renderers ({EvidencePosition()}): {reason}");
    }

    private void OnDisable()
    {
        CompletePendingTransforms();
        groups.Remove(this);
        RestoreSkins();
    }

    private void OnDestroy()
    {
        CompletePendingTransforms();
        if (bones.isCreated) bones.Dispose();
        if (bindPoses.IsCreated) bindPoses.Dispose();
        if (matrices.IsCreated) matrices.Dispose();
        if (clipRoots.isCreated) clipRoots.Dispose();
        if (clipRootMatrices.IsCreated) clipRootMatrices.Dispose();
        gpuMatrices?.Dispose();
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
