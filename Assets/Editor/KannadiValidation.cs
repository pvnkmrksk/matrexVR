#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run on a disposable project copy: Unity -batchmode -projectPath ... -executeMethod KannadiValidation.Run
[InitializeOnLoad]
public static class KannadiValidation
{
    private const string Key = "KannadiValidation.Active";
    private static int phase, nextFrame, checks;
    private static double timeout;
    private static MainController main;
    private static readonly List<string> failures = new List<string>();
    private static readonly List<string> editorMessages = new List<string>();
    private static Kannadi[] rigs;
    private static Vector3 expectedPosition;
    private static int rigInstance;
    private static string logDirectory;
    static KannadiValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                phase = 0; nextFrame = Time.frameCount + 2; timeout = EditorApplication.timeSinceStartup + 120;
                Application.logMessageReceived += OnLog;
                SceneManager.sceneLoaded += DisableLiveInputs;
                EditorApplication.update += Tick;
            }
        };
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new InvalidOperationException("Run KannadiValidation only on a disposable copy with KANNADI_VALIDATION_COPY at its root.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) { editorMessages.Add(message); return; }
        if (type == LogType.Exception || type == LogType.Error) failures.Add(message + "\n" + stack);
    }
    private static void DisableLiveInputs(Scene scene, LoadSceneMode mode)
    {
        foreach (ZmqListener listener in Object.FindObjectsOfType<ZmqListener>())
        {
            listener.enabled = false;
            Call(listener, "ApplySystemConfig");
        }
    }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Validation failed: " + message);
    }
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static Dictionary<string, object> Params(string json) => JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
    private static void Later() { phase++; nextFrame = Time.frameCount + 8; }
    private static void NextSequenceStep()
    {
        typeof(MainController).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, 0f);
        Call(main, "ManageTimerAndTransitions");
    }
    private static void CheckViewports(string scene)
    {
        ViewportSetter[] setters = Object.FindObjectsOfType<ViewportSetter>();
        Check(setters.Length == 4, scene + " has four configured rigs");
        foreach (ViewportSetter setter in setters)
        {
            bool found = main.TryGetSystemConfigForGameObject(setter.gameObject, out SystemConfig config);
            string order = found ? ViewportSetter.NormalizeDisplayOrder(config.displayOrder) : "";
            Camera[] cameras = setter.GetComponentsInChildren<Camera>(true);
            foreach (Camera camera in cameras)
            {
                int slot = camera.name.StartsWith("Main Camera ") && camera.name.Length == 13 ? order.IndexOf(camera.name[12]) : -1;
                Check(camera.enabled == (slot >= 0), scene + "/" + setter.name + "/" + camera.name + " enabled only by configured letter");
                if (slot < 0) continue;
                Check(camera.gameObject.activeInHierarchy && camera.targetDisplay == config.targetDisplay, scene + " listed camera active on configured display");
                float x = (config.startCol + (config.horizontal ? slot : 0)) * config.ledPanelWidth;
                float y = Screen.height - (config.startRow + 1 + (config.horizontal ? 0 : slot)) * config.ledPanelHeight;
                // Batch Game View clips pixelRect at its 640x480 edge; verify the configured rectangle before clipping.
                Rect rect = new Rect(camera.rect.x * Screen.width, camera.rect.y * Screen.height, camera.rect.width * Screen.width, camera.rect.height * Screen.height);
                Check(Mathf.Abs(rect.x - x) < 0.1f && Mathf.Abs(rect.y - y) < 0.1f &&
                    Mathf.Abs(rect.width - config.ledPanelWidth) < 0.1f && Mathf.Abs(rect.height - config.ledPanelHeight) < 0.1f,
                    scene + "/" + setter.name + "/" + camera.name + " expected pixels " + new Rect(x, y, config.ledPanelWidth, config.ledPanelHeight) + " got " + rect + " normalized " + camera.rect + " screen " + Screen.width + "x" + Screen.height);
            }
            Check(cameras.Count(c => c.enabled) == order.Count(letter => cameras.Any(c => c.name == "Main Camera " + letter)), scene + " each available configured camera enabled once");
        }
    }
    private static void CheckCameraReconfiguration()
    {
        var all = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json")));
        var configs = (Newtonsoft.Json.Linq.JArray)all["configs"];
        string[] orders = { "UL", "FDRBLU", "", "B" };
        for (int i = 0; i < configs.Count; i++)
        {
            configs[i]["displayOrder"] = orders[i];
            configs[i]["horizontal"] = i % 2 == 1;
            configs[i]["startRow"] = i;
            configs[i]["startCol"] = i + 1;
            configs[i]["ledPanelWidth"] = 64;
            configs[i]["ledPanelHeight"] = 96;
        }
        string file = "Kannadi/validation-system.json";
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, file), all.ToString());
        main.SetSystemConfigFile(file);
        CheckViewports("Kannadi reordered system config");
        configs.RemoveAt(3);
        ((Newtonsoft.Json.Linq.JObject)configs[1]).Remove("displayOrder");
        configs[0]["displayOrder"] = null;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, file), all.ToString());
        main.SetSystemConfigFile(file);
        CheckViewports("Kannadi missing rig / missing or null displayOrder");
        main.SetSystemConfigFile("system_config.json");
        CheckViewports("Kannadi restored VR1 config");

        var unknown = new GameObject("VR10 validation");
        Camera camera = new GameObject("Main Camera R").AddComponent<Camera>();
        camera.transform.SetParent(unknown.transform);
        ViewportSetter setter = unknown.AddComponent<ViewportSetter>();
        Check(!main.TryGetSystemConfigForGameObject(unknown, out _) && !camera.enabled, "VR10 cannot borrow VR1 config");
        unknown.name = "VR1 validation";
        setter.RefreshSystemConfig();
        Check(camera.enabled, "known rig re-enables its listed camera");
        Check(main.TryGetSystemConfigForGameObject(camera.gameObject, out var inherited) && inherited.vrId == "VR1", "child resolves parent rig configuration");
        Camera up = new GameObject("Main Camera U").AddComponent<Camera>();
        up.transform.SetParent(unknown.transform); up.gameObject.SetActive(false);
        Camera unlisted = new GameObject("Diagnostic camera").AddComponent<Camera>();
        unlisted.transform.SetParent(unknown.transform);
        setter.ApplyConfiguration(new SystemConfig { targetDisplay = 0, displayOrder = "uUr?R", ledPanelWidth = 64, ledPanelHeight = 96 });
        Check(camera.enabled && up.enabled && up.gameObject.activeSelf && !unlisted.enabled, "inactive listed cameras enabled; unknown and duplicate letters ignored");
        Check(Mathf.Abs(up.pixelRect.x) < 0.1f && Mathf.Abs(camera.pixelRect.x - 64) < 0.1f, "duplicate/invalid letters do not shift camera slots");
        setter.ApplyConfiguration(new SystemConfig { targetDisplay = Display.displays.Length, displayOrder = "RU" });
        Check(!camera.enabled && !up.enabled && !unlisted.enabled, "unavailable display cannot render cameras elsewhere");
        setter.ApplyConfiguration(new SystemConfig { targetDisplay = 0, displayOrder = "R" });
        setter.enabled = false;
        Check(!camera.enabled, "disabled layout cannot leave stimulus camera rendering");
        unknown.SetActive(false);
        Object.Destroy(unknown);
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < nextFrame) return;
        try
        {
            if (EditorApplication.timeSinceStartup > timeout) throw new Exception("Validation timed out.");
            switch (phase)
            {
                case 0:
                    var parsed = KannadiConfig.Load(Params("{\"closedLoopPosition\":true,\"closedLoopOrientation\":0.5,\"numberOfRings\":0,\"spacing\":7}"));
                    Check(parsed.closedLoopPosition == 1 && parsed.closedLoopOrientation == 0.5f && parsed.numberOfRings == 0, "legacy boolean/numeric gains and zero rings");
                    Check(Mathf.Approximately(Kannadi.Wrap(351, 100), -49) && Mathf.Approximately(Kannadi.Wrap(-251, 100), 49), "overshoot wrapping");
                    bool rejected = false;
                    try { KannadiConfig.Load(Params("{\"vrConfigs\":[{\"vrIndex\":1,\"watchIndex\":5}]}")); } catch (ArgumentException) { rejected = true; }
                    Check(rejected, "reject invalid watchIndex");
                    Screen.SetResolution(1920, 1080, false);
                    Directory.CreateDirectory(Path.Combine(Application.dataPath, "RunData/Backup"));
                    File.WriteAllText(Path.Combine(Application.dataPath, "RunData/Backup/FlyMetaData.json"), "{\"ExperimenterName\":\"Validation\",\"Comments\":\"Synthetic test session\",\"Flies\":[],\"UsedFlyIDs\":[]}");
                    new GameObject("Validation Logger").AddComponent<MasterDataLogger>();
                    main = new GameObject("Validation Main").AddComponent<MainController>();
                    logDirectory = MasterDataLogger.Instance.directoryPath;
                    Later(); break;
                case 1:
                    Check(main.GetSystemConfig("VR4").zmqPort == 9874, "four VR1 hardware configurations loaded");
                    main.sequenceSteps[0].duration = 1000;
                    main.sequenceSteps.Insert(1, new SequenceStep("Swarm", 1000, Params("{\"numberOfLocusts\":3}"), false));
                    main.StartSequence(); Later(); break;
                case 2:
                    Check(SceneManager.GetActiveScene().name == "Swarm", "Swarm sequence dispatch");
                    var spawners = Object.FindObjectsOfType<LocustSpawner>();
                    Check(spawners.Length == 4, "four swarm rigs");
                    Check(GameObject.FindGameObjectsWithTag("SimulatedLocust").Length == 512, "128 swarm agents per rig, no duplicate Start spawn");
                    foreach (ClosedLoop cl in Object.FindObjectsOfType<ClosedLoop>()) Check(Field<float>(cl, "sphereDiameter") == 2.6f, "system sphere diameter applies to Swarm");
                    CheckViewports("Swarm");
                    Check(Object.FindObjectsOfType<Kannadi>().Length == 0, "Swarm contains no mirror controller");
                    int swarmInstance = Object.FindObjectOfType<SwarmController>().GetInstanceID();
                    NextSequenceStep();
                    Check(Object.FindObjectOfType<SwarmController>().GetInstanceID() == swarmInstance, "Swarm sequence advances without reload");
                    Check(GameObject.FindGameObjectsWithTag("SimulatedLocust").Length == 12, "Swarm replaces population on in-scene step");
                    NextSequenceStep(); Later(); break;
                case 3:
                    CheckViewports("Kannadi");
                    rigs = Object.FindObjectsOfType<Kannadi>().OrderBy(r => r.VRIndex).ToArray();
                    Check(rigs.Length == 4 && rigs.All(r => r.Clones.Length == 37), "four Kannadi grids with 37 clones each");
                    Check(GameObject.FindGameObjectsWithTag("SimulatedLocust").Length == 148, "Swarm agents cleaned up at Kannadi transition");
                    var catalog = Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog");
                    Check(catalog != null && catalog.Find("SimulatedLocust") != null && catalog.Find("LocustBand_black") != null, "build-safe single and band prefab references");
                    foreach (GameObject prefab in catalog.prefabs)
                    {
                        Check(prefab != null && prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "catalog prefab resolves without missing scripts");
                        Check(prefab.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(m => m != null)), "catalog materials resolve");
                    }
                    var orbit = Object.FindObjectOfType<OverheadCameraController>();
                    typeof(OverheadCameraController).GetField("verticalAngle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(orbit, 89f);
                    Call(orbit, "UpdateCameraPosition");
                    Check(orbit.transform.position.y - orbit.targetPosition.y > orbit.distance * 0.99f, "orbit remains overhead immediately below 90 degrees");
                    orbit.ResetToSkyView();
                    foreach (Kannadi rig in rigs)
                    {
                        var cl = rig.GetComponent<ClosedLoop>();
                        Check(Field<float>(cl, "sphereDiameter") == 2.6f, "system sphere diameter applies to Kannadi");
                        Check(rig.GetComponent<ZmqListener>().port == 9870 + rig.VRIndex, "per-rig ZMQ config");
                        foreach (Camera camera in rig.GetComponentsInChildren<Camera>())
                        {
                            int own = LayerMask.GetMask("SimulatedLocustsVR" + rig.VRIndex);
                            Check((camera.cullingMask & own) == 0, "default camera excludes own replicas");
                            Check((camera.cullingMask & LayerMask.GetMask("OverheadCameraMarkers")) == 0, "markers excluded from stimulus cameras");
                            Check(camera.targetDisplay == 0, "system target display");
                        }
                    }
                    Check(Object.FindObjectsOfType<SimpleOverheadCamera>().Length == 1, "single overview");
                    Check(GameObject.Find("Overhead Camera Display") != null && Object.FindObjectOfType<OverheadCameraController>().inputRect != null, "overview panel input binding");
                    var rt = GameObject.Find("Simple Overhead Camera").GetComponent<Camera>().targetTexture;
                    RenderTexture.active = rt; var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(Application.dataPath, "../kannadi-overview.png"), image.EncodeToPNG()); Object.Destroy(image); RenderTexture.active = null;
                    rigs[0].AdvanceStep(Params("{\"numberOfRings\":1,\"hexRadius\":5,\"boundaryLengthX\":200,\"boundaryLengthZ\":120,\"vrConfigs\":[{\"vrIndex\":1,\"watchIndex\":1}]}"));
                    Check(rigs[0].Clones.Length == 6 && rigs[1].Clones.Length == 7, "self-view omits only its own center");
                    rigs[0].transform.SetPositionAndRotation(new Vector3(301, 1, 183), Quaternion.Euler(0, 95, 0));
                    Later(); break;
                case 4:
                    Check(Mathf.Approximately(rigs[0].transform.position.x, -99) && Mathf.Approximately(rigs[0].transform.position.z, -57), "rectangular arena preserves overshoot");
                    foreach (var clone in rigs[0].Clones)
                    {
                        Check(clone.transform.position.x >= -100 && clone.transform.position.x < 100 && clone.transform.position.z >= -60 && clone.transform.position.z < 60, "replicas remain inside rectangular boundary");
                        Check(Quaternion.Angle(clone.transform.rotation, rigs[0].transform.rotation * rigs[0].tilePrefab.transform.rotation) < 0.01f, "replica follows tracked yaw");
                        Check(Mathf.Approximately(clone.transform.position.y, rigs[0].tilePrefab.transform.position.y), "prefab height preserved");
                    }
                    rigs[0].AdvanceStep(Params("{\"numberOfRings\":0,\"vrConfigs\":[{\"vrIndex\":1,\"watchIndex\":2},{\"vrIndex\":2,\"watchIndex\":2}]}"));
                    Check(rigs[0].Clones.Length == 1 && rigs[1].Clones.Length == 0, "zero-ring peer and self views");
                    Camera peer = rigs[0].GetComponentInChildren<Camera>();
                    Check((peer.cullingMask & LayerMask.GetMask("SimulatedLocustsVR2")) != 0 && (peer.cullingMask & LayerMask.GetMask("SimulatedLocustsVR3")) == 0, "watchIndex selects the peer layer");
                    Check((peer.cullingMask & 1) != 0, "watchIndex preserves the shared environment");
                    rigs[0].AdvanceStep(Params("{\"numberOfRings\":-1}"));
                    Check(rigs.All(r => r.Clones.Length == 0), "negative rings remove all replicas");
                    rigs[0].AdvanceStep(Params("{\"numberOfRings\":0,\"kannadiTilePrefab\":\"LocustBand_black\"}"));
                    Later(); break;
                case 5:
                    Check(rigs.All(r => r.Clones.Length == 1 && r.Clones[0].GetComponentsInChildren<Renderer>().Length > 0), "band prefabs spawn visible members");
                    Check(rigs.SelectMany(r => r.Clones[0].GetComponentsInChildren<DirectionalMovement>()).All(m => !m.enabled), "mirrored bands have no independent movement");
                    expectedPosition = rigs[0].Clones[0].transform.GetChild(0).position + new Vector3(3, 0, 4);
                    rigs[0].transform.position += new Vector3(3, 0, 4);
                    Later(); break;
                case 6:
                    Check(Vector3.Distance(expectedPosition, rigs[0].Clones[0].transform.GetChild(0).position) < 0.01f, "band members follow tracked translation");
                    main.sequenceSteps.Clear(); main.executionOrder.Clear(); main.sequenceSteps.Add(new SequenceStep("Kannadi", 1000, Params("{\"numberOfRings\":1,\"spacing\":9}"), true));
                    main.sequenceSteps.Add(new SequenceStep("Kannadi", 1000, Params("{\"numberOfRings\":0}"), false));
                    main.sequenceSteps.Add(new SequenceStep("Choice", 1000, Params("{\"configFile\":\"Choice_empty.json\"}"), true));
                    main.sequenceSteps.Add(new SequenceStep("Choice_TwoTreeIndia_nopath_path", 1000, Params("{\"configFile\":\"BinaryChoiceIndia_flip_noflip.json\"}"), true));
                    main.sequenceSteps.Add(new SequenceStep("Choice_desync", 1000, Params("{\"design\":\"dynamicSequenceDesign.json\"}"), true));
                    main.StartSequence(); Later(); break;
                case 7:
                    rigs = Object.FindObjectsOfType<Kannadi>();
                    Check(rigs.Length == 4 && rigs.All(r => r.Clones.Length == 7), "Kannadi reload through sequence config");
                    CheckCameraReconfiguration();
                    Check(Object.FindObjectsOfType<SimpleOverheadCamera>().Length == 1, "Kannadi reload gets one overview");
                    var tracking = rigs[0].GetComponent<ClosedLoop>();
                    var listener = rigs[0].GetComponent<ZmqListener>();
                    tracking.SetLocustGains(0.5f, 1f); tracking.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 90, 0));
                    typeof(ZmqListener).GetProperty("pose").SetValue(listener, new Pose(Vector3.zero, Quaternion.identity));
                    Call(tracking, "InitializeFicTracData");
                    typeof(ZmqListener).GetProperty("pose").SetValue(listener, new Pose(new Vector3(0, 2, 0), Quaternion.identity));
                    Call(tracking, "UpdateTransform");
                    Check(Vector3.Distance(tracking.transform.position, new Vector3(0, 0, -1.3f)) < 0.01f, "numeric translation gain and sphere diameter respect base rotation");
                    expectedPosition = tracking.transform.position;
                    Call(tracking, "Update");
                    Check(tracking.transform.position == expectedPosition, "missing tracking pose cannot move animal");
                    typeof(ZmqListener).GetProperty("HasPose").SetValue(listener, true);
                    typeof(ZmqListener).GetField("lastPoseTicksUtc", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(listener, DateTime.UtcNow.AddSeconds(-30).Ticks);
                    Call(tracking, "Update");
                    Check(tracking.transform.position == expectedPosition, "stale tracking pose cannot move animal");
                    rigInstance = rigs[0].GetInstanceID();
                    NextSequenceStep();
                    Check(rigs[0].GetInstanceID() == rigInstance && rigs.All(r => r.Clones.Length == 1), "Kannadi sequence advances without reload");
                    CheckViewports("Kannadi in-scene sequence step");
                    NextSequenceStep(); Later(); break;
                case 8:
                    Check(SceneManager.GetActiveScene().name == "Choice" && Object.FindObjectOfType<ChoiceController>() != null, "Choice sequence dispatch");
                    CheckViewports("Choice");
                    Check(Object.FindObjectsOfType<Kannadi>().Length == 0, "Choice has no mirror components");
                    NextSequenceStep(); Later(); break;
                case 9:
                    Check(SceneManager.GetActiveScene().name == "Choice_TwoTreeIndia_nopath_path", "JuliusTree experiment sequence dispatch");
                    CheckViewports("JuliusTree two-tree scene");
                    NextSequenceStep(); Later(); break;
                case 10:
                    Check(SceneManager.GetActiveScene().name == "Choice_desync" && Object.FindObjectOfType<DynamicSequenceController>() != null, "dynamic sequence dispatch");
                    CheckViewports("JuliusTree dynamic sequence");
                    main.StopSequence();
                    SceneManager.LoadScene("ControlScene"); Later(); break;
                case 11:
                    Check(Object.FindObjectsOfType<SimpleOverheadCamera>().Length == 0 && GameObject.Find("Overhead Camera Canvas") == null, "overview resources cleaned up on exit");
                    Check(Directory.GetFiles(logDirectory, "*Kannadi*Clones.csv.gz").Length >= 8, "Kannadi clone logging across reloads");
                    Finish(); break;
            }
        }
        catch (Exception error) { failures.Add(error.ToString()); Finish(); }
    }
    private static void Finish()
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SceneManager.sceneLoaded -= DisableLiveInputs;
        File.WriteAllText(Path.Combine(Application.dataPath, "../kannadi-validation.json"), JsonConvert.SerializeObject(new { checks, failures, editorMessages }, Formatting.Indented));
        Debug.Log($"KANNADI VALIDATION: {checks} checks, {failures.Count} failures");
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }
}
#endif
