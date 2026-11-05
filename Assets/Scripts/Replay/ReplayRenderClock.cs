using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One explicitly defined shader clock for acquisition and replay, including reverse seeks.</summary>
[RequireComponent(typeof(Camera))]
public sealed class ReplayRenderClock : MonoBehaviour
{
    private Camera target;
    private CommandBuffer commands, restore;
    private static readonly string[] Names={"_Time","_SinTime","_CosTime","_TimeParameters","unity_DeltaTime"};
    private static readonly int[] PropertyIds={Shader.PropertyToID("_Time"),Shader.PropertyToID("_SinTime"),Shader.PropertyToID("_CosTime"),Shader.PropertyToID("_TimeParameters"),Shader.PropertyToID("unity_DeltaTime")};
    internal readonly struct Values
    {
        internal readonly Vector4 time,sine,cosine,parameters,delta;
        internal Values(Vector4 time,Vector4 sine,Vector4 cosine,Vector4 parameters,Vector4 delta)
        {this.time=time;this.sine=sine;this.cosine=cosine;this.parameters=parameters;this.delta=delta;}
        internal Vector4 At(int index)=>index==0?time:index==1?sine:index==2?cosine:index==3?parameters:delta;
    }
    internal static Values Evaluate(float seconds,float delta,float smooth) => new Values(
        new Vector4(seconds/20f,seconds,seconds*2,seconds*3),
        new Vector4(Mathf.Sin(seconds/8),Mathf.Sin(seconds/4),Mathf.Sin(seconds/2),Mathf.Sin(seconds)),
        new Vector4(Mathf.Cos(seconds/8),Mathf.Cos(seconds/4),Mathf.Cos(seconds/2),Mathf.Cos(seconds)),
        new Vector4(seconds,Mathf.Sin(seconds),Mathf.Cos(seconds),0),
        new Vector4(delta,delta>0?1/delta:0,smooth,smooth>0?1/smooth:0));
    private static Values Read(JObject recorded)
    {
        Vector4 V(string name)
        {
            var value=recorded?[name] as JArray;
            if(value==null||value.Count!=4)throw new System.IO.InvalidDataException("Recorded shader clock is missing "+name);
            return new Vector4((float)value[0],(float)value[1],(float)value[2],(float)value[3]);
        }
        return new Values(V(Names[0]),V(Names[1]),V(Names[2]),V(Names[3]),V(Names[4]));
    }
    private static readonly CameraEvent[] Events = { CameraEvent.BeforeDepthTexture, CameraEvent.BeforeDepthNormalsTexture,
        CameraEvent.BeforeGBuffer,CameraEvent.BeforeReflections,CameraEvent.BeforeLighting,
        CameraEvent.BeforeSkybox, CameraEvent.BeforeForwardOpaque, CameraEvent.BeforeForwardAlpha };
    public static JObject CaptureFrameClock() => Clock(Time.timeSinceLevelLoad,Time.deltaTime,Time.smoothDeltaTime);
    internal static JObject Clock(float seconds,float delta,float smooth)
    {
        var values=Evaluate(seconds,delta,smooth);var result=new JObject();
        for(int i=0;i<Names.Length;i++){var v=values.At(i);result[Names[i]]=new JArray(v.x,v.y,v.z,v.w);}
        return result;
    }
    public void SetClock(float seconds,float delta,float smooth)
    {var values=Evaluate(seconds,delta,smooth);Install(values);SetSunClocks(values);}
    public void SetClock(JObject recorded)
    {
        var values=Read(recorded);Install(values);SetSunClocks(values);
    }
    /// <summary>One recorded clock, parsed once and shared by all camera/shadow passes of the frame.</summary>
    public static void SetFrameClocks(IEnumerable<Camera> cameras,JObject recorded)
    {
        var values=Read(recorded);
        foreach(var camera in cameras)
            if(camera!=null)(camera.GetComponent<ReplayRenderClock>()??camera.gameObject.AddComponent<ReplayRenderClock>()).Install(values);
        SetSunClocks(values);
    }
    private void Install(Values values)
    {
        if(target==null)target=GetComponent<Camera>();
        if(commands==null)
        {
            commands=new CommandBuffer{name="Recorded experiment shader clock"};
            restore=new CommandBuffer{name="Restore engine shader clock"};
            foreach(var stage in Events)target.AddCommandBuffer(stage,commands);
            target.AddCommandBuffer(CameraEvent.AfterEverything,restore);
        }
        commands.Clear();Set(commands,values);
        RefreshRestore();
    }
    private static void SetSunClocks(Values values)
    {
        foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(light.type==LightType.Directional&&light.enabled)
            {
                var sun=light.GetComponent<RecordedSunClock>()??light.gameObject.AddComponent<RecordedSunClock>();
                sun.SetClock(values);
            }
    }
    internal static void Set(CommandBuffer buffer,JObject values)=>Set(buffer,Read(values));
    internal static void Set(CommandBuffer buffer,Values values)
    {
        for(int i=0;i<PropertyIds.Length;i++)buffer.SetGlobalVector(PropertyIds[i],values.At(i));
    }
    private void RefreshRestore()
    {
        if(restore==null)return;
        restore.Clear();Set(restore,Evaluate(Time.time,Time.deltaTime,Time.smoothDeltaTime));
    }
    private void OnPreCull(){RefreshRestore();}
    private void OnDestroy()
    {
        if(commands==null)return;
        if(target!=null)
        {
            foreach(var stage in Events)target.RemoveCommandBuffer(stage,commands);
            target.RemoveCommandBuffer(CameraEvent.AfterEverything,restore);
        }
        commands.Release();restore.Release();commands=null;restore=null;
    }
}

/// <summary>Shadow caster shaders use the same frame clock as camera-visible geometry.</summary>
[RequireComponent(typeof(Light))]
public sealed class RecordedSunClock:MonoBehaviour
{
    private Light sun;
    private CommandBuffer before,after;
    public void SetClock(JObject clock)
    {
        if(sun==null)sun=GetComponent<Light>();
        EnsureBuffers();before.Clear();ReplayRenderClock.Set(before,clock);
        RefreshRestore();
    }
    internal void SetClock(ReplayRenderClock.Values clock)
    {
        if(sun==null)sun=GetComponent<Light>();
        EnsureBuffers();before.Clear();ReplayRenderClock.Set(before,clock);RefreshRestore();
    }
    private void EnsureBuffers()
    {
        if(before==null)
        {
            before=new CommandBuffer{name="Recorded experiment shadow clock"};
            after=new CommandBuffer{name="Restore engine shadow clock"};
            sun.AddCommandBuffer(LightEvent.BeforeShadowMap,before);sun.AddCommandBuffer(LightEvent.AfterShadowMap,after);
        }
    }
    private void RefreshRestore(){after.Clear();ReplayRenderClock.Set(after,ReplayRenderClock.Evaluate(Time.time,Time.deltaTime,Time.smoothDeltaTime));}
    private void OnDestroy()
    {
        if(before==null)return;
        if(sun!=null){sun.RemoveCommandBuffer(LightEvent.BeforeShadowMap,before);sun.RemoveCommandBuffer(LightEvent.AfterShadowMap,after);}
        before.Release();after.Release();before=null;after=null;
    }
}
