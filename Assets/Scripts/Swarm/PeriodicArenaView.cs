using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Draw neighboring images of the arena on cameras configured for a periodic view.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(1460)]
public sealed class PeriodicArenaView : MonoBehaviour
{
    private static readonly List<BodyVisual> bodies = new List<BodyVisual>();
    private static readonly HashSet<GameObject> registeredBodies = new HashSet<GameObject>();
    private static readonly List<RendererVisual> activeRenderers = new List<RendererVisual>();
    private static int preparedFrame = -1;
    private static float rendererMargin;

    private readonly List<Vector3> tileOffsets = new List<Vector3>();
    private readonly Plane[] frustum = new Plane[6];
    private Camera attachedCamera;
    private float width, depth;
    private int frustumFrame = -1;
    private Matrix4x4 clipMatrix;
    private Vector2 frustumMin, frustumMax;
    private bool validFrustum;
    private int submittedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBodies()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        foreach (BodyVisual body in bodies) body.Release();
        bodies.Clear();
        registeredBodies.Clear();
        activeRenderers.Clear();
        preparedFrame = -1;
    }

    internal static void Configure(Camera camera, bool enabled, float width, float depth)
    {
        var component = camera.GetComponent<PeriodicArenaView>();
        if (enabled)
        {
            if (!(width > 0) || !(depth > 0) || float.IsInfinity(width) || float.IsInfinity(depth))
                throw new ArgumentException("Periodic camera views require finite, positive arena dimensions.");
            if (component == null) component = camera.gameObject.AddComponent<PeriodicArenaView>();
            component.attachedCamera = camera;
            component.width = width;
            component.depth = depth;
        }
        if (component != null) component.enabled = enabled;
    }

    internal static bool TryGet(Camera camera, out PeriodicArenaView view) =>
        camera.TryGetComponent(out view) && view.isActiveAndEnabled;

    internal static void RegisterBody(GameObject body)
    {
        if (body == null || !registeredBodies.Add(body)) return;
        bodies.Add(new BodyVisual(body));
        preparedFrame = -1;
    }

    internal void GetTileOffsets(float margin, List<Vector3> offsets)
    {
        offsets.Clear();
        if (!TryGetFrustumBounds()) return;

        float halfWidth = width * .5f + margin, halfDepth = depth * .5f + margin;
        int firstX = Mathf.CeilToInt((frustumMin.x - halfWidth) / width);
        int lastX = Mathf.FloorToInt((frustumMax.x + halfWidth) / width);
        int firstZ = Mathf.CeilToInt((frustumMin.y - halfDepth) / depth);
        int lastZ = Mathf.FloorToInt((frustumMax.y + halfDepth) / depth);
        for (int z = firstZ; z <= lastZ; z++)
            for (int x = firstX; x <= lastX; x++)
                offsets.Add(new Vector3(x * width, 0, z * depth));
    }

    private bool TryGetFrustumBounds()
    {
        var matrix = attachedCamera.projectionMatrix * attachedCamera.worldToCameraMatrix;
        if (frustumFrame == Time.frameCount && matrix.Equals(clipMatrix)) return validFrustum;

        frustumFrame = Time.frameCount;
        clipMatrix = matrix;
        validFrustum = false;
        var inverse = matrix.inverse;
        frustumMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        frustumMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        // Camera.projectionMatrix uses Unity's CPU clip space, with near/far at -1/+1.
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector4((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1, 1);
            Vector4 position = inverse * corner;
            var horizontal = new Vector2(position.x / position.w, position.z / position.w);
            if (float.IsNaN(horizontal.x) || float.IsInfinity(horizontal.x) ||
                float.IsNaN(horizontal.y) || float.IsInfinity(horizontal.y)) return false;
            frustumMin = Vector2.Min(frustumMin, horizontal);
            frustumMax = Vector2.Max(frustumMax, horizontal);
        }
        return validFrustum = true;
    }

    private void LateUpdate()
    {
        if (attachedCamera.isActiveAndEnabled) SubmitFallback();
    }

    // Other callers can render a configured camera manually, including disabled preview cameras.
    private void OnPreCull()
    {
        if (isActiveAndEnabled) SubmitFallback();
    }

    private void SubmitFallback()
    {
        if (submittedFrame == Time.frameCount) return;
        submittedFrame = Time.frameCount;
        PrepareRenderers();
        if (activeRenderers.Count == 0) return;

        GetTileOffsets(rendererMargin, tileOffsets);
        GeometryUtility.CalculateFrustumPlanes(attachedCamera, frustum);
        int visible = attachedCamera.cullingMask;
        foreach (Vector3 offset in tileOffsets)
        {
            if (offset == Vector3.zero) continue; // Unity draws the source renderer in the canonical tile.
            foreach (RendererVisual visual in activeRenderers)
            {
                if ((visible & (1 << visual.Layer)) == 0) continue;
                Bounds translated = visual.WorldBounds;
                translated.center += offset;
                if (GeometryUtility.TestPlanesAABB(frustum, translated))
                    visual.Draw(attachedCamera, offset, translated);
            }
        }
    }

    private static void OnSceneUnloaded(Scene _)
    {
        PruneDestroyedBodies();
        preparedFrame = -1;
    }

    private static void PruneDestroyedBodies()
    {
        for (int i = bodies.Count - 1; i >= 0; i--)
        {
            var visual = bodies[i];
            if (visual.Root != null) continue;
            registeredBodies.Remove(visual.Root);
            visual.Release();
            bodies.RemoveAt(i);
        }
    }

    private static void PrepareRenderers()
    {
        if (preparedFrame == Time.frameCount) return;
        preparedFrame = Time.frameCount;
        PruneDestroyedBodies();
        activeRenderers.Clear();
        rendererMargin = 0;
        foreach (var body in bodies)
        {
            if (!body.Root.activeInHierarchy) continue;
            Vector3 position = body.Root.transform.position;
            foreach (var visual in body.Renderers)
            {
                if (!visual.Prepare()) continue;
                // Include limbs and any renderer pivot offset at the edge of a tile.
                Vector3 offset = visual.WorldBounds.center - position;
                Vector3 extent = visual.WorldBounds.extents;
                rendererMargin = Mathf.Max(rendererMargin, Mathf.Max(Mathf.Abs(offset.x) + extent.x, Mathf.Abs(offset.z) + extent.z));
                activeRenderers.Add(visual);
            }
        }
    }

    private sealed class BodyVisual
    {
        internal readonly GameObject Root;
        internal readonly List<RendererVisual> Renderers = new List<RendererVisual>();

        internal BodyVisual(GameObject root)
        {
            Root = root;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Renderers.Add(new RendererVisual(skin, skin, null));
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.TryGetComponent(out MeshFilter filter))
                    Renderers.Add(new RendererVisual(renderer, null, filter));
        }

        internal void Release()
        {
            foreach (var visual in Renderers) visual.Release();
        }
    }

    private sealed class RendererVisual
    {
        internal Bounds WorldBounds { get; private set; }
        internal int Layer { get; private set; }
        private readonly Renderer renderer;
        private readonly SkinnedMeshRenderer skin;
        private readonly MeshFilter filter;
        private readonly List<Material> materials = new List<Material>();
        private MaterialPropertyBlock sharedProperties, indexedProperties;
        private bool hasProperties;
        private Mesh mesh, bakedMesh;
        private Matrix4x4 localToWorld;
        private int drawnFrame = -1;

        internal RendererVisual(Renderer renderer, SkinnedMeshRenderer skin, MeshFilter filter)
        {
            this.renderer = renderer;
            this.skin = skin;
            this.filter = filter;
        }

        internal bool Prepare()
        {
            // The native stock path owns disabled skins and submits their periodic instances itself.
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy)
                return false;
            Layer = renderer.gameObject.layer;
            WorldBounds = renderer.bounds;
            localToWorld = renderer.localToWorldMatrix;
            return true;
        }

        internal void Draw(Camera camera, Vector3 offset, Bounds bounds)
        {
            if (drawnFrame != Time.frameCount)
            {
                drawnFrame = Time.frameCount;
                mesh = filter != null ? filter.sharedMesh : null;
                if (skin != null && skin.sharedMesh != null)
                {
                    if (bakedMesh == null)
                    {
                        bakedMesh = new Mesh { name = "Periodic locust skin" };
                        bakedMesh.MarkDynamic();
                    }
                    // Keep the original renderer matrix responsible for scale, exactly once.
                    skin.BakeMesh(bakedMesh, false);
                    mesh = bakedMesh;
                }
                renderer.GetSharedMaterials(materials);
                hasProperties = renderer.HasPropertyBlock();
                if (hasProperties)
                {
                    sharedProperties ??= new MaterialPropertyBlock();
                    indexedProperties ??= new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(sharedProperties);
                }
            }

            if (mesh == null || mesh.subMeshCount == 0) return;
            Matrix4x4 matrix = localToWorld;
            matrix.m03 += offset.x;
            matrix.m23 += offset.z;
            for (int slot = 0; slot < materials.Count; slot++)
            {
                var material = materials[slot];
                if (material == null) continue;
                MaterialPropertyBlock properties = null;
                if (hasProperties)
                {
                    renderer.GetPropertyBlock(indexedProperties, slot);
                    properties = indexedProperties.isEmpty ? sharedProperties : indexedProperties;
                }
                var parameters = new RenderParams(material)
                {
                    camera = camera, layer = Layer, worldBounds = bounds, matProps = properties,
                    shadowCastingMode = renderer.shadowCastingMode, receiveShadows = renderer.receiveShadows,
                    lightProbeUsage = renderer.lightProbeUsage, reflectionProbeUsage = renderer.reflectionProbeUsage,
                    motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
                };
                Graphics.RenderMesh(parameters, mesh, Mathf.Min(slot, mesh.subMeshCount - 1), matrix);
            }
        }

        internal void Release()
        {
            if (bakedMesh == null) return;
            if (Application.isPlaying) Destroy(bakedMesh); else DestroyImmediate(bakedMesh);
        }
    }
}
