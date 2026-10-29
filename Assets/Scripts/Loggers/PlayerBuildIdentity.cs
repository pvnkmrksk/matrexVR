using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Identity is captured from this player, never from a mutable external release folder or Git checkout.</summary>
public static class PlayerBuildIdentity
{
    private static readonly string EditorRuntimeIdentity = Guid.NewGuid().ToString("N");
    private static readonly object Gate = new object();
    private static JObject cached;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache(){lock(Gate)cached=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WarmCache()=>Capture();
    public static JObject Capture()
    { lock(Gate){if(cached==null)cached=CaptureUncached();return (JObject)cached.DeepClone();} }
    private static JObject CaptureUncached()
    {
        var result = new JObject { ["applicationVersion"] = Application.version, ["unityVersion"] = Application.unityVersion,
            ["buildGuid"] = Application.buildGUID, ["sourceRevision"] = null, ["sourceState"] = "unknown" };
        if (Application.isEditor) result["editorRuntimeIdentity"] = EditorRuntimeIdentity;
        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, "player_build_info.json");
            if (!File.Exists(path)) { result["provenanceWarning"] = "This build has no embedded source identity."; return result; }
            if (new FileInfo(path).Length > 1024 * 1024) throw new IOException("Embedded build identity exceeds the size limit.");
            var saved = JObject.Parse(File.ReadAllText(path));
            if ((int?)saved["schemaVersion"] != 1 || (string)saved["applicationVersion"] != Application.version)
                throw new IOException("Embedded build identity does not match this player's version.");
            string revision = (string)saved["sourceRevision"];
            if (revision != null && !Regex.IsMatch(revision, "\\A[0-9a-f]{40}\\z")) throw new IOException("Invalid embedded source revision.");
            foreach (string key in new[] { "sourceRevision", "sourceState", "sourceTreeDirty", "runtimeSourceFingerprintSha256", "runtimeSourceFileCount", "fingerprintScope" })
                if (saved[key] != null) result[key] = saved[key].DeepClone();
        }
        catch (Exception error) { result["provenanceWarning"] = error.Message; }
        return result;
    }
}
