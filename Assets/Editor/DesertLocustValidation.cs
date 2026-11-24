#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ConfigurationUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DesertLocustValidation
{
    private const string Active = "DesertLocustValidation.Active";
    private const string SequenceFile = "Examples/Sequences/desert-locust-16rig.sequence.json";
    private static readonly List<object> observations = new List<object>();
    private static readonly List<string> errors = new List<string>();
    private static readonly Dictionary<int, Quaternion[]> cycleHeadings = new Dictionary<int, Quaternion[]>();
    private static readonly HashSet<int> rows = new HashSet<int>();
    private static int stage, cycle = -1;
    private static string recording;
    private static double deadline;

    static DesertLocustValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            deadline = EditorApplication.timeSinceStartup + 220;
            SceneManager.sceneLoaded += DisableInputs;
            Application.logMessageReceived += CaptureErrors;
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new InvalidOperationException("Use a marked disposable project.");
        string profile = RuntimePaths.ConfigRoot;
        if (Path.GetFullPath(profile) == Path.GetFullPath(Application.streamingAssetsPath) || Directory.EnumerateFileSystemEntries(profile).Any())
            throw new InvalidOperationException("Supply an empty isolated --config-dir.");
        var sequence = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, SequenceFile)));
        var sequenceRows = (JArray)sequence["sequences"];
        Check(sequenceRows.Count == 3 && sequenceRows.All(row => (float)row["duration"] == 10f) &&
            (bool)sequence["randomise"] && (bool)sequence["loop"], "Use the unmodified three-row, ten-second sequence.");
        foreach (string reference in sequenceRows.Select(row => (string)row["parameters"]["configFile"]).Prepend(SequenceFile))
        {
            string target = Path.Combine(profile, reference);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(Path.Combine(Application.streamingAssetsPath, reference), target);
        }
        File.Copy(Path.Combine(profile, SequenceFile), Path.Combine(profile, "sequenceConfig.json"));
        var rigs = new JArray(Enumerable.Range(1, 16).Select(index => new JObject {
            ["vrId"] = "VR" + index, ["closedLoopMode"] = "Hejjetrac",
            ["zmqAddress"] = "127.0.0.1", ["zmqPort"] = 59000 + index,
            ["sphereDiameter"] = 2.6, ["ledPanelWidth"] = 96, ["ledPanelHeight"] = 96,
            ["startRow"] = (index - 1) / 4, ["startCol"] = (index - 1) % 4, ["displayOrder"] = "F",
            ["targetDisplay"] = 0, ["tracker"] = new JObject { ["saveVideo"] = false, ["recordingControl"] = "Manual" }
        }));
        File.WriteAllText(Path.Combine(profile, "system_config.json"), new JObject {
            ["configs"] = rigs, ["targetDisplay"] = 0, ["targetFrameRate"] = 60,
            ["saveTrackerVideo"] = false, ["telemetry"] = new JObject { ["enabled"] = false },
            ["overheadCamera"] = new JObject { ["enabled"] = false }, ["statusOverlay"] = new JObject { ["visible"] = false }
        }.ToString());
        var workspace = new ConfigWorkspace("system_config.json", SequenceFile, ConfigSchema.Load());
        var validation = workspace.Validate(scenes: false);
        Check(validation.Count == 0, "Configuration workbench rejected recipe: " + string.Join("; ", validation));

        DisableInputs(EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity"), LoadSceneMode.Single);
        SessionState.SetBool(Active, true);
        EditorApplication.isPlaying = true;
    }

    private static void DisableInputs(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var listener in root.GetComponentsInChildren<ZmqListener>(true)) listener.enabled = false;
            foreach (var keyboard in root.GetComponentsInChildren<Keyboard>(true)) keyboard.enabled = false;
        }
    }

    private static void CaptureErrors(string message, string trace, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Assert || type == LogType.Exception) &&
            !trace.Contains("UnityEditor.Search.SearchDatabase")) errors.Add(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Timed out in desert validation stage " + stage);

            var main = MainController.Instance;
            var logger = MasterDataLogger.Instance;
            if (stage == 0)
            {
                if (Time.frameCount < 5 || main == null || logger == null || ConfigWorkbench.Instance == null) return;
                DisableInputs(SceneManager.GetActiveScene(), LoadSceneMode.Single);
                if (ZmqListener.ActiveWorkerCount != 0) return;
                if (logger.AcceptingWrites) { _ = logger.FinalizeSessionAsync("desert-validation-setup"); return; }
                if (logger.IsFinalizing) return;
                main.PrepareConfiguredRun("system_config.json", SequenceFile);
                CheckGroundPicker(ConfigWorkbench.Instance);
                recording = logger.directoryPath;
                main.StartSequence();
                stage = 1;
                return;
            }
            Check(ZmqListener.ActiveWorkerCount == 0, "A hardware tracking worker started.");
            if (stage == 1)
            {
                Check(main != null && main.SequenceError == null, "Sequence error: " + main?.SequenceError);
                if (!main.SequenceRunning || main.SceneTransitionPending || SceneManager.GetActiveScene().name != "Kannadi") return;
                if (main.currentTrial == 2)
                {
                    Check(observations.Count == 6 && rows.Count == 3, "Two complete shuffled cycles must precede the third.");
                    main.StopSequence("desert-validation-two-cycles");
                    stage = 2;
                    return;
                }
                if (cycle != main.currentTrial)
                {
                    Check(cycle < 0 || rows.Count == 3, "All three conditions must run before reshuffling.");
                    rows.Clear();
                    cycle = main.currentTrial;
                }
                int row = main.executionOrder[main.currentStep];
                if (rows.Contains(row))
                {
                    Check(rows.Count == main.currentStep + 1, "A row was repeated within a shuffled cycle.");
                    return;
                }
                InspectRow(main, row);
                rows.Add(row);
                return;
            }
            if (stage == 2)
            {
                if (logger.IsFinalizing) return;
                Check(logger.FinalizationError == null, "Run finalization failed: " + logger.FinalizationError);
                var archive = new ReplaySessionArchive(recording);
                archive.LoadRigSamples();
                Check(archive.Rigs.Count == 16 && archive.Rigs.Values.All(stream => stream.Count > 0),
                    "The saved run must contain actual pose samples for all sixteen rigs.");
                ReplayController.OpenSession(recording);
                stage = 3;
                return;
            }
            var replay = Object.FindFirstObjectByType<ReplayController>();
            if (replay == null) return;
            Check(replay.LoadError == null, "Same-build replay failed: " + replay.LoadError);
            if (!replay.Ready || replay.Loading || replay.environmentLoader.Loading) return;
            var replayGround = Object.FindFirstObjectByType<LocustGround>(FindObjectsInactive.Include);
            if (replayGround == null) return;
            CheckGround(replayGround.GetComponent<MeshRenderer>(), 200, 200);
            Check(errors.Count == 0, "Unity logged errors: " + string.Join("; ", errors));
            Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void CheckGroundPicker(ConfigWorkbench workbench)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var document = workbench.Workspace.Documents.First(item => item.Path.EndsWith("desert-16rig-blank.experiment.json"));
        var image = new Texture2D(2, 2, TextureFormat.RGB24, false);
        image.SetPixels(Enumerable.Repeat(Color.gray, 4).ToArray());
        image.Apply();
        const string reference = "GroundTextures/fixture.png";
        string imagePath = Path.Combine(RuntimePaths.ConfigRoot, reference);
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
        File.WriteAllBytes(imagePath, image.EncodeToPNG());
        Object.Destroy(image);
        Type type = typeof(ConfigWorkbench);
        type.GetMethod("Select", hidden).Invoke(workbench, new object[] { document });
        type.GetField("settingsTab", hidden).SetValue(workbench, "Appearance");
        type.GetMethod("BuildContent", hidden).Invoke(workbench, null);
        var picker = workbench.Document.rootVisualElement.Q<DropdownField>("ground-texture");
        Check(picker != null && picker.value == "Sand" && picker.choices.Contains("Grass") &&
            picker.choices.Contains(reference), "Ground picker did not list the stock and newly added textures.");
        picker.value = reference;
        Check((string)document.Root["groundAppearance"]["groundTexturePath"] == reference &&
            workbench.Workspace.Validate().Count == 0, "Selecting a custom ground image did not update and validate the same document.");
        document.Undo();
        Check((string)document.Root["groundAppearance"]["texture"] == "Sand", "Ground texture selection must be undoable.");
    }

    private static void InspectRow(MainController main, int row)
    {
        var scene = SceneManager.GetActiveScene();
        var rigs = Object.FindObjectsByType<Kannadi>(FindObjectsSortMode.None)
            .Where(rig => rig.gameObject.scene == scene).OrderBy(rig => rig.VRIndex).ToArray();
        Check(rigs.Select(rig => rig.VRIndex).SequenceEqual(Enumerable.Range(1, 16)), "Expected VR1–VR16.");
        var config = KannadiConfig.Load(main.GetCurrentSequenceStep().parameters);
        var rotations = rigs.Select(rig => rig.transform.rotation).ToArray();
        if (cycleHeadings.TryGetValue(cycle, out var previous))
            Check(rotations.Zip(previous, Quaternion.Angle).All(angle => angle < .05f), "Headings differ across conditions in one cycle.");
        else
        {
            if (cycle > 0)
                Check(rotations.Zip(cycleHeadings[0], Quaternion.Angle).Count(angle => angle > 1f) >= 12, "Repeated cycle must sample new headings.");
            cycleHeadings.Add(cycle, rotations);
        }
        float lowestOrder = 1;
        foreach (var rig in rigs)
        {
            Check(Vector3.Distance(rig.transform.position, config.vrConfigs[rig.VRIndex - 1].initialPosition.ToVector3()) < .02f,
                "Unexpected start position for VR" + rig.VRIndex + " in row " + row + ".");
            var spawner = rig.GetComponent<LocustSpawner>();
            var front = FrontCamera(rig);
            int visible = front.cullingMask & RuntimeRigRegistry.StimulusMask;
            int ownLayer = RuntimeRigRegistry.StimulusLayer(rig.VRIndex);
            Check(visible == (row == 2 ? RuntimeRigRegistry.PeerStimulusMask(rig.VRIndex) : 1 << ownLayer),
                "Incorrect population isolation on VR" + rig.VRIndex + ".");
            Check(rig.Clones.Length == (row == 2 ? 7 : 0), "Incorrect Kannadi ring size.");
            Check((spawner?.Spawned.Count ?? 0) == (row == 1 ? 64 : 0), "Incorrect number of generated locusts.");
            if (row != 1) continue;
            Check(spawner.Spawned.All(agent => agent.layer == ownLayer &&
                Mathf.Abs(agent.transform.position.x - rig.transform.position.x) <= 50.1f &&
                Mathf.Abs(agent.transform.position.z - rig.transform.position.z) <= 50.1f), "Generated body escaped its arena or layer.");
            Vector3 headings = spawner.Spawned.Aggregate(Vector3.zero, (sum, body) => sum + body.transform.forward);
            lowestOrder = Mathf.Min(lowestOrder, headings.magnitude / spawner.Spawned.Count);
        }
        if (row == 1) Check(lowestOrder > .95f && lowestOrder < .9999f, "The swarm must be highly ordered with varied headings.");
        var ground = Object.FindFirstObjectByType<LocustGround>().GetComponent<MeshRenderer>();
        CheckGround(ground, 200, 200);
        if (cycle == 0) CaptureFrame(FrontCamera(rigs[0]), row);
        observations.Add(new { cycle, authoredRow = row, executionStep = main.currentStep, rigs = rigs.Length,
            agentsPerRig = row == 1 ? 64 : 0, peerBodiesPerRig = row == 2 ? 105 : 0,
            lowestOrder = row == 1 ? (float?)lowestOrder : null, remainingSeconds = main.RemainingStepSeconds });
    }

    private static Camera FrontCamera(Kannadi rig) =>
        rig.GetComponentsInChildren<Camera>(true).First(camera => camera.name == "Main Camera F");

    private static void CheckGround(MeshRenderer ground, float tilingX, float tilingY)
    {
        Check(ground.sharedMaterial.shader.name == "Locust/Sandy Textured Ground" && ground.sharedMaterial.shader.isSupported,
            "The sand material or its shader is missing.");
        var properties = new MaterialPropertyBlock();
        ground.GetPropertyBlock(properties);
        var scale = properties.GetVector("_MainTex_ST");
        Check(scale.x == tilingX && scale.y == tilingY, "The ground renderer did not receive X/Y tiling.");
    }

    private static void CaptureFrame(Camera camera, int row)
    {
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, "desert-row-" + row + ".png"), image.EncodeToPNG());
            Color sky = image.GetPixel(128, 230);
            Check(Mathf.Abs(sky.r - sky.g) < .02f && Mathf.Abs(sky.r - sky.b) < .02f && sky.r > .4f,
                "The rendered sky is not uniform grey.");
            var ground = Enumerable.Range(0, 24).Select(index => image.GetPixel(10 + 10 * index, 18)).ToArray();
            Check(ground.Count(color => color.r > color.g && color.g > color.b) >= 18, "The lower frame does not show the sand texture.");
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(image);
        }
    }

    private static string OutputDirectory => Path.GetDirectoryName(RuntimePaths.ConfigRoot);

    private static void Finish(string failure)
    {
        SessionState.SetBool(Active, false);
        EditorApplication.update -= Tick;
        SceneManager.sceneLoaded -= DisableInputs;
        Application.logMessageReceived -= CaptureErrors;
        File.WriteAllText(Path.Combine(OutputDirectory, "desert-locust-validation.json"),
            JsonConvert.SerializeObject(new { unity = Application.unityVersion, observations, recording, errors, failure }, Formatting.Indented));
        if (failure != null) Debug.LogError(failure);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
