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
            Check(design["steps"].Count()==12&&(int)design["repetitions"]==1,"Production contains one complete twelve-condition block");
            Check((double)design["intertrial"]["trigger"]["seconds"]==5,"Production intertrial is five seconds");
            foreach(JObject step in design["steps"].Take(9)) {
                Check((double)step["trigger"]["timeoutSeconds"]==120 && step["objects"].Count()==2,"Bar condition uses two bars and 120-second timeout: "+step["name"]);
                foreach(JObject bar in step["objects"])Check((double)bar["polar"]["radius"]==60&&Math.Abs((double)bar["polar"]["angle"])==20&&(double)bar["visualAngleDegrees"]==10&&bar["color"].Take(3).All(v=>(double)v==0),"Fixed black scaling bar geometry: "+step["name"]);
            }
            foreach(var noBars in new[] {design["steps"][9],design["steps"][11]})
                Check(!noBars["objects"].Any()&&(string)noBars["trigger"]["type"]=="time"&&(double)noBars["trigger"]["seconds"]==20,"No-bars condition uses only a 20-second timer: "+noBars["name"]);
            var noPathBars=design["steps"][10];
            Check(!(bool)noPathBars["pathGround"]["pathVisible"]&&JToken.DeepEquals(noPathBars["objects"],design["steps"][3]["objects"])&&JToken.DeepEquals(noPathBars["trigger"],design["steps"][3]["trigger"]),
                  "Two-bars no-path control retains the same goals, bars and 120-second contact/timeout trigger");
            Check(!(bool)design["steps"][11]["pathGround"]["pathVisible"],"Blank-ground control hides the path");
            Check(JsonConvert.DeserializeObject<PathGroundConfig>("{}").pathVisible,"Old configurations retain visible paths");
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
            config.pathVisible=false;ground=PathGround.Create(root.transform,0,config,appearance,false);
            var noPath=Render(camera);Object.DestroyImmediate(ground.gameObject);
            config.pathVisible=true;float savedDarkness=appearance.pathDarkness;appearance.pathDarkness=0;
            ground=PathGround.Create(root.transform,0,config,appearance,false);var grassOnly=Render(camera);Object.DestroyImmediate(ground.gameObject);appearance.pathDarkness=savedDarkness;
            Check(noPath.GetPixels32().SequenceEqual(grassOnly.GetPixels32()),"No-path rendering preserves grass exactly and removes every painted path pixel");
            Check(noPath.GetPixel(128,154).grayscale>narrow.GetPixel(128,154).grayscale*1.3f,"No-path ground is visibly unmarked at the former stem");
            Object.DestroyImmediate(noPath);Object.DestroyImmediate(grassOnly);
            Object.DestroyImmediate(narrow);Object.DestroyImmediate(wide);
            // Zoom onto the straight stem: physical feathering must soften both sides
            // of the nominal boundary without changing the centre or distant grass.
            camera.orthographicSize=10;camera.transform.position=new Vector3(0,200,20);
            appearance.pathWidthCm=4;appearance.groundBrightness=1;
            ground=PathGround.Create(root.transform,0,config,appearance,false);
            var hardEdge=Render(camera);Object.DestroyImmediate(ground.gameObject);
            appearance.pathEdgeSoftnessCm=.8f;
            ground=PathGround.Create(root.transform,0,config,appearance,false);
            var softEdge=Render(camera);Object.DestroyImmediate(ground.gameObject);
            Check(softEdge.GetPixel(151,128).grayscale>hardEdge.GetPixel(151,128).grayscale+.03f &&
                  softEdge.GetPixel(154,128).grayscale<hardEdge.GetPixel(154,128).grayscale-.03f,
                  "Physical edge fade blends inside and outside the nominal boundary");
            Check(Math.Abs(softEdge.GetPixel(128,128).grayscale-hardEdge.GetPixel(128,128).grayscale)<.01f &&
                  Math.Abs(softEdge.GetPixel(174,128).grayscale-hardEdge.GetPixel(174,128).grayscale)<.01f,
                  "Edge fade preserves the dark centre and distant grass");
            appearance.pathEdgeRoughnessCm=.5f;appearance.pathMottling=.04f;
            ground=PathGround.Create(root.transform,0,config,appearance,false);
            var irregular=Render(camera);var repeat=Render(camera);Object.DestroyImmediate(ground.gameObject);
            Check(Enumerable.Range(0,256).Any(y=>Math.Abs(irregular.GetPixel(154,y).grayscale-softEdge.GetPixel(154,y).grayscale)>.01f),
                  "Edge irregularity changes the rendered contour");
            Check(irregular.GetPixels32().SequenceEqual(repeat.GetPixels32()),
                  "Painted irregularities remain identical across repeated renders");
            Object.DestroyImmediate(irregular);Object.DestroyImmediate(repeat);
            Object.DestroyImmediate(hardEdge);Object.DestroyImmediate(softEdge);
            camera.orthographicSize=100;camera.transform.position=new Vector3(0,200,0);
            Check(JsonConvert.DeserializeObject<PathGroundAppearance>("{}").pathEdgeSoftnessCm==0,
                  "Old appearance records retain the historical edge by default");
            var oldAppearance=JsonConvert.DeserializeObject<PathGroundAppearance>("{}");
            Check(oldAppearance.pathEdgeRoughnessCm==0&&oldAppearance.pathMottling==0,
                  "Old records do not acquire painted irregularities or mottling");
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
            // Exercise the acquisition snapshot and original-controller replay together.
            var replayHost=new GameObject("Pilot replay validation");SceneManager.MoveGameObjectToScene(replayHost,scene);
            var controller=replayHost.AddComponent<DynamicSequenceController>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(DynamicSequenceController).GetField("pilotAppearance",flags).SetValue(controller,appearance);
            var stepType=typeof(DynamicSequenceController).GetNestedType("Step",System.Reflection.BindingFlags.NonPublic);
            var replayStep=design["steps"].Last().ToObject(stepType);
            var recorded=(JObject)typeof(DynamicSequenceController).GetMethod("CaptureStepConfiguration",flags).Invoke(controller,new[]{replayStep});
            Check((string)recorded["pathGroundAppearance"]["texturePath"]==image,"Acquisition snapshot retains effective pilot ground appearance");
            var replayRoot=controller.RecreateRecordedStep("VR1",recorded,replayHost.transform,null);
            var replayGround=replayRoot.GetComponentInChildren<PathGround>();
            Check(replayGround!=null&&replayGround.GetComponentsInChildren<Collider>().Length==0,"Original-controller replay recreates the visual pilot ground without colliders");
            var replayMaterial=replayGround.GetComponent<Renderer>().sharedMaterial;
            Check(!(bool)recorded["pathGround"]["pathVisible"]&&replayMaterial.GetFloat("_PathVisible")==0,"No-path visibility is archived and restored in replay");
            Check(Math.Abs((float)recorded["pathGroundAppearance"]["pathEdgeSoftnessCm"]-.8f)<.001 &&
                  Math.Abs(replayMaterial.GetFloat("_PathEdgeSoftness")-.8f)<.001,
                  "Acquisition and replay preserve physical edge softness");
            Check(Math.Abs((float)recorded["pathGroundAppearance"]["pathEdgeRoughnessCm"]-.5f)<.001 &&
                  Math.Abs(replayMaterial.GetFloat("_PathEdgeRoughness")-.5f)<.001 &&
                  Math.Abs((float)recorded["pathGroundAppearance"]["pathMottling"]-.04f)<.001 &&
                  Math.Abs(replayMaterial.GetFloat("_PathMottling")-.04f)<.001,
                  "Acquisition and replay preserve edge irregularity and mottling");
            Check(Math.Abs(replayMaterial.GetFloat("_PathWidth")-appearance.pathWidthCm)<.001&&replayMaterial.GetTexture("_GroundTexture")!=null,"Replay restores recorded width and supplied texture");
            var replayTarget=replayRoot.GetComponent<RecordedWorldStateTarget>();
            var checkpoint=replayTarget.Capture(true);replayTarget.Apply(checkpoint);
            Check(replayTarget.Configuration.ToString()==recorded.ToString(),"Pilot ground survives world checkpoint capture and restore");
            Object.DestroyImmediate(replayHost);
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
            foreach(float bad in new[] {-1f,5f,float.NaN,float.PositiveInfinity}) {
                bool rejected=false;try{new PathGroundAppearance {pathEdgeSoftnessCm=bad}.Validate();}catch(ArgumentException){rejected=true;}
                Check(rejected,"Invalid edge softness rejected: "+bad);
            }
            foreach(float bad in new[] {-1f,2f,float.NaN,float.PositiveInfinity}) {
                bool rejected=false;try{new PathGroundAppearance {pathEdgeRoughnessCm=bad}.Validate();}catch(ArgumentException){rejected=true;}
                Check(rejected,"Invalid edge irregularity rejected: "+bad);
                rejected=false;try{new PathGroundAppearance {pathMottling=bad}.Validate();}catch(ArgumentException){rejected=true;}
                Check(rejected,"Invalid mottling rejected: "+bad);
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
