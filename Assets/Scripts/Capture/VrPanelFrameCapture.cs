using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders ordered face cameras into a horizontal LED-strip RenderTexture and
/// writes PNGs via AsyncGPUReadback (GPU async) + background file I/O.
/// </summary>
public class VrPanelFrameCapture : MonoBehaviour
{
    [Tooltip("Cameras in strip order (e.g. Main Camera R, B, L, …).")]
    public Camera[] faceCameras;

    [Tooltip("Width of one face tile in pixels.")]
    public int faceWidth = 64;

    [Tooltip("Height of one face tile in pixels.")]
    public int faceHeight = 64;

    [Tooltip("Max GPU readbacks in flight; drop frames if exceeded.")]
    public int maxInFlight = 3;

    public bool IsRecording { get; private set; }

    private RenderTexture _compositeRt;
    private readonly List<RenderTexture> _faceRts = new();
    private int _inFlight;
    private string _outputDir;
    private int _frameIndex;
    private readonly Queue<Action> _mainThreadQueue = new();

    public void BeginRecording(string outputDirectory)
    {
        if (faceCameras == null || faceCameras.Length == 0)
        {
            Debug.LogWarning("[VrPanelFrameCapture] No face cameras configured.");
            return;
        }

        _outputDir = outputDirectory;
        Directory.CreateDirectory(_outputDir);
        _frameIndex = 0;
        _inFlight = 0;
        EnsureRenderTargets();
        IsRecording = true;
    }

    public void EndRecording()
    {
        IsRecording = false;
    }

    /// <summary>Call from LateUpdate after scene cameras have rendered.</summary>
    public void CaptureFrame()
    {
        if (!IsRecording || _inFlight >= maxInFlight)
            return;

        EnsureRenderTargets();
        RenderStripToComposite();

        _inFlight++;
        int frame = _frameIndex++;
        AsyncGPUReadback.Request(_compositeRt, 0, TextureFormat.RGB24, request =>
        {
            _inFlight = Mathf.Max(0, _inFlight - 1);
            if (request.hasError)
                return;

            int w = _compositeRt.width;
            int h = _compositeRt.height;
            NativeArray<byte> raw = request.GetData<byte>();
            byte[] copy = new byte[raw.Length];
            raw.CopyTo(copy);
            string path = Path.Combine(_outputDir, $"{frame:D6}.png");
            _mainThreadQueue.Enqueue(() => EncodeAndWritePng(copy, w, h, path));
        });
    }

    private void Update()
    {
        int budget = 4;
        while (_mainThreadQueue.Count > 0 && budget-- > 0)
            _mainThreadQueue.Dequeue()?.Invoke();
    }

    private static void EncodeAndWritePng(byte[] rgb, int width, int height, string path)
    {
        try
        {
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.LoadRawTextureData(rgb);
            tex.Apply();
            byte[] png = ImageConversion.EncodeToPNG(tex);
            Destroy(tex);
            File.WriteAllBytes(path, png);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VrPanelFrameCapture] Write failed: {ex.Message}");
        }
    }

    private void RenderStripToComposite()
    {
        RenderTexture.active = _compositeRt;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = null;

        int n = faceCameras.Length;
        for (int i = 0; i < n; i++)
        {
            Camera cam = faceCameras[i];
            if (cam == null)
                continue;

            RenderTexture faceRt = _faceRts[i];
            var prevTarget = cam.targetTexture;
            var prevRect = cam.rect;
            bool prevEnabled = cam.enabled;

            cam.targetTexture = faceRt;
            cam.rect = new Rect(0, 0, 1, 1);
            cam.Render();
            cam.targetTexture = prevTarget;
            cam.rect = prevRect;
            cam.enabled = prevEnabled;

            float u0 = (float)i / n;
            Graphics.Blit(faceRt, _compositeRt, new Vector2(1f / n, 1f), new Vector2(u0, 0f));
        }
    }

    private void EnsureRenderTargets()
    {
        int n = faceCameras.Length;
        int totalW = faceWidth * n;

        if (_compositeRt != null && _compositeRt.width == totalW && _compositeRt.height == faceHeight)
            return;

        ReleaseTargets();
        _compositeRt = new RenderTexture(totalW, faceHeight, 24, RenderTextureFormat.ARGB32);
        _compositeRt.Create();

        for (int i = 0; i < n; i++)
        {
            var rt = new RenderTexture(faceWidth, faceHeight, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            _faceRts.Add(rt);
        }
    }

    private void ReleaseTargets()
    {
        if (_compositeRt != null)
        {
            _compositeRt.Release();
            Destroy(_compositeRt);
            _compositeRt = null;
        }
        foreach (var rt in _faceRts)
        {
            rt.Release();
            Destroy(rt);
        }
        _faceRts.Clear();
    }

    private void OnDestroy()
    {
        ReleaseTargets();
    }

    /// <summary>Build camera array from ViewportSetter display order.</summary>
    public static Camera[] CollectFaceCameras(ViewportSetter viewport)
    {
        if (viewport == null)
            return Array.Empty<Camera>();

        var main = FindMainController();
        SystemConfig config = main != null
            ? main.GetSystemConfigForGameObject(viewport.gameObject)
            : null;
        string order = config != null ? config.displayOrder : "DRBLFU";

        var all = viewport.GetComponentsInChildren<Camera>(true);
        var list = new List<Camera>();
        foreach (char c in order)
        {
            string targetName = $"Main Camera {c}";
            foreach (var cam in all)
            {
                if (cam.name == targetName)
                {
                    list.Add(cam);
                    break;
                }
            }
        }
        return list.ToArray();
    }

    private static MainController FindMainController()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<MainController>();
#else
        return FindObjectOfType<MainController>();
#endif
    }
}
