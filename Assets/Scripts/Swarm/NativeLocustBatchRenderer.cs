using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

/// <summary>Instancing of stock locusts using a shared clip or live Animator bones.</summary>
[DefaultExecutionOrder(1050)]
public sealed class NativeLocustBatchRenderer : MonoBehaviour
{
    private const int MaxInstances = 256;
    private const int PlanesPerCamera = 6;
    private const int MinimumParallelCull = 384;
    private const int MaxPackedInstancesPerGroup = 32768;
    private const byte Culled = 0, Visible = 1, Boundary = 2;
    private static readonly bool requested =
        Array.IndexOf(Environment.GetCommandLineArgs(), "--stock-locust-renderers") < 0;
    private static readonly bool verifyGeometry =
        Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-batch-native-locusts") >= 0;
    private static readonly bool useSharedClip = !verifyGeometry &&
        Array.IndexOf(Environment.GetCommandLineArgs(), "--live-stock-locust-animators") < 0;
    private static readonly bool parallelCull =
        Array.IndexOf(Environment.GetCommandLineArgs(), "--serial-stock-locust-cull") < 0;
    private static readonly bool parallelDrawPacking =
        Array.IndexOf(Environment.GetCommandLineArgs(), "--serial-stock-locust-draw-pack") < 0;
    private static readonly int BoneData = Shader.PropertyToID("_LocustBones");
    private static readonly int BoneOffset = Shader.PropertyToID("_LocustBoneOffset");
    private static readonly List<NativeLocustBatchRenderer> groups = new List<NativeLocustBatchRenderer>();
    private static Camera[] cameras = Array.Empty<Camera>();
    private static int cameraCount;
    private static int[] cameraMasks = Array.Empty<int>();
    private static int lastFrame = -1;
    private static int submittedFrame = -1;
    private static JobHandle pendingTransforms;
    private static bool transformsPending;
    private static NativeArray<float4> cameraPlanes;
    private static CameraCullSnapshot[] cameraSnapshots = Array.Empty<CameraCullSnapshot>();
    private static int cullFrame = -1;

    private SkinnedMeshRenderer[] skins;
    private Transform[] roots;
    private GameObject[] agentObjects;
    internal bool UsesParallelCull => replacingSkins && cullRadii.IsCreated;
    internal bool UsesParallelDrawPacking => UsesParallelCull && parallelDrawPacking;
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
    private NativeArray<float> cullRadii;
    private NativeArray<int> cullCameraIndices;
    private NativeArray<byte> cullResults;
    private int[] cullSlots = Array.Empty<int>();
    private NativeArray<float> packetPhases;
    private NativeArray<Matrix4x4> packetMatrices;
    private NativeArray<float> packetOffsets;
    private NativeArray<Bounds> packetBounds;
    private NativeArray<int> packetCounts;
    private int packetFrame = -1;
    private long packetCameras, fallbackCameras;
    private Mesh mesh;
    private Material material;
    private MaterialPropertyBlock properties;
    private readonly Plane[] frustum = new Plane[PlanesPerCamera];
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

    private struct CameraCullSnapshot
    {
        public Matrix4x4 clipMatrix;
        public Plane[] planes;
        public int mask;
        public bool unchanged;
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

    [BurstCompile(FloatMode = FloatMode.Strict)]
    private struct CullAgents : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Matrix4x4> roots;
        [ReadOnly] public NativeArray<float> radii;
        [ReadOnly] public NativeArray<int> cameraIndices;
        [ReadOnly] public NativeArray<float4> planes;
        [WriteOnly] public NativeArray<byte> results;

        public void Execute(int index)
        {
            int agent = index % roots.Length;
            int firstPlane = cameraIndices[index / roots.Length] * PlanesPerCamera;
            Matrix4x4 root = roots[agent];
            var center = new float3(root.m03, root.m13, root.m23);
            byte visibility = Visible;
            for (int i = 0; i < PlanesPerCamera; i++)
            {
                float4 plane = planes[firstPlane + i];
                float3 products = center * plane.xyz;
                float reach = radii[agent] * math.csum(math.abs(plane.xyz));
                float distance = math.csum(products) + plane.w + reach;
                // Unity resolves boundary cases using the original AABB test.
                float tolerance = 4e-6f * (math.csum(math.abs(products)) + math.abs(plane.w) + reach + 1);
                if (distance < -tolerance)
                {
                    results[index] = Culled;
                    return;
                }
                if (!math.isfinite(distance) || distance <= tolerance) visibility = Boundary;
            }
            results[index] = visibility;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Strict)]
    private struct PackDraws : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Matrix4x4> roots;
        [ReadOnly] public NativeArray<float> radii;
        [ReadOnly] public NativeArray<float> phases;
        [ReadOnly] public NativeArray<byte> visibility;
        // Each camera iteration owns its full, nonoverlapping packet range.
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<Matrix4x4> matrices;
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> offsets;
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<Bounds> bounds;
        [WriteOnly] public NativeArray<int> counts;

