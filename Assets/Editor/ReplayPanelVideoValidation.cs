#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Fresh saved run + native video -> actual ReplayScene media panel -> GPU pixels, including reverse seeks.</summary>
[InitializeOnLoad]
public static class ReplayPanelVideoValidation
{
    const string Key="ReplayPanelVideoValidation.Active";
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static string output,run,sourceRun,sourceVideo;
    static Dictionary<string,string> runBefore,videoBefore;
    static readonly List<string> errors=new List<string>();
    static readonly List<object> observations=new List<object>();
    static readonly Dictionary<long,string> priorPixels=new Dictionary<long,string>();
    static ReplayController replay;
    static ReplayMediaArchive media;
    static ReplayMediaClip clip;
    static List<double> seeks;
    static int stage,index,checks,nextFrame;
    static double deadline;
    static int captureFrame;
    static object guiCapture;
    static ReplayPanelVideoValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            output=SessionState.GetString(Key+".Output","");run=Path.Combine(output,"copied-run");
            sourceRun=SessionState.GetString(Key+".Run","");sourceVideo=SessionState.GetString(Key+".Video","");
            runBefore=JObject.Parse(File.ReadAllText(Path.Combine(output,"source-run-hashes.json"))).ToObject<Dictionary<string,string>>();
            videoBefore=JObject.Parse(File.ReadAllText(Path.Combine(output,"source-video-hashes.json"))).ToObject<Dictionary<string,string>>();
            errors.Clear();observations.Clear();priorPixels.Clear();stage=index=checks=0;nextFrame=Time.frameCount+5;deadline=EditorApplication.timeSinceStartup+120;
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        };
    }
    static string Arg(string name,string fallback=null)
    {var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];return fallback??throw new ArgumentException("Missing "+name);}
    static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static Dictionary<string,string> Snapshot(string directory)
    {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(string file in Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories))
        {
            if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Validation cannot follow source symlinks.");
            using(var stream=File.OpenRead(file))using(var hash=SHA256.Create())result[Path.GetRelativePath(directory,file)]=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        return result;
    }
    static void CopyTree(string source,string target)
    {
        Directory.CreateDirectory(target);
        foreach(string directory in Directory.GetDirectories(source))
        {if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("Source directory is a symlink.");CopyTree(directory,Path.Combine(target,Path.GetFileName(directory)));}
        foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),false);
    }
    static bool Unchanged(string source,Dictionary<string,string> before)
    {var after=Snapshot(source);return before.Count==after.Count&&before.All(pair=>after.TryGetValue(pair.Key,out string hash)&&hash==pair.Value);}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new InvalidOperationException("Disposable project only.");
        sourceRun=Path.GetFullPath(Arg("-panel-run-source"));sourceVideo=Path.GetFullPath(Arg("-panel-video-source"));output=Path.GetFullPath(Arg("-panel-output"));
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Use a fresh validation output path.");
        if(new[]{sourceRun,sourceVideo}.Any(source=>output.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))throw new IOException("Output may not be inside source evidence.");
        Directory.CreateDirectory(output);run=Path.Combine(output,"copied-run");
        runBefore=Snapshot(sourceRun);videoBefore=Snapshot(sourceVideo);CopyTree(sourceRun,run);
        File.WriteAllText(Path.Combine(output,"source-run-hashes.json"),JsonConvert.SerializeObject(runBefore,Formatting.Indented));
        File.WriteAllText(Path.Combine(output,"source-video-hashes.json"),JsonConvert.SerializeObject(videoBefore,Formatting.Indented));
        var saved=new ReplaySessionArchive(run);saved.LoadRigSamples();
        var receipt=MediaArchive.ImportAsync(new MediaImportRequest{RunDirectory=run,SourceDirectory=sourceVideo,RigId="VR1",ExpectedBackend=Arg("-panel-backend","Rekkefly"),SourceClosed=true,SyncMode="consumed-frame-id",ReplayOriginTicks=saved.OriginTicks}).GetAwaiter().GetResult();
        if(!receipt.Complete||receipt.ImportedClips<1)throw new Exception("Closed native video did not import.");
        SessionState.SetString(Key+".Output",output);SessionState.SetString(Key+".Run",sourceRun);SessionState.SetString(Key+".Video",sourceVideo);SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/ReplayScene.unity");
        var controller=Object.FindFirstObjectByType<ReplayController>();if(controller==null)throw new Exception("Replay scene has no controller.");
        controller.absoluteSessionPath=run;controller.loop=false;controller.showOverviewOnStart=true;
        EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Log(string text,string stack,LogType type)
    {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))errors.Add(text);}
    static Texture2D RenderTexturePixels(Texture image)
    {
        var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(image.width,image.height,0,RenderTextureFormat.ARGB32);
        try
        {
            Graphics.Blit(image,target);RenderTexture.active=target;
            var read=new Texture2D(image.width,image.height,TextureFormat.RGB24,false);read.ReadPixels(new Rect(0,0,image.width,image.height),0,0);read.Apply(false,false);return read;
        }
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Replay panel stage "+stage+", seek "+index);
            if(Time.frameCount<nextFrame)return;nextFrame=Time.frameCount+1;
            replay=Object.FindFirstObjectByType<ReplayController>();if(replay==null||!replay.Ready||replay.Loading)return;
            if(stage==3)
            {
                string screenshot=Path.Combine(output,"replay-panel-ui.png");
                if(Time.frameCount<captureFrame+8)return;
                if(!File.Exists(screenshot)&&Time.frameCount<captureFrame+60)return;
                if(File.Exists(screenshot))
                {
                    var image=new Texture2D(2,2,TextureFormat.RGB24,false);
                    try {Check(image.LoadImage(File.ReadAllBytes(screenshot)),"Actual GameView replay GUI screenshot decodes");guiCapture=new{available=true,file="replay-panel-ui.png",image.width,image.height};}
                    finally {Object.Destroy(image);}
                }
                else guiCapture=new{available=false,note="Editor batch GameView did not publish a screenshot; inspect the final native player overlay separately."};
                Check(Unchanged(sourceRun,runBefore)&&Unchanged(sourceVideo,videoBefore),"Every original run and native video source file remains unchanged");
                Check(errors.Count==0,"No replay runtime error: "+string.Join(";",errors));Finish(null);return;
            }
            if(stage==0)
            {
                replay.SetPlaying(false);Check(replay.Session.DirectoryPath==run,"Actual ReplayScene loads only the diagnostic run copy");
                media=ReplayMediaArchive.LoadAsync(run,replay.Session.OriginTicks).GetAwaiter().GetResult();
                clip=media.Clips.FirstOrDefault(c=>c.Complete&&c.State=="ready"&&c.RigId=="VR1");
                Check(clip!=null&&clip.SyncMode=="consumed-frame-id","Fresh native frames map to actually consumed saved input IDs");
                var manifest=JObject.Parse(File.ReadAllText(Path.Combine(run,"media_manifest.json")));
                var entry=((JArray)manifest["clips"]).OfType<JObject>().Single(c=>(string)c["id"]==clip.Id);
                var anchors=File.ReadLines(Path.Combine(run,(string)entry["indexPath"])).Where(line=>!string.IsNullOrWhiteSpace(line)).Select(JObject.Parse).GroupBy(row=>(long)row["mediaFrame"]).Select(group=>group.First()).ToList();
                Check(anchors.Count>4,"Real recording provides multiple distinct consumed camera frames");
                seeks=new[]{0,anchors.Count/2,anchors.Count-1,anchors.Count/2,0}.Select(i=>(double)anchors[i]["replaySeconds"]).ToList();
                stage=1;return;
            }
            var panel=(ReplayMediaPanel)typeof(ReplayController).GetField("mediaPanel",Hidden).GetValue(replay);
            if(panel==null)return;
            if(stage==1){replay.SetPlaying(false);replay.SeekSeconds(seeks[index]);stage=2;return;}
            if(stage==2)
            {
                if(replay.environmentLoader!=null&&replay.environmentLoader.Loading)return;
                Check(Math.Abs(replay.ReplayTime-seeks[index])<1e-7,"Pose replay remains paused at the requested shared clock");
                Check(media.TryResolveFrame(clip,replay.ReplayTime,out var expected),"Saved camera association resolves for this exact replay time");
                if(panel.VisibleVideoTexture==null||panel.DisplayedVideoFrame!=expected.LocalFrame)return;
                Check(replay.OverviewVisible,"God's-eye remains active while tracker video renders");
                var animal=(RenderTexture)typeof(ReplayMediaPanel).GetField("animalPreview",Hidden).GetValue(panel);
                Check(animal!=null&&animal.IsCreated(),"Animal front-camera preview is present simultaneously");
                var actual=RenderTexturePixels(panel.VisibleVideoTexture);var direct=new Texture2D(2,2,TextureFormat.RGB24,false);Texture2D expectedGpu=null;
                try
                {
                    using(var decoder=ReplayAviFrameSource.Open(clip.AbsolutePath)){var frame=decoder.ReadFrame(expected.LocalFrame);Check(direct.LoadImage(frame.Bytes,false),"Reference native frame decodes");}
                    expectedGpu=RenderTexturePixels(direct);string actualHash=Hash(actual.EncodeToPNG()),expectedHash=Hash(expectedGpu.EncodeToPNG());
                    Check(actualHash==expectedHash,"Actual replay panel's GPU-rendered frame matches the requested saved source frame");
                    if(priorPixels.TryGetValue(expected.LocalFrame,out string previous))Check(previous==actualHash,"Backward seek returns identical rendered panel pixels");else priorPixels[expected.LocalFrame]=actualHash;
                    string file="panel-seek-"+index+"-frame-"+expected.LocalFrame+".png";File.WriteAllBytes(Path.Combine(output,file),actual.EncodeToPNG());
                    observations.Add(new{replaySeconds=replay.ReplayTime,expectedFrame=expected.LocalFrame,panelFrame=panel.DisplayedVideoFrame,actual.width,actual.height,gpuPngSha256=actualHash,referenceGpuPngSha256=expectedHash,file,association=expected.AssociationLabel});
                }
                finally {Object.Destroy(actual);Object.Destroy(direct);if(expectedGpu!=null)Object.Destroy(expectedGpu);}
                index++;if(index<seeks.Count){stage=1;return;}
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"replay-panel-ui.png"));captureFrame=Time.frameCount;stage=3;
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    static void Finish(string failure)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
        File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new{passed=failure==null,checks,failure,errors,sourceRun,sourceVideo,testRun=run,observations,guiCapture,scope="Fresh native recording from an explicitly labelled demo/sample and actual acquired pose CSV -> production import using consumed input IDs -> actual ReplayScene + asynchronous ReplayMediaPanel -> GPU-rendered visible texture, forward/back seeks. God's-eye and animal preview active simultaneously. No physical camera, new animal measurement, exposure or LED timing claim."},Formatting.Indented));
        if(failure!=null)Debug.LogError(failure);else Debug.Log("REPLAY PANEL VIDEO PASSED: "+checks);
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
