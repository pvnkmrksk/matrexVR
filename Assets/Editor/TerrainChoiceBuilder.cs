using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Four thin spatial-layout scenes; all editable environment content lives in one prefab.</summary>
public static class TerrainChoiceBuilder
{
    public const string SharedPrefab = "Assets/TerrainCueLab/TerrainChoiceWorld.prefab";
    public static readonly string[] Patterns = { "RoughSmooth", "SmoothRough", "SmoothSmooth", "RoughRough" };
    public static readonly string[] Scenes = { "TerrainChoice_RoughLeft_SmoothRight", "TerrainChoice_SmoothLeft_RoughRight", "TerrainChoice_BothSmooth", "TerrainChoice_BothRough" };

    [MenuItem("Tools/Rugosity/Create configured terrain choice scenes")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var shared = AssetDatabase.LoadAssetAtPath<GameObject>(SharedPrefab);
        if (shared == null)
        {
            EditorSceneManager.OpenScene(RugosityLabBuilder.ScenePath);
            var world = Object.FindFirstObjectByType<RugosityWorld>();
            if (world == null) throw new InvalidOperationException("Existing terrain lab is required as the geometry source.");
            foreach (var old in Object.FindObjectsByType<ClosedLoop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Terrain choice — shared environment");
            foreach (var item in root.scene.GetRootGameObjects().Where(g => g != root)) item.transform.SetParent(root.transform, true);
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Camera Prefabs/VR1.prefab");
            if (rigPrefab == null) throw new InvalidOperationException("Standard configured VR1 prefab is missing.");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.name = "VR1";
            rig.transform.SetParent(root.transform, false);
            rig.transform.SetPositionAndRotation(new Vector3(0, 1, 0), Quaternion.identity);
            var follower = rig.GetComponent<RugosityRig>() ?? rig.AddComponent<RugosityRig>();
            follower.world = world;
            foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
            {
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 100000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.56f, .71f, .86f);
                camera.enabled = false; // ViewportSetter alone owns the configured camera allow-list.
            }
            world.rigTargets = Array.Empty<Transform>(); // Discover every configured clone, including VR2–VRn.
            world.settings.visualSurface = world.settings.collisionSurface = "RoughSmooth";
            CreateTargets(root.transform);
            world.ApplyLiveSettings();
            shared = PrefabUtility.SaveAsPrefabAsset(root, SharedPrefab);
            if (shared == null) throw new InvalidOperationException("Could not save shared terrain environment.");
        }
        for (int i = 0; i < Scenes.Length; i++)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(shared, scene);
            var world = instance.GetComponentInChildren<RugosityWorld>(true);
            world.settings.visualSurface = world.settings.collisionSurface = Patterns[i];
            PrefabUtility.RecordPrefabInstancePropertyModifications(world);
            RenderSettings.skybox = null;
            RenderSettings.ambientLight = new Color(.45f, .45f, .45f);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/" + Scenes[i] + ".unity");
        }
        // Retrofit the existing lab as another instance, keeping its GUID and walkthrough start pose.
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(shared, scene);
            var rig = instance.GetComponentInChildren<ClosedLoop>(true).transform;
            rig.SetPositionAndRotation(new Vector3(-60, 1, 0), Quaternion.Euler(0, 90, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
            RenderSettings.skybox = null;
            RenderSettings.ambientLight = new Color(.45f, .45f, .45f);
            EditorSceneManager.SaveScene(scene, RugosityLabBuilder.ScenePath);
        }
        var paths = Scenes.Select(s => "Assets/Scenes/" + s + ".unity").ToArray();
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path))
            .Concat(paths.Select(p => new EditorBuildSettingsScene(p, true))).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("TERRAIN CHOICE BUILD: four spatial scenes and configured TerrainCueLab share " + SharedPrefab);
    }

    private static Material Material(string name, Color color)
    {
        string path = "Assets/TerrainCueLab/" + name + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result != null) return result;
        result = new Material(Shader.Find("Standard")) { color = color };
        result.SetFloat("_Glossiness", 0);
        AssetDatabase.CreateAsset(result, path);
        return result;
    }

    private static void CreateTargets(Transform parent)
    {
        string path = "Assets/TerrainCueLab/TerrainChoiceTarget.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var tree = new GameObject("Placeholder tree — edit this shared prefab");
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk"; trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0, 10, 0); trunk.transform.localScale = new Vector3(4, 10, 4);
            trunk.GetComponent<Renderer>().sharedMaterial = Material("TargetBark", new Color(.28f, .17f, .09f));
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown"; crown.transform.SetParent(tree.transform, false);
            crown.transform.localPosition = new Vector3(0, 25, 0); crown.transform.localScale = new Vector3(22, 24, 22);
            crown.GetComponent<Renderer>().sharedMaterial = Material("TargetLeaves", new Color(.13f, .27f, .08f));
            foreach (var collider in tree.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            prefab = PrefabUtility.SaveAsPrefabAsset(tree, path);
            Object.DestroyImmediate(tree);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tree.name = side < 0 ? "Left choice target" : "Right choice target";
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = new Vector3(side * 40, 0, 60);
        }
    }
}
