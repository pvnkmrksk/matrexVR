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

public static class SixCameraValidation
{
    private static int checks;
    private static readonly Dictionary<char,Vector3> directions=new Dictionary<char,Vector3> {
        {'F',Vector3.forward},{'B',Vector3.back},{'R',Vector3.right},{'L',Vector3.left},{'U',Vector3.up},{'D',Vector3.down}
    };
    private static void Check(bool condition,string message) { checks++; if(!condition) throw new Exception(message); }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a disposable validation copy.");
        string failure=null;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var names=new List<string> {"VR","VR1 Cube","VR2 Cube"};
            for(int i=1;i<=4;i++) names.AddRange(new[] {"VR"+i,"VR"+i+" Swarm","VR"+i+" Kannadi Variant"});
            foreach(string name in names)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Camera Prefabs/"+name+".prefab"));
                Check(instance!=null,name+" prefab loads.");
                CheckRig(instance.GetComponent<ViewportSetter>(), name, !name.Contains("Cube"));
                Object.DestroyImmediate(instance);
            }
            foreach(string scene in new[] {"ControlScene","Choice","Swarm","Kannadi"})
            {
                EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");
                var rigs=Object.FindObjectsByType<ViewportSetter>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                Check(rigs.Length==4,scene+" contains four VR rigs.");
                foreach(var rig in rigs) CheckRig(rig,scene+"/"+rig.name,true);
            }
        }
        catch(Exception error) { failure=error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(Application.dataPath,"../six-camera-validation.json"),JsonConvert.SerializeObject(new {checks,failure,unity=Application.unityVersion},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
    private static void CheckRig(ViewportSetter rig,string label,bool canonicalAxes)
    {
        Camera[] cameras=rig.GetComponentsInChildren<Camera>(true);
        Check(cameras.Length==6,label+" has exactly six cameras, got "+string.Join(",",cameras.Select(c=>c.name)));
        foreach(char face in directions.Keys) Check(cameras.Count(c=>c.name=="Main Camera "+face)==1,label+" contains exactly one "+face);
        rig.gameObject.SetActive(true); rig.enabled=true;
        rig.ApplyConfiguration(new SystemConfig { targetDisplay=0,displayOrder="DRBLFU",ledPanelWidth=64,ledPanelHeight=64 });
        Check(cameras.All(c=>c.enabled && c.gameObject.activeInHierarchy),label+" activates every requested face.");
        Camera front=cameras.Single(c=>c.name=="Main Camera F");
        foreach(Camera camera in cameras)
        {
            char face=camera.name[12];
            Check(Math.Abs(camera.fieldOfView-90)<.001,label+"/"+face+" has 90-degree FOV.");
            Check(Vector3.Distance(camera.transform.position,front.transform.position)<.001,label+"/"+face+" shares the front-camera origin.");
            if(canonicalAxes) Check(Vector3.Angle(rig.transform.InverseTransformDirection(camera.transform.forward),directions[face])<.01,label+"/"+face+" points in the correct local direction.");
            if(face=='U'||face=='D') Check(camera.cullingMask==front.cullingMask,label+"/"+face+" sees the same rig layers as Front.");
            // A real rendered marker verifies the view direction instead of just the serialized Euler angles.
            var target=GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.layer=30; target.transform.position=camera.transform.position+rig.transform.TransformDirection(directions[face])*10;
            target.transform.localScale=Vector3.one;
            var mat=new Material(Shader.Find("Unlit/Color")); mat.color=Color.magenta; target.GetComponent<Renderer>().sharedMaterial=mat;
            int mask=camera.cullingMask; var clear=camera.clearFlags; var background=camera.backgroundColor;
            var rect=camera.rect; float aspect=camera.aspect;
            camera.cullingMask=1<<30; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            var rt=new RenderTexture(32,32,24); rt.Create(); var capture=new Texture2D(32,32,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=rt; camera.rect=new Rect(0,0,1,1); camera.aspect=1;
                if(canonicalAxes)
                {
                    camera.Render(); RenderTexture.active=rt; capture.ReadPixels(new Rect(0,0,32,32),0,0); capture.Apply();
                    var pixel=capture.GetPixel(16,16);
                    Check(pixel.r>.95 && pixel.b>.95 && pixel.g<.05,label+"/"+face+" renders its direction marker.");
                }
            }
            finally
            {
                camera.targetTexture=null; camera.rect=rect; camera.aspect=aspect; camera.cullingMask=mask; camera.clearFlags=clear; camera.backgroundColor=background;
                RenderTexture.active=previous; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(capture); Object.DestroyImmediate(target); Object.DestroyImmediate(mat);
            }
        }
        rig.ApplyConfiguration(new SystemConfig { targetDisplay=0,displayOrder="RBLF",ledPanelWidth=64,ledPanelHeight=64 });
        Check(cameras.Count(c=>c.enabled)==4 && cameras.Where(c=>c.name.EndsWith(" U")||c.name.EndsWith(" D")).All(c=>!c.enabled),label+" still honors a four-panel configuration.");
    }
}
#endif
