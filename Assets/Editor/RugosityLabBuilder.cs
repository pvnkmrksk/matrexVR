using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RugosityLabBuilder
{
    public const string ScenePath = "Assets/Scenes/Rugosity_RS.unity";
    private const string Root = "Assets/RugosityLab";
    [MenuItem("Tools/Rugosity/Open RS tuning scene")]
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
        var world = new GameObject("Rugosity — select to tune").AddComponent<RugosityWorld>();
        var material = new Material(Shader.Find("Standard")) { color = new Color(.38f, .48f, .32f) };
        material.SetFloat("_Glossiness", 0);
        AssetDatabase.CreateAsset(material, Root + "/Ground.mat");
        var rs = MakeSurface("RS", MakeMesh(false), material);
        var flat = MakeSurface("Flat", MakeMesh(true), material);
        rs.transform.SetParent(world.transform); flat.transform.SetParent(world.transform);
        world.surfaces = new[] { rs, flat };
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
        camera.transform.position = new Vector3(-25, 1, -60);
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

    private static RugositySurface MakeSurface(string id, Mesh mesh, Material material)
    {
        AssetDatabase.CreateAsset(mesh, Root + "/" + id + ".asset");
        var obj = new GameObject(id + " geometry");
        var filter = obj.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        var collider = obj.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
        var surface = obj.AddComponent<RugositySurface>(); surface.surfaceId = id;
        surface.meshes = new[] { filter }; surface.renderers = new Renderer[] { renderer }; surface.colliders = new Collider[] { collider };
        return surface;
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
