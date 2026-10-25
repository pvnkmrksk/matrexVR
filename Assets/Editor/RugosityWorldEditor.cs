using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RugosityWorld))]
public sealed class RugosityWorldEditor : Editor
{
    private const string KeepKey = "Rugosity.KeepPlayTuning";
    [InitializeOnLoadMethod]
    private static void RestorePlayTuning()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            string saved = SessionState.GetString(KeepKey, "");
            SessionState.EraseString(KeepKey);
            if (string.IsNullOrEmpty(saved)) return;
            var record = JObject.Parse(saved);
            if (!GlobalObjectId.TryParse(record.Value<string>("target"), out var id)) return;
            var world = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as RugosityWorld;
            if (world == null) { Debug.LogWarning("Cannot restore rugosity tuning: scene object is unavailable."); return; }
            Undo.RecordObject(world, "Keep rugosity Play tuning");
            world.settings = record["settings"].ToObject<RugositySettings>();
            world.ApplyLiveSettings();
            EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
        };
    }

    public override void OnInspectorGUI()
    {
        var world = (RugosityWorld)target;
        EditorGUILayout.HelpBox("Visual surface controls appearance. Navigation uses the collision surface for both height and tilt unless explicitly overridden. RoughSmooth / SmoothRough describe spatial patterns, not visual/navigation pairings. Edit live; JSON is applied only at trial boundaries.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("configurationPolicy"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("surfaces"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rigTargets"), true);
        var settings = serializedObject.FindProperty("settings");
        EditorGUILayout.PropertyField(settings.FindPropertyRelative("enabled"));
        SurfacePopup(settings.FindPropertyRelative("visualSurface"), world, false);
        SurfacePopup(settings.FindPropertyRelative("collisionSurface"), world, false);
        SurfacePopup(settings.FindPropertyRelative("heightSurface"), world, true);
        SurfacePopup(settings.FindPropertyRelative("normalSurface"), world, true);
        foreach (string name in new[] { "heightMode", "orientationMode", "centimetersPerUnit", "clearanceCm", "absoluteHeight", "normalRadiusCm", "normalSamples", "tiltResponse" })
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(name));
        serializedObject.ApplyModifiedProperties();
        if (EditorGUI.EndChangeCheck()) world.ApplyLiveSettings();
        if (!string.IsNullOrEmpty(world.LastError)) EditorGUILayout.HelpBox(world.LastError, MessageType.Error);
        if (GUILayout.Button("Preview pose at current rig positions"))
        {
            world.ApplyLiveSettings();
            foreach (var rig in FindObjectsByType<RugosityRig>(FindObjectsSortMode.None))
                if (rig.world == world) { Undo.RecordObject(rig.transform, "Preview rugosity pose"); rig.Conform(0, true); }
        }
        if (GUILayout.Button("Copy effective rugosity JSON"))
            EditorGUIUtility.systemCopyBuffer = JsonConvert.SerializeObject(new { rugosity = world.settings }, Formatting.Indented);
        if (GUILayout.Button("Save rugosity into choice JSON…"))
        {
            string path = EditorUtility.SaveFilePanel("Save tuned rugosity (other choice fields are preserved)", Application.streamingAssetsPath, "rugosity-choice", "json");
            if (!string.IsNullOrEmpty(path))
            {
                var json = File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : JObject.FromObject(new SceneConfig());
                // Phased experiment documents keep their settings inside the stimulus block.
                var destination = json["stimulus"] as JObject ?? json;
                destination["rugosity"] = JObject.FromObject(world.settings);
                File.WriteAllText(path, json.ToString(Formatting.Indented) + "\n");
                AssetDatabase.Refresh();
            }
        }
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
            if (GUILayout.Button("Keep current settings in scene after Stop"))
                KeepAfterStop(world);
        EditorGUILayout.HelpBox("Keep stores a snapshot now and restores it when Play stops; save the scene afterward. InspectorOnly ignores config only in the Editor. Builds always use config overrides.", MessageType.None);
    }
    public static void KeepAfterStop(RugosityWorld world)
    {
        SessionState.SetString(KeepKey, new JObject {
            ["target"] = GlobalObjectId.GetGlobalObjectIdSlow(world).ToString(),
            ["settings"] = JObject.FromObject(world.settings)
        }.ToString());
    }
    private static void SurfacePopup(SerializedProperty property, RugosityWorld world, bool inherit)
    {
        var values = world.surfaces.Where(s => s != null).Select(s => s.surfaceId).Distinct().ToList();
        if (inherit) values.Insert(0, "");
        if (!values.Contains(property.stringValue)) values.Add(property.stringValue);
        var labels = values.Select(v => v == "" ? "Collision surface (inherit)" : v).ToArray();
        int current = values.IndexOf(property.stringValue);
        int selected = EditorGUILayout.Popup(property.displayName, current, labels);
        if (selected >= 0 && selected < values.Count) property.stringValue = values[selected];
    }

}
