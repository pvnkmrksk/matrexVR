#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Native camera-generation/allow-list checks in a disposable project.</summary>
public static class ViewportFaceValidation
{
    private static int checks;
    private static readonly List<string> passed=new List<string>();
    private static void Check(bool value,string label)
    {checks++;if(!value)throw new InvalidOperationException(label);passed.Add(label);}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new InvalidOperationException("Use a marked disposable project.");
        string output=Path.Combine(Application.dataPath,"../viewport-face-results.json");
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-viewport-output")output=args[i+1];
        string failure=null;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Exercise();
            ExerciseAuthoredScene();
        }
        catch(Exception error){failure=error.ToString();}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output,JsonConvert.SerializeObject(new{passed=failure==null,checks,failure,unity=Application.unityVersion,
            scope="Actual Unity camera components, transforms, view matrices, culling masks, skybox references and viewport rectangles. No camera hardware or tracker acquisition.",results=passed},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
    private static Camera Front(GameObject rig)
    {
        var node=new GameObject("Main Camera F");node.transform.SetParent(rig.transform,false);
        node.layer=8;node.transform.localPosition=new Vector3(.2f,.3f,.4f);node.transform.localRotation=Quaternion.Euler(7,13,5);
        var camera=node.AddComponent<Camera>();camera.nearClipPlane=.12f;camera.farClipPlane=777;camera.cullingMask=1<<8;camera.allowHDR=false;
        var material=new Material(Shader.Find("Skybox/Procedural"));node.AddComponent<Skybox>().material=material;
        return camera;
    }
    private static SystemConfig Config(string order,bool horizontal=true,bool preview=false)
    {return new SystemConfig{vrId="VR1",displayOrder=order,targetDisplay=0,ledPanelWidth=64,ledPanelHeight=64,startRow=1,startCol=2,horizontal=horizontal,desktopPreview=preview};}
    private static void Exercise()
    {
        var rig=new GameObject("VR1");var setter=rig.AddComponent<ViewportSetter>();var front=Front(rig);
        rig.transform.rotation=Quaternion.Euler(11,27,3);
        var sky=front.GetComponent<Skybox>().material;
        var authored=new GameObject("Main Camera R");authored.transform.SetParent(rig.transform,false);
        authored.transform.localRotation=Quaternion.Euler(2,87,1);var right=authored.AddComponent<Camera>();
        var authoredRotation=authored.transform.localRotation;
        var six=Config("DRBLFU");setter.ApplyConfiguration(six);
        var cameras=rig.GetComponentsInChildren<Camera>(true);
        Check(cameras.Length==6,"six requested faces exist when the source authored only Front and Right");
        Check(Quaternion.Angle(authored.transform.localRotation,authoredRotation)<.001f,"authored Right calibration orientation is preserved");
        var expected=new Dictionary<char,Vector3>{{'F',front.transform.forward},{'B',-front.transform.forward},{'L',-front.transform.right},{'U',front.transform.up},{'D',-front.transform.up}};
        foreach(char face in "DRBLFU")
        {
            var camera=cameras.Single(c=>c.name=="Main Camera "+face);
            Check(camera.enabled&&camera.gameObject.activeInHierarchy,"requested face active: "+face);
            int slot="DRBLFU".IndexOf(face);var rect=ViewportSetter.PanelRect(slot,six,Screen.width,Screen.height);
            Check(Vector4.Distance(new Vector4(camera.rect.x,camera.rect.y,camera.rect.width,camera.rect.height),new Vector4(rect.x,rect.y,rect.width,rect.height))<.00001f,"native rectangle matches requested slot: "+face);
            if(face!='R')Check(Vector3.Dot(camera.transform.forward,expected[face])>.99999f,"face has Front-relative optical direction: "+face+" actual="+camera.transform.forward+" expected="+expected[face]);
            if(face=='F'||face=='R')continue;
            Check(camera.transform.parent==front.transform.parent&&Vector3.Distance(camera.transform.localPosition,front.transform.localPosition)<.00001f,"generated face shares rig optical centre: "+face);
            Check(camera.gameObject.layer==front.gameObject.layer&&camera.cullingMask==front.cullingMask,"generated face retains its own rig layer and mask: "+face);
            Check(Mathf.Abs(camera.nearClipPlane-front.nearClipPlane)<.00001f&&Mathf.Abs(camera.farClipPlane-front.farClipPlane)<.00001f&&camera.allowHDR==front.allowHDR,"generated face retains projection settings: "+face);
            Check(camera.GetComponent<Skybox>()!=null&&camera.GetComponent<Skybox>().material==sky,"generated face shares authored skybox material: "+face);
            Vector3 forwardInView=camera.worldToCameraMatrix.MultiplyVector(camera.transform.forward).normalized;
            Check(Vector3.Dot(forwardInView,Vector3.back)>.99999f,"generated face uses its own view matrix: "+face);
        }
        var ids=cameras.Select(c=>c.GetInstanceID()).OrderBy(i=>i).ToArray();
        for(int i=0;i<100;i++)setter.ApplyConfiguration(six);
        Check(rig.GetComponentsInChildren<Camera>(true).Select(c=>c.GetInstanceID()).OrderBy(i=>i).SequenceEqual(ids),"repeated apply is idempotent and reentrant callbacks do not duplicate cameras");
        setter.ApplyConfiguration(Config("FRBL"));
        Check(cameras.Count(c=>c.enabled)==4&&!cameras.Single(c=>c.name=="Main Camera U").enabled&&!cameras.Single(c=>c.name=="Main Camera D").enabled,"four-face configuration disables both vertical faces");
        var vertical=Config("FRBLUD",false);setter.ApplyConfiguration(vertical);
        Check(Mathf.Abs(cameras.Single(c=>c.name=="Main Camera U").rect.y-ViewportSetter.PanelRect(4,vertical,Screen.width,Screen.height).y)<.00001f,"vertical strip order uses configured rows");
        setter.ApplyConfiguration(Config("F",true,true));
        Check(cameras.Count(c=>c.enabled)==1&&front.rect==new Rect(0,0,1,1),"desktop demo remains one full-frame Front camera");
        var nested=new GameObject("Nested rig");nested.transform.SetParent(rig.transform,false);var nestedSetter=nested.AddComponent<ViewportSetter>();var nestedFront=Front(nested);
        nestedSetter.ApplyConfiguration(Config("FU"));
        int nestedCount=nested.GetComponentsInChildren<Camera>(true).Length;
        setter.ApplyConfiguration(six);
        Check(nested.GetComponentsInChildren<Camera>(true).Length==nestedCount&&nestedFront.enabled,"parent generation neither takes nor disables nested rig cameras");
        Object.DestroyImmediate(nestedFront.GetComponent<Skybox>().material);
        Object.DestroyImmediate(rig);Object.DestroyImmediate(sky);
        Check(Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0,"rig destruction releases all generated cameras");
        var four=new GameObject("Four-face source");var fourSetter=four.AddComponent<ViewportSetter>();var fourFront=Front(four);var fourSky=fourFront.GetComponent<Skybox>().material;
        fourSetter.ApplyConfiguration(Config("FRBL"));
        Check(four.GetComponentsInChildren<Camera>(true).Length==4,"unrequested vertical faces are never generated");
        Object.DestroyImmediate(four);Object.DestroyImmediate(fourSky);
        var absent=new GameObject("No Front source");var absentSetter=absent.AddComponent<ViewportSetter>();absentSetter.ApplyConfiguration(six);
        Check(absent.GetComponentsInChildren<Camera>(true).Length==0,"missing Front does not invent an uncalibrated source projection");
        Object.DestroyImmediate(absent);
    }
    private static void ExerciseAuthoredScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Choice_desync.unity");
        var rigs=Object.FindObjectsByType<ViewportSetter>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        Check(rigs.Length==4,"Choice_desync exposes four authored rig viewports");
        foreach(var rig in rigs)
        {
            rig.gameObject.SetActive(true);rig.enabled=true;
            var before=rig.GetComponentsInChildren<Camera>(true).Where(c=>c.GetComponentInParent<ViewportSetter>(true)==rig).ToArray();
            var rotations=before.ToDictionary(c=>c.GetInstanceID(),c=>c.transform.localRotation);
            rig.ApplyConfiguration(Config("DRBLFU"));
            var after=rig.GetComponentsInChildren<Camera>(true).Where(c=>c.GetComponentInParent<ViewportSetter>(true)==rig).ToArray();
            Check(after.Count(c=>c.enabled&&c.gameObject.activeInHierarchy)==6,"Choice_desync renders all six requested faces: "+rig.name);
            foreach(var camera in before)Check(Quaternion.Angle(rotations[camera.GetInstanceID()],camera.transform.localRotation)<.001f,"Choice_desync authored orientation remains unchanged: "+rig.name+"/"+camera.name);
            Check(after.Length==Math.Max(6,before.Length),"Choice_desync generates only missing requested faces: "+rig.name);
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }
}
#endif
