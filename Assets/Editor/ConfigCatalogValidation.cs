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
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ConfigCatalogValidation
{
    private const string Key = "ConfigCatalogValidation.Active";
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks, phase, nextFrame;
    private static double deadline;
    private static readonly List<string> errors = new List<string>();
    private static string backup, savedBackup;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Flags).GetValue(obj);
    private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);

    static ConfigCatalogValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 40;
            Application.logMessageReceived += OnLog;
            SceneManager.sceneLoaded += DisableInputs;
            EditorApplication.update += Tick;
        };
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        errors.Add(message);
    }
    private static void DisableInputs(Scene scene, LoadSceneMode mode)
    {
        foreach (var input in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) input.enabled = false;
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable validation copy only.");
        var system = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/system_config.template.json")));
        system["telemetry"]["enabled"] = false;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), system.ToString());
        var sequence = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Templates/sequence.template.json")));
        sequence["autoStart"] = false;
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), sequence.ToString());
        backup = Path.Combine(Application.dataPath, "RunData/Backup/FlyMetaData.json");
        savedBackup = backup + ".config-validation-saved";
        if (File.Exists(backup)) File.Move(backup, savedBackup);
        SessionState.SetString(Key + ".backup", savedBackup);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    private static void Complete(JObject obj, Type type, string path, HashSet<string> excluded = null)
    {
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (excluded != null && excluded.Contains(field.Name)) continue;
            Check(obj.Property(field.Name) != null, path + " missing explicit field " + field.Name);
            JToken token = obj[field.Name];
            Type child = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
            if (token is JObject nested && !typeof(System.Collections.IDictionary).IsAssignableFrom(child)) Complete(nested, child, path + "." + field.Name);
            if (token is JArray list)
            {
                child = child.IsArray ? child.GetElementType() : child.IsGenericType ? child.GetGenericArguments()[0] : null;
                if (child != null) foreach (var item in list.OfType<JObject>()) Complete(item, child, path + "." + field.Name);
            }
        }
    }
    private static void Catalog()
    {
        var strict = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };
        var swarmOnly = new HashSet<string> { "numberOfRings", "hexRadius", "spacing", "kannadiTilePrefab", "periodicBoundary" };
        string root = Application.streamingAssetsPath;
        foreach (string file in Directory.GetFiles(Path.Combine(root, "Templates"), "*.json")
            .Concat(Directory.GetFiles(Path.Combine(root, "Examples"), "*.json", SearchOption.AllDirectories)))
        {
            var obj = JObject.Parse(File.ReadAllText(file));
            if (obj["configs"] != null)
            {
                foreach (JObject rig in obj["configs"])
                {
                    Check(JsonConvert.DeserializeObject<SystemConfig>(rig.ToString(), strict) != null, "Strict system schema");
                    Complete(rig, typeof(SystemConfig), file);
                }
                Complete((JObject)obj["telemetry"], typeof(TelemetryConfig), file);
                Complete((JObject)obj["statusOverlay"], typeof(StatusOverlayConfig), file);
                Complete((JObject)obj["overheadCamera"], typeof(OverheadCameraConfig), file);
                continue;
            }
            if (obj["stimulus"] != null || obj["preStimulus"] != null || obj["postStimulus"] != null)
            {
                foreach (var phase in ExperimentPhase.Parse(obj)) CheckExperiment(phase.Config, file + ":" + phase.Name, strict, swarmOnly, true);
            }
            else CheckExperiment(obj, file, strict, swarmOnly, false);
        }

        string oldName = "BinaryChoiceIndia_flip_noflip.json";
        string archived = ExperimentConfigFiles.Resolve(oldName);
        Check(archived == Path.Combine(root, "Archive/Legacy", oldName) && File.Exists(archived), "Untouched Kannadi reference resolves archived Choice config");
        var parameters = new Dictionary<string, object> { ["configFile"] = oldName };
        var resolved = ExperimentConfigFiles.ResolveReferences(parameters);
        Check((string)parameters["configFile"] == oldName && (string)resolved["configFile"] == "Archive/Legacy/" + oldName, "Runtime resolution preserves original sequence parameters");
        var choiceRoot = new GameObject("Archive config test"); choiceRoot.SetActive(false);
        var choice = choiceRoot.AddComponent<ChoiceController>(); choice.enabled = false;
        Check(Call(choice, "LoadSceneConfig", oldName) is SceneConfig, "Choice loads archived config through real loader");
        Object.Destroy(choice.gameObject);
        string current = Path.Combine(root, oldName);
        File.WriteAllText(current, "{}");
        try { Check(ExperimentConfigFiles.Resolve(oldName) == current, "Current config overrides archive"); }
        finally { File.Delete(current); }
        Check(ExperimentConfigFiles.Resolve("does-not-exist.json") == Path.Combine(root, "does-not-exist.json"), "Missing config remains missing");
        Check(ExperimentConfigFiles.Resolve(archived) == archived, "Absolute config path preserved");
    }
    private static void CheckExperiment(JObject obj, string file, JsonSerializerSettings strict, HashSet<string> swarmOnly, bool phased)
    {
        Type type = obj["sequences"] != null ? typeof(SequenceConfig) : obj["objects"] != null ? typeof(SceneConfig) :
            obj["stimuli"] != null ? typeof(OptomotorConfig) : obj["steps"] != null ? typeof(DynamicSequenceController).GetNestedType("DesignFile", BindingFlags.NonPublic) : typeof(KannadiConfig);
        Check(JsonConvert.DeserializeObject(obj.ToString(), type, strict) != null, "Strict schema " + file);
        bool preservedKannadi = file.EndsWith("kannadi.template.json") || file.EndsWith("kinematic.template.json");
        var excluded = type == typeof(KannadiConfig) ? new HashSet<string>(swarmOnly) : new HashSet<string>();
        if (phased) excluded.Add("headingReference");
        if (!preservedKannadi) Complete(obj, type, file, excluded);
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Config/UI phase " + phase);
            if (Time.frameCount < nextFrame) return;
            if (phase == 0) { Catalog(); SceneManager.LoadScene("ControlScene"); phase = 1; nextFrame = Time.frameCount + 4; return; }
            var ui = Object.FindFirstObjectByType<UIDataLogger>();
            Check(ui != null, "Control metadata form exists");
            foreach (var dropdown in ui.sexDropdowns)
            {
                Check(dropdown.gameObject.activeInHierarchy && dropdown.interactable, "Sex control visible and interactive");
                Check(dropdown.options.Select(o => o.text).SequenceEqual(new[] { "Unspecified", "Female", "Male" }), "Three explicit sex options");
                Check(dropdown.value == 0 && dropdown.captionText.text == "Unspecified", "Fresh default is Unspecified");
            }
            ui.sexDropdowns[0].value = 1; ui.sexDropdowns[1].value = 2;
            for (int i = 0; i < 4; i++) ui.flyIDInputs[i].text = "sex-test-" + i;
            ui.SaveData();
            string saved = File.ReadAllText(Path.Combine(Get<string>(ui, "backupDirectoryPath"), "FlyMetaData.json"));
            var metadata = JObject.Parse(saved);
            Check(metadata["Flies"].Select(f => (string)f["Sex"]).SequenceEqual(new[] { "Female", "Male", "Unspecified", "Unspecified" }), "Save records exact selected sex per rig");
            foreach (var d in ui.sexDropdowns) d.value = 0;
            ui.LoadLastSessionData();
            Check(ui.sexDropdowns.Select(d => d.value).SequenceEqual(new[] { 1, 2, 0, 0 }), "Reload restores saved Female/Male by label");
            metadata["Flies"][0]["Sex"] = "Unknown"; metadata["Flies"][1]["Sex"] = null;
            metadata["Flies"][2]["Sex"] = "male"; metadata["Flies"][3]["Sex"] = "female";
            Call(ui, "DeserializeAndSetData", metadata.ToString());
            Check(ui.sexDropdowns.Select(d => d.value).SequenceEqual(new[] { 0, 0, 2, 1 }), "Old Unknown/missing and mixed-case labels map correctly");
            foreach (var d in ui.sexDropdowns) d.value = 0;
            Render(ui);
            Canvas.ForceUpdateCanvases();
            foreach (var dropdown in ui.sexDropdowns)
                Check(!WorldRect(dropdown.transform).Overlaps(WorldRect(ui.commentsInput.transform)), "Sex controls do not overlap comments");
            ui.sexDropdowns[0].Show();
            Check(ui.sexDropdowns[0].GetComponentsInChildren<Toggle>().Count() == 3, "Existing dropdown opens all three selectable options");
            ui.sexDropdowns[0].Hide();
            Check(errors.Count == 0, "No config/UI runtime errors: " + string.Join("; ", errors));
            Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }
    private static Rect WorldRect(Transform transform)
    {
        var corners = new Vector3[4]; ((RectTransform)transform).GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
    private static void Render(UIDataLogger ui)
    {
        Canvas canvas = ui.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = ui.sexDropdowns[0].GetComponentInParent<Canvas>();
        var cameraRoot = new GameObject("Metadata validation camera"); var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f,.12f,.15f); camera.cullingMask = 1 << 5;
        var target = new RenderTexture(1600,900,24); target.Create(); camera.targetTexture = target;
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; var oldDistance = canvas.planeDistance;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(1600,900,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../sex-metadata-ui.png"),texture.EncodeToPNG());
        RenderTexture.active=previous;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;
        camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(texture);Object.Destroy(cameraRoot);
    }
    private static void Finish(string failure)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; SceneManager.sceneLoaded -= DisableInputs;
        SessionState.SetBool(Key,false);
        string original = Path.Combine(Application.dataPath,"RunData/Backup/FlyMetaData.json");
        if (File.Exists(original)) File.Delete(original);
        string saved = SessionState.GetString(Key + ".backup", "");
        if (File.Exists(saved)) File.Move(saved, original);
        File.WriteAllText(Path.Combine(Application.dataPath,"../config-catalog-validation.json"),JsonConvert.SerializeObject(new {checks,failure,errors,unity=Application.unityVersion},Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
