using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Read-only archived inputs while the original build configures its replay visuals.</summary>
public sealed class ReplayExecutionContext : IDisposable
{
    private static ReplayExecutionContext current;
    private readonly ReplayExecutionContext prior;
    private readonly ReplaySessionArchive archive;
    private readonly JObject presentation;
    private readonly IReadOnlyDictionary<string,string> modelKeys;
    public static bool Active => current != null;
    public ReplayExecutionContext(ReplaySessionArchive archive, JObject presentation, IReadOnlyDictionary<string,string> modelKeys)
    { prior = current; this.archive = archive; this.presentation = presentation; this.modelKeys = modelKeys; current = this; }
    public static string Resolve(string reference)
    {
        if (current == null) return null;
        string path = current.archive.Resolve(reference);
        if (path == null) throw new FileNotFoundException("Archived replay dependency is missing: " + reference);
        return path;
    }
    public static string ModelKey(string reference)
    {
        if(current==null)return reference;
        if(current.modelKeys!=null&&current.modelKeys.TryGetValue(reference,out var key))return key;
        throw new FileNotFoundException("Archived external model was not loaded: "+reference);
    }
    public static bool TrySystemConfig(GameObject target,out SystemConfig config)
    {
        for(var item=target.transform;item!=null;item=item.parent)
        {
            int index=RuntimeRigRegistry.ParseIndex(item.name);if(index<1)continue;
            foreach(var saved in current.archive.SystemConfiguration?["configs"] as JArray??new JArray())
                if((string)saved["vrId"]=="VR"+index){config=saved.ToObject<SystemConfig>();return true;}
        }
        config=null;return false;
    }
    public static HeadingReferenceResult Heading(ClosedLoop rig)
    {
        string id = "VR" + RuntimeRigRegistry.ParseIndex(rig.name);
        foreach (var token in current?.presentation?["rigsBeforeApply"] as JArray ?? new JArray())
            if ((string)token["id"] == id) return token["headingReference"]?.ToObject<HeadingReferenceResult>();
        return null;
    }
    public static ClosedLoop[] Rigs(UnityEngine.SceneManagement.Scene scene)
    {
        if (!Active) return RuntimeRigRegistry.Rigs(scene);
        return Array.FindAll(UnityEngine.Object.FindObjectsByType<ClosedLoop>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            rig => rig.gameObject.scene == scene && rig.gameObject.activeInHierarchy);
    }
    public void Dispose() { current = prior; }
}
