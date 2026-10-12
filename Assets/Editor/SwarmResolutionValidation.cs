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

/// <summary>Same-pose resolution comparisons, full-sphere retention and physical scale checks.
/// Uses a disposable project and no tracking input. Capture readbacks never run in the player.</summary>
public static class SwarmResolutionValidation
{
    private static readonly string Output = "/Users/pavan/src/ledpanelVR/Builds/BogongSwarm-resolution";
    private static int checks;
    private static readonly List<object> results=new List<object>();
    private static void Check(bool ok,string message) { checks++; if(!ok) throw new Exception(message); }
    private static readonly string[] Faces={"F","R","B","L","U","D"};
    private static readonly Vector3[] Directions={Vector3.forward,Vector3.right,Vector3.back,Vector3.left,Vector3.up,Vector3.down};
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var recipe=KannadiConfig.Load(new Dictionary<string,object> { ["configFile"]="Examples/Swarm/selwyn-trajectory-playback.example.json" });
        var data=SwarmTrajectoryData.Load(Path.Combine(Application.streamingAssetsPath,recipe.trajectoryFile));
        string failure=null;
        try
        {
            Check(Math.Abs(data.Centroid.y-4.274847073185945)<1e-5,"Corrected source +Z down reaches the Unity parser.");
            Check(recipe.worldUnitsPerMetre==1,"Recorded scene uses metres, not centimetres.");
            var color=recipe.uniformSkyColor;
            Check(new[]{color.r,color.g,color.b}.All(c=>c>=0 && c<=1),"Diagnostic uses the same valid normalized sky as the active scene.");
            var sky=new Color(color.r,color.g,color.b,1);
            Population(data,recipe,0,5,"stationary",sky);
            Population(data,recipe,5,0,"flying",sky);
            PhysicalScale(recipe.bogongVisual,sky);
        }
        catch(Exception error) {failure=error.ToString();Debug.LogError(failure);}
        File.WriteAllText(Path.Combine(Output,"resolution-validation.json"),JsonConvert.SerializeObject(new {
            checks,failure,unity=Application.unityVersion,graphics=SystemInfo.graphicsDeviceName,
            visual=recipe.bogongVisual,requestedSky=recipe.uniformSkyColor,results,
            note="1024px is a resolution diagnostic, not a calibrated human-vision simulation. Native 64/1024 images share camera position, orientation, 90-degree FOV, world positions and wing phase. Stationary diagnostic uses 5s extrapolated lead-in; active 5m/s flight uses zero. No extra births, spatial compression, minimum opacity, or moth-size boost. Readbacks and image encoding are editor validation only."
        },Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
    private static Camera Camera(Color sky)
    {
        var root=new GameObject("Resolution camera"); var camera=root.AddComponent<Camera>();camera.enabled=false;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=sky;
        camera.allowHDR=false;camera.allowMSAA=false;camera.nearClipPlane=.001f;camera.farClipPlane=150;
        camera.fieldOfView=90;camera.aspect=1;camera.cullingMask=1<<29;
        return camera;
    }
    private static object Render(Camera camera,int size,string filename,bool save)
    {
        var target=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;
        try
        {
            camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,size,size),0,0);texture.Apply();
            var pixels=texture.GetPixels32();int sky=((Color32)camera.backgroundColor).r;
            int peak=pixels.Max(p=>Math.Abs(sky-p.r)),total=pixels.Sum(p=>Math.Abs(sky-p.r));
            int changed=pixels.Count(p=>p.r!=sky),three=pixels.Count(p=>Math.Abs(sky-p.r)>=3);
            Check(pixels.All(p=>p.a==255),"Resolution capture preserves opaque panel compositing.");
            if(save) File.WriteAllBytes(Path.Combine(Output,filename+".png"),texture.EncodeToPNG());
            return new {size,sky,absoluteContrast=true,peakContrastBytes=peak,totalContrastBytes=total,changedPixels=changed,pixelsAtLeastThreeBytes=three,
                image=save?filename+".png":null};
        }
        finally {RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);}
    }
    private static void Population(SwarmTrajectoryData data,KannadiConfig recipe,float observerSpeed,float preRoll,string name,Color sky)
    {
        var animal=new GameObject("Diagnostic animal");animal.transform.position=Vector3.up*5;
        var host=new GameObject("Diagnostic moth batch");host.layer=29;
        var roots=new List<GameObject>();for(int i=0;i<data.Tracks.Length;i++) {var r=new GameObject("Diagnostic_ID_pool");r.layer=29;roots.Add(r);}
        var settings=JsonConvert.DeserializeObject<SwarmPlaybackConfig>(JsonConvert.SerializeObject(recipe.playback));
        settings.startTimeSeconds=0;settings.speed=1;settings.loop=false;settings.preRollSeconds=preRoll;
        var batch=host.AddComponent<BogongSwarmRenderer>();batch.Configure(roots,recipe.bogongVisual,1);
        var material=host.GetComponent<MeshRenderer>().sharedMaterial;material.SetFloat("_ManualAnimation",1);
        var camera=Camera(sky);
        var samples=new List<object>();var seen=new HashSet<string>();int maxLive=0;double liveSum=0,near3Sum=0,behindSum=0;int steps=0;float maximumDistanceSquared=0;
        Func<GameObject> grow=()=> {var r=new GameObject("Diagnostic_ID_pool");r.layer=29;roots.Add(r);return r;};
        var stream=new SwarmTrajectoryStream(data,settings,roots,animal.transform,Vector3.zero,
            Quaternion.Euler(0,settings.yawOffsetDegrees,0),1,animal.transform.position,grow,null);
        try
        {
            double[] captures={30,268.4963751630769};int captureIndex=0;
            for(int step=0;step<=(int)Math.Ceiling(data.Duration/.02);step++)
            {
                double time=Math.Min(step*.02,data.Duration);
                if(captureIndex<captures.Length && captures[captureIndex]<time)
                {
                    double exact=captures[captureIndex++];animal.transform.position=new Vector3(0,5,observerSpeed*(float)exact);stream.Advance(exact);
                    Snapshot(camera,batch,material,roots,animal.transform,exact,name,samples);
                }
                animal.transform.position=new Vector3(0,5,observerSpeed*(float)time);stream.Advance(time);
                int live=0,near3=0,behind=0;
                foreach(var root in roots)
                {
                    if(!root.activeSelf) continue;live++;
                    seen.Add(root.name.Substring(root.name.IndexOf("_ID_",StringComparison.Ordinal)+4).Split(new[]{"_C"},StringSplitOptions.None)[0]);
                    Vector3 relative=root.transform.position-animal.transform.position;
                    if(relative.z<0) behind++;
                    if(relative.sqrMagnitude<9) near3++;
                    maximumDistanceSquared=Mathf.Max(maximumDistanceSquared,relative.sqrMagnitude);
                }
                steps++;liveSum+=live;near3Sum+=near3;behindSum+=behind;maxLive=Math.Max(maxLive,live);
            }
            Check(seen.Count==430 && stream.TotalBirths==437,"Every measured ID/segment is retained in the full-sphere replay.");
            Check(maximumDistanceSquared<=10000.1f,"Live moths remain within the configured 100m retirement distance.");
            Check(behindSum>0,"Moths behind the observer are not culled by playback.");
            results.Add(new {scenario=name,observerSpeedMetresPerSecond=observerSpeed,preRollSeconds=preRoll,
                sourceIdsSeen=seen.Count,totalBirths=stream.TotalBirths,meanLivePopulation=liveSum/steps,maxLive,
                meanWithin3Metres=near3Sum/steps,meanBehindObserver=behindSum/steps,snapshots=samples});
        }
        finally {Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(host);foreach(var root in roots) Object.DestroyImmediate(root);Object.DestroyImmediate(animal);}
    }
    private static void Snapshot(Camera camera,BogongSwarmRenderer batch,Material material,List<GameObject> roots,Transform animal,double time,string name,List<object> samples)
    {
        batch.RefreshCenters();material.SetFloat("_AnimationTime",(float)time);camera.transform.position=animal.position;
        var active=roots.Where(r=>r.activeSelf).ToArray();var images=new List<object>();
        for(int face=0;face<Faces.Length;face++)
        {
            camera.transform.rotation=Quaternion.LookRotation(Directions[face],face>=4?Vector3.forward:Vector3.up);
            string prefix=name+"-"+(time<100?"ordinary":"close")+"-"+Faces[face];
            images.Add(new {face=Faces[face],renders=new[]{Render(camera,64,prefix+"-64",true),Render(camera,1024,prefix+"-1024",true)}});
            if((face==0 && time<100) || (face==4 && time>100))
            {
                Color original=camera.backgroundColor;camera.backgroundColor=Color.gray;
                images.Add(new {face=Faces[face],control="Mid-gray sky; otherwise identical",renders=new[]{
                    Render(camera,64,prefix+"-mid-gray-64",true),Render(camera,1024,prefix+"-mid-gray-1024",true)}});
                camera.backgroundColor=original;
            }
        }
        samples.Add(new {timeSeconds=time,live=active.Length,within3Metres=active.Count(r=>Vector3.Distance(r.transform.position,animal.position)<3),
            nearestMetres=active.Min(r=>Vector3.Distance(r.transform.position,animal.position)),
            behind=active.Count(r=>r.transform.position.z<animal.position.z),images});
    }
    private static void PhysicalScale(BogongVisualConfig visual,Color sky)
    {
        var host=new GameObject("Physical moth batch");host.layer=29;var animal=new GameObject("Physical moth");animal.layer=29;
        var batch=host.AddComponent<BogongSwarmRenderer>();batch.Configure(new[]{animal},visual,1);
        var material=host.GetComponent<MeshRenderer>().sharedMaterial;material.SetFloat("_ManualAnimation",1);
        float phase=host.GetComponent<MeshFilter>().sharedMesh.uv2[0].x;
        var camera=Camera(sky);var images=new List<object>();
        try
        {
            Check(Math.Abs(material.GetFloat("_BodyLength")-visual.bodyLengthMetres)<1e-7 &&
                Math.Abs(material.GetFloat("_WingSpan")-visual.wingSpanMetres)<1e-7,"Moth dimensions reach GPU in physical metres.");
            animal.transform.rotation=Quaternion.Euler(0,90,0);
            foreach(float distance in new[]{.177f,.5f,1f,3f,10f,30f})
            {
                // Pixel-center alignment reduces accidental four-way dilution in this controlled diagnostic.
                animal.transform.position=new Vector3(distance/64,distance/64,distance);batch.RefreshCenters();
                foreach(var flap in new[]{new{label="up",cycle=.25f},new{label="down",cycle=.75f}})
                {
                    material.SetFloat("_AnimationTime",(flap.cycle-phase)/30f);
                    string prefix="physical-"+distance.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"-"+flap.label;
                    images.Add(new {distanceMetres=distance,flap=flap.label,renders=new[]{Render(camera,64,prefix+"-64",true),Render(camera,1024,prefix+"-1024",true)}});
                }
            }
            animal.transform.position=Vector3.zero;animal.transform.rotation=Quaternion.identity;batch.RefreshCenters();
            camera.transform.position=new Vector3(.06f,.06f,-.12f);camera.transform.LookAt(Vector3.zero);camera.fieldOfView=35;
            foreach(var flap in new[]{new{label="up",cycle=.25f},new{label="down",cycle=.75f}})
            {
                material.SetFloat("_AnimationTime",(flap.cycle-phase)/30f);
                images.Add(new {detail="One coupled wing pair",flap=flap.label,render=Render(camera,1024,"coupled-pair-"+flap.label,true)});
            }
            results.Add(new {scenario="physical-broadside",bodyLengthMetres=visual.bodyLengthMetres,bodyDiameterMetres=visual.bodyDiameterMetres,
                wingSpanMetres=visual.wingSpanMetres,images});
        }
        finally {Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(host);Object.DestroyImmediate(animal);}
    }
}
#endif
