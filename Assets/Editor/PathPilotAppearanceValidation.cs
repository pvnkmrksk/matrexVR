#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class PathPilotAppearanceValidation
{
    static readonly List<string> checks=new();
    static void Check(bool condition,string message) {if(!condition)throw new Exception(message);checks.Add(message);}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../ANT_PILOT_VALIDATION_COPY")))throw new Exception("Isolated validation copy required.");
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../validation-output/appearance"));
        Directory.CreateDirectory(output);
        string failure=null;
        Scene scene=default;
        GameObject root=null;
        try {
            var design=JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Experiments/PathAnglePilot/design.json")));
            Check((double)design["sessionSeconds"]==10800,"Production session deadline is three hours");
            Check(design["steps"].Count()==10&&(int)design["repetitions"]==1,"Production contains one complete ten-condition block");
            Check((double)design["intertrial"]["trigger"]["seconds"]==5,"Production intertrial is five seconds");
            foreach(JObject step in design["steps"].Take(9)) {
                Check((double)step["trigger"]["timeoutSeconds"]==120 && step["objects"].Count()==2,"Bar condition uses two bars and 120-second timeout: "+step["name"]);
                foreach(JObject bar in step["objects"])Check((double)bar["polar"]["radius"]==60&&Math.Abs((double)bar["polar"]["angle"])==20&&(double)bar["visualAngleDegrees"]==10&&bar["color"].Take(3).All(v=>(double)v==0),"Fixed black scaling bar geometry: "+step["name"]);
            }
            var noBars=design["steps"].Last();Check(!noBars["objects"].Any()&&(string)noBars["trigger"]["type"]=="time"&&(double)noBars["trigger"]["seconds"]==20,"Production no-bars condition uses only a 20-second timer");
            PathPilotPreview.ExportImages(Path.Combine(output,"previews"));
            scene=EditorSceneManager.NewPreviewScene();root=new GameObject("Ground validation");SceneManager.MoveGameObjectToScene(root,scene);
            var cameraObject=new GameObject("Camera");cameraObject.transform.SetParent(root.transform);var camera=cameraObject.AddComponent<Camera>();
            camera.scene=scene;camera.enabled=false;camera.orthographic=true;camera.orthographicSize=100;camera.aspect=1;camera.nearClipPlane=.01f;camera.farClipPlane=500;
            camera.transform.SetPositionAndRotation(new Vector3(0,200,0),Quaternion.Euler(90,0,0));
            var appearance=new PathGroundAppearance {extentCm=200};var config=new PathGroundConfig();
            var ground=PathGround.Create(root.transform,0,config,appearance,false);
            var narrow=Render(camera);Object.DestroyImmediate(ground.gameObject);
            appearance.pathWidthCm=10;ground=PathGround.Create(root.transform,0,config,appearance,false);var wide=Render(camera);Object.DestroyImmediate(ground.gameObject);
            // 256px maps -100..100 cm. x=4,z=20 lies outside a 4cm path and inside a 10cm path.
            Check(wide.GetPixel(133,154).grayscale<narrow.GetPixel(133,154).grayscale*.65f,"Width control changes rendered path coverage");
            Object.DestroyImmediate(narrow);Object.DestroyImmediate(wide);
            var sample=new Texture2D(4,4,TextureFormat.RGB24,false);
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)sample.SetPixel(x,y,x>=2?(y>=2?Color.red:Color.blue):Color.green);
            sample.Apply();string image=Path.Combine(output,"supplied-test-texture.png");File.WriteAllBytes(image,sample.EncodeToPNG());Object.DestroyImmediate(sample);
            appearance.texturePath=image;appearance.preparedGroundImage=true;appearance.groundBrightness=.05f;
            ground=PathGround.Create(root.transform,0,config,appearance,false);var prepared=Render(camera);Object.DestroyImmediate(ground.gameObject);
            Color topRight=prepared.GetPixel(220,220),bottomRight=prepared.GetPixel(220,36);
            Check(topRight.r>.8&&topRight.g<.2&&bottomRight.b>.8&&bottomRight.r<.2,"Prepared image maps top to +z and right to +x without applying brightness or painted paths");
            File.WriteAllBytes(Path.Combine(output,"prepared-ground.png"),prepared.EncodeToPNG());Object.DestroyImmediate(prepared);
            appearance.preparedGroundImage=false;appearance.groundBrightness=.8f;
            ground=PathGround.Create(root.transform,0,config,appearance,false);var tiled=Render(camera);Object.DestroyImmediate(ground.gameObject);
            Check(tiled.GetPixels().Any(p=>p.r>p.g+.2)&&tiled.GetPixels().Any(p=>p.g>p.r+.2),"Supplied grass image is rendered in tiled mode");
            File.WriteAllBytes(Path.Combine(output,"tiled-ground.png"),tiled.EncodeToPNG());Object.DestroyImmediate(tiled);
            // Per-condition imagery overrides a bad shared reference; same API as runtime trials.
            appearance.texturePath="missing-shared-image.png";config.preparedTexturePath=image;
            ground=PathGround.Create(root.transform,0,config,appearance,false);Object.DestroyImmediate(ground.gameObject);
            Check(true,"Per-condition prepared texture overrides shared grass reference");
            string archiveDirectory=Path.Combine(output,"archive-check");Directory.CreateDirectory(archiveDirectory);
            var archive=new RunConfigArchive(archiveDirectory,Application.streamingAssetsPath,"texture-test",Application.unityVersion);
            byte[] archived=archive.ReadBytes(image,"pilot-ground-texture","test");archive.RequireComplete();
            Check(archived.SequenceEqual(File.ReadAllBytes(image)),"Texture archive preserves exact supplied bytes");
            foreach(float bad in new[] {0f,float.NaN,float.PositiveInfinity}) {
                bool rejected=false;try{new PathGroundAppearance {pathWidthCm=bad}.Validate();}catch(ArgumentException){rejected=true;}
                Check(rejected,"Invalid path width rejected: "+bad);
            }
        }catch(Exception e){failure=e.ToString();}
        finally {if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
        File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new {passed=failure==null,failure,checks,unity=Application.unityVersion},Formatting.Indented));
        if(failure!=null)throw new Exception(failure);
    }
    static Texture2D Render(Camera camera)
    {
        var target=new RenderTexture(256,256,24);var previous=RenderTexture.active;
        camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var pixels=new Texture2D(256,256,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
        camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);return pixels;
    }
}
#endif
