using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>A fixed reference for checking motion on the existing apparatus outputs.</summary>
internal sealed class PreExperimentWorld : IDisposable
{
    private const string WorldName = "Pre-experiment world";
    private static readonly Color Background = new Color32(103, 103, 103, 255);

    private readonly Scene scene;
    private readonly List<Action> restore = new List<Action>();
    private GameObject root;
    private Texture2D pattern;
    private Material material;
    private Mesh mesh;
    private bool disposed;

    private PreExperimentWorld(Scene scene) => this.scene = scene;

    public static PreExperimentWorld Create(Scene scene)
    {
        var preview = new PreExperimentWorld(scene);
        try
        {
            preview.Show();
            SceneManager.sceneUnloaded += preview.SceneUnloaded;
            return preview;
        }
        catch
        {
            preview.Dispose();
            throw;
        }
    }

    private void Show()
    {
        var rigs = RuntimeRigRegistry.Rigs(scene);
        if (rigs.Length == 0) throw new InvalidOperationException("No configured rigs are available for pre-experiment feedback.");

        var shader = Shader.Find("Unlit/Texture");
        if (shader == null || !shader.isSupported)
            throw new InvalidOperationException("The pre-experiment display shader is unavailable.");

        // This built-in layer keeps the reference out of the operator camera and any ground raycasts.
        int layer = LayerMask.NameToLayer("Ignore Raycast");
        int mask = 1 << layer;
        var stimulusCameras = new HashSet<Camera>();
        foreach (var rig in rigs)
        {
            var setter = rig.GetComponentInChildren<ViewportSetter>(true);
            if (setter == null) throw new InvalidOperationException(rig.name + " has no stimulus cameras.");
            foreach (var camera in setter.GetComponentsInChildren<Camera>(true))
                if (camera.GetComponentInParent<ViewportSetter>(true) == setter) stimulusCameras.Add(camera);
        }

        float backgroundDepth = -1f;
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (camera.gameObject.scene != scene || stimulusCameras.Contains(camera)) continue;
            int previousMask = camera.cullingMask;
            restore.Add(() => { if (camera != null) camera.cullingMask = previousMask; });
            // The ControlScene background must clear first, including when rig strips share display 0.
            if (camera.targetDisplay == 0 && camera.transform.parent == null)
            {
                camera.cullingMask = 0;
                backgroundDepth = Mathf.Max(backgroundDepth, camera.depth);
            }
            else camera.cullingMask &= ~mask;
        }

        foreach (var camera in stimulusCameras)
            ConfigureCamera(camera, mask, backgroundDepth);

        var rigBounds = new Bounds(rigs[0].transform.position, Vector3.zero);
        foreach (var rig in rigs)
        {
            ConfigureTracking(rig);
            rigBounds.Encapsulate(rig.transform.position);
        }

        root = new GameObject(WorldName) { layer = layer };
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = new Vector3(rigBounds.center.x, rigBounds.min.y - 1.5f, rigBounds.center.z);
        pattern = CreatePattern();
        material = new Material(shader) { name = "Pre-experiment texture", mainTexture = pattern };
        mesh = CreateMesh();
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = material;
    }

    private void ConfigureCamera(Camera camera, int mask, float backgroundDepth)
    {
        int previousMask = camera.cullingMask;
        var previousClear = camera.clearFlags;
        var previousBackground = camera.backgroundColor;
        float previousDepth = camera.depth;
        restore.Add(() =>
        {
            if (camera == null) return;
            camera.cullingMask = previousMask;
            camera.clearFlags = previousClear;
            camera.backgroundColor = previousBackground;
            camera.depth = previousDepth;
        });

        camera.cullingMask = mask;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        camera.depth = Mathf.Max(camera.depth, backgroundDepth + 1f);
    }

    private void ConfigureTracking(ClosedLoop rig)
    {
        var mode = MainController.Instance.GetSystemConfigForGameObject(rig.gameObject).closedLoopMode;
        bool previousPosition = rig.PositionTrackingEnabled;
        bool previousOrientation = rig.OrientationTrackingEnabled;
        restore.Add(() =>
        {
            if (rig == null) return;
            rig.SetClosedLoopPosition(previousPosition);
            rig.SetClosedLoopOrientation(previousOrientation);
        });

        // Walking uses the floor as well as the landmarks. Flight uses the horizon for yaw feedback.
        rig.SetClosedLoopPosition(TrackerBackend.IsWalking(mode));
        rig.SetClosedLoopOrientation(true);
    }

    private static Texture2D CreatePattern()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "Pre-experiment grayscale grid",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool alternating = (x < size / 2) != (y < size / 2);
                bool gridLine = x % (size / 2) < 2 || y % (size / 2) < 2;
                byte shade = gridLine ? (byte)123 : alternating ? (byte)87 : (byte)108;
                pixels[y * size + x] = new Color32(shade, shade, shade, 255);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private static Mesh CreateMesh()
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u, float v)
        {
            int i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            uv.Add(Vector2.zero);
            uv.Add(new Vector2(0f, v));
            uv.Add(new Vector2(u, v));
            uv.Add(new Vector2(u, 0f));
            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
            triangles.Add(i);
            triangles.Add(i + 2);
            triangles.Add(i + 3);
        }

        const float extent = 4096f;
        const float repetitions = extent * 2f / 8f;
        Quad(new Vector3(-extent, 0f, -extent), new Vector3(-extent, 0f, extent),
            new Vector3(extent, 0f, extent), new Vector3(extent, 0f, -extent), repetitions, repetitions);

        // Near and far landmarks give different amounts of parallax. Their positions and pattern are fixed.
        for (int ring = 0; ring < 3; ring++)
        {
            for (int point = 0; point < 12; point++)
            {
                float angle = (point + ring * .37f) * Mathf.PI / 6f;
                float radius = 8f + 13f * ring;
                float halfWidth = .3f + .23f * ring;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float top = 3f + (point % 3) * .8f + 2.2f * ring;
                var a = new Vector3(x - halfWidth, 0f, z - halfWidth);
                var b = new Vector3(x + halfWidth, 0f, z - halfWidth);
                var c = new Vector3(x + halfWidth, 0f, z + halfWidth);
                var d = new Vector3(x - halfWidth, 0f, z + halfWidth);
                var aTop = new Vector3(a.x, top, a.z);
                var bTop = new Vector3(b.x, top, b.z);
                var cTop = new Vector3(c.x, top, c.z);
                var dTop = new Vector3(d.x, top, d.z);
                Quad(a, aTop, bTop, b, .5f, 1f);
                Quad(b, bTop, cTop, c, .5f, 1f);
                Quad(c, cTop, dTop, d, .5f, 1f);
                Quad(d, dTop, aTop, a, .5f, 1f);
                Quad(aTop, dTop, cTop, bTop, .5f, .5f);
            }
        }

        var result = new Mesh { name = "Pre-experiment floor and landmarks" };
        result.SetVertices(vertices);
        result.SetUVs(0, uv);
        result.SetTriangles(triangles, 0);
        result.UploadMeshData(true);
        return result;
    }

    private void SceneUnloaded(Scene unloaded)
    {
        if (unloaded == scene) Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        SceneManager.sceneUnloaded -= SceneUnloaded;
        if (root != null) root.SetActive(false);
        for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
        restore.Clear();
        Object.Destroy(root);
        Object.Destroy(mesh);
        Object.Destroy(material);
        Object.Destroy(pattern);
    }
}
