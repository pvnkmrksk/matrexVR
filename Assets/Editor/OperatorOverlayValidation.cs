#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Real Game-view framebuffer tests: stimulus cameras plus unchanged overlay canvases.</summary>
[InitializeOnLoad]
public static class OperatorOverlayValidation
{
    private const string Key="OperatorOverlayValidation.Active";
    private static readonly List<string> checks=new List<string>(),errors=new List<string>();
    private static readonly List<object> cases=new List<object>();
    private static readonly List<Camera> stimulus=new List<Camera>();
    private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static RuntimeStatusOverlay status;
    private static fps frames;
    private static SimpleOverheadCamera overview;
    private static UnityEngine.InputSystem.Keyboard keyboard;
    private static double deadline;
    private static bool finished;
    private static string Output=>Path.Combine(Application.dataPath,"../operator-overlay-validation");
    static OperatorOverlayValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Watchdog;
            Application.logMessageReceived+=Log;
            var host=Object.FindFirstObjectByType<RuntimeStatusOverlay>();
            if(host==null){Finish("Production runtime status host did not initialize.");return;}
            host.StartCoroutine(Exercise());
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project; validation changes its configs.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),"{\"configs\":[{\"vrId\":\"VR1\",\"targetDisplay\":0,\"closedLoopMode\":\"Rekkefly\",\"displayOrder\":\"RBLF\",\"ledPanelWidth\":64,\"ledPanelHeight\":64}],\"statusOverlay\":{\"visible\":true,\"targetDisplay\":0},\"overheadCamera\":{\"enabled\":false},\"telemetry\":{\"enabled\":false},\"targetFrameRate\":60,\"vSyncCount\":0}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"autoStart\":false,\"loop\":false,\"sequences\":[{\"sceneName\":\"Choice_Selwyn\",\"duration\":1000}]}");
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        var gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var window=EditorWindow.GetWindow(gameView);window.Show();window.Focus();
        // A fixed actual Game view, not per-camera target textures or converted canvases.
        ConfigureGameView(window);
        EditorApplication.isPlaying=true;
    }
    private static void ConfigureGameView(EditorWindow window)
    {
        var assembly=typeof(EditorWindow).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
        var groupMethod=sizesType.GetMethod("GetGroup");
        object group=groupMethod.Invoke(sizes,new[]{Enum.Parse(groupMethod.GetParameters()[0].ParameterType,"Standalone")});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");
        var kindType=assembly.GetType("UnityEditor.GameViewSizeType");
        object size=Activator.CreateInstance(sizeType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{Enum.Parse(kindType,"FixedResolution"),(object)1920,1080,"Overlay framebuffer validation"},null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        int index=(int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group,null)+(int)group.GetType().GetMethod("GetCustomCount").Invoke(group,null)-1;
        window.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,index);
        window.Repaint();
    }
    private static void Watchdog(){if(!finished&&EditorApplication.timeSinceStartup>deadline)Finish("Game-view framebuffer capture timed out. Run with a graphics-enabled Editor and visible Game view; camera-only render targets do not validate overlay compositing.");}
    private static void Log(string text,string stack,LogType type)
    {if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&text!="No backup data file found."&&!stack.Contains("UnityEditor.Search.SearchDatabase"))errors.Add(text);}
    private static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    private static IEnumerator Exercise()
    {
        // Exceptions are caught around MoveNext by the wrapper, including across yields.
        return Guard(ExerciseFrames());
    }
    private static IEnumerator Guard(IEnumerator routine)
    {
        while(true){object next;try{if(!routine.MoveNext())yield break;next=routine.Current;}catch(Exception error){Finish(error.ToString());yield break;}yield return next;}
    }
    private static IEnumerator ExerciseFrames()
    {
        for(int n=0;n<20;n++)yield return null;
        Check(MainController.Instance!=null,"real main controller initialized");
        foreach(var listener in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None))listener.enabled=false;
        foreach(var telemetry in Object.FindObjectsByType<ExperimentTelemetry>(FindObjectsSortMode.None))telemetry.enabled=false;
        if(ConfigurationUI.ConfigWorkbench.Instance!=null)ConfigurationUI.ConfigWorkbench.Instance.gameObject.SetActive(false);
        MainController.Instance.enabled=false;
        foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))camera.enabled=false;
        status=Object.FindFirstObjectByType<RuntimeStatusOverlay>();frames=Object.FindFirstObjectByType<fps>();
        Check(status!=null&&frames!=null,"production status and FPS components exist");
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if(canvas.GetComponentInParent<RuntimeStatusOverlay>()==null&&canvas.GetComponentInParent<fps>()==null)canvas.gameObject.SetActive(false);
        keyboard=InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("OverlayValidationKeyboard");
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
        typeof(MainController).GetProperty("OverheadCameraSettings").SetValue(MainController.Instance,new OverheadCameraConfig{enabled=true,targetDisplay=0,x=.6f,y=.02f,width=.25f,height=.25f,resolution=128});
        overview=new GameObject("Production overview validation").AddComponent<SimpleOverheadCamera>();status.SetVisible(true);
        for(int n=0;n<10;n++)yield return null;
        Check(Screen.width==1920&&Screen.height==1080,"actual Game-view framebuffer uses1920x1080 pixels");
        // Cover sparse strips, common4-rig6-face apparatus, vertical wiring, and a fully allocated display.
        foreach(var layout in new[]{new[]{1,4,64,1,0,0},new[]{4,6,128,1,0,0},new[]{4,6,128,0,0,0},new[]{1,4,128,1,7,0},new[]{1,4,128,1,0,11},new[]{1,4,128,1,7,9},new[]{1,1,1920,2,0,0}}){
            ConfigureStimulus(layout[0],layout[1],layout[2],layout[3],layout[4],layout[5]);
            OperatorOverlayVisibility.SetHidden(true);for(int n=0;n<5;n++)yield return null;
            yield return new WaitForEndOfFrame();var baseline=Capture("baseline-"+cases.Count);
            var protectedRects=OperatorOverlayLayout.ProtectedRects(0,Screen.width,Screen.height);
            Check(protectedRects.Count==layout[0]*layout[1],"framebuffer protected rectangles match configured cameras");
            foreach(var rect in protectedRects){var pixel=baseline.GetPixel(Mathf.Clamp(Mathf.RoundToInt(rect.center.x),0,baseline.width-1),Mathf.Clamp(Mathf.RoundToInt(rect.center.y),0,baseline.height-1));Check(pixel.r+pixel.g+pixel.b>.15f,"stimulus cameras naturally render nonblack test colors");}
            OperatorOverlayVisibility.SetHidden(false);for(int n=0;n<6;n++)yield return null;
            yield return new WaitForEndOfFrame();var visible=Capture("visible-"+cases.Count);
            int changedLed=Changed(baseline,visible,protectedRects,true),changedOperator=Changed(baseline,visible,protectedRects,false);
            Check(changedLed==0,"status/FPS/overview composite never changes LED pixels");
            bool full=layout[3]==2;
            if(full)Check(!status.ShouldDisplay&&!frames.OverlayVisible&&!OverviewCanvas().enabled,"a fully allocated LED display omits all operator graphics");
            else Check(status.ShouldDisplay&&frames.OverlayVisible&&changedOperator>1000,"real overlay canvases draw visibly in free display pixels");
            Check(status.GetComponentInChildren<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay&&OverviewCanvas().renderMode==RenderMode.ScreenSpaceOverlay,"tested real overlay render modes remain unmodified");
            PressTab();yield return null;yield return new WaitForEndOfFrame();var firstHidden=Capture("tab-hidden-first-frame-"+cases.Count);
            Check(OperatorOverlayVisibility.Hidden&&!status.ShouldDisplay&&!frames.OverlayVisible&&!OverviewCanvas().enabled,"actual Tab input hides status/FPS/overview together");
            Check(Changed(baseline,firstHidden,protectedRects,true)==0,"Tab first presented frame keeps every LED pixel intact during washout");
            ReleaseTab();for(int n=0;n<3;n++)yield return null;
            yield return new WaitForEndOfFrame();var hidden=Capture("tab-hidden-settled-"+cases.Count);
            Check(Changed(baseline,hidden,protectedRects,true)==0,"hidden settled framebuffer keeps every LED pixel intact");
            Check(Changed(baseline,hidden,protectedRects,false)==0,"Tab removes operator pixels without retained HUD ghosts");
            PressTab();yield return null;ReleaseTab();for(int n=0;n<4;n++)yield return null;
            yield return new WaitForEndOfFrame();var restored=Capture("tab-restored-"+cases.Count);
            Check(!OperatorOverlayVisibility.Hidden&&Changed(baseline,restored,protectedRects,true)==0,"second Tab restores overlays and preserves stimulus pixels");
            if(!full)Check(Changed(baseline,restored,protectedRects,false)>1000,"second Tab restores visible operator graphics");
            cases.Add(new{rigs=layout[0],faces=layout[1],pixels=layout[2],horizontal=layout[3]==1,startRow=layout[4],startCol=layout[5],fullDisplay=full,ledPixelsChanged=changedLed,operatorPixelsDrawn=changedOperator,overviewVisible=OverviewCanvas().enabled,width=Screen.width,height=Screen.height});
            foreach(var texture in new[]{baseline,visible,firstHidden,hidden,restored})Object.Destroy(texture);
        }
        Finish(null);
    }
    private static Canvas OverviewCanvas()=>GameObject.Find("Overhead Camera Canvas").GetComponent<Canvas>();
    private static void PressTab(){if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Tab));InputSystem.Update();}
    private static void ReleaseTab(){InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();}
    private static void ConfigureStimulus(int rigs,int faces,int pixels,int direction,int row,int column)
    {
        foreach(var camera in stimulus)if(camera!=null){camera.enabled=false;Object.Destroy(camera.gameObject);}stimulus.Clear();
        var configs=(Dictionary<string,SystemConfig>)typeof(MainController).GetField("systemConfigs",Hidden).GetValue(MainController.Instance);configs.Clear();
        var back=new GameObject("Synthetic full-frame background").AddComponent<Camera>();back.clearFlags=CameraClearFlags.SolidColor;back.backgroundColor=Color.black;back.cullingMask=0;back.depth=-500;back.allowHDR=false;back.allowMSAA=false;stimulus.Add(back);
        for(int rig=0;rig<rigs;rig++){
            var config=new SystemConfig{vrId="VR"+(rig+1),targetDisplay=0,displayOrder=faces==1?"F":faces==4?"RBLF":"DRBLFU",ledPanelWidth=pixels,ledPanelHeight=direction==2?Screen.height:pixels,horizontal=direction!=0,startRow=row+(direction==0?0:rig),startCol=column+(direction==0?rig:0)};configs.Add(config.vrId,config);
            for(int slot=0;slot<faces;slot++){
                var camera=new GameObject("Synthetic LED "+config.vrId+" "+slot).AddComponent<Camera>();camera.targetDisplay=0;camera.rect=ViewportSetter.PanelRect(slot,config,Screen.width,Screen.height);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.HSVToRGB((rig*faces+slot)*.137f%1,.8f,.8f);camera.cullingMask=0;camera.depth=slot+rig*faces;camera.allowHDR=false;camera.allowMSAA=false;stimulus.Add(camera);
            }
        }
        OperatorOverlayLayout.Invalidate();
    }
    private static Texture2D Capture(string name)
    {
        // This includes ScreenSpaceOverlay UI. Camera.Render()/targetTexture alone does not.
        var texture=ScreenCapture.CaptureScreenshotAsTexture();Check(texture!=null&&texture.width==Screen.width&&texture.height==Screen.height,"full Game-view screenshot contains the presented framebuffer");
        File.WriteAllBytes(Path.Combine(Output,name+".png"),texture.EncodeToPNG());return texture;
    }
    private static int Changed(Texture2D before,Texture2D after,List<Rect> regions,bool inside)
    {
        var a=before.GetPixels32();var b=after.GetPixels32();int changed=0;
        var protectedPixels=new bool[a.Length];
        // Match raster pixel centers rather than shrink the LED region to hide edge defects.
        foreach(var region in regions){
            int x0=Mathf.Clamp(Mathf.CeilToInt(region.xMin-.5f),0,before.width),x1=Mathf.Clamp(Mathf.CeilToInt(region.xMax-.5f),0,before.width);
            int y0=Mathf.Clamp(Mathf.CeilToInt(region.yMin-.5f),0,before.height),y1=Mathf.Clamp(Mathf.CeilToInt(region.yMax-.5f),0,before.height);
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)protectedPixels[y*before.width+x]=true;
        }
        for(int y=0;y<before.height;y++)for(int x=0;x<before.width;x++){
            int index=y*before.width+x;if(protectedPixels[index]!=inside)continue;
            if(Math.Abs(a[index].r-b[index].r)>2||Math.Abs(a[index].g-b[index].g)>2||Math.Abs(a[index].b-b[index].b)>2)changed++;
        }
        return changed;
    }
    private static void Finish(string failure)
    {
        if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Watchdog;Application.logMessageReceived-=Log;
        if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{failure,checks=checks.Count,passed=checks,cases,errors,unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,scope="Natural Game-view framebuffer ScreenCapture at end-of-frame, unchanged ScreenSpaceOverlay status/FPS/overview canvases, actual queued Tab keyboard input; synthetic solid-color stimulus only, no tracker/camera acquisition or experiment"},Formatting.Indented));
        EditorApplication.Exit(failure==null&&errors.Count==0?0:1);
    }
}
#endif
