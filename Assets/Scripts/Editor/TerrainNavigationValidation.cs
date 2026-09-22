using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TerrainNavigationValidation
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("Terrain navigation: " + message);
    }

    [MenuItem("Tools/Terrain Navigation/Validate")]
    public static void Run()
    {
        if (Application.isBatchMode)
            EditorSceneManager.OpenScene("Assets/Scenes/Choice.unity");
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(100, 20, 100) };
        try
        {
            SceneManager.SetActiveScene(scene);
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = x / 64f;
            data.SetHeights(0, 0, heights);
            var ground = new GameObject("Synthetic slope");
            ground.transform.position = new Vector3(-50, 7, -50);
            var surface = ground.AddComponent<TerrainNavigationSurface>();
            surface.ruggedData = data;
            surface.flatHeightCm = 3;
            var rig = new GameObject("Test rig");
            var follower = rig.AddComponent<TerrainOrientationUpdater>();
            follower.navigationSurface = surface;
            follower.antSize = 0;
            foreach (bool flat in new[] { false, true })
            foreach (TerrainOrientationUpdater.HeightMode height in Enum.GetValues(typeof(TerrainOrientationUpdater.HeightMode)))
            foreach (TerrainOrientationUpdater.GroundConformanceMode orientation in Enum.GetValues(typeof(TerrainOrientationUpdater.GroundConformanceMode)))
            {
                surface.flat = flat;
                follower.heightMode = height;
                follower.conformanceMode = orientation;
                follower.absoluteHeight = 100;
                rig.transform.SetPositionAndRotation(new Vector3(0, -100, 0), Quaternion.Euler(0, 35, 0));
                Require(surface.Sample(rig.transform.position, out float y, out var normal), "slope sample");
                Require(follower.Conform(0, true), "conform from below ground");
                Require(Mathf.Abs(rig.transform.position.y - (height == TerrainOrientationUpdater.HeightMode.AboveGround ? y + 1 : 100)) < .01f, "AGL/absolute height");
                Require(Vector3.Angle(rig.transform.up, orientation == TerrainOrientationUpdater.GroundConformanceMode.HeightOnly ? Vector3.up : normal) < .1f, "orientation mode");
                Require(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y, 35)) < .1f, "heading preservation");
                Vector3 valid = rig.transform.position;
                rig.transform.position = new Vector3(200, 100, 0);
                Require(!follower.Conform(0, true) && rig.transform.position == valid, "boundary rejection");
            }
            follower.ApplyConfiguration(new Dictionary<string, object> { { "terrainNavigation", JObject.Parse("{\"heightAboveGroundCm\":2,\"heightMode\":\"Absolute\"}") } });
            Require(follower.verticalOffset == 2 && follower.heightMode == TerrainOrientationUpdater.HeightMode.Absolute, "JSON override");
            follower.ApplyConfiguration(null);
            Require(follower.verticalOffset == 1, "no override leakage between trials");

            foreach (string suffix in new[] { "rr", "rs", "sr", "ss" })
            {
                var loaded = EditorSceneManager.OpenScene("Assets/Scenes/navrug_" + suffix + ".unity", OpenSceneMode.Additive);
                try
                {
                    NavrugSceneSetup setup = null;
                    foreach (var root in loaded.GetRootGameObjects())
                    {
                        foreach (var candidate in root.GetComponentsInChildren<NavrugSceneSetup>(true)) setup = candidate;
                    }
                    Require(setup != null && setup.ruggedData != null && setup.terrain != null, suffix + " scene references");
                    Require(setup.flatAppearance == (suffix[0] == 's') && setup.flatNavigation == (suffix[1] == 's'), suffix + " defaults");
                    setup.Initialize();
                    Require(setup.GetComponent<TerrainNavigationSurface>().Sample(Vector3.zero, out _, out _), suffix + " origin spawn inside navigation bounds");
                    foreach (bool appearance in new[] { false, true })
                    foreach (bool navigation in new[] { false, true })
                    {
                        setup.SetSurfaces(appearance, navigation);
                        var nav = setup.GetComponent<TerrainNavigationSurface>();
                        Require(nav != null && nav.flat == navigation, suffix + " navigation independent of rendering");
                        Require((setup.terrain.terrainData == setup.ruggedData) == !appearance, suffix + " appearance data");
                    }
                    int count = 0;
                    foreach (var root in loaded.GetRootGameObjects())
                    foreach (var cl in root.GetComponentsInChildren<ClosedLoop>(true))
                    {
                        var f = cl.GetComponent<TerrainOrientationUpdater>();
                        Require(f != null && f.enabled && f.navigationSurface != null, suffix + " rig wiring");
                        count++;
                    }
                    Require(count > 0, suffix + " navigation rigs");
                    Debug.Log("Validated navrug_" + suffix + "; forest size cm: " + setup.ruggedData.size + "; rigs: " + count);
                }
                finally { EditorSceneManager.CloseScene(loaded, true); }
            }
            File.WriteAllText("/tmp/ledpanel-terrain-validation.txt", "PASS: height/orientation modes, yaw, boundaries, JSON overrides, and all four scene surface combinations.\n");
            Debug.Log("TERRAIN NAVIGATION VALIDATION PASSED");
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(scene, true);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }
}
