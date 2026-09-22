using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exercises MainController -> ChoiceController -> terrain setup on the actual navrug scenes.
[InitializeOnLoad]
public static class TerrainNavigationPlayValidation
{
    private const string Key = "TerrainNavigationSequenceValidation";
    private static MainController main;
    private static int stage, step = -1, samples, lastFrame = -1, totalSamples, conditions;
    private static double started, lastTick;
    private static int previousSceneHandle;
    private static bool pendingPose;
    private static TerrainOrientationUpdater[] rigs;
    private static JObject expectedWorld, expectedNavigation;
    private static readonly List<string> results = new List<string>();
    private static readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();

    static TerrainNavigationPlayValidation()
    {
        if (SessionState.GetBool(Key, false))
        {
            EditorApplication.update += Tick;
            Application.logMessageReceivedThreaded += OnLog;
        }
    }

    // CLI only: omit -quit. No writes to the user's active sequence or system config.
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Run this validation in a separate Unity batch process.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Enqueue(message);
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("navrug sequence validation: " + message);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || lastFrame == Time.frameCount) return;
        if (EditorApplication.timeSinceStartup - lastTick < .02) return;
        lastTick = EditorApplication.timeSinceStartup;
        lastFrame = Time.frameCount;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        try
        {
            Check(EditorApplication.timeSinceStartup - started < 180, "timed out");
            if (main == null)
            {
                new GameObject("Validation logger").AddComponent<MasterDataLogger>();
                main = new GameObject("Sequence validation controller").AddComponent<MainController>();
                return; // Let MainController.Start run before selecting the test sequence.
            }
            if (stage == 0)
            {
                Begin("sequenceConfig_terrainNavigation.json");
                stage = 1;
                return;
            }
            int index = main.currentStep;
            if (index != step)
            {
                // SceneManager.LoadScene completes on the next frame after the timer advances.
                if (SceneManager.GetActiveScene().name != main.GetCurrentSequenceStep().sceneName) return;
                if (step >= 0) CompleteStep();
                if (index == 0 && step >= 0)
                {
                    if (stage == 1)
                    {
                        main.StopSequence();
                        Begin("sequenceConfig_terrainNavigation_allModes.json");
                        stage = 2;
                        return;
                    }
                    Check(errors.Count == 0, "runtime errors: " + string.Join("; ", errors));
                    Finish(0);
                    return;
                }
                step = index;
                samples = 0;
                pendingPose = false;
                var current = main.GetCurrentSequenceStep();
                Check(SceneManager.GetActiveScene().name == current.sceneName, "wrong active scene");
                if (step > 0 && !current.reloadScene && main.sequenceSteps[step - 1].sceneName == current.sceneName)
                    Check(previousSceneHandle == SceneManager.GetActiveScene().handle, "reloadScene:false reloaded the scene");
                previousSceneHandle = SceneManager.GetActiveScene().handle;
                var config = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, current.parameters["configFile"].ToString())));
                expectedWorld = (JObject)config["terrainWorld"].DeepClone();
                expectedNavigation = (JObject)config["terrainNavigation"].DeepClone();
                if (current.parameters.TryGetValue("terrainWorld", out var world)) expectedWorld.Merge(JObject.FromObject(world));
                if (current.parameters.TryGetValue("terrainNavigation", out var navigation)) expectedNavigation.Merge(JObject.FromObject(navigation));
                rigs = UnityEngine.Object.FindObjectsByType<TerrainOrientationUpdater>(FindObjectsSortMode.None)
                    .Where(r => r.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
                Check(rigs.Length == 4, "expected four active rigs, got " + rigs.Length);
            }
            var setup = UnityEngine.Object.FindFirstObjectByType<NavrugSceneSetup>();
            Check(setup.flatAppearance == expectedWorld.Value<bool>("flatAppearance"), "appearance config");
            Check(setup.flatNavigation == expectedWorld.Value<bool>("flatNavigation"), "navigation config");
            Check((setup.terrain.terrainData == setup.ruggedData) == !setup.flatAppearance, "visible data");
            foreach (var rig in rigs)
            {
                Check(rig.heightMode.ToString() == expectedNavigation.Value<string>("heightMode"), "height mode config");
                Check(rig.conformanceMode.ToString() == expectedNavigation.Value<string>("orientationMode"), "orientation config");
                Check(Mathf.Abs(rig.verticalOffset - expectedNavigation.Value<float>("heightAboveGroundCm")) < .001f, "choice clearance lost");
                Check(rig.navigationSurface.Sample(rig.transform.position, out float ground, out _), "rig outside terrain");
                float expectedY = rig.heightMode == TerrainOrientationUpdater.HeightMode.AboveGround ? ground + rig.verticalOffset : expectedNavigation.Value<float>("absoluteHeightCm");
                Check(Mathf.Abs(rig.transform.position.y - expectedY) < .01f, "LateUpdate height mismatch");
                if (rig.conformanceMode == TerrainOrientationUpdater.GroundConformanceMode.HeightOnly)
                    Check(Vector3.Angle(rig.transform.up, Vector3.up) < .1f, "level orientation mismatch");
                else if (pendingPose && rig.alignSpeed == 0)
                {
                    Vector3 normal = AverageNormal(rig);
                    Check(Vector3.Angle(rig.transform.up, normal) < .1f, "LateUpdate normal mismatch");
                }
            }
            if (pendingPose) { samples++; totalSamples += rigs.Length; }
            // Supply deterministic XZ movement only. Production LateUpdate must apply height/tilt.
            foreach (var rig in rigs)
                rig.transform.position = new Vector3(Mathf.Sin(samples * .7f) * 40, rig.transform.position.y, Mathf.Cos(samples * .7f) * 40);
            pendingPose = true;
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    private static Vector3 AverageNormal(TerrainOrientationUpdater rig)
    {
        rig.navigationSurface.Sample(rig.transform.position, out _, out var sum);
        int count = Mathf.Clamp(rig.sampleCount, 1, 32);
        for (int i = 0; i < count; i++)
        {
            float a = 2 * Mathf.PI * i / count;
            if (rig.navigationSurface.Sample(rig.transform.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rig.antSize, out _, out var normal)) sum += normal;
        }
        return sum.normalized;
    }

    private static void Begin(string file)
    {
        main.LoadSequenceConfiguration(file);
        foreach (var item in main.sequenceSteps) item.duration = 2;
        main.loopSequence = true;
        step = -1;
        main.StartSequence();
    }

    private static void CompleteStep()
    {
        Check(samples >= 5, "insufficient frames in condition " + step);
        conditions++;
        results.Add("PASS condition " + conditions + ": " + samples + " frames, four rigs");
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceivedThreaded -= OnLog;
        results.Add((code == 0 ? "PASS" : "FAIL") + ": " + conditions + " conditions; " + totalSamples + " rig pose checks; actual navrug scenes via MainController and ChoiceController.");
        results.AddRange(errors);
        File.WriteAllLines("/tmp/ledpanel-terrain-play-validation.txt", results);
        Debug.Log(results[results.Count - 1]);
        EditorApplication.Exit(code);
    }
}
