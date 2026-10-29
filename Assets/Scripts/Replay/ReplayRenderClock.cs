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
    private JObject clock;
    private static readonly string[] Names={"_Time","_SinTime","_CosTime","_TimeParameters","unity_DeltaTime"};
    private static readonly CameraEvent[] Events = { CameraEvent.BeforeDepthTexture, CameraEvent.BeforeDepthNormalsTexture,
        CameraEvent.BeforeGBuffer,CameraEvent.BeforeReflections,CameraEvent.BeforeLighting,
        CameraEvent.BeforeSkybox, CameraEvent.BeforeForwardOpaque, CameraEvent.BeforeForwardAlpha };
    public static JObject CaptureFrameClock() => Clock(Time.timeSinceLevelLoad,Time.deltaTime,Time.smoothDeltaTime);
    internal static JObject Clock(float seconds,float delta,float smooth)
    {
        JArray V(float x,float y,float z,float w)=>new JArray(x,y,z,w);
        return new JObject {
            ["_Time"]=V(seconds/20f,seconds,seconds*2,seconds*3),
            ["_SinTime"]=V(Mathf.Sin(seconds/8),Mathf.Sin(seconds/4),Mathf.Sin(seconds/2),Mathf.Sin(seconds)),
            ["_CosTime"]=V(Mathf.Cos(seconds/8),Mathf.Cos(seconds/4),Mathf.Cos(seconds/2),Mathf.Cos(seconds)),
            ["_TimeParameters"]=V(seconds,Mathf.Sin(seconds),Mathf.Cos(seconds),0),
            ["unity_DeltaTime"]=V(delta,delta>0?1/delta:0,smooth,smooth>0?1/smooth:0) };
    }
    public void SetClock(float seconds,float delta,float smooth) => SetClock(Clock(seconds,delta,smooth));
    public void SetClock(JObject recorded)
    {
        clock=recorded;
        if(target==null)target=GetComponent<Camera>();
        if(commands==null)
        {
            commands=new CommandBuffer{name="Recorded experiment shader clock"};
            restore=new CommandBuffer{name="Restore engine shader clock"};
            foreach(var stage in Events)target.AddCommandBuffer(stage,commands);
            target.AddCommandBuffer(CameraEvent.AfterEverything,restore);
        }
        commands.Clear();Set(commands,clock);
        foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(light.type==LightType.Directional&&light.enabled)
            {
                var sun=light.GetComponent<RecordedSunClock>()??light.gameObject.AddComponent<RecordedSunClock>();
                sun.SetClock(clock);
            }
        RefreshRestore();
    }
    internal static void Set(CommandBuffer buffer,JObject values)
    {
        foreach(var name in Names)
        {
            var v=values[name] as JArray;
            if(v==null||v.Count!=4)throw new System.IO.InvalidDataException("Recorded shader clock is missing "+name);
            buffer.SetGlobalVector(name,new Vector4((float)v[0],(float)v[1],(float)v[2],(float)v[3]));
        }
    }
    private void RefreshRestore()
    {
        if(restore==null)return;
        restore.Clear();Set(restore,Clock(Time.time,Time.deltaTime,Time.smoothDeltaTime));
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
        if(before==null)
        {
            before=new CommandBuffer{name="Recorded experiment shadow clock"};
            after=new CommandBuffer{name="Restore engine shadow clock"};
            sun.AddCommandBuffer(LightEvent.BeforeShadowMap,before);sun.AddCommandBuffer(LightEvent.AfterShadowMap,after);
        }
        before.Clear();ReplayRenderClock.Set(before,clock);
        after.Clear();ReplayRenderClock.Set(after,ReplayRenderClock.Clock(Time.time,Time.deltaTime,Time.smoothDeltaTime));
    }
    private void OnDestroy()
    {
        if(before==null)return;
        if(sun!=null){sun.RemoveCommandBuffer(LightEvent.BeforeShadowMap,before);sun.RemoveCommandBuffer(LightEvent.AfterShadowMap,after);}
        before.Release();after.Release();before=null;after=null;
    }
}
