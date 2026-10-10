#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real Selwyn phase transitions plus circular-window, continuity and opt-in checks.</summary>
[InitializeOnLoad]
public static class ExperimentPhaseValidation
{
    private const string Key = "ExperimentPhaseValidation.Active";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly List<string> errors = new List<string>();
    private static readonly List<string> observations = new List<string>();
    private static int checks, stage, nextFrame, sceneId, trial;
    private static double deadline, preAt, stimulusAt;
    private static MainController main;
    private static ClosedLoop[] rigs;
    private static int[] rigIds, terrainIds;
    private static string starId;
    private static Vector3[] prePositions;
    private static Quaternion[] preRotations;
    private static float[] means;
    private static bool full;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static bool Angle(double a, double b) => Math.Abs(Mathf.DeltaAngle((float)a, (float)b)) < .02f;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    static ExperimentPhaseValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            full = SessionState.GetBool(Key + ".full", false);
            deadline = EditorApplication.timeSinceStartup + (full ? 600 : 120);
            nextFrame = Time.frameCount + 3; stage = 0;
            EditorApplication.update += Tick;
            SceneManager.sceneLoaded += DisableInputs;
            Application.logMessageReceived += OnLog;
        };
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (stack.Contains("UnityEditor.Search.SearchDatabase") || message == "No backup data file found.") return;
        errors.Add(message + "\n" + stack);
    }
    private static void DisableInputs(Scene scene, LoadSceneMode mode)
    {
        foreach (var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled = false;
    }
    public static void Run() => Begin(false);
    public static void RunFull() => Begin(true);
    private static void Begin(bool realDuration)
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable validation copy only.");
        string root = Application.streamingAssetsPath;
        var experiment = JObject.Parse(File.ReadAllText(Path.Combine(root, "Examples/Flight/selwyn-stars-left-swarm.example.json")));
        if (!realDuration)
        {
            experiment["preStimulus"]["durationSeconds"] = 1;
            experiment["preStimulus"]["headingReference"]["windowSeconds"] = 2;
            experiment["preStimulus"]["headingReference"]["startOffsetSeconds"] = .5;
            experiment["stimulus"]["durationSeconds"] = 2;
            var post = (JObject)experiment["stimulus"].DeepClone();
            post["durationSeconds"] = 1; post["swarm"] = null;
            experiment["postStimulus"] = post;
            experiment["preStimulus"]["autoTrimSettings"] = JObject.FromObject(new AutoTrimConfig { flightVarianceThreshold = .02 });
            experiment["stimulus"]["autoTrimSettings"] = JObject.FromObject(new AutoTrimConfig { flightVarianceThreshold = .04 });
            experiment["postStimulus"]["autoTrimSettings"] = JObject.FromObject(new AutoTrimConfig { flightCheckEnabled = false, flightVarianceThreshold = .005 });
        }
        File.WriteAllText(Path.Combine(root, "phase-validation.json"), experiment.ToString());
        var sequence = JObject.Parse(File.ReadAllText(Path.Combine(root, "Examples/Sequences/selwyn-stars-left-swarm.example.json")));
        sequence["autoStart"] = true;
        sequence["sequences"][0]["parameters"]["configFile"] = "phase-validation.json";
        File.WriteAllText(Path.Combine(root, "sequenceConfig.json"), sequence.ToString());
        var system = JObject.Parse(File.ReadAllText(Path.Combine(root, "Templates/system_config.template.json")));
        system["telemetry"]["enabled"] = false; system["overheadCamera"]["enabled"] = false;
        File.WriteAllText(Path.Combine(root, "system_config.json"), system.ToString());
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".full", realDuration);
        EditorApplication.isPlaying = true;
    }
    private static void UnitChecks()
    {
        var phases = ExperimentPhase.Parse(JObject.Parse("{ 'preStimulus': {'enabled': true, 'durationSeconds': 3, 'headingReference': {'enabled':true,'startOffsetSeconds':2,'windowSeconds':4}}, 'stimulus': {'durationSeconds': 5}, 'postStimulus': {'enabled':false} }"));
        Check(phases.Count == 2 && phases[0].Duration == 6 && phases[1].Duration == 5, "Observation overrun extends pre; disabled post omitted; stimulus defaults enabled");
        Check(phases[0].Config["headingReference"] == null, "Runner owns observation independently of visible world settings");
        foreach (string invalid in new[] { "{'stimulus':{'durationSeconds':0}}", "{'preStimulus':{'enabled':true,'durationSeconds':1}}", "{'stimulus':{'durationSeconds':1,'headingReference':{'enabled':true}}}", "{'stimulus':{'durationSeconds':1},'objects':[]}" })
        {
            bool rejected = false; try { ExperimentPhase.Parse(JObject.Parse(invalid)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid/ambiguous phase config rejected: " + invalid);
        }
        Check(ExperimentPhase.Parse(JObject.Parse("{'preStimulus':{},'stimulus':{'durationSeconds':1},'postStimulus':null}")).Count == 1, "Missing enabled defaults optional phases off");
        var host = new GameObject("Reference offset unit");
        var reference = host.AddComponent<StimulusHeadingReference>(); reference.enabled = false;
        double now = Time.timeAsDouble;
        reference.Begin(new HeadingReferenceConfig { enabled = true, startOffsetSeconds = 1, windowSeconds = 2 }, null);
        host.transform.rotation = Quaternion.Euler(0, 180, 0); reference.Tick(now + .5);
        host.transform.rotation = Quaternion.Euler(0, 359, 0); reference.Tick(now + 1.2);
        host.transform.rotation = Quaternion.Euler(0, 1, 0); reference.Tick(now + 2.8);
        host.transform.rotation = Quaternion.Euler(0, 180, 0); reference.Tick(now + 3.1);
        Check(reference.Result.sampleCount == 2 && Angle(reference.Result.meanHeadingDegrees, 0), "Only offset/window samples contribute; late completion does not add a sample");
        reference.Begin(new HeadingReferenceConfig { enabled = true, windowSeconds = 1 }, null);
        host.transform.rotation = Quaternion.Euler(0, 70, 0); reference.Tick(now + .5); reference.Tick(now + 1.1);
        Check(reference.Result.sampleCount == 1 && Angle(reference.Result.meanHeadingDegrees, 70), "Repeat begins with fresh history");
        new HeadingReferenceConfig { enabled = false, startOffsetSeconds = -1, windowSeconds = -1 }.Validate();
        bool badOffset = false; try { new HeadingReferenceConfig { enabled = true, startOffsetSeconds = -1 }.Validate(); } catch (ArgumentException) { badOffset = true; }
        Check(badOffset, "Enabled negative offset rejected; disabled parameters inert");
        Object.DestroyImmediate(host);
        var poseRoot = new GameObject("Selective reset unit"); poseRoot.SetActive(false);
        var cl = poseRoot.AddComponent<ClosedLoop>();
        foreach (bool position in new[] { false, true }) foreach (bool rotation in new[] { false, true })
        {
            poseRoot.transform.SetPositionAndRotation(new Vector3(1,2,3), Quaternion.Euler(0, 37, 0));
            cl.ApplyStartPose(new Vector3(9,8,7), Quaternion.Euler(0, 81, 0), position, rotation);
            Check(poseRoot.transform.position == (position ? new Vector3(9,8,7) : new Vector3(1,2,3)), "Position reset independent: " + position + "/" + rotation);
            Check(Angle(poseRoot.transform.eulerAngles.y, rotation ? 81 : 37), "Rotation reset independent: " + position + "/" + rotation);
        }
        var heading = StimulusHeadingReference.For(cl); heading.enabled = false;
        heading.Begin(new HeadingReferenceConfig { enabled = true, windowSeconds = 1 }, null);
        poseRoot.transform.rotation = Quaternion.Euler(0,45,0); heading.Tick(now + .5); heading.Tick(now + 1.1);
        var spawner = poseRoot.AddComponent<LocustSpawner>();
        spawner.boundaryManager = poseRoot.AddComponent<BoundaryManager>();
        var swarm = new SwarmConfig { agentVisual = "Bogong", numberOfLocusts = 0, useHeadingReference = true,
            mu = 90, spawnCenter = new Vector3Config { z = 10 }, dimension = "3D", boundaryLengthX = 100, boundaryLengthZ = 100 };
        SwarmConfiguration.Apply(spawner, swarm, cl);
        Vector3 expected = poseRoot.transform.position + Quaternion.Euler(0,45,0) * Vector3.forward * 10;
        Check(Angle(spawner.mu,135) && Vector3.Distance(spawner.spawnCenter,expected)<.001f, "Swarm heading and spawn-center offset use the same frozen reference");
        var moverRoot = new GameObject("Rotated boundary unit"); var mover = moverRoot.AddComponent<LocustMover>(); mover.enabled = false; mover.speed = 0; mover.boundaryManager = spawner.boundaryManager;
        moverRoot.transform.position = expected + Quaternion.Euler(0,45,0) * new Vector3(60,0,0);
        Call(mover, "Update");
        Vector3 wrapped = Quaternion.Euler(0,-45,0) * (moverRoot.transform.position - expected);
        Check(Mathf.Abs(wrapped.x + 49.9f) < .001f && Mathf.Abs(wrapped.z) < .001f, "Periodic wrapping uses the frozen rotated boundary frame");
        Object.DestroyImmediate(moverRoot);
        swarm.useHeadingReference = false; SwarmConfiguration.Apply(spawner,swarm,cl);
        Check(Angle(spawner.mu,90) && spawner.spawnCenter == Vector3.forward*10, "Computed reference does not affect an opted-out swarm");
        swarm.useHeadingReference = true; heading.Cancel(); SwarmConfiguration.Apply(spawner,swarm,cl);
        Check(Angle(spawner.mu,90) && spawner.spawnCenter == Vector3.forward*10, "Absent reference keeps authored world coordinates even when opted in");
        Object.DestroyImmediate(poseRoot);
        Check(new SceneConfig().resetPositionOnStart && new SceneConfig().resetRotationOnStart && !new SceneObject().useHeadingReference && !new SwarmConfig().useHeadingReference, "Backward-compatible reset and opt-in defaults");
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Phase validation stage " + stage);
            if (Time.frameCount < nextFrame) return;
            nextFrame = Time.frameCount + 1;
            if (stage == 0) { UnitChecks(); SceneManager.LoadScene("ControlScene"); stage = 1; nextFrame = Time.frameCount + 3; return; }
            main = MainController.Instance;
            if (stage == 1)
            {
                if (SceneManager.GetActiveScene().name != "Choice_Selwyn" || main == null || main.VRClosedLoops.Count != 4) return;
                rigs = main.VRClosedLoops.OrderBy(k => k.Key).Select(k => k.Value).ToArray();
                rigIds = rigs.Select(r => r.GetInstanceID()).ToArray(); terrainIds = Terrain.activeTerrains.Select(t => t.GetInstanceID()).ToArray();
                sceneId = SceneManager.GetActiveScene().handle; trial = main.currentTrial;
                Check(main.ExperimentPhase == "preStimulus" && main.AssessmentPending, "Pre-stimulus world and observation start together");
                if (!full)
                {
                    Check(rigs.All(r => r.AutoTrim.FlightThreshold == .02 && r.AutoTrim.FlightCheckEnabled), "Pre-stimulus experiment trim settings reach every rig");
                    main.SetCurrentFlightCheck(false);
                }
                Check(Object.FindObjectsByType<Bogong>(FindObjectsSortMode.None).Length == 0 && terrainIds.Length > 0, "Stars and terrain without conspecifics");
                starId = NightSkyController.CurrentId; Check(!string.IsNullOrEmpty(starId), "Star image active and archived");
                preAt = Time.timeAsDouble;
                if (!full) for (int i = 0; i < rigs.Length; i++) rigs[i].transform.rotation = Quaternion.Euler(0, 30 + i * 70, 0);
                prePositions = rigs.Select(r => r.transform.position).ToArray(); preRotations = rigs.Select(r => r.transform.rotation).ToArray();
                Render("phase-stars.png"); stage = 2; return;
            }
            if (stage == 2)
            {
                if (main.ExperimentPhase == "preStimulus")
                {
                    Check(NightSkyController.CurrentId == starId, "Pre world remains visible while reference is sampled");
                    return;
                }
                Check(main.ExperimentPhase == "stimulus", "Pre transitions directly to stimulus");
                if (!full) Check(rigs.All(r => r.AutoTrim.FlightThreshold == .04 && r.AutoTrim.FlightCheckEnabled), "New phase loads its settings and clears temporary flight bypass");
                stimulusAt = Time.timeAsDouble;
                Check(stimulusAt - preAt >= (full ? 238 : 2), "Pre-stimulus duration includes offset/window overrun");
                Check(sceneId == SceneManager.GetActiveScene().handle && rigIds.SequenceEqual(rigs.Select(r => r.GetInstanceID())) && terrainIds.SequenceEqual(Terrain.activeTerrains.Select(t => t.GetInstanceID())), "Scene, rigs and terrain retained across phase change");
                means = new float[rigs.Length];
                for (int i = 0; i < rigs.Length; i++)
                {
                    var reference = rigs[i].GetComponent<StimulusHeadingReference>().Result;
                    Check(reference != null && reference.sampleCount > 0, "Frozen independent reference exists " + i);
                    means[i] = (float)reference.meanHeadingDegrees;
                    Check(Quaternion.Angle(preRotations[i], rigs[i].transform.rotation) < .02, "Heading is not reset at stimulus " + i);
                    Check(Vector3.Distance(prePositions[i], rigs[i].transform.position) > 1, "Autopilot continued during pre-stimulus " + i);
                    var spawner = Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).Single(s => s.name == "VR" + (i + 1) + "_EmbeddedSwarm");
                    Check(Angle(spawner.mu, means[i] - 90), "Swarm travels left of this rig's frozen mean " + i);
                    Check(spawner.numberOfLocusts == 256 && spawner.bogongVisual.sizeMode == "Angular" && spawner.bogongVisual.angularSizeDegrees == 2 && spawner.bogongVisual.flickerFrequencyHz == 0, "256 steady two-degree dots per rig " + i);
                    Check(spawner.bogongVisual.color.r < .12f, "Dots darker than the uniform gray sky " + i);
                    var own = spawner.Spawned.Where(b => b.gameObject.layer == LayerMask.NameToLayer(spawner.layerName)).ToArray();
                    Check(own.Length == 256 && own.All(b => b.transform.position.y > rigs[i].transform.position.y), "Only dorsal dots, isolated to rig " + i);
                    Check(spawner.GetComponentsInChildren<BogongSwarmRenderer>().Length == 1 &&
                          own.All(b => b.GetComponent<Renderer>() == null), "Embedded swarm uses one batch without per-agent renderers " + i);
                }
                Check(NightSkyController.CurrentId != starId && !string.IsNullOrEmpty(NightSkyController.CurrentId), "Uniform sky replaces stars with a new recorded identity");
                string archive = Path.Combine(MasterDataLogger.Instance.directoryPath, "Skyboxes", NightSkyController.CurrentId + ".json");
                Check((string)JObject.Parse(File.ReadAllText(archive))["description"]["mode"] == "uniform", "Gray stimulus records its exact color and sky ID");
                var testSpawner = Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).Single(s => s.name == "VR1_EmbeddedSwarm");
                var testConfig = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "phase-validation.json")))["stimulus"]["swarm"].ToObject<SwarmConfig>();
                testConfig.useHeadingReference = false; SwarmConfiguration.Apply(testSpawner, testConfig, rigs[0]);
                Check(Angle(testSpawner.mu, -90), "Computing a reference does not force an opted-out spawner to use it");
                testConfig.useHeadingReference = true; SwarmConfiguration.Apply(testSpawner, testConfig, rigs[0]);
                Check(Angle(testSpawner.mu, means[0] - 90), "Explicit opt-in restores the fixed heading reference");
                Render("phase-gray-swarm.png");
                // Subsequent turns must not rotate the stimulus zero.
                rigs[0].transform.rotation *= Quaternion.Euler(0, 53, 0);
                var telemetry = JObject.FromObject(Object.FindFirstObjectByType<ExperimentTelemetry>().Capture(1));
                Check((string)telemetry["system"]["phase"] == "stimulus" && telemetry["rigs"].Count() == 4 && telemetry["system"]["remainingPhaseSeconds"] != null, "Phase, countdown and all rigs are available to monitoring");
                observations.Add("preStimulus → stimulus in same scene at " + Time.timeAsDouble);
                stage = 3; return;
            }
            if (stage == 3)
            {
                if (main.ExperimentPhase == "stimulus")
                {
                    Check(Angle(rigs[0].GetComponent<StimulusHeadingReference>().Result.meanHeadingDegrees, means[0]), "Frozen zero does not follow later turns");
                    var swarm = Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).Single(s => s.name == "VR1_EmbeddedSwarm");
                    Check(Angle(swarm.mu, means[0] - 90), "World swarm heading remains fixed");
                    if (full && Time.timeAsDouble - stimulusAt < 237) return;
                    return;
                }
                if (!full)
                {
                    Check(main.ExperimentPhase == "postStimulus", "Optional post-stimulus reached");
                    Check(rigs.All(r => r.AutoTrim.FlightThreshold == .005 && !r.AutoTrim.FlightCheckEnabled), "Post-stimulus reads the experiment's persistent flight-check bypass");
                    Check(Object.FindObjectsByType<Bogong>(FindObjectsSortMode.None).Length == 0, "Post-stimulus clears embedded swarm");
                    stage = 4; return;
                }
                VerifyRepeat(); return;
            }
            if (stage == 4)
            {
                if (main.currentTrial == trial) return;
                VerifyRepeat(); return;
            }
            if (stage == 5)
            {
                CheckRecordedPhases();
                Check(errors.Count == 0, "Clean phase run and shutdown: " + string.Join("; ", errors));
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }
    private static void CheckRecordedPhases()
    {
        string[] files = Directory.GetFiles(MasterDataLogger.Instance.directoryPath, "*_Choice_Selwyn_VR*_.csv");
        Check(files.Length == 4, "Four rig CSVs exist after phase cycle");
        foreach (string file in files)
        {
            string[] lines = File.ReadAllLines(file);
            string[] header = lines[0].Split(',');
            int phaseIndex = Array.IndexOf(header, "experimentPhase");
            Check(phaseIndex == header.Length - 1, "Phase column appended after existing rig columns");
            var phases = new HashSet<string>();
            foreach (string line in lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                string[] cells = line.Split(',');
                Check(cells.Length == header.Length, "Rig row retains header/column alignment");
                Check(!string.IsNullOrEmpty(cells[phaseIndex]), "Each persisted row carries a phase");
                phases.Add(cells[phaseIndex]);
            }
            Check(phases.Contains("preStimulus") && phases.Contains("stimulus") && (full || phases.Contains("postStimulus")), "Buffered CSV preserves each phase after transitions and shutdown");
        }
    }
    private static void VerifyRepeat()
    {
        Check(main.currentTrial == trial + 1 && main.ExperimentPhase == "preStimulus", "Eight-minute cycle repeats into a fresh observation");
        Check(sceneId == SceneManager.GetActiveScene().handle && rigIds.SequenceEqual(rigs.Select(r => r.GetInstanceID())), "Repeat uses the same scene and rigs");
        Check(rigs.All(r => r.GetComponent<StimulusHeadingReference>().Result == null), "Previous frozen zero cleared at next cycle");
        Check(Object.FindObjectsByType<Bogong>(FindObjectsSortMode.None).Length == 0, "Repeated pre-stimulus has no leftover swarm");
        observations.Add("repeat → preStimulus at " + Time.timeAsDouble);
        main.StopSequence(); SceneManager.LoadScene("ControlScene"); stage = 5; nextFrame = Time.frameCount + 4;
    }
    private static void Render(string file)
    {
        var camera = rigs[0].GetComponentsInChildren<Camera>().Single(c => c.name == "Main Camera U");
        var target = RenderTexture.GetTemporary(256,256,24); var oldTarget = camera.targetTexture; var oldRect = camera.rect; float oldAspect = camera.aspect;
        var previous = RenderTexture.active; var image = new Texture2D(256,256,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = target; camera.rect = new Rect(0,0,1,1); camera.aspect = 1; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,256,256),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../" + file), image.EncodeToPNG());
            var pixels = image.GetPixels();
            if (file.Contains("gray"))
            {
                int dark = pixels.Count(p => p.r < .06f), gray = pixels.Count(p => p.r > .09f && p.r < .15f);
                Check(dark > 0 && gray > pixels.Length / 2, "Dorsal render contains dark dots on uniform gray");
            }
        }
        finally { RenderTexture.active=previous;camera.targetTexture=oldTarget;camera.rect=oldRect;camera.aspect=oldAspect;RenderTexture.ReleaseTemporary(target);Object.Destroy(image); }
    }
    private static void Finish(string failure)
    {
        EditorApplication.update -= Tick; SceneManager.sceneLoaded -= DisableInputs; Application.logMessageReceived -= OnLog;
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Application.dataPath, full ? "../experiment-full-cycle-validation.json" : "../experiment-phase-validation.json"), JsonConvert.SerializeObject(new { checks, failure, errors, observations, fullDuration=full, unity=Application.unityVersion }, Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
