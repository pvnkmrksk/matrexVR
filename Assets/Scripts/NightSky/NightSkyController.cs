using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>Owns a scene's sky stimulus; archives each image before making it visible.</summary>
[DefaultExecutionOrder(-200)]
public sealed class NightSkyController : MonoBehaviour
{
    public const string RendererVersion = "nasa-j2000-panorama-v2";
    private static NightSkyController owner;
    public static string CurrentId => IsCurrent ? owner.currentId : "";
    public static string CurrentSampleUtc => IsCurrent ? owner.sampleUtc : "";
    private static bool IsCurrent => owner != null && owner.isActiveAndEnabled &&
        owner.material != null && RenderSettings.skybox == owner.material;

    private NightSkyConfig config;
    private Material material, bakeMaterial, previousMaterial;
    private Texture2D texture;
    private DateTimeOffset startUtc, lastSample;
    private double startRealtime;
    private string currentId = "", sampleUtc = "", archiveDirectory;
    private bool advancing;
    private readonly Dictionary<Camera, CameraClearFlags> cameraFlags = new Dictionary<Camera, CameraClearFlags>();
    private readonly Dictionary<Skybox, bool> cameraSkyboxes = new Dictionary<Skybox, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { owner = null; }

    public static void Apply(GameObject host, NightSkyConfig settings, string skyboxPath, ColorConfig uniformSkyColor = null)
    {
        NightSkyController controller = host.GetComponent<NightSkyController>();
        bool requested = uniformSkyColor != null || !string.IsNullOrWhiteSpace(skyboxPath) || (settings != null && settings.enabled);
        if (!requested && owner != null && owner.gameObject.scene == host.scene) owner.Release();
        if (controller == null && !requested) return;
        if (controller == null) controller = host.AddComponent<NightSkyController>();
        if (requested) controller.enabled = true;
        controller.Configure(settings, skyboxPath, uniformSkyColor);
    }

    public void Configure(NightSkyConfig settings, string skyboxPath, ColorConfig uniformSkyColor = null)
    {
        Release();
        bool fromFile = !string.IsNullOrWhiteSpace(skyboxPath);
        if (uniformSkyColor == null && !fromFile && (settings == null || !settings.enabled)) return;
        if (owner != null && owner != this) owner.Release();
        owner = this;
        previousMaterial = RenderSettings.skybox;
        try
        {
            Shader shader = Resources.Load<Shader>("NightSky/Panorama");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("NightSky/Panorama shader is unavailable on this device.");
            material = new Material(shader) { name = "Recorded night sky" };
            config = settings == null ? new NightSkyConfig() : JsonConvert.DeserializeObject<NightSkyConfig>(JsonConvert.SerializeObject(settings));
            DateTimeOffset now = DateTimeOffset.UtcNow;
            startRealtime = Time.realtimeSinceStartupAsDouble;
            archiveDirectory = ResolveArchiveDirectory();
            if (uniformSkyColor != null)
            {
                Color color = new Color(uniformSkyColor.r, uniformSkyColor.g, uniformSkyColor.b, 1);
                foreach (float channel in new[] { color.r, color.g, color.b })
                    if (float.IsNaN(channel) || float.IsInfinity(channel) || channel < 0 || channel > 1)
                        throw new ArgumentException("uniformSkyColor RGB channels must be finite and in [0,1].");
                var solid = new Texture2D(2, 1, TextureFormat.RGBA32, false, false);
                solid.SetPixels(new[] { color, color }); solid.Apply();
                try { Commit(solid, null, new { mode = "uniform", color = uniformSkyColor, renderer = RendererVersion }); }
                catch { Destroy(solid); throw; }
            }
            else if (fromFile)
            {
                string path = Path.IsPathRooted(skyboxPath) ? skyboxPath : Path.Combine(Application.streamingAssetsPath, skyboxPath);
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                try
                {
                    if (!loaded.LoadImage(bytes) || loaded.width != 2 * loaded.height)
                        throw new ArgumentException("skyboxPath must be a 2:1 equirectangular PNG or JPEG image.");
                    Commit(loaded, null, new { mode = "file", sourcePath = skyboxPath, sourceSha256 = Hash(bytes), renderer = RendererVersion });
                }
                catch { Destroy(loaded); throw; }
            }
            else
            {
                startUtc = config.ResolveStartUtc(now);
                Bake(SampleAtElapsed(0));
                advancing = config.advanceWithRealTime;
            }
            EnableRigSkyboxes();
        }
        catch
        {
            // Never keep an earlier trial's image/ID when a new request fails.
            Release();
            throw;
        }
    }

