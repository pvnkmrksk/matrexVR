#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class AdaminabyValidation
{
    const string Key="AdaminabyValidation.Active";
    static int checks, frames;
    static double deadline;
    static void Check(bool condition,string message) { checks++; if(!condition) throw new Exception(message); }
    static AdaminabyValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false)) {
                deadline=EditorApplication.timeSinceStartup+90;
                EditorApplication.update+=Validate;
            }
        };
        SceneManager.sceneLoaded += (scene,mode) => {
            if(!SessionState.GetBool(Key,false)) return;
            foreach(var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) listener.enabled=false;
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable copy required");
        File.Copy(Path.Combine(Application.streamingAssetsPath,"Examples/Sequences/adaminaby-historical.example.json"),
            Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),true);
        File.Copy(Path.Combine(Application.streamingAssetsPath,"Examples/System/fictrac-six-cameras.example.json"),
            Path.Combine(Application.streamingAssetsPath,"system_config.json"),true);
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        SessionState.SetBool(Key,true);
        EditorApplication.isPlaying=true;
    }
    static void Validate()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Choice startup timed out");
            if(SceneManager.GetActiveScene().name!="Choice") return;
            if(++frames<20) return;
            Check(MainController.Instance.GetCurrentSequenceStep().parameters["configFile"].ToString()=="choice__SM_adaminaby.json","Sequence selects restored config");
            var sky=RenderSettings.skybox;
            Check(sky!=null && sky.shader.name=="Skybox/Panoramic","Historical image uses panoramic shader");
            var texture=sky.mainTexture as Texture2D;
            Check(texture!=null && texture.width==3024 && texture.height==1890,"Loaded original screenshot dimensions");
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            source.LoadImage(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,"Photosphere/adaminaby.png")));
            Check(source.GetRawTextureData<byte>().SequenceEqual(texture.GetRawTextureData<byte>()),"Loaded pixels equal original source");
            Object.Destroy(source);
            var rigs=Object.FindObjectsByType<ViewportSetter>(FindObjectsSortMode.None).OrderBy(r=>r.name).ToArray();
            Check(rigs.Length==4,"Four active rigs");
            var sheet=new Texture2D(6*128,4*128,TextureFormat.RGB24,false);
            int row=0;
            foreach(var rig in rigs)
            {
                var cameras=rig.GetComponentsInChildren<Camera>();
                Check(cameras.Length==6 && cameras.All(c=>c.enabled),rig.name+" enables all six cameras");
                Check(Math.Abs(rig.transform.position.y-10.7f)<.01,rig.name+" applies historical initial height");
                int col=0;
                foreach(char face in "DRBLFU")
                {
                    var camera=cameras.Single(c=>c.name=="Main Camera "+face);
                    Check(camera.clearFlags==CameraClearFlags.Skybox,camera.name+" clears to skybox");
                    var custom=camera.GetComponent<Skybox>();
                    Check(custom==null || !custom.enabled || custom.material==null,camera.name+" inherits configured skybox");
                    var target=RenderTexture.GetTemporary(128,128,24);
                    var previous=RenderTexture.active; var previousTarget=camera.targetTexture; var rect=camera.rect; float aspect=camera.aspect;
                    var capture=new Texture2D(128,128,TextureFormat.RGB24,false);
                    try {
                        camera.targetTexture=target; camera.rect=new Rect(0,0,1,1); camera.aspect=1;
                        camera.Render(); RenderTexture.active=target;
                        capture.ReadPixels(new Rect(0,0,128,128),0,0); capture.Apply();
                        sheet.SetPixels(col*128,(3-row)*128,128,128,capture.GetPixels());
                        if(face=='U') Check(capture.GetPixels().Any(p=>p.maxColorComponent>.05),rig.name+" renders visible sky pixels");
                    }
                    finally { camera.targetTexture=previousTarget;camera.rect=rect;camera.aspect=aspect;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.Destroy(capture); }
                    col++;
                }
                row++;
            }
            sheet.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"../adaminaby-historical-six-faces.png"),sheet.EncodeToPNG());Object.Destroy(sheet);
            CheckRestoration(rigs);
            Finish(null);
        }
        catch(Exception error) { Finish(error.ToString()); }
    }
    static void CheckRestoration(ViewportSetter[] rigs)
    {
        var controller=Object.FindFirstObjectByType<ChoiceController>();
        var empty=new Dictionary<string,object> {{"configFile","Choice_empty.json"}};
        var historical=new Dictionary<string,object> {{"configFile","choice__SM_adaminaby.json"}};
        controller.AdvanceStep(empty);
        var defaultMaterial=RenderSettings.skybox;
        var cameras=rigs.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).ToArray();
        // Also cover a face authored with a solid background, and an inactive face.
        cameras[0].clearFlags=CameraClearFlags.SolidColor;
        cameras[0].gameObject.SetActive(false);
        var flags=cameras.ToDictionary(c=>c,c=>c.clearFlags);
        var overrides=cameras.Select(c=>c.GetComponent<Skybox>()).Where(s=>s!=null).ToDictionary(s=>s,s=>s.enabled);
        Check(overrides.Any(pair=>pair.Value),"Clearing the image restores legacy local skybox overrides");
        for(int attempt=0;attempt<2;attempt++)
        {
            controller.AdvanceStep(historical);
            Check(cameras.All(c=>c.clearFlags==CameraClearFlags.Skybox),"All faces use the skybox, including inactive faces");
            Check(overrides.All(pair=>!pair.Key.enabled),"All local skyboxes yield to the selected image");
            Check(!cameras[0].gameObject.activeSelf,"Loading an image preserves face activation");
        }
        controller.AdvanceStep(empty);
        Check(RenderSettings.skybox==defaultMaterial,"Empty skybox restores the scene material");
        Check(flags.All(pair=>pair.Key.clearFlags==pair.Value),"Empty skybox restores original clear flags after repeated loads");
        Check(overrides.All(pair=>pair.Key.enabled==pair.Value),"Empty skybox restores original override states");
        controller.AdvanceStep(historical);
        Object.DestroyImmediate(controller);
        Check(RenderSettings.skybox==defaultMaterial,"Destroying the controller restores the scene material");
        Check(flags.All(pair=>pair.Key.clearFlags==pair.Value),"Destroying the controller restores original clear flags");
        Check(overrides.All(pair=>pair.Key.enabled==pair.Value),"Destroying the controller restores original override states");
    }
    static void Finish(string failure)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Validate;
        File.WriteAllText(Path.Combine(Application.dataPath,"../adaminaby-historical-validation.json"),JsonConvert.SerializeObject(new {checks,failure,unity=Application.unityVersion},Formatting.Indented));
        if(failure!=null) Debug.LogError(failure);
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
