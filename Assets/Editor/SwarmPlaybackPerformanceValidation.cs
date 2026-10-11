#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Reproducible CPU submission benchmark, including inactive CSV pool slots.</summary>
public static class SwarmPlaybackPerformanceValidation
{
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY")))
            throw new Exception("Disposable project only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var results = new List<object>(); string failure = null;
        try { foreach (int active in new[] { 35, 70, 430 }) results.Add(Measure(active)); }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(Application.dataPath,"../swarm-playback-performance.json"),
            JsonConvert.SerializeObject(new {failure,results,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,
                note="CPU submission timings only, not end-to-end GPU/display FPS. Four pools of 430 slots; 24 cameras at 64px. Fixed positions and random seed."},Formatting.Indented));
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    private static object Measure(int active)
    {
        var roots=new List<GameObject>(); var batches=new List<BogongSwarmRenderer>();
        var cameras=new List<Camera>(); var targets=new List<RenderTexture>();
        var random=new System.Random(470);
        try
        {
            for(int rig=0;rig<4;rig++)
            {
                Vector3 origin=Vector3.right*rig*300; int layer=25+rig; var agents=new List<GameObject>();
                for(int i=0;i<430;i++)
                {
                    var a=new GameObject("Pooled moth"); roots.Add(a); agents.Add(a);
                    a.transform.SetPositionAndRotation(origin+new Vector3((float)random.NextDouble()*20-10,
                        (float)random.NextDouble()*4-2,(float)random.NextDouble()*80-40),Quaternion.Euler(0,0,0));
                    a.SetActive(i<active);
                }
                var host=new GameObject("Batch") {layer=layer}; roots.Add(host);
                var batch=host.AddComponent<BogongSwarmRenderer>(); batch.Configure(agents,new BogongVisualConfig {shape="Moth"},1); batches.Add(batch);
                foreach(Quaternion q in new[] {Quaternion.identity,Quaternion.Euler(0,90,0),Quaternion.Euler(0,180,0),
                    Quaternion.Euler(0,270,0),Quaternion.Euler(-90,0,0),Quaternion.Euler(90,0,0)})
                {
                    var root=new GameObject("Panel"); roots.Add(root); var camera=root.AddComponent<Camera>(); camera.enabled=false;
                    camera.transform.SetPositionAndRotation(origin,q); camera.fieldOfView=90; camera.aspect=1;
                    camera.nearClipPlane=.001f; camera.farClipPlane=150; camera.allowHDR=false; camera.allowMSAA=false;
                    camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.gray; camera.cullingMask=1<<layer;
                    var rt=new RenderTexture(64,64,24); rt.Create(); targets.Add(rt); camera.targetTexture=rt; cameras.Add(camera);
                }
            }
            var updates=new List<double>(); var submissions=new List<double>();
            for(int frame=0;frame<130;frame++)
            {
                long start=System.Diagnostics.Stopwatch.GetTimestamp();
                foreach(var batch in batches) batch.RefreshCenters();
                long middle=System.Diagnostics.Stopwatch.GetTimestamp();
                foreach(var camera in cameras) camera.Render();
                long end=System.Diagnostics.Stopwatch.GetTimestamp();
                if(frame>=30) { updates.Add(1000.0*(middle-start)/System.Diagnostics.Stopwatch.Frequency);
                    submissions.Add(1000.0*(end-start)/System.Diagnostics.Stopwatch.Frequency); }
            }
            // No timers/LINQ or list growth inside the measured allocation interval.
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int frame=0;frame<100;frame++) foreach(var batch in batches) batch.RefreshCenters();
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            updates.Sort(); submissions.Sort();
            return new {allocatedAgents=1720,activeAgents=4*active,rigs=4,cameras=24,
                refreshMedianMilliseconds=updates[50],refreshP95Milliseconds=updates[95],
                refreshAndRenderMedianMilliseconds=submissions[50],refreshAndRenderP95Milliseconds=submissions[95],
                refreshManagedBytesPerFrame=allocated/100.0};
        }
        finally
        {
            foreach(var camera in cameras) camera.targetTexture=null;
            foreach(var rt in targets) {rt.Release();Object.DestroyImmediate(rt);}
            foreach(var root in roots) Object.DestroyImmediate(root);
        }
    }
}
#endif
