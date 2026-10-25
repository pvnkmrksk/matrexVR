using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RugosityLabBuilder
{
    public const string ScenePath = "Assets/Scenes/TerrainCueLab.unity";
    public const string LegacyScenePath = "Assets/Scenes/Rugosity_RS.unity";
    private const string Root = "Assets/TerrainCueLab";
    public enum SpatialPattern { RoughSmooth, SmoothRough, RoughRough, SmoothSmooth, Flat }
    [MenuItem("Tools/Rugosity/Open terrain cue lab")]
    public static void Open()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath)) Build();
        else EditorSceneManager.OpenScene(ScenePath);
        Selection.activeObject = Object.FindFirstObjectByType<RugosityWorld>();
    }
    public static void Build()
    {
        if (File.Exists(ScenePath)) { Open(); return; }
        Directory.CreateDirectory(Root);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var world = new GameObject("Terrain cues — select to tune").AddComponent<RugosityWorld>();
        var material = new Material(Shader.Find("Standard")) { color = new Color(.38f, .48f, .32f) };
        material.SetFloat("_Glossiness", 0);
        AssetDatabase.CreateAsset(material, Root + "/Ground.mat");
        var checker = new Texture2D(64, 64) { name = "Ground depth cues", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            checker.SetPixel(x, y, (x < 32) == (y < 32) ? Color.white : new Color(.7f, .7f, .7f));
        checker.Apply();
        AssetDatabase.CreateAsset(checker, Root + "/GroundPattern.asset");
        material.mainTexture = checker; material.mainTextureScale = new Vector2(40, 40);
        world.surfaces = System.Enum.GetValues(typeof(SpatialPattern)).Cast<SpatialPattern>()
            .Select(pattern => MakeSurface(pattern.ToString(), MakeProfileMesh(pattern), material)).ToArray();
        foreach (var surface in world.surfaces) surface.transform.SetParent(world.transform);
        world.settings.visualSurface = world.settings.collisionSurface = SpatialPattern.RoughSmooth.ToString();
        world.settings.normalRadiusCm = 0;
        var camera = new GameObject("VR1 — manual tuning rig").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.nearClipPlane = .01f; camera.farClipPlane = 1000; camera.fieldOfView = 75;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.56f, .71f, .86f);
        camera.gameObject.AddComponent<AudioListener>();
        camera.gameObject.AddComponent<Keyboard>();
        camera.gameObject.AddComponent<ZmqListener>();
        var tracking = camera.gameObject.AddComponent<ClosedLoop>();
        tracking.SetClosedLoopPosition(false); tracking.SetClosedLoopOrientation(false);
        var rig = camera.gameObject.AddComponent<RugosityRig>(); rig.world = world;
        world.rigTargets = new[] { camera.transform };
        camera.transform.SetPositionAndRotation(new Vector3(-60, 1, 0), Quaternion.Euler(0, 90, 0));
        var controller = new GameObject("SceneController").AddComponent<ChoiceController>();
        controller.prefabs = new GameObject[0]; controller.materials = new Material[0];
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.1f;
        light.transform.rotation = Quaternion.Euler(35, -35, 0);
        RenderSettings.ambientLight = new Color(.45f, .45f, .45f);
        RenderSettings.skybox = null;
        world.ApplyLiveSettings(); rig.Conform(0, true);
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        AssetDatabase.SaveAssets();
        ConfigurationSchemaBuilder.Generate();
    }

    // New profiles use full names, separate from legacy RS scene names. Max extrusion
    // stays below 1 cm so both mismatched conditions can use the same 1 cm clearance
    // without placing the camera inside the visible surface.
    public static Mesh MakeProfileMesh(SpatialPattern pattern)
    {
        var mesh = MakeMesh(true);
        mesh.name = pattern.ToString();
        var vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i];
            float blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-5, 5, p.x));
            float amplitude = pattern == SpatialPattern.RoughSmooth ? Mathf.Lerp(.8f, .025f, blend)
                : pattern == SpatialPattern.SmoothRough ? Mathf.Lerp(.025f, .8f, blend)
                : pattern == SpatialPattern.RoughRough ? .8f
                : pattern == SpatialPattern.SmoothSmooth ? .025f : 0;
            p.y = amplitude * (.5f + .25f * Mathf.Sin(p.x * .65f) + .25f * Mathf.Cos(p.z * .53f + p.x * .25f));
            vertices[i] = p;
        }
        mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static RugositySurface MakeSurface(string id, Mesh mesh, Material material)
    {
        AssetDatabase.CreateAsset(mesh, Root + "/" + id + ".asset");
        var obj = new GameObject(id + " geometry");
        var filter = obj.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        var collider = obj.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
        var surround = new GameObject("Visual surround — no navigation");
        surround.transform.SetParent(obj.transform, false);
        var surroundMesh = MakeSurround(mesh);
        AssetDatabase.CreateAsset(surroundMesh, Root + "/" + id + "Surround.asset");
        surround.AddComponent<MeshFilter>().sharedMesh = surroundMesh;
        var surroundRenderer = surround.AddComponent<MeshRenderer>(); surroundRenderer.sharedMaterial = material;
        var surface = obj.AddComponent<RugositySurface>(); surface.surfaceId = id;
        surface.meshes = new[] { filter }; surface.renderers = new Renderer[] { renderer, surroundRenderer }; surface.colliders = new Collider[] { collider };
        return surface;
    }

    // Extend the visible ground to 1 km so a flat horizon is a straight plane edge,
    // rather than the nearby corners of the 2 m navigation footprint. The skirt
    // shares the core boundary vertices but never changes navigation bounds.
    private static Mesh MakeSurround(Mesh core)
    {
        const int count = 129;
        var source = core.vertices;
        var edge = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < count - 1; i++) edge.Add(source[i]);
        for (int i = 0; i < count - 1; i++) edge.Add(source[i * count + count - 1]);
        for (int i = count - 1; i > 0; i--) edge.Add(source[(count - 1) * count + i]);
        for (int i = count - 1; i > 0; i--) edge.Add(source[i * count]);
        var vertices = new Vector3[edge.Count * 2];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[edge.Count * 6];
        for (int i = 0; i < edge.Count; i++)
        {
            vertices[i * 2] = edge[i];
            vertices[i * 2 + 1] = new Vector3(edge[i].x * 1000, 0, edge[i].z * 1000);
            for (int j = 0; j < 2; j++) uv[i * 2 + j] = new Vector2((vertices[i * 2 + j].x + 100) / 200, (vertices[i * 2 + j].z + 100) / 200);
            int a = i * 2, b = ((i + 1) % edge.Count) * 2, t = i * 6;
            triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = a + 1;
            triangles[t + 3] = a + 1; triangles[t + 4] = b; triangles[t + 5] = b + 1;
        }
        var mesh = new Mesh { name = core.name + " visual surround", vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    public static Mesh MakeMesh(bool flat)
    {
        const int count = 129;
        var vertices = new Vector3[count * count]; var uv = new Vector2[vertices.Length];
        var triangles = new int[(count - 1) * (count - 1) * 6];
        for (int z = 0; z < count; z++) for (int x = 0; x < count; x++)
        {
            float px = -100 + 200f * x / (count - 1), pz = -100 + 200f * z / (count - 1);
            float rough = Mathf.Lerp(.12f, 4f, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-5, 5, px)));
            float wave = .5f + .25f * Mathf.Sin(px * .35f) + .25f * Mathf.Cos(pz * .28f + px * .13f);
            vertices[z * count + x] = new Vector3(px, flat ? 0 : rough * wave, pz);
            uv[z * count + x] = new Vector2(x / (float)(count - 1), z / (float)(count - 1));
            if (x == count - 1 || z == count - 1) continue;
            int v = z * count + x, t = (z * (count - 1) + x) * 6;
            triangles[t] = v; triangles[t + 1] = v + count; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + count; triangles[t + 5] = v + count + 1;
        }
        var mesh = new Mesh { name = flat ? "Flat" : "RS — rough left, smooth right", vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
}
