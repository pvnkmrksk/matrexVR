#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Actual replay cameras, built-in renderers and forward/backward seeks with a controlled four-rig recording.</summary>
[InitializeOnLoad]
public static class ReplayCameraVisualValidation
{
    private const string Key = "ReplayCameraVisualValidation.Active";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    private static string output;
    private static bool finished;
    private static double deadline;
    private static ReplayController replay;
    private static string Arg(string key, string fallback)
    { var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
    static ReplayCameraVisualValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            output = SessionState.GetString(Key + ".Output", ""); deadline = EditorApplication.timeSinceStartup + 120;
            Application.logMessageReceived += Log; EditorApplication.update += Watchdog;
            replay = Object.FindFirstObjectByType<ReplayController>();
            replay.StartCoroutine(Guard(Exercise()));
        };
    }
    public static void Run()
    {
        if (!File.Exists("KANNADI_VALIDATION_COPY")) throw new InvalidOperationException("Use a marked disposable project.");
        string fixture = Path.GetFullPath(Arg("-replayVisualFixture", "replay-camera-fixture"));
        if (!File.Exists(Path.Combine(fixture, "FIXTURE-SCOPE.txt"))) throw new InvalidOperationException("Use the explicitly generated visual fixture.");
        output = Path.GetFullPath(Arg("-replayVisualOutput", "replay-camera-visual-validation")); Directory.CreateDirectory(output);
        SessionState.SetString(Key + ".Output", output); SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/ReplayScene.unity");
        var controller = Object.FindFirstObjectByType<ReplayController>(); controller.absoluteSessionPath = fixture; controller.loadRecordedAgents = false;
        controller.loop = false; controller.showOverviewOnStart = true;
        var view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")); view.Show(); view.Focus();
        typeof(OperatorOverlayValidation).GetMethod("ConfigureGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { view });
        EditorApplication.isPlaying = true;
    }
    private static void Watchdog() { if (!finished && EditorApplication.timeSinceStartup > deadline) Finish("Timed out waiting for replay visuals."); }
    private static void Log(string text, string stack, LogType type)
    { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !stack.Contains("UnityEditor.Search.SearchDatabase")) errors.Add(text); }
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); checks.Add(text); }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Hidden).GetValue(target);
    private static object Call(object target, string name, params object[] values) => target.GetType().GetMethod(name, Hidden).Invoke(target, values);
    private static IEnumerator Guard(IEnumerator routine)
    { while (true) { object next; try { if (!routine.MoveNext()) yield break; next = routine.Current; } catch (Exception e) { Finish(e.ToString()); yield break; } yield return next; } }
    private static IEnumerator Settled(double seconds)
    {
        replay.SetPlaying(false); replay.SeekSeconds(seconds);
        for (int i = 0; i < 8; i++) yield return null;
        while (replay.environmentLoader.Loading) yield return null;
        for (int i = 0; i < 4; i++) yield return null;
    }
    private static IEnumerator Exercise()
    {
        while (!replay.Ready || replay.Loading) yield return null;
        var wait = Settled(3); while (wait.MoveNext()) yield return wait.Current;
        Check(replay.AnimalCount == 4, "Actual ReplayScene loads four generated recorded rigs");
        Check(Screen.width == 1920 && Screen.height == 1080, "Actual replay Game-view framebuffer is Full HD");
        var camera = Field<Camera>(replay, "overview");
        var annotations = Field<Dictionary<string, ReplayAgentVisuals>>(replay, "agentAnnotations");
        var markers = Field<Dictionary<string, GameObject>>(replay, "markerByRig");
        var fronts = Field<Dictionary<string, Camera>>(replay, "camerasByRig");
        Check(annotations.Values.Select(value => value.Tint).Distinct().Count() == 4, "Each rig has a distinct stable overview color");
        foreach (var pair in annotations)
        {
            var samples = replay.Session.Rigs[pair.Key]; var visual = pair.Value;
            Check(visual.AnnotationVisible && visual.CameraFrustum.positionCount == 11, pair.Key + " has a visible standard-line camera frustum");
            Check(Vector3.Distance(visual.CameraFrustum.GetPosition(0), fronts[pair.Key].transform.position) < .00001f, pair.Key + " cone starts at the actual front camera");
            Check(visual.HistoryPointCount > 10 && samples[visual.HistoryEndIndex].time <= replay.ReplayTime, pair.Key + " bright trail uses only recorded past positions");
            Check(markers[pair.Key].transform.localScale == Vector3.one, pair.Key + " pose root retains unit scale independently of marker size");
            Check((fronts[pair.Key].cullingMask & (1 << LayerMask.NameToLayer("OverheadCameraMarkers"))) == 0, pair.Key + " animal camera excludes all overview annotations");
        }
        var front = fronts["VR1"]; var marker = markers["VR1"];
        Vector3 offset = new Vector3(.2f, .4f, .6f); front.transform.localPosition = offset;
        Vector3 expected = marker.transform.position + marker.transform.rotation * offset;
        float originalSize = camera.orthographicSize; camera.orthographicSize *= 2; Call(replay, "RefreshReplayAnnotations");
        Check(Vector3.Distance(front.transform.position, expected) < .00001f, "Resizing the operator marker never scales a saved animal-camera offset");
        camera.orthographicSize = originalSize; front.transform.localPosition = Vector3.zero; Call(replay, "RefreshReplayAnnotations");
        yield return new WaitForEndOfFrame(); Capture("four-rig-overview.png");

        // Scene/trial resets and backward scrubs rebuild the recent tail instead of retaining renderer history.
        foreach (double seconds in new[] { 9.5, 2.0, 11.0, .5 })
        {
            wait = Settled(seconds); while (wait.MoveNext()) yield return wait.Current;
            foreach (var pair in annotations)
            {
                var samples = replay.Session.Rigs[pair.Key]; var visual = pair.Value;
                Check(samples[visual.HistoryStartIndex].ContextKey == samples[visual.HistoryEndIndex].ContextKey, "Seek " + seconds + " keeps " + pair.Key + " tail inside the active recorded trial");
                Check(samples[visual.HistoryEndIndex].time <= seconds + .000001, "Seek " + seconds + " discards " + pair.Key + " future trail samples");
                Check(samples[visual.HistoryStartIndex].time >= Math.Max(seconds < 6 ? 0 : 6, seconds - ReplayAgentVisuals.RecentHistorySeconds) - .011, "Seek " + seconds + " uses the bounded five-second recorded tail for " + pair.Key);
            }
        }
        wait = Settled(3); while (wait.MoveNext()) yield return wait.Current;

        camera.orthographic = true; camera.orthographicSize = .001f; camera.transform.SetPositionAndRotation(new Vector3(0, 0, -.01f), Quaternion.identity);
        camera.farClipPlane = 10000000;
        ReplayCameraGeometry.UpdateClipping(camera, new Bounds(Vector3.zero, Vector3.one * .001f), null);
        Check(camera.nearClipPlane < .001f, "Close insect-scale inspection uses a sub-millimeter near clip");
        Check(camera.farClipPlane < 20, "Small-context clipping discards the preceding large scene's far range");
        camera.orthographicSize = 30; camera.transform.SetPositionAndRotation(new Vector3(0, 40, 0), Quaternion.Euler(90, 0, 0));
        ReplayCameraGeometry.UpdateClipping(camera, new Bounds(Vector3.zero, Vector3.one * 10), new Bounds(Vector3.zero, new Vector3(200000, 1, 200000)));
        Check(camera.farClipPlane < 100, "Top-down clipping projects scene bounds along depth instead of treating horizontal terrain width as depth");
        camera.orthographic = false; camera.fieldOfView = 50; camera.transform.SetPositionAndRotation(new Vector3(0, 0, -1000), Quaternion.identity);
        ReplayCameraGeometry.UpdateClipping(camera, new Bounds(Vector3.zero, Vector3.one * 100), new Bounds(Vector3.zero, Vector3.one * 2000));
        Check(camera.nearClipPlane > 0 && camera.farClipPlane > 2000 && camera.farClipPlane > camera.nearClipPlane, "Perspective clipping includes the actual current environment with finite ordered planes");
        replay.FitOverview();
        var viewport = camera.pixelRect; var gui = new Rect(viewport.x, Screen.height - viewport.yMax, viewport.width, viewport.height);
        Vector3 rightOutside = camera.transform.position + camera.transform.forward * 100 + camera.transform.right * camera.orthographicSize * 20;
        Vector2 point = ReplayCameraGeometry.MarkerPosition(camera, rightOutside, gui, 24, out bool onScreen, out Vector2 direction);
        Check(!onScreen && gui.Contains(point) && direction.x > .9f, "Right-offscreen target gets an inside-viewport indicator pointing right");
        Vector3 behind = camera.transform.position - camera.transform.forward * 100 + camera.transform.right * 50;
        point = ReplayCameraGeometry.MarkerPosition(camera, behind, gui, 24, out onScreen, out direction);
        Check(!onScreen && gui.Contains(point), "Targets behind the view also receive bounded edge indicators");
        // Deliberate pan creates an actual four-rig off-screen screenshot without moving recorded poses.
        typeof(ReplayController).GetField("overviewPivot", Hidden).SetValue(replay, Field<Vector3>(replay, "overviewPivot") + Vector3.right * 100);
        typeof(ReplayController).GetField("overviewUserAdjusted", Hidden).SetValue(replay, true); Call(replay, "ApplyOverviewPose");
        for (int i = 0; i < 4; i++) yield return null;
        yield return new WaitForEndOfFrame(); Capture("offscreen-rig-indicators.png");
        replay.FitOverview();
        Check(ZmqListener.ActiveWorkerCount == 0, "Replay annotation validation starts no acquisition listeners");
        var before = Render(front, "animal-with-operator-annotations.png");
        foreach (var pair in annotations)
        {
            var samples = replay.Session.Rigs[pair.Key]; int cursor = ReplayController.AtOrBefore(samples, replay.ReplayTime);
            pair.Value.Refresh(camera, fronts[pair.Key], samples, cursor, cursor, replay.ReplayTime, markers[pair.Key].transform.position,
                markers[pair.Key].transform.rotation, false, true);
        }
        var after = Render(front, "animal-with-annotations-hidden.png");
        Check(before.SequenceEqual(after), "Showing overview markers, trails and cones changes zero pixels in the saved animal view");
        Call(replay, "RefreshReplayAnnotations");

        // Render an actual near object, not just a scalar assertion about clip-plane values.
        var inspection = new GameObject("Close clip render proof", typeof(Camera)).GetComponent<Camera>(); inspection.enabled = false;
        inspection.orthographic = true; inspection.orthographicSize = .002f; inspection.clearFlags = CameraClearFlags.SolidColor;
        inspection.backgroundColor = Color.black; inspection.cullingMask = 1 << 30;
        inspection.transform.SetPositionAndRotation(new Vector3(100, 100, 100), Quaternion.identity);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.layer = 30; quad.transform.position = inspection.transform.position + Vector3.forward * .002f;
        quad.transform.localScale = Vector3.one * .002f;
        var material = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default")) { color = Color.red };
        RuntimeMaterialOwner.Assign(quad.GetComponent<Renderer>(), material); Object.Destroy(material);
        inspection.nearClipPlane = .01f; inspection.farClipPlane = 10;
        var clipped = Render(inspection, "near-object-old-clip.png");
        ReplayCameraGeometry.UpdateClipping(inspection, new Bounds(quad.transform.position, Vector3.one * .002f), null);
        var resolved = Render(inspection, "near-object-adaptive-clip.png");
        int center = (64 * 128 + 64) * 3;
        Check(clipped[center] < 4 && resolved[center] > 180, "An actual close object clipped by the old .01 near plane is rendered by adaptive clipping");
        Object.Destroy(quad); Object.Destroy(inspection.gameObject);
        Check(errors.Count == 0, "No replay camera or renderer runtime errors");
        Finish(null);
    }
    private static byte[] Render(Camera camera, string file)
    {
        var previous = camera.targetTexture; var rect = camera.rect; var active = RenderTexture.active;
        var target = RenderTexture.GetTemporary(128, 128, 24); camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(128, 128, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
        var bytes = image.GetRawTextureData<byte>().ToArray(); File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
        camera.targetTexture = previous; camera.rect = rect; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); return bytes;
    }
    private static void Capture(string name)
    { var image = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); Object.Destroy(image); }
    private static void Finish(string failure)
    {
        if (finished) return; finished = true; SessionState.SetBool(Key, false); Application.logMessageReceived -= Log; EditorApplication.update -= Watchdog;
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "results.json"), JsonConvert.SerializeObject(new { checks = checks.Count, failure, errors, passed = checks,
            scope = "Real ReplayScene, standard Unity cameras and LineRenderer annotation geometry, actual forward/backward seeks and Full HD Game-view screenshots, with explicitly generated four-rig coordinates. No physical tracking/video acquisition claim." }, Formatting.Indented));
        EditorApplication.Exit(failure == null && errors.Count == 0 ? 0 : 1);
    }
}
#endif
