using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One small acquisition-time preview; replay only reads it.</summary>
public static class RecordingThumbnail
{
    public static IEnumerator Capture(Scene scene, string directory)
    {
        yield return new WaitForEndOfFrame();
        if (!scene.IsValid() || !scene.isLoaded || !Directory.Exists(directory)) yield break;
        string path = Path.Combine(directory, "thumbnail.png");
        if (File.Exists(path)) yield break;
        var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .Where(value => value.enabled && value.gameObject.activeInHierarchy)
            .OrderBy(value => value.name.IndexOf("Front", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1).FirstOrDefault();
        if (camera == null) yield break;
        RenderTexture target = null; Texture2D image = null;
        var oldTarget = camera.targetTexture; var oldRect = camera.rect; float oldAspect = camera.aspect; var oldActive = RenderTexture.active;
        try
        {
            float aspect = Mathf.Clamp(oldAspect, .1f, 10f);
            int width = aspect >= 1 ? 320 : Mathf.Max(1, Mathf.RoundToInt(320 * aspect));
            int height = aspect >= 1 ? Mathf.Max(1, Mathf.RoundToInt(320 / aspect)) : 320;
            target = new RenderTexture(width, height, 24); target.Create();
            camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = aspect;
            camera.Render(); RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            // The file belongs to the active acquisition, and is never replaced later.
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            { byte[] bytes = image.EncodeToPNG(); file.Write(bytes, 0, bytes.Length); }
        }
        catch (Exception error) { Debug.LogWarning("Recording thumbnail unavailable: " + error.Message); }
        finally
        {
            if (camera != null) { camera.targetTexture = oldTarget; camera.rect = oldRect; camera.aspect = oldAspect; }
            RenderTexture.active = oldActive;
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (image != null) UnityEngine.Object.Destroy(image);
        }
    }
}
