#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Audit ten minutes of the real scene's births and native panel visibility.</summary>
public static class SwarmEncounterAudit
{
    private sealed class Panel
    {
        public Camera camera;
        public RenderTexture original;
        public Rect rect;
        public int mask;
    }
    public static object Run(LocustSpawner population, SwarmTrajectoryData data, Action<bool,string> check)
    {
        var replay=population.GetComponent<SwarmTrajectoryPlayback>();
        var animal=population.playbackTarget;
        var batch=population.GetComponentInChildren<BogongSwarmRenderer>();
        var material=batch.GetComponent<MeshRenderer>().sharedMaterial;
        bool loop=population.playbackSettings.loop;
        population.playbackSettings.loop=false;
        Vector3 origin=animal.position;
        var panels=animal.GetComponentsInChildren<Camera>(true)
            .Where(c=>c.enabled && c.name.StartsWith("Main Camera "))
            .Select(c=>new Panel {camera=c,original=c.targetTexture,rect=c.rect,mask=c.cullingMask}).ToArray();
        var targets=new Dictionary<int,RenderTexture>();
        var visible=new Dictionary<int,HashSet<string>> { [64]=new HashSet<string>(),[128]=new HashSet<string>() };
        var measurable=new Dictionary<int,HashSet<string>> { [64]=new HashSet<string>(),[128]=new HashSet<string>() };
        var seen=new HashSet<string>(); var minuteBirths=new List<long>();
        int[] liveAtSeconds={10,30,60,120,300,599}; var live=new Dictionary<int,int>();
        long birthsAtMinute=0;
        int maxLive=0; double sumLive=0; int steps=0;
        var pixels=new Texture2D(128,128,TextureFormat.RGBA32,false);
        try
        {
            foreach(int size in new[] {64,128}) { var rt=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32); rt.Create(); targets[size]=rt; }
            replay.Advance(0);
            int end=(int)Math.Ceiling(data.Duration/.02);
            material.SetFloat("_ManualAnimation",1);
            for(int step=0;step<=end;step++)
            {
                double time=Math.Min(step*.02,data.Duration);
                animal.position=origin+Vector3.forward*(5*(float)time); replay.Advance(time);
                int count=replay.ActiveCount; maxLive=Math.Max(maxLive,count); sumLive+=count; steps++;
                foreach(int second in liveAtSeconds)
                    if(step==second*50) live[second]=count;
                if(step%3000==0) { minuteBirths.Add(replay.TotalBirths); if(step==3000) birthsAtMinute=replay.TotalBirths; }
                if(step%5!=0) continue;
                var active=population.Spawned.Where(a=>a.activeSelf).ToArray();
                foreach(var a in active) seen.Add(a.name.Substring(a.name.IndexOf("_ID_",StringComparison.Ordinal)+4).Split(new[] {"_C"},StringSplitOptions.None)[0]);
                // Render only the first minute. The remaining nine minutes audit all births and retirement.
                if(time>60) continue;
                batch.RefreshCenters(); material.SetFloat("_AnimationTime",(float)time);
                foreach(int size in new[] {64,128})
                {
                    foreach(var panel in panels)
                    {
                        var camera=panel.camera; camera.targetTexture=targets[size]; camera.rect=new Rect(0,0,1,1);
                        camera.cullingMask=1<<batch.gameObject.layer; camera.Render();
                        var previous=RenderTexture.active; RenderTexture.active=targets[size];
                        if(pixels.width!=size) { Object.DestroyImmediate(pixels); pixels=new Texture2D(size,size,TextureFormat.RGBA32,false); }
                        pixels.ReadPixels(new Rect(0,0,size,size),0,0); pixels.Apply(); RenderTexture.active=previous;
                        var colors=pixels.GetPixels32(); int sky=128;
                        foreach(var a in active)
                        {
                            if(Vector3.Distance(a.transform.position,animal.position)>3) continue;
                            Vector3 point=camera.WorldToViewportPoint(a.transform.position);
                            if(point.z<=camera.nearClipPlane || point.x<0 || point.x>=1 || point.y<0 || point.y>=1) continue;
                            int x=(int)(point.x*size), y=(int)(point.y*size), darkest=sky;
                            for(int dy=-2;dy<=2;dy++) for(int dx=-2;dx<=2;dx++)
                            {
                                int px=x+dx,py=y+dy;
                                if(px>=0 && px<size && py>=0 && py<size) darkest=Math.Min(darkest,colors[py*size+px].r);
                            }
                            if(sky-darkest>=1) measurable[size].Add(a.name);
                            if(sky-darkest>=3) visible[size].Add(a.name);
                        }
                    }
                }
            }
            int expectedSegments=data.Tracks.Sum(t=>1+t.samples.Zip(t.samples.Skip(1),(a,b)=>b.time-a.time).Count(dt=>dt>population.playbackSettings.maxGapSeconds+1e-8));
            int expectedFirstMinute=data.Tracks.Sum(t=>t.samples.Where((p,i)=>i==0 || p.time-t.samples[i-1].time>population.playbackSettings.maxGapSeconds+1e-8).Count(p=>p.time<=60));
            check(replay.TotalBirths==expectedSegments,"Ten-minute scene playback emits every recorded segment exactly once.");
            check(seen.Count==data.Tracks.Length,"Every source ID actually becomes active in the real scene.");
            check(birthsAtMinute==expectedFirstMinute,"First-minute birth count matches the CSV schedule.");
            check(live[60]>=20 && maxLive>20,"Moths remain alive after short recordings instead of disappearing at the endpoint.");
            check(visible[128].Count>=30,"At least 30 first-minute births produce measurable 3-byte contrast within 3m in the native 128px panels.");
            return new {sourceIdsSeen=seen.Count,segmentBirths=replay.TotalBirths,firstMinuteBirths=birthsAtMinute,
                cumulativeBirthsAtMinute=minuteBirths,meanLivePopulation=sumLive/steps,maxLivePopulation=maxLive,liveAtSeconds=live,
                firstMinuteNativePanelDetectableWithin3m=visible.ToDictionary(v=>v.Key.ToString(),v=>v.Value.Count),
                firstMinuteNativePanelAnyByteContrastWithin3m=measurable.ToDictionary(v=>v.Key.ToString(),v=>v.Value.Count),
                observerSpeedMetresPerSecond=5,continuationSpeedMetresPerSecond=population.playbackSettings.continuationSpeedMetresPerSecond,
                sampleStepSeconds=.02,renderStepSeconds=.1,
                note="Real four-rig Swarm startup; audit one rig using its configured cameras at native 64/128 pixels, gray sky and current world positions. Contrast requires 3 bytes (any-byte count also reported) in a 5x5 patch around a moth within 3m. Overlapping moths can contribute to the same patch. This is numerical detectability, not a biological visibility threshold. Recorded positions/time/births unchanged; extrapolated tails use the configured physical speed."};
        }
        finally
        {
            foreach(var p in panels) { p.camera.targetTexture=p.original; p.camera.rect=p.rect; p.camera.cullingMask=p.mask; }
            foreach(var rt in targets.Values) { rt.Release(); Object.DestroyImmediate(rt); }
            Object.DestroyImmediate(pixels); material.SetFloat("_ManualAnimation",0); population.playbackSettings.loop=loop;
        }
    }
}
#endif
