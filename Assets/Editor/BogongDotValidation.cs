#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Raster-level checks at real panel resolution; only run in a disposable project.</summary>
public static class BogongDotValidation
{
    private static int checks;
    private static readonly List<object> samples = new List<object>();
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))
            throw new InvalidOperationException("Use a disposable validation project.");
        string failure=null;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        try
        {
            var camera=new GameObject("64-pixel stimulus camera").AddComponent<Camera>();
            camera.enabled=false; camera.fieldOfView=90; camera.aspect=1;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            camera.nearClipPlane=.01f; camera.farClipPlane=2000; camera.cullingMask=1<<28;
            camera.allowHDR=false;
            // Deliberately misleading main camera and coloured illumination.
            var decoy=new GameObject("Wrong main camera").AddComponent<Camera>();
            decoy.tag="MainCamera"; decoy.transform.position=new Vector3(0,0,-500);
            var light=new GameObject("Coloured light").AddComponent<Light>();
            light.type=LightType.Directional; light.color=Color.blue; light.intensity=10;
            RenderSettings.fog=true; RenderSettings.fogColor=Color.green; RenderSettings.fogDensity=1;
            var marker=GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.layer=28;
            var dot=marker.AddComponent<Bogong>();
            // Edit-mode validation invokes the same subscription used on enabling at runtime.
            dot.SendMessage("OnDisable"); dot.SendMessage("OnEnable");
            var config=new BogongVisualConfig { sizeMode="Angular", angularSizeDegrees=2,
                color=new ColorConfig { r=1,g=.5f,b=.5f,a=1 } };
            dot.Configure(config);
            Quaternion heading=Quaternion.Euler(-5,60,0); marker.transform.rotation=heading;
            foreach (int resolution in new[] {64,128})
            foreach (float distance in new[] {5f,50f})
            foreach (float x in new[] {.5f+.5f/resolution,.8f,.999f,1.005f})
            {
                Vector3 direction=camera.ViewportPointToRay(new Vector3(x,.5f+.5f/resolution,0)).direction;
                marker.transform.position=camera.transform.position+direction*distance;
                RenderAndCheck(camera,marker,config,resolution,1,"angular");
            }
            marker.transform.position=new Vector3(.078125f,.078125f,10);
            var other=new GameObject("Another rig camera").AddComponent<Camera>();
            other.CopyFrom(camera); other.transform.position=new Vector3(0,0,-80);
            RenderAndCheck(other,marker,config,64,1,"other-camera");
            RenderAndCheck(camera,marker,config,64,1,"original-camera-again");
            // Multisample targets must not add shaded/partially transparent colour values.
            RenderAndCheck(camera,marker,config,64,4,"msaa");
            config.sizeMode="World"; config.size=2; dot.Configure(config);
            marker.transform.position=new Vector3(0,0,5);
            int near=RenderAndCheck(camera,marker,config,64,1,"world-near");
            marker.transform.position=new Vector3(0,0,10);
            int far=RenderAndCheck(camera,marker,config,64,1,"world-far");
            Check(near>3*far,"World-size dot shrinks with distance.");
            config.angularSizeDegrees=20; config.sizeMode="Angular"; dot.Configure(config);
            RenderAndCheck(camera,marker,config,64,1,"large-solid-circle");
            Check(Quaternion.Angle(marker.transform.rotation,heading)<.001f,"Billboarding preserves movement heading.");
            var renderer=marker.GetComponent<Renderer>();
            config.flickerFrequencyHz=5; config.flickerDutyCycle=0; dot.Configure(config);
            Check(!renderer.enabled,"Zero-duty flicker hides the marker.");
            config.flickerFrequencyHz=0; dot.Configure(config);
            Check(renderer.enabled,"Disabling flicker restores solid visibility.");
            dot.SendMessage("OnDisable");
            ValidateBatch(camera, marker);
            Object.DestroyImmediate(marker); Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(other.gameObject); Object.DestroyImmediate(decoy.gameObject); Object.DestroyImmediate(light.gameObject);
        }
        catch (Exception error) { failure=error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(Application.dataPath,"../bogong-dot-validation.json"),
            JsonConvert.SerializeObject(new {checks,failure,samples,unity=Application.unityVersion},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }

    private static void ValidateBatch(Camera camera, GameObject marker)
    {
        foreach (string sizeMode in new[] { "Angular", "World" })
        foreach (float angle in new[] { 2f, 20f })
        {
            var root = new GameObject("Batched circle reference"); root.layer = marker.layer;
            var config = new BogongVisualConfig { sizeMode = sizeMode, size = 2, angularSizeDegrees = angle,
                color = new ColorConfig { r = 1, g = .5f, b = .5f, a = 1 } };
            var batch = root.AddComponent<BogongSwarmRenderer>();
            batch.Configure(new[] { marker }, config);
            batch.SendMessage("OnDisable"); batch.SendMessage("OnEnable");
            foreach (int resolution in new[] { 64, 128 })
            foreach (float distance in new[] { 5f, 50f })
            foreach (float x in new[] { .5f + .5f / resolution, .999f, 1.005f })
            {
                marker.transform.position = camera.transform.position + camera.ViewportPointToRay(new Vector3(x, .5f + .5f / resolution, 0)).direction * distance;
                batch.RefreshCenters();
                RenderAndCheck(camera, marker, config, resolution, 1, "batch-" + sizeMode);
            }
            batch.SendMessage("OnDisable"); Object.DestroyImmediate(root);
        }
    }

    private static int RenderAndCheck(Camera camera, GameObject marker, BogongVisualConfig config, int resolution, int msaa, string label)
    {
        var target=new RenderTexture(resolution,resolution,24,RenderTextureFormat.ARGB32) { antiAliasing=msaa };
        target.Create(); camera.targetTexture=target; camera.rect=new Rect(0,0,1,1); camera.aspect=1; camera.allowMSAA=true;
        var capture=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false);
        RenderTexture previous=RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active=target;
            capture.ReadPixels(new Rect(0,0,resolution,resolution),0,0); capture.Apply();
            Color32[] pixels=capture.GetPixels32();
            Color32 expected=new Color(config.color.r,config.color.g,config.color.b,1);
            Vector3 offset=marker.transform.position-camera.transform.position;
            float halfAngle=config.sizeMode=="Angular" ? .5f*config.angularSizeDegrees*Mathf.Deg2Rad : Mathf.Atan(.5f*config.size/offset.magnitude);
            double cosine=Math.Cos(halfAngle);
            int actualCount=0,expectedCount=0;
            for(int y=0;y<resolution;y++) for(int x=0;x<resolution;x++)
            {
                var pixel=pixels[y*resolution+x];
                bool filled=pixel.r>0 || pixel.g>0 || pixel.b>0;
                if(filled)
                {
                    actualCount++;
                    Check(Math.Abs(pixel.r-expected.r)<=1 && Math.Abs(pixel.g-expected.g)<=1 && Math.Abs(pixel.b-expected.b)<=1,
                        label+": every covered pixel must be the configured solid RGB, got "+pixel);
                }
                Vector3 ray=camera.ViewportPointToRay(new Vector3((x+.5f)/resolution,(y+.5f)/resolution,0)).direction;
                bool inside=Vector3.Dot(ray,offset.normalized)>=cosine;
                if(inside) expectedCount++;
            }
            Check(actualCount==expectedCount,label+": rendered pixel area matches independent angular cone; actual="+actualCount+", expected="+expectedCount);
            samples.Add(new {label,resolution,msaa,distance=offset.magnitude,actualCount,expectedCount});
            if(label=="large-solid-circle" || (label=="angular" && resolution==64 && actualCount==1))
                File.WriteAllBytes(Path.Combine(Application.dataPath,"../bogong-"+label+".png"),capture.EncodeToPNG());
            return actualCount;
        }
        finally
        {
            camera.targetTexture=null; RenderTexture.active=previous;
            target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(capture);
        }
    }
}
#endif