    private void Update()
    {
        if (!advancing || owner != this) return;
        DateTimeOffset sample = SampleAtElapsed(Time.realtimeSinceStartupAsDouble - startRealtime);
        if (sample == lastSample) return;
        try { Bake(sample); }
        catch (Exception error)
        {
            Release();
            Debug.LogError("Night sky update failed; sky and ID cleared: " + error);
        }
    }

    private DateTimeOffset SampleAtElapsed(double elapsed)
    {
        if (config.roundToInterval) return config.SampleUtc(startUtc.AddSeconds(elapsed));
        double intervalSeconds = config.updateIntervalMinutes * 60.0;
        return startUtc.AddSeconds(Math.Floor(Math.Max(0, elapsed) / intervalSeconds) * intervalSeconds);
    }

    private void Bake(DateTimeOffset sample)
    {
        Texture2D source = Resources.Load<Texture2D>("NightSky/starmap_2020_4k");
        TextAsset provenance = Resources.Load<TextAsset>("NightSky/source");
        if (source == null || provenance == null) throw new FileNotFoundException("Bundled NASA night-sky map or provenance is missing.");
        if (config.imageWidth > SystemInfo.maxTextureSize) throw new InvalidOperationException("Night sky imageWidth exceeds this device's texture limit.");
        Matrix4x4 matrix = NightSkyOrientation.WorldToEquatorial(config, sample);
        if (bakeMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("NightSky/Bake");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("NightSky/Bake shader is unavailable on this device.");
            bakeMaterial = new Material(shader);
        }
        bakeMaterial.SetMatrix("_WorldToEquatorial", matrix);
        bakeMaterial.SetFloat("_Exposure", config.exposure);
        bakeMaterial.SetFloat("_FaintDetailCutoff", config.faintDetailCutoff);
        bakeMaterial.SetFloat("_MaskBelowHorizon", config.maskBelowHorizon ? 1 : 0);
        RenderTexture target = RenderTexture.GetTemporary(config.imageWidth, config.imageWidth / 2, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;
        bool previousSrgb = GL.sRGBWrite;
        Texture2D baked = null;
        try
        {
            GL.sRGBWrite = false;
            Graphics.Blit(source, target, bakeMaterial, 0);
            RenderTexture.active = target;
            baked = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, false);
            baked.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            baked.Apply(false, false);
            float[] transform = new float[9];
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++) transform[row * 3 + col] = matrix[row, col];
            Commit(baked, sample, new {
                mode = "astronomical", renderer = RendererVersion,
                source = JsonConvert.DeserializeObject(provenance.text),
                sampleUtc = Iso(sample), settings = config, worldToJ2000RowMajor = transform,
                axes = "+X east, +Y zenith, +Z north before northYawDegrees",
                projection = "Unity Skybox/Panoramic 360 equirectangular; north at u=0.25; zenith at v=1",
                colour = "sRGB 8-bit, bilinear, no mipmaps; faint-detail smoothstep on max linear source channel, then exposure and clipping to [0,1]"
            });
            baked = null; // Commit owns the displayed texture.
        }
        finally
        {
            RenderTexture.active = previous;
            GL.sRGBWrite = previousSrgb;
            RenderTexture.ReleaseTemporary(target);
            if (baked != null) Destroy(baked);
        }
    }

    private void Commit(Texture2D next, DateTimeOffset? sample, object description)
    {
        next.wrapModeU = TextureWrapMode.Repeat;
        next.wrapModeV = TextureWrapMode.Clamp;
        next.filterMode = FilterMode.Bilinear;
        byte[] png = next.EncodeToPNG();
        string imageHash = Hash(png);
        string id = Hash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(description) + "\n" + imageHash));
        Directory.CreateDirectory(archiveDirectory);
        // Save the exact texture shown, not just a path to an external mutable source.
        File.WriteAllBytes(Path.Combine(archiveDirectory, id + ".png"), png);
        File.WriteAllText(Path.Combine(archiveDirectory, id + ".json"), JsonConvert.SerializeObject(new {
            skyboxId = id, imageFile = id + ".png", imageSha256 = imageHash,
            width = next.width, height = next.height, description,
            unityVersion = Application.unityVersion, colourSpace = QualitySettings.activeColorSpace.ToString()
        }, Formatting.Indented));
        DateTimeOffset applied = DateTimeOffset.UtcNow;
        string appliedLocal = (sample.HasValue ? config.ObserverLocalTime(applied) : applied.ToLocalTime()).ToString("o", CultureInfo.InvariantCulture);
        string sampledLocal = sample.HasValue ? config.ObserverLocalTime(sample.Value).ToString("o", CultureInfo.InvariantCulture) : "";
        string previousId = currentId;
        File.AppendAllText(Path.Combine(archiveDirectory, "usage.jsonl"), JsonConvert.SerializeObject(new {
            appliedUtc = Iso(applied), appliedLocal, previousSkyboxId = previousId, skyboxId = id, imageFile = id + ".png",
            sampleUtc = sample.HasValue ? Iso(sample.Value) : null, sampleLocal = sampledLocal,
            resolvedStartUtc = sample.HasValue ? Iso(startUtc) : null,
            scene = gameObject.scene.name, frame = Time.frameCount
        }) + "\n");
        string csvPath = Path.Combine(archiveDirectory, "loads.csv");
        string csv = File.Exists(csvPath) ? "" : "appliedUtc,appliedLocal,sampleUtc,sampleLocal,previousSkyboxId,skyboxId,imageFile,frame\n";
        csv += string.Join(",", Iso(applied), appliedLocal, sample.HasValue ? Iso(sample.Value) : "", sampledLocal,
            previousId, id, id + ".png", Time.frameCount.ToString(CultureInfo.InvariantCulture)) + "\n";
        File.AppendAllText(csvPath, csv);
        Texture2D old = texture;
        texture = next;
        material.mainTexture = next;
        RenderSettings.skybox = material;
        currentId = id;
        sampleUtc = sample.HasValue ? Iso(sample.Value) : "";
        if (sample.HasValue) lastSample = sample.Value;
        if (old != null) Destroy(old);
        Debug.Log("Night sky loaded " + appliedLocal + ": " + id + ".png in " + archiveDirectory +
            (sample.HasValue ? "; sky time " + sampledLocal + " (" + sampleUtc + ")" : " (explicit image)"));
    }

    private string ResolveArchiveDirectory()
    {
        MasterDataLogger logger = MasterDataLogger.Instance;
        if (logger != null && !string.IsNullOrEmpty(logger.directoryPath)) return Path.Combine(logger.directoryPath, "Skyboxes");
        // Direct scene play without the experiment runner still leaves an audit trail.
        string path = Path.Combine(Application.persistentDataPath, "NightSkyRuns", Guid.NewGuid().ToString("N"), "Skyboxes");
        Debug.LogWarning("No MasterDataLogger: night-sky audit saved to " + path);
        return path;
    }

    private void EnableRigSkyboxes()
    {
        // Include inactive face cameras without enabling them or changing panel layout.
        foreach (ViewportSetter rig in FindObjectsByType<ViewportSetter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rig.gameObject.scene != gameObject.scene) continue;
            foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
            {
                cameraFlags[camera] = camera.clearFlags;
                camera.clearFlags = CameraClearFlags.Skybox;
                Skybox local = camera.GetComponent<Skybox>();
                if (local != null) { cameraSkyboxes[local] = local.enabled; local.enabled = false; }
            }
        }
    }

    private void OnDisable() { Release(); }

    private void Release()
    {
        advancing = false;
        if (material != null && RenderSettings.skybox == material) RenderSettings.skybox = previousMaterial;
        foreach (var entry in cameraFlags) if (entry.Key != null) entry.Key.clearFlags = entry.Value;
        foreach (var entry in cameraSkyboxes) if (entry.Key != null) entry.Key.enabled = entry.Value;
        cameraFlags.Clear(); cameraSkyboxes.Clear();
        if (material != null) Destroy(material);
        if (bakeMaterial != null) Destroy(bakeMaterial);
        if (texture != null) Destroy(texture);
        material = null; bakeMaterial = null; texture = null;
        currentId = ""; sampleUtc = "";
        if (owner == this) owner = null;
    }

    public static string Hash(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    private static string Iso(DateTimeOffset time) => time.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
}
