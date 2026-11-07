#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Checks worker-owned animation evidence, CSV fidelity and pooled queue lifetime.</summary>
public static class PopulationSnapshotValidation
{
    private static int checks;
    private static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    private static readonly MethodInfo Copy=typeof(RecordedAnimationSnapshot).GetMethod("CopyFrom",BindingFlags.Instance|BindingFlags.NonPublic);
    private static void Capture(RecordedAnimationSnapshot target,RecordedAnimationState value)=>Copy.Invoke(target,new object[]{value});
    private static string Json(RecordedAnimationState value)=>JsonConvert.SerializeObject(value,Formatting.None,new JsonSerializerSettings{NullValueHandling=NullValueHandling.Ignore});
    private static string Csv(string json)=>",\""+json.Replace("\"","\"\"")+"\"";
    private static string Cell(RecordedAnimationSnapshot value){using(var output=new StringWriter(CultureInfo.InvariantCulture)){value.WriteCsvCell(output);return output.ToString();}}
    private static RecordedAnimationState Rich()=>new RecordedAnimationState{
        version=1,
        animators=new[]{new RecordedAnimationState.AnimatorPose{path="0/quoted\",\r\n",speed=.75f,layers=new[]{new RecordedAnimationState.LayerPose{state=123,time=.25f,weight=.6f,transitioning=true}},parameters=new[]{new RecordedAnimationState.ParameterPose{hash=234,type=1,value=1.25f}}},null},
        legacy=new[]{new RecordedAnimationState.LegacyPose{path="1",clips=new[]{new RecordedAnimationState.ClipPose{name="clip,\"line\n",time=.4f,speed=.8f,weight=.7f,layer=2,wrap=3,blend=1,enabled=true}}}},
        procedural=new RecordedAnimationState.ProceduralPose{time=1.5f,phase=.3f},
        realizedPose=new[]{new RecordedAnimationState.LocalPose{path="0/0",position=new[]{1f,2f,3f},rotation=new[]{0f,.5f,0f,.8660254f},scale=new[]{2f,3f,4f}}},
        blendShapes=new[]{new RecordedAnimationState.BlendPose{path="0/skin",weights=new[]{10f,25f}}}};
    private sealed class Gate:AsyncSwarmWriter.IAnimationSnapshot
    {
        internal readonly ManualResetEventSlim entered=new ManualResetEventSlim(false),release=new ManualResetEventSlim(false);
        internal RecordedAnimationSnapshot inner;
        public void WriteCsvCell(TextWriter output){entered.Set();if(!release.Wait(5000))throw new Exception("Gate timed out");inner.WriteCsvCell(output);}
    }
    private static void QueueChecks(string output)
    {
        const int count=120;
        var source=Rich();var expected=new List<string>();var snapshots=new HashSet<RecordedAnimationSnapshot>();
        var writer=new AsyncSwarmWriter(Path.Combine(output,"snapshots.csv.gz"),"Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase,RotationX,RotationY,RotationZ,AnimationState,UnityFrame,ScaleX,ScaleY,ScaleZ");
        var gate=new Gate();
        try
        {
            for(int i=0;i<count;i++)
            {
                source.animators[0].layers[0].time=i+.125f;
                source.realizedPose[0].position[0]=i+.25f;source.blendShapes[0].weights[0]=i+.5f;
                source.legacy[0].clips[0].time=i+.75f;source.procedural.time=i;
                expected.Add(Json(source));
                var frame=writer.Rent(1);var snapshot=frame.agents[0].animationSnapshot as RecordedAnimationSnapshot??new RecordedAnimationSnapshot();
                Capture(snapshot,source);snapshots.Add(snapshot);
                AsyncSwarmWriter.IAnimationSnapshot queued=snapshot;
                if(i==0){gate.inner=snapshot;queued=gate;}
                frame.count=1;frame.timestamp=new DateTime(2026,11,6,10,0,0).AddMilliseconds(i);frame.unityFrame=i;
                frame.agents[0]=new AsyncSwarmWriter.Agent{name="agent",layer="AgentsVR1",animationSnapshot=queued};
                Check(writer.Submit(frame),"Snapshot frame accepted "+i);
                if(i==0)Check(gate.entered.Wait(5000),"Worker entered blocked serialization");
            }
            Check(writer.PendingFrames==count,"Queued frame snapshots remain owned while writer blocked");
            gate.release.Set();Check(SpinWait.SpinUntil(()=>writer.PendingFrames==0,5000),"Worker drains queued snapshots");
            int reused=0;
            for(int i=count;i<count+16;i++)
            {
                source.animators[0].layers[0].time=i;expected.Add(Json(source));
                var frame=writer.Rent(1);var previous=frame.agents[0].animationSnapshot as RecordedAnimationSnapshot;
                if(previous!=null&&snapshots.Contains(previous))reused++;
                var snapshot=previous??new RecordedAnimationSnapshot();Capture(snapshot,source);
                frame.count=1;frame.timestamp=new DateTime(2026,11,6,10,0,0).AddMilliseconds(i);frame.unityFrame=i;
                frame.agents[0]=new AsyncSwarmWriter.Agent{name="agent",layer="AgentsVR1",animationSnapshot=snapshot};
                Check(writer.Submit(frame),"Reused frame accepted "+i);
            }
            Check(reused>0,"Rented Agent structs retain snapshot buffers across pool reuse");
            writer.WaitForCompletion();Check(writer.Failure==null&&writer.DroppedFrames==0&&writer.WrittenRows==expected.Count,"All worker-owned snapshots drain without drops");
            using(var input=new StreamReader(new GZipStream(File.OpenRead(Path.Combine(output,"snapshots.csv.gz")),CompressionMode.Decompress)))
            {
                input.ReadLine();
                foreach(string json in expected){string line=input.ReadLine();Check(line!=null,"Population row exists");var cells=ReplaySessionArchive.Csv(line);Check(cells[12]==json,"Queued JSON remains exact after source mutation and pooled reuse");}
                Check(input.ReadLine()==null,"Exactly one row per accepted snapshot");
            }
        }
        finally{gate.release.Set();writer.WaitForCompletion();gate.entered.Dispose();gate.release.Dispose();}
    }
    private static void LiveParity(RecordedAnimationCapture source,RecordedAnimationSnapshot snapshot,string label,BogongSwarmRenderer batch=null)
    {
        string before=source.Capture(batch,0);source.CaptureSnapshot(snapshot,batch,0);
        Check(Cell(snapshot)==(before.Length==0?",":Csv(before)),"Worker snapshot matches standard capture: "+label);
    }
    private static void SteadyNumbers()
    {
        var state=new RecordedAnimationState { animators=new[]{new RecordedAnimationState.AnimatorPose {
            path="",layers=new[]{new RecordedAnimationState.LayerPose()},parameters=Array.Empty<RecordedAnimationState.ParameterPose>()}},legacy=Array.Empty<RecordedAnimationState.LegacyPose>() };
        var snapshot=new RecordedAnimationSnapshot();
        foreach(float number in new[]{0f,-0f,1f,-1f,.15f,.99f,1.4f,float.Epsilon,1e-6f,1e7f,-1e10f,float.MaxValue,float.MinValue,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            state.animators[0].speed=number;var layer=state.animators[0].layers[0];layer.time=number;layer.weight=number;layer.state=int.MinValue;layer.transitioning=false;
            Capture(snapshot,state);Check(Cell(snapshot)==Csv(Json(state)),"Steady Animator preserves standard numeric JSON at "+number);
        }
        state.animators[0].path="child/quoted\"";Capture(snapshot,state);
        Check(Cell(snapshot)==Csv(Json(state)),"Non-root Animator keeps standard serializer fallback");
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use marked disposable project");
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../population-snapshot-validation"));Directory.CreateDirectory(output);
        string failure=null;var oldCulture=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");
            var rich=Rich();var snapshot=new RecordedAnimationSnapshot();string expected=Json(rich);Capture(snapshot,rich);
            Check(Cell(snapshot)==Csv(expected),"CSV encoding equals standard JSON with quotes, commas and newlines");
            rich.animators[0].layers[0].time=99;rich.animators[0].parameters[0].value=98;
            rich.realizedPose[0].position[0]=97;rich.realizedPose[0].rotation[0]=96;rich.realizedPose[0].scale[0]=95;
            rich.blendShapes[0].weights[0]=94;rich.legacy[0].clips[0].time=93;rich.procedural.time=92;
            Check(Cell(snapshot)==Csv(expected),"Snapshot has no mutable array/object aliases to capture buffers");
            rich.realizedPose=null;rich.blendShapes=null;rich.procedural=null;rich.legacy=Array.Empty<RecordedAnimationState.LegacyPose>();
            Capture(snapshot,rich);Check(Cell(snapshot)==Csv(Json(rich)),"Reused snapshot clears transition, blend, procedural and legacy state");
            Capture(snapshot,null);Check(Cell(snapshot)==",","Empty snapshot never writes stale previous evidence");
            SteadyNumbers();QueueChecks(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var prefab=Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog").Find("SimulatedLocust");var animal=Object.Instantiate(prefab);
            var animator=animal.GetComponentInChildren<Animator>(true);animator.enabled=true;animator.Rebind();animator.Update(0);
            int state=animator.GetCurrentAnimatorStateInfo(0).fullPathHash;var capture=new RecordedAnimationCapture(animal);
            foreach(float phase in new[]{0f,.15f,.5f,.99f,1.4f,.15f})
            {
                animator.Play(state,0,phase);animator.Update(0);
                if(phase==.5f){animator.CrossFade(state,.25f,0,.7f);animator.Update(.05f);}
                LiveParity(capture,snapshot,"native animator/transition "+phase);
            }
            var moth=new GameObject("Snapshot procedural");var batch=moth.AddComponent<BogongSwarmRenderer>();
            batch.Configure(new[]{moth},new BogongVisualConfig{shape="Moth"});batch.SetRecordedAnimation(.35f,.17f);
            LiveParity(new RecordedAnimationCapture(moth),snapshot,"Bogong time and phase",batch);
            var empty=new GameObject("No animation");LiveParity(new RecordedAnimationCapture(empty),snapshot,"no animation");
            Object.DestroyImmediate(animal);Object.DestroyImmediate(moth);Object.DestroyImmediate(empty);
        }
        catch(Exception error){failure=error.ToString();Debug.LogError(failure);}
        finally{CultureInfo.CurrentCulture=oldCulture;}
        File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new{checks,failure,unity=Application.unityVersion},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
