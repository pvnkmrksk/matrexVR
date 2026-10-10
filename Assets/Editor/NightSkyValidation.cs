#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using CosineKitty;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>GPU, clock, astronomy, archive and lifecycle regression on a disposable copy.</summary>
[InitializeOnLoad]
public static class NightSkyValidation
{
    private const string Key = "NightSkyValidation.Active";
    private const string PreviewKey = "NightSkyValidation.ControlPreview";
    private static int checks;
    private static int previewPhase;
    private static double previewDeadline;
    private static string previewArchive;
    private static double motionStarted;
    private static readonly Dictionary<LocustMover, Vector3> motionStarts = new Dictionary<LocustMover, Vector3>();
    static NightSkyValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                EditorApplication.update += Validate;
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PreviewKey, false))
            {
                previewDeadline = EditorApplication.timeSinceStartup + 90;
                EditorApplication.update += ValidateControlPreview;
            }
        };
        SceneManager.sceneLoaded += (scene, mode) => {
            if (!SessionState.GetBool(PreviewKey, false)) return;
            foreach (ZmqListener listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None))
            {
                listener.enabled = false;
                typeof(ZmqListener).GetMethod("ApplySystemConfig", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(listener, null);
            }
        };
    }

    public static void RunControlPreview()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new InvalidOperationException("Use a disposable project copy with KANNADI_VALIDATION_COPY.");
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        SessionState.SetBool(PreviewKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void ValidateControlPreview()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > previewDeadline) throw new Exception("Control-scene preview timed out.");
            if (previewPhase == 0)
            {
                if (SceneManager.GetActiveScene().name != "Swarm" || NightSkyController.CurrentId == "") return;
                Check(MainController.Instance != null, "ControlScene automatically started the preview sequence.");
                var spawners = Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None);
                var agents = spawners.SelectMany(s => s.Spawned).ToArray();
                Check(agents.Length == spawners.Length * 256 && agents.Length > 0, "Preview spawns 256 Bogong markers per rig.");
                Check(spawners.All(s => s.bogongVisual.flickerFrequencyHz == 0 &&
                    s.GetComponentInChildren<BogongSwarmRenderer>().GetComponent<Renderer>().enabled), "Flicker is disabled and batched visuals are visible.");
                foreach (var agent in agents)
                {
                    var mover = agent.GetComponent<LocustMover>();
                    Check(mover != null && mover.speed == 2 && mover.boundaryManager != null, "Bogong has working speed and boundary control.");
                    Vector3 relative = agent.transform.position - mover.boundaryManager.transform.position;
                    Check(Math.Abs(relative.x)<=60.1 && Math.Abs(relative.y)<=30.1 && Math.Abs(relative.z)<=60.1, "Agents stay in the 120 x 60 x 120 volume.");
                    Check(Vector3.Angle(agent.transform.forward, Quaternion.Euler(-5,60,0)*Vector3.forward)<.05, "Swarm is aligned at heading 60 degrees, 5 degrees upward.");
                    if (Math.Abs(relative.x)<50 && Math.Abs(relative.y)<20 && Math.Abs(relative.z)<50)
                        motionStarts[mover]=agent.transform.position;
                }
                Check(motionStarts.Count>0, "Interior agents available for actual-frame movement check.");
                previewArchive = Path.Combine(MasterDataLogger.Instance.directoryPath, "Skyboxes");
                string first = NightSkyController.CurrentId;
                var sky = Object.FindFirstObjectByType<NightSkyController>();
                DateTimeOffset sample = Utc(NightSkyController.CurrentSampleUtc);
                Check(Math.Abs((DateTimeOffset.UtcNow-sample).TotalSeconds)<30, "First sky uses the actual current instant.");
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(previewArchive, first+".json")));
                Check(Math.Abs((double)manifest["description"]["settings"]["latitude"]+35.996113)<.000001 &&
                    Math.Abs((double)manifest["description"]["settings"]["longitude"]-148.773895)<.000001, "Runtime uses Adaminaby coordinates.");
                Check((double)manifest["description"]["settings"]["utcOffsetHours"]==11, "Adaminaby fixed UTC+11 offset is archived.");
                Check((float)manifest["description"]["settings"]["faintDetailCutoff"]==.15f, "Faint-detail cutoff is archived.");
                Check((int)manifest["width"]==4096 && (int)manifest["height"]==2048, "RunData contains a full 4K equirectangular panorama.");
                string[] before=File.ReadAllLines(Path.Combine(previewArchive,"loads.csv"));
                Check(before.Length==2 && before[1].Contains(first+".png"), "First load is recorded with image filename.");
                var event1=JsonConvert.DeserializeObject<JObject>(File.ReadAllLines(Path.Combine(previewArchive,"usage.jsonl"))[0],
                    new JsonSerializerSettings { DateParseHandling=DateParseHandling.None });
                Check(Utc((string)event1["sampleLocal"])==sample && Utc((string)event1["appliedLocal"])==Utc((string)event1["appliedUtc"]), "Local times retain offsets and resolve to the recorded UTC instants.");
                DateTimeOffset initial=(DateTimeOffset)typeof(NightSkyController).GetField("startUtc",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sky);
                Check(sample==initial, "Exact-start mode does not round to a half-hour.");
                File.Copy(Path.Combine(previewArchive, first+".png"), Path.Combine(Application.dataPath,"../adaminaby-current-preview.png"), true);
                typeof(NightSkyController).GetField("startRealtime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(sky,Time.realtimeSinceStartupAsDouble-1801);
                typeof(NightSkyController).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sky,null);
                Check(NightSkyController.CurrentId!=first && Utc(NightSkyController.CurrentSampleUtc)==sample.AddMinutes(30), "The next image represents exactly 30 minutes after the initial sample.");
                string[] after=File.ReadAllLines(Path.Combine(previewArchive,"loads.csv"));
                Check(after.Length==3 && after[2].Contains(first+","+NightSkyController.CurrentId), "Second load records the previous and new file IDs.");
                // Record after synchronous baking, then let real Update frames run.
                foreach (var mover in new List<LocustMover>(motionStarts.Keys)) motionStarts[mover]=mover.transform.position;
                motionStarted=Time.timeAsDouble;
                previewPhase=1; previewDeadline=EditorApplication.timeSinceStartup+30;
            }
            else if (previewPhase == 1)
            {
                double elapsed=Time.timeAsDouble-motionStarted;
                if (elapsed<.5) return;
                foreach (var entry in motionStarts)
                {
                    Vector3 delta=entry.Key.transform.position-entry.Value;
                    Check(Vector3.Dot(delta,entry.Key.transform.forward)>.1f &&
                        Vector3.Cross(delta,entry.Key.transform.forward).magnitude<.02f, "Bogong travels forward over actual frames without sideways jitter.");
                    Check(entry.Key.boundaryManager.GetComponent<LocustSpawner>().GetComponentInChildren<BogongSwarmRenderer>().GetComponent<Renderer>().enabled,
                        "Bogong batch remains visible over time without flicker.");
                }
                SaveSwarmPreview();
                MainController.Instance.HandleEscape();
                previewPhase=2; previewDeadline=EditorApplication.timeSinceStartup+15;
            }
            else if (SceneManager.GetActiveScene().name == "ControlScene")
            {
                Check(NightSkyController.CurrentId=="", "Escape returns to Control and clears sky.");
                // Wait a few frames to prove the preview does not auto-restart after Escape.
                if (previewPhase++<8) return;
                FinishControlPreview(null);
            }
        }
        catch (Exception error) { FinishControlPreview(error.ToString()); }
    }

    private static void FinishControlPreview(string failure)
    {
        EditorApplication.update-=ValidateControlPreview;
        SessionState.SetBool(PreviewKey,false);
        File.WriteAllText(Path.Combine(Application.dataPath,"../adaminaby-preview-validation.json"),
            JsonConvert.SerializeObject(new { checks, failure, archive=previewArchive, unity=Application.unityVersion },Formatting.Indented));
        if (failure!=null) Debug.LogError(failure);
        EditorApplication.Exit(failure==null ? 0 : 1);
    }

    private static void SaveSwarmPreview()
    {
        var rig = Object.FindFirstObjectByType<LocustSpawner>();
        Camera camera = null;
        foreach (var candidate in rig.GetComponentsInChildren<Camera>())
            if (candidate.enabled && candidate.name.EndsWith(" F")) { camera=candidate; break; }
        Check(camera!=null, "A forward panel camera is active for the swarm preview.");
        var panels=rig.GetComponent<ViewportSetter>();
        int width=panels.ledPanelWidth, height=panels.ledPanelHeight;
        RenderTexture target=RenderTexture.GetTemporary(width,height,24);
        RenderTexture previous=RenderTexture.active, previousTarget=camera.targetTexture;
        Rect rect=camera.rect; float aspect=camera.aspect;
        var capture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target; camera.rect=new Rect(0,0,1,1); camera.aspect=(float)width/height;
            camera.Render(); RenderTexture.active=target;
            capture.ReadPixels(new Rect(0,0,width,height),0,0); capture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath,"../adaminaby-swarm-preview.png"),capture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previousTarget; camera.rect=rect; camera.aspect=aspect;
            RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); Object.Destroy(capture);
        }
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new InvalidOperationException("Use a disposable project copy with KANNADI_VALIDATION_COPY.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        Debug.Log("NightSkyValidation: entering Play Mode");
        EditorApplication.isPlaying = true;
    }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; } catch (FormatException) { rejected = true; }
        Check(rejected, message);
    }
    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
    private static void Validate()
    {
        EditorApplication.update -= Validate;
        Debug.Log("NightSkyValidation: running checks");
        string failure = null;
        string archive = null;
        try
        {
            ValidateClock();
            ValidateOrientation();
            ValidateBakeProjection();
            ValidateFaintDetailCutoff();
            archive = ValidateRuntime();
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(Application.dataPath, "../night-sky-validation.json"),
            JsonConvert.SerializeObject(new { checks, failure, archive, unity = Application.unityVersion }, Formatting.Indented));
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    private static void ValidateClock()
    {
        DateTimeOffset now = Utc("2026-07-01T23:45:42Z");
        var c = new NightSkyConfig();
        Check(c.ResolveStartUtc(now) == now, "Default clock must use actual UTC, not machine-local reinterpretation.");
        Check(!c.roundToInterval, "Explicit clock times default to exact start, without rounding.");
        foreach (string clock in new[] { null, "", "  ", "now", " NOW " })
        {
            var current = JsonConvert.DeserializeObject<NightSkyConfig>(JsonConvert.SerializeObject(new { utcOffsetHours=11, localTime=clock }));
            Check(current.ResolveStartUtc(now)==now, "Missing/blank/now means the current instant.");
        }
        var fixedOffset = new NightSkyConfig { utcOffsetHours=11, localTime="22:17:13.125" };
        Check(fixedOffset.ResolveStartUtc(now)==Utc("2026-07-02T11:17:13.125Z"), "Explicit local time uses offset-local today and preserves seconds exactly.");
        fixedOffset.date="2026-10-05";
        Check(fixedOffset.ResolveStartUtc(now)==Utc("2026-10-05T11:17:13.125Z"), "Explicit date and local time resolve using UTC offset hours.");
        fixedOffset.utcOffsetHours=5.5;
        Check(fixedOffset.ObserverLocalTime(now).Offset==TimeSpan.FromMinutes(330), "Fractional-hour offsets are supported.");
        fixedOffset.utcOffsetHours=-3.5;
        Check(fixedOffset.ObserverLocalTime(now).Offset==TimeSpan.FromMinutes(-210), "Negative fractional-hour offsets are supported.");
        fixedOffset.utcOffsetHours=0;
        Check(fixedOffset.ResolveStartUtc(now)==Utc("2026-10-05T22:17:13.125Z"), "Zero offset means UTC.");
        fixedOffset.localTime="now";
        Check(fixedOffset.ResolveStartUtc(now)==Utc("2026-10-05T23:45:42Z"), "Date plus now retains the current local clock.");
        fixedOffset.utcOffsetHours=15; Reject(fixedOffset.Validate,"Reject out-of-range offset.");
        fixedOffset.utcOffsetHours=double.NaN; Reject(fixedOffset.Validate,"Reject non-finite offset.");
        fixedOffset.utcOffsetHours=1.001; Reject(fixedOffset.Validate,"Reject sub-minute offset.");
        fixedOffset.utcOffsetHours=11; fixedOffset.utcOffsetMinutes=660; Reject(fixedOffset.Validate,"Reject conflicting offset fields.");
        c.localTime = "01:30";
        Check(c.ResolveStartUtc(now) == Utc("2026-07-01T23:30Z"), "Missing date uses today's Berlin date, including midnight rollover.");
        c.date = "2026-01-01";
        Check(c.ResolveStartUtc(now) == Utc("2026-01-01T00:30Z"), "Berlin winter uses CET.");
        c.date = "2026-07-01";
        Check(c.ResolveStartUtc(now) == Utc("2026-06-30T23:30Z"), "Berlin summer uses CEST.");
        c.localTime = null;
        Check(c.ResolveStartUtc(now) == Utc("2026-06-30T23:45:42Z"), "Date-only override retains local clock.");
        c.date = "2026-03-29"; c.localTime = "02:30";
        Reject(() => c.ResolveStartUtc(now), "Reject nonexistent spring DST time.");
        c.date = "2026-10-25";
        Reject(() => c.ResolveStartUtc(now), "Reject ambiguous autumn DST time.");
        c.utcOffsetMinutes = 120;
        Check(c.ResolveStartUtc(now) == Utc("2026-10-25T00:30Z"), "Explicit offset resolves DST fold.");
        c.utcOffsetMinutes = 330; c.date = "2026-07-01"; c.localTime = "05:30";
        Check(c.ResolveStartUtc(now) == Utc("2026-07-01T00:00Z"), "Fractional-hour time zones.");
        c.localTime = "25:30";
        Reject(() => c.ResolveStartUtc(now), "Reject invalid local time.");
        c = new NightSkyConfig();
        var sydney = new NightSkyConfig { timeZoneId = "Australia/Sydney" };
        Check(sydney.ObserverLocalTime(Utc("2026-01-01T00:00Z")).Offset==TimeSpan.FromHours(11), "Sydney summer uses AEDT.");
        Check(sydney.ObserverLocalTime(Utc("2026-07-01T00:00Z")).Offset==TimeSpan.FromHours(10), "Sydney winter uses AEST.");
        Check(c.SampleUtc(Utc("2026-01-01T23:44:59Z")) == Utc("2026-01-01T23:30Z"), "Nearest half-hour before boundary.");
        Check(c.SampleUtc(Utc("2026-01-01T23:45:00Z")) == Utc("2026-01-02T00:00Z"), "Nearest half-hour across midnight, ties forward.");
        c.updateIntervalMinutes = 15;
        Check(c.SampleUtc(Utc("2026-01-01T00:07:30Z")) == Utc("2026-01-01T00:15Z"), "15-minute sampling.");
        c.latitude = double.NaN; Reject(c.Validate, "Reject NaN latitude.");
        c.latitude = 91; Reject(c.Validate, "Reject invalid latitude.");
        c.latitude = 0; c.updateIntervalMinutes = 0; Reject(c.Validate, "Reject zero update interval.");
        c.updateIntervalMinutes = 30; c.imageWidth = 500; Reject(c.Validate, "Reject unsupported output dimensions.");
    }
    private static void ValidateOrientation()
    {
        // Compare matrix adapter against the separate RA/Dec -> Horizon API.
        double[,] stars = { { 6.75248, -16.7161 }, { 2.5303, 89.2641 }, { 14.6601, -60.8339 }, { 18.6156, 38.7837 } };
        foreach (double latitude in new[] { 47.6896, 0, -33.8688 })
        foreach (double longitude in new[] { 9.1881, 151.2093, -122.4194 })
        foreach (string stamp in new[] { "2000-01-01T12:00Z", "2026-01-15T00:00Z", "2026-07-15T21:00Z" })
        {
            var c = new NightSkyConfig { latitude = latitude, longitude = longitude };
            var date = Utc(stamp); var time = new AstroTime(date.UtcDateTime);
            Matrix4x4 toWorld = NightSkyOrientation.WorldToEquatorial(c, date).inverse;
            for (int i = 0; i < stars.GetLength(0); i++)
            {
                AstroVector eq = Astronomy.VectorFromSphere(new Spherical(stars[i,1], 15 * stars[i,0], 1), time);
                Equatorial ofDate = Astronomy.EquatorFromVector(Astronomy.RotateVector(Astronomy.Rotation_EQJ_EQD(time), eq));
                Topocentric hor = Astronomy.Horizon(time, new Observer(latitude, longitude, 0), ofDate.ra, ofDate.dec, Refraction.None);
                Vector3 world = toWorld.MultiplyVector(new Vector3((float)eq.x,(float)eq.y,(float)eq.z)).normalized;
                double alt = Math.Asin(world.y) * 180 / Math.PI;
                double az = Math.Atan2(world.x, world.z) * 180 / Math.PI;
                Check(Math.Abs(alt - hor.altitude) < .001, "Altitude agrees with library horizon API.");
                Check(Math.Abs(Mathf.DeltaAngle((float)az, (float)hor.azimuth)) < .001, "Azimuth has correct east/west handedness.");
            }
        }
        var config = new NightSkyConfig { latitude = 0, longitude = 0 };
        Matrix4x4 j2000 = NightSkyOrientation.WorldToEquatorial(config, Utc("2000-01-01T12:00Z"));
        Vector3 zenith = j2000.MultiplyVector(Vector3.up);
        double ra = (Math.Atan2(zenith.y, zenith.x) * 180 / Math.PI + 360) % 360;
        Check(Math.Abs(ra - 280.46061837) < .02, "Independent Greenwich J2000 sidereal-angle reference.");
        config.northYawDegrees = 90;
        Matrix4x4 turned = NightSkyOrientation.WorldToEquatorial(config, Utc("2000-01-01T12:00Z"));
        Check(Vector3.Distance(turned.MultiplyVector(Vector3.right), j2000.MultiplyVector(Vector3.forward)) < .00001, "North yaw rotates north toward +X.");
    }
    private static string ValidateRuntime()
    {
        var master = new GameObject("Validation master").AddComponent<MasterDataLogger>();
        string archive = Path.Combine(master.directoryPath, "Skyboxes");
        GameObject rig = new GameObject("VR1 night-sky test");
        rig.AddComponent<ViewportSetter>().enabled = false;
        Camera camera = new GameObject("Main Camera F").AddComponent<Camera>();
        camera.transform.SetParent(rig.transform);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.enabled = false;
        Skybox localSkybox = camera.gameObject.AddComponent<Skybox>();
        var controller = new GameObject("Night-sky test").AddComponent<NightSkyController>();
        var c = new NightSkyConfig { date = "2026-07-01", localTime = "23:00", imageWidth = 1024, roundToInterval = true };
        Material previous = RenderSettings.skybox;
        controller.Configure(c, null);
        string firstId = NightSkyController.CurrentId;
        Check(firstId.Length == 64, "Content ID is full SHA-256.");
        Check(camera.clearFlags == CameraClearFlags.Skybox && !camera.enabled && !localSkybox.enabled, "Sky applies to rig without enabling disabled panel cameras.");
        Check(NightSkyController.CurrentSampleUtc == "2026-07-01T21:00:00.0000000Z", "Logged sample is resolved UTC.");
        string path = Path.Combine(archive, firstId + ".png");
        JObject manifest = JObject.Parse(File.ReadAllText(Path.Combine(archive, firstId + ".json")));
        Check((string)manifest["imageSha256"] == NightSkyController.Hash(File.ReadAllBytes(path)), "Archived image checksum matches manifest.");
        Texture2D saved = new Texture2D(2,2); saved.LoadImage(File.ReadAllBytes(path));
        Check(saved.width == 1024 && saved.height == 512, "Saved panorama dimensions.");
        double bottom = 0, top = 0;
        foreach (Color p in saved.GetPixels(0,0,1024,256)) bottom += p.maxColorComponent;
        foreach (Color p in saved.GetPixels(0,256,1024,256)) top += p.maxColorComponent;
        Check(bottom == 0 && top > 1, "GPU output has black ground and nonempty sky, with correct vertical orientation.");
        Check(NightSkyController.Hash(((Texture2D)RenderSettings.skybox.mainTexture).EncodeToPNG()) == (string)manifest["imageSha256"], "Saved PNG is exactly the displayed texture.");
        controller.Configure(c, null);
        Check(NightSkyController.CurrentId == firstId, "Same sky has same reproducible ID.");
        typeof(NightSkyController).GetField("startRealtime", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller, Time.realtimeSinceStartupAsDouble - 1800);
        float scale = Time.timeScale; Time.timeScale = 0;
        typeof(NightSkyController).GetMethod("Update", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller, null);
        Time.timeScale = scale;
        Check(NightSkyController.CurrentId != firstId && NightSkyController.CurrentSampleUtc.Contains("21:30"), "Half-hour update follows real time even when Unity timeScale is zero.");
        c.advanceWithRealTime = false;
        controller.Configure(c, null);
        typeof(NightSkyController).GetField("startRealtime", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller, Time.realtimeSinceStartupAsDouble - 3600);
        typeof(NightSkyController).GetMethod("Update", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller, null);
        Check(NightSkyController.CurrentSampleUtc.Contains("21:00"), "Frozen sky does not advance.");
        controller.Configure(new NightSkyConfig { date = "invalid" }, path);
        Check(NightSkyController.CurrentId.Length == 64 && NightSkyController.CurrentSampleUtc == "", "Explicit image overrides astronomical config, including invalid calendar input.");
        ValidateCameraProjection(controller, camera, archive);
        try { controller.Configure(c, path + ".missing"); } catch (FileNotFoundException) { }
        Check(NightSkyController.CurrentId == "" && RenderSettings.skybox == previous, "Missing explicit image clears old sky and ID.");
        Check(camera.clearFlags == CameraClearFlags.SolidColor && localSkybox.enabled, "Original camera settings restored.");
        c.date = null; c.localTime = null;
        controller.Configure(c, null);
        Check(NightSkyController.CurrentSampleUtc == c.SampleUtc(DateTimeOffset.UtcNow).UtcDateTime.ToString("o"), "Restart with omitted time resolves today, not prior fixed date.");
        controller.Configure(null, null);
        Check(NightSkyController.CurrentId == "" && RenderSettings.skybox == previous, "No nightSky returns to original background.");
        c.date = "2026-07-01"; c.localTime = "23:00"; c.imageWidth = 4096;
        controller.Configure(c, null);
        File.Copy(Path.Combine(archive, NightSkyController.CurrentId + ".png"), Path.Combine(Application.dataPath, "../night-sky-preview.png"), true);
        Check(((Texture2D)RenderSettings.skybox.mainTexture).width == 4096, "Production resolution renders.");
        // Main dataset rows contain the same ID as the stimulus manifest.
        DataLogger logger = rig.AddComponent<DataLogger>(); logger.includeZmqData = false;
        typeof(DataLogger).GetMethod("Start", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(logger, null);
        typeof(DataLogger).GetMethod("PrepareLogData", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(logger, null);
        string row = (string)typeof(DataLogger).GetField("line", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(logger);
        Check(row.Contains(NightSkyController.CurrentId) && row.Contains(NightSkyController.CurrentSampleUtc), "Main dataset includes ID and sample UTC.");
        controller.enabled = false;
        Check(NightSkyController.CurrentId == "" && RenderSettings.skybox == previous, "Disable/unload restores sky and clears static state.");
        Check(File.ReadAllLines(Path.Combine(archive,"usage.jsonl")).Length >= 7, "Usage audit records each activation, including repeated IDs.");
        Object.Destroy(saved);
        return archive;
    }

    private static void ValidateFaintDetailCutoff()
    {
        var source = new Texture2D(2,2,TextureFormat.RGBAHalf,false,true);
        var material = new Material(Resources.Load<Shader>("NightSky/Bake"));
        material.SetMatrix("_WorldToEquatorial",Matrix4x4.identity);
        material.SetFloat("_Exposure",1);
        material.SetFloat("_MaskBelowHorizon",0);
        RenderTexture target = RenderTexture.GetTemporary(8,4,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var capture = new Texture2D(8,4,TextureFormat.RGBA32,false,false);
        RenderTexture previous=RenderTexture.active; bool srgb=GL.sRGBWrite;
        try
        {
            GL.sRGBWrite=false;
            foreach (float brightness in new[] { .005f,.02f,.03f,.05f,.5f })
            {
                var color = new Color(brightness,brightness,brightness,1);
                source.SetPixels(new[] { color,color,color,color }); source.Apply();
                material.SetFloat("_FaintDetailCutoff",0);
                Graphics.Blit(source,target,material);
                RenderTexture.active=target; capture.ReadPixels(new Rect(0,0,8,4),0,0); capture.Apply();
                float original=capture.GetPixel(4,2).r;
                Check(Math.Abs(original-color.gamma.r)<.01, "Zero cutoff preserves original source brightness.");
                material.SetFloat("_FaintDetailCutoff",.02f);
                Graphics.Blit(source,target,material);
                capture.ReadPixels(new Rect(0,0,8,4),0,0); capture.Apply();
                float filtered=capture.GetPixel(4,2).r;
                if (brightness<=.02f) Check(filtered<.005, "Cutoff removes faint source pixels.");
                else if (brightness<.04f) Check(filtered>0 && filtered<original, "Cutoff fades intermediate pixels smoothly.");
                else Check(Math.Abs(filtered-original)<.005, "Cutoff preserves bright pixels.");
            }
            var invalid = new NightSkyConfig { faintDetailCutoff=-.1f };
            Reject(invalid.Validate,"Reject negative cutoff.");
            invalid.faintDetailCutoff=float.NaN; Reject(invalid.Validate,"Reject nonfinite cutoff.");
        }
        finally
        {
            RenderTexture.active=previous; GL.sRGBWrite=srgb; RenderTexture.ReleaseTemporary(target);
            Object.Destroy(source); Object.Destroy(material); Object.Destroy(capture);
        }
    }

    private static void ValidateBakeProjection()
    {
        // Encode equatorial XYZ as linear RGB, then independently predict the baked pixels.
        Texture2D source = new Texture2D(256,128,TextureFormat.RGBAHalf,false,true);
        source.wrapModeU = TextureWrapMode.Repeat; source.wrapModeV = TextureWrapMode.Clamp;
        source.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[256*128];
        for (int y=0; y<128; y++)
        for (int x=0; x<256; x++)
        {
            double ra = (.5-(x+.5)/256)*2*Math.PI, dec = ((y+.5)/128-.5)*Math.PI;
            pixels[y*256+x] = new Color((float)(.5+.5*Math.Cos(dec)*Math.Cos(ra)),
                (float)(.5+.5*Math.Cos(dec)*Math.Sin(ra)), (float)(.5+.5*Math.Sin(dec)),1);
        }
        source.SetPixels(pixels); source.Apply();
        var config = new NightSkyConfig { latitude = -33.8688, longitude = 151.2093, northYawDegrees = 37 };
        Matrix4x4 matrix = NightSkyOrientation.WorldToEquatorial(config, Utc("2026-12-01T11:00Z"));
        var material = new Material(Resources.Load<Shader>("NightSky/Bake"));
        material.SetMatrix("_WorldToEquatorial",matrix); material.SetFloat("_Exposure",1);
        material.SetFloat("_MaskBelowHorizon",0);
        RenderTexture target = RenderTexture.GetTemporary(256,128,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var readback = new Texture2D(256,128,TextureFormat.RGBA32,false,false);
        RenderTexture previous = RenderTexture.active; bool srgb = GL.sRGBWrite;
        GL.sRGBWrite = false;
        Graphics.Blit(source,target,material);
        RenderTexture.active = target; readback.ReadPixels(new Rect(0,0,256,128),0,0); readback.Apply();
        foreach (int x in new[] { 0,32,64,128,192,255 })
        foreach (int y in new[] { 16,48,80,112 })
        {
            double lon=(.5-(x+.5)/256)*2*Math.PI, lat=((y+.5)/128-.5)*Math.PI;
            Vector3 eq=matrix.MultiplyVector(new Vector3((float)(Math.Cos(lat)*Math.Cos(lon)),
                (float)Math.Sin(lat),(float)(Math.Cos(lat)*Math.Sin(lon))));
            Color expected = new Color(.5f+.5f*eq.x,.5f+.5f*eq.y,.5f+.5f*eq.z).gamma;
            Color actual=readback.GetPixel(x,y);
            Check(Math.Abs(expected.r-actual.r)<.012 && Math.Abs(expected.g-actual.g)<.012 && Math.Abs(expected.b-actual.b)<.012,
                "Baked J2000 sampling, matrix layout, handedness and gamma at " + x + "," + y);
        }
        RenderTexture.active=previous; GL.sRGBWrite=srgb; RenderTexture.ReleaseTemporary(target);
        Object.Destroy(source); Object.Destroy(readback); Object.Destroy(material);
    }

    private static void ValidateCameraProjection(NightSkyController controller, Camera camera, string archive)
    {
        // Independent direction-encoded fixture detects flips/handedness in the rendered skybox.
        Texture2D axes = new Texture2D(256, 128, TextureFormat.RGBA32, false, false);
        Color[] pixels = new Color[256 * 128];
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 256; x++)
        {
            double lon = (.5 - (x + .5) / 256) * 2 * Math.PI;
            double lat = ((y + .5) / 128 - .5) * Math.PI;
            pixels[y * 256 + x] = new Color((float)(.5 + .5*Math.Cos(lat)*Math.Cos(lon)),
                (float)(.5 + .5*Math.Sin(lat)), (float)(.5 + .5*Math.Cos(lat)*Math.Sin(lon)), 1);
        }
        axes.SetPixels(pixels); axes.Apply();
        string fixture = Path.Combine(archive, "validation-axes.png");
        File.WriteAllBytes(fixture, axes.EncodeToPNG());
        controller.Configure(null, fixture);
        RenderTexture target = RenderTexture.GetTemporary(128, 128, 24);
        RenderTexture previous = RenderTexture.active;
        var capture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        camera.targetTexture = target; camera.aspect = 1; camera.fieldOfView = 60;
        foreach (Vector3 direction in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.up, Vector3.down })
        {
            camera.transform.rotation = Quaternion.LookRotation(direction, Math.Abs(direction.y) > .9 ? Vector3.forward : Vector3.up);
            camera.Render();
            RenderTexture.active = target;
            capture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); capture.Apply();
            Color value = capture.GetPixel(64,64);
            Vector3 decoded = new Vector3(value.r*2-1, value.g*2-1, value.b*2-1);
            Check(Vector3.Distance(decoded, direction) < .035, "Camera skybox projection agrees for direction " + direction + ": " + decoded);
        }
        camera.targetTexture = null; RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        Object.Destroy(capture); Object.Destroy(axes);
    }
}
#endif