        public void Execute(int cameraSlot)
        {
            int first = cameraSlot * roots.Length;
            int firstChunk = cameraSlot * ChunksFor(roots.Length);
            int count = 0;
            Bounds chunk = default;
            for (int i = 0; i < roots.Length; i++)
            {
                byte result = visibility[first + i];
                if (result == Culled) continue;
                if (result == Boundary)
                {
                    counts[cameraSlot] = -1;
                    return;
                }
                Matrix4x4 matrix = roots[i];
                matrices[first + count] = matrix;
                offsets[first + count] = phases[i];
                var center = new Vector3(matrix.m03, matrix.m13, matrix.m23);
                var agentBounds = new Bounds(center, Vector3.one * (2 * radii[i]));
                if (count % MaxInstances == 0) chunk = agentBounds;
                else chunk.Encapsulate(agentBounds);
                count++;
                if (count % MaxInstances == 0) bounds[firstChunk + count / MaxInstances - 1] = chunk;
            }
            if (count % MaxInstances != 0) bounds[firstChunk + count / MaxInstances] = chunk;
            counts[cameraSlot] = count;
        }
    }

    private static int ChunksFor(int count) => (count + MaxInstances - 1) / MaxInstances;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGroups()
    {
        CompletePendingTransforms();
        if (cameraPlanes.IsCreated) cameraPlanes.Dispose();
        groups.Clear();
        lastFrame = submittedFrame = -1;
        cameras = Array.Empty<Camera>();
        cameraMasks = Array.Empty<int>();
        cameraCount = 0;
        cameraSnapshots = Array.Empty<CameraCullSnapshot>();
        cullFrame = -1;
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
        agentObjects = new GameObject[agents.Count];
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
            agentObjects[i] = agent;
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
        if (parallelCull && roots.Length >= MinimumParallelCull) cullRadii = new NativeArray<float>(radii, Allocator.Persistent);
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
        using var timing = RuntimePerformanceProbe.Measure(RuntimePerformanceProbe.CpuStage.RenderPrepare);
        CompletePendingTransforms();
        lastFrame = Time.frameCount;
        if (!SupportsStockForward())
        {
            foreach (var group in groups) group.FallBack("the rendering quality changed");
            groups.Clear();
            return;
        }

        cameraCount = Camera.allCamerasCount;
        if (cameras.Length < cameraCount)
        {
            cameras = new Camera[cameraCount];
            cameraMasks = new int[cameraCount];
        }
        Camera.GetAllCameras(cameras);
        int groupLayers = 0;
        foreach (var group in groups) groupLayers |= 1 << group.gameObject.layer;
        int unsupportedLayers = 0;
        for (int i = 0; i < cameraCount; i++)
        {
            var camera = cameras[i];
            int mask = camera != null && camera.isActiveAndEnabled ? camera.cullingMask : 0;
            cameraMasks[i] = mask;
            if ((mask & groupLayers) != 0 &&
                (camera.actualRenderingPath != RenderingPath.Forward || camera.depthTextureMode != DepthTextureMode.None))
                unsupportedLayers |= mask;
        }
        bool useCameraJobs = false;
        for (int i = groups.Count - 1; i >= 0; i--)
        {
            var group = groups[i];
            string reason = group.playback != null && !group.playback.UsesCache ? "shared clip playback stopped" :
                (unsupportedLayers & (1 << group.gameObject.layer)) != 0 ? "a visible camera requires an unsupported rendering pass" : null;
            if (reason == null)
            {
                useCameraJobs |= group.cullRadii.IsCreated;
                continue;
            }
            group.FallBack(reason);
            groups.RemoveAt(i);
        }
        try
        {
            if (useCameraJobs) CaptureCameraPlanes();
            foreach (var group in groups)
            {
                JobHandle job = group.playback != null
                    ? new ReadRoots { output = group.clipRootMatrices }.ScheduleReadOnly(group.clipRoots, 64)
                    : new ReadBones { bindPoses = group.bindPoses, output = group.matrices }.ScheduleReadOnly(group.bones, 64);
                pendingTransforms = JobHandle.CombineDependencies(pendingTransforms, job);
                transformsPending = true;
                if (group.cullRadii.IsCreated)
                    pendingTransforms = JobHandle.CombineDependencies(pendingTransforms, group.ScheduleCull(job));
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
        using (RuntimePerformanceProbe.Measure(RuntimePerformanceProbe.CpuStage.RenderWait))
            CompletePendingTransforms();
        using var timing = RuntimePerformanceProbe.Measure(RuntimePerformanceProbe.CpuStage.RenderSubmit);
        for (int i = 0; i < cameraCount; i++)
        {
            var camera = cameras[i];
            int mask = camera != null && camera.isActiveAndEnabled ? camera.cullingMask : 0;
            cameraMasks[i] = mask;
            if (cullFrame != lastFrame) continue;
            ref var snapshot = ref cameraSnapshots[i];
            snapshot.unchanged = snapshot.mask != 0 && mask != 0 &&
                snapshot.clipMatrix.Equals(CameraClipMatrix(camera));
        }
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

    private static Matrix4x4 CameraClipMatrix(Camera camera) => camera.projectionMatrix * camera.worldToCameraMatrix;

    private static void CaptureCameraPlanes()
    {
        if (cameraCount == 0) return;
        if (!cameraPlanes.IsCreated || cameraSnapshots.Length < cameraCount)
        {
            if (cameraPlanes.IsCreated) cameraPlanes.Dispose();
            cameraPlanes = new NativeArray<float4>(cameraCount * PlanesPerCamera, Allocator.Persistent);
            cameraSnapshots = new CameraCullSnapshot[cameraCount];
            for (int i = 0; i < cameraCount; i++) cameraSnapshots[i].planes = new Plane[PlanesPerCamera];
        }
        for (int i = 0; i < cameraCount; i++)
        {
            var camera = cameras[i];
            ref var snapshot = ref cameraSnapshots[i];
            snapshot.mask = cameraMasks[i];
            if (snapshot.mask == 0) continue;
            snapshot.clipMatrix = CameraClipMatrix(camera);
            GeometryUtility.CalculateFrustumPlanes(camera, snapshot.planes);
            for (int p = 0; p < PlanesPerCamera; p++)
            {
                Plane plane = snapshot.planes[p];
                Vector3 normal = plane.normal;
                cameraPlanes[i * PlanesPerCamera + p] = new float4(normal.x, normal.y, normal.z, plane.distance);
            }
        }
        cullFrame = lastFrame;
    }

    private JobHandle ScheduleCull(JobHandle rootsReady)
    {
        packetFrame = -1;
        if (cameraCount == 0) return rootsReady;
        if (cullSlots.Length < cameraCount)
        {
            if (cullCameraIndices.IsCreated) cullCameraIndices.Dispose();
            if (cullResults.IsCreated) cullResults.Dispose();
            cullSlots = new int[cameraCount];
            cullCameraIndices = new NativeArray<int>(cameraCount, Allocator.Persistent);
            cullResults = new NativeArray<byte>(checked(cameraCount * roots.Length), Allocator.Persistent);
        }
        int selected = 0;
        int layerMask = 1 << gameObject.layer;
        for (int i = 0; i < cameraCount; i++)
        {
            cullSlots[i] = -1;
            if ((cameraSnapshots[i].mask & layerMask) == 0) continue;
            cullSlots[i] = selected;
            cullCameraIndices[selected++] = i;
        }
        if (selected == 0) return rootsReady;
        bool pack = parallelDrawPacking && TryPreparePackets(selected);
        JobHandle cull = new CullAgents {
            roots = clipRootMatrices, radii = cullRadii, cameraIndices = cullCameraIndices,
            planes = cameraPlanes, results = cullResults
        }.Schedule(selected * roots.Length, 128, rootsReady);
        if (!pack) return cull;
        try
        {
            JobHandle packets = new PackDraws {
                roots = clipRootMatrices, radii = cullRadii, phases = packetPhases, visibility = cullResults,
                matrices = packetMatrices, offsets = packetOffsets, bounds = packetBounds, counts = packetCounts
            }.Schedule(selected, 1, cull);
            packetFrame = lastFrame;
            return packets;
        }
        catch
        {
            cull.Complete();
            throw;
        }
    }

    private bool TryPreparePackets(int camerasSelected)
    {
        if ((long)camerasSelected * roots.Length > MaxPackedInstancesPerGroup) return false;
        if (!packetPhases.IsCreated) packetPhases = new NativeArray<float>(roots.Length, Allocator.Persistent);
        if (!packetCounts.IsCreated || packetCounts.Length < camerasSelected)
        {
            DisposePacketStorage();
            packetMatrices = new NativeArray<Matrix4x4>(camerasSelected * roots.Length, Allocator.Persistent);
            packetOffsets = new NativeArray<float>(packetMatrices.Length, Allocator.Persistent);
            packetBounds = new NativeArray<Bounds>(camerasSelected * ChunksFor(roots.Length), Allocator.Persistent);
            packetCounts = new NativeArray<int>(camerasSelected, Allocator.Persistent);
        }
        for (int i = 0; i < roots.Length; i++) packetPhases[i] = playback.SampleIndex(i) * boneCount;
        return true;
    }

    private void DisposePacketStorage()
    {
        if (packetMatrices.IsCreated) packetMatrices.Dispose();
        if (packetOffsets.IsCreated) packetOffsets.Dispose();
        if (packetBounds.IsCreated) packetBounds.Dispose();
        if (packetCounts.IsCreated) packetCounts.Dispose();
    }

    private void Draw(int cameraCount)
    {
        if (verifyGeometry && !verifiedGeometry)
        {
            verifiedGeometry = true;
            CompareGeometry();
        }
        bool cached = playback != null;
        if (cached && !playback.UsesCache)
            throw new InvalidOperationException("Shared clip playback stopped before draw submission.");
        bool packetsReady = packetFrame == lastFrame && AllAgentsActive();
        bool agentDataReady = false;
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
            if ((cameraMasks[c] & (1 << layer)) == 0) continue;
            var camera = cameras[c];
            parameters.camera = camera;
            int cullSlot = cached && cullRadii.IsCreated && cullFrame == lastFrame && cameraSnapshots[c].unchanged
                ? cullSlots[c] : -1;
            if (packetsReady && cullSlot >= 0 && packetCounts[cullSlot] >= 0)
            {
                SubmitPackets(ref parameters, cullSlot);
                packetCameras++;
                continue;
            }
            if (parallelDrawPacking && cullRadii.IsCreated) fallbackCameras++;
            if (!agentDataReady)
            {
                PrepareAgentData(cached);
                agentDataReady = true;
            }
            if (cullSlot < 0) GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            DrawInstances(ref parameters, cached, cullSlot < 0 ? frustum : cameraSnapshots[c].planes, cullSlot * roots.Length);
        }
    }

    private bool AllAgentsActive()
    {
        foreach (var agent in agentObjects)
            if (agent == null || !agent.activeInHierarchy) return false;
        return true;
    }

    private void PrepareAgentData(bool cached)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            var agent = agentObjects[i];
            activeAgents[i] = agent != null && agent.activeInHierarchy;
            if (!activeAgents[i]) continue;
            Vector3 center;
            if (cached)
            {
                var matrix = clipRootMatrices[i];
                center = new Vector3(matrix.m03, matrix.m13, matrix.m23);
                clipOffsets[i] = playback.SampleIndex(i) * boneCount;
            }
            else center = roots[i].position;
            worldBounds[i] = new Bounds(center, Vector3.one * (2 * radii[i]));
        }
    }

    private void SubmitPackets(ref RenderParams parameters, int cullSlot)
    {
        int first = cullSlot * roots.Length;
        int boundsIndex = cullSlot * ChunksFor(roots.Length);
        int count = packetCounts[cullSlot];
        for (int index = 0; index < count; index += MaxInstances)
        {
            int size = Mathf.Min(count - index, MaxInstances);
            int start = first + index;
            parameters.worldBounds = packetBounds[boundsIndex++];
            NativeArray<float>.Copy(packetOffsets, start, offsets, 0, size);
            properties.SetFloatArray(BoneOffset, offsets);
            Graphics.RenderMeshInstanced(parameters, mesh, 0, packetMatrices.GetSubArray(start, size), size);
        }
    }

    private void DrawInstances(ref RenderParams parameters, bool cached, Plane[] planes, int cullOffset)
    {
        int count = 0;
        Bounds bounds = default;
        for (int i = 0; i < roots.Length; i++)
        {
            if (!activeAgents[i]) continue;
            byte visibility = cullOffset < 0 ? Boundary : cullResults[cullOffset + i];
            if (visibility == Culled || (visibility == Boundary && !GeometryUtility.TestPlanesAABB(planes, worldBounds[i]))) continue;
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
        if (packetCameras != 0 || fallbackCameras != 0)
            Debug.Log($"Native locust draw packets ({EvidencePosition()}): {packetCameras} packed camera frames, {fallbackCameras} fallback camera frames.");
        groups.Remove(this);
        if (groups.Count == 0 && cameraPlanes.IsCreated) cameraPlanes.Dispose();
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
        if (cullRadii.IsCreated) cullRadii.Dispose();
        if (cullCameraIndices.IsCreated) cullCameraIndices.Dispose();
        if (cullResults.IsCreated) cullResults.Dispose();
        if (packetPhases.IsCreated) packetPhases.Dispose();
        DisposePacketStorage();
        gpuMatrices?.Dispose();
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
