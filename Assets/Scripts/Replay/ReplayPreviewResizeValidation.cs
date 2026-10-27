#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

/// <summary>Actual IMGUI controls, saved camera footage and bounded preview geometry at desktop framebuffer scales.</summary>
[InitializeOnLoad]
public sealed class ReplayPreviewResizeValidation : MonoBehaviour
{
    private const string Key="ReplayPreviewResizeValidation.Active";
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>(),errors=new List<string>();
    private static readonly List<object> cases=new List<object>(),states=new List<object>();
    private static string Output=>Path.Combine(Application.dataPath,"../replay-preview-resize-validation");
    private static bool finished;private static double deadline;
    private ReplayController replay;private ReplayMediaPanel media;private float scale=1;
    private bool draw;private EditorWindow view;private Mouse mouse;private Event queuedEvent;
    static ReplayPreviewResizeValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Watchdog;Application.logMessageReceived+=Log;
            var replay=Object.FindFirstObjectByType<ReplayController>();
            if(replay==null){Finish("Replay controller did not initialize.");return;}
            var driver=replay.gameObject.AddComponent<ReplayPreviewResizeValidation>();driver.replay=replay;
            driver.StartCoroutine(Guard(driver.Exercise()));
        };
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(Output);string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-replayDesignSource");
        if(index<0||index+1>=args.Length)throw new Exception("Supply -replayDesignSource saved-run path.");
        string source=Path.GetFullPath(args[index+1]),copy=Path.Combine(Output,"copied-run");
        if(Directory.Exists(copy))throw new Exception("Use a fresh validation output.");CopyTree(source,copy);
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"system_config.json"),"{\"configs\":[{\"vrId\":\"VR1\",\"closedLoopMode\":\"Hejjetrac\",\"displayOrder\":\"F\",\"desktopPreview\":true}],\"statusOverlay\":{\"visible\":true},\"telemetry\":{\"enabled\":false},\"targetFrameRate\":60}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"sequenceConfig.json"),"{\"autoStart\":false,\"sequences\":[]}");
        EditorSceneManager.OpenScene("Assets/Scenes/ReplayScene.unity");
        var replay=Object.FindFirstObjectByType<ReplayController>();replay.absoluteSessionPath=copy;replay.loop=false;
        SessionState.SetBool(Key,true);var view=GetView();ConfigureGameView(view,1920,1080);EditorApplication.isPlaying=true;
    }
    private static EditorWindow GetView(){var v=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));v.Show();v.Focus();return v;}
    private static void ConfigureGameView(EditorWindow window,int width,int height)
    {
        var assembly=typeof(EditorWindow).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
        var method=sizesType.GetMethod("GetGroup");object group=method.Invoke(sizes,new[]{Enum.Parse(method.GetParameters()[0].ParameterType,"Standalone")});
        object size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),(object)width,height,"Replay preview resize "+width},null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        int index=(int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group,null)+(int)group.GetType().GetMethod("GetCustomCount").Invoke(group,null)-1;
        window.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,index);window.Repaint();
    }
    private static void CopyTree(string source,string target){Directory.CreateDirectory(target);foreach(string f in Directory.GetFiles(source))File.Copy(f,Path.Combine(target,Path.GetFileName(f)));foreach(string d in Directory.GetDirectories(source))CopyTree(d,Path.Combine(target,Path.GetFileName(d)));}
    private static T Field<T>(object target,string name)=>target.GetType().GetField(name,Hidden).GetValue(target) is T value?value:default;
    private static void Put(object target,string name,object value)=>target.GetType().GetField(name,Hidden).SetValue(target,value);
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Hidden).Invoke(target,args);
    private static Rect Toolbar(float scale)=>(Rect)typeof(ReplayController).GetMethod("ReplayToolbarRect",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{(float)Screen.width,(float)Screen.height,scale});
    private void OnGUI()
    {
        if(!draw||media==null)return;
        Event previousEvent=null;
        // Exercise the real runtime IMGUI buttons without depending on the
        // Editor window's separate point-to-framebuffer input conversion.
        if(queuedEvent!=null&&Event.current.type==EventType.Repaint)
        {previousEvent=Event.current;Event.current=queuedEvent;queuedEvent=null;}
        Call(replay,"EnsureReplayStyles",scale);var oldSkin=GUI.skin;var oldMatrix=GUI.matrix;
        GUI.skin=Field<GUISkin>(replay,"replaySkin");GUI.matrix=Matrix4x4.identity;
        typeof(ReplayController).GetMethod("NormalizeReplayPointer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Event.current,mouse,(float)Screen.height});
        Call(replay,"DrawReplayHeader",scale,oldSkin,oldMatrix);Call(replay,"DrawReplayTransport",scale);
        media.Draw(scale,Field<GUIStyle>(replay,"smallStyle"),true,Toolbar(scale).y-8*scale);
        GUI.skin=oldSkin;GUI.matrix=oldMatrix;if(previousEvent!=null)Event.current=previousEvent;
    }
    private IEnumerator Click(Vector2 point)
    {
        InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,Screen.height-point.y)});yield return null;
        queuedEvent=new Event{type=EventType.MouseDown,button=0,mousePosition=point};while(queuedEvent!=null)yield return null;
        yield return null;queuedEvent=new Event{type=EventType.MouseUp,button=0,mousePosition=point};while(queuedEvent!=null)yield return null;
        for(int i=0;i<5;i++)yield return null;
    }
    private IEnumerator Exercise()
    {
        while(!replay.Ready||replay.Loading||replay.environmentLoader.Loading)yield return null;
        replay.SetPlaying(false);replay.SeekSeconds(2.8);for(int i=0;i<20;i++)yield return null;
        media=Field<ReplayMediaPanel>(replay,"mediaPanel");Check(media!=null,"Production replay media panel loaded");
        for(int i=0;i<180&&media.VisibleVideoTexture==null;i++)yield return null;
        Check(media.VisibleVideoTexture!=null,"Previously recorded native tracker video decoded");
        Check(Field<RenderTexture>(media,"animalPreview")!=null,"Actual recorded animal camera preview rendered");
        replay.enabled=false;draw=true;view=GetView();
        mouse=InputSystem.AddDevice<Mouse>("ReplayPreviewResizeMouse");InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach(var size in new[]{new[]{1920,1080,1},new[]{3024,1898,2}})
        {
            int width=size[0],height=size[1];scale=size[2];ConfigureGameView(view,width,height);Put(media,"previewScale",1f);Put(media,"showMediaTools",false);media.SetRigs(new[]{"VR1","VR2","VR3","VR4"});
            for(int i=0;i<12;i++)yield return null;
            Check(Screen.width==width&&Screen.height==height,"Actual framebuffer "+width+"×"+height+" with supplied backing scale "+scale);
            Rect baselineAnimal=media.AnimalPreviewRect,baselineVideo=media.TrackerVideoRect;Bounds("default");
            var c=Capture(width+"-01-default-four-rig");while(c.MoveNext())yield return c.Current;
            Vector2 Plus()=>new Vector2(media.ScreenRect.xMax-92*scale,media.ScreenRect.y+23*scale);
            Vector2 Minus()=>new Vector2(media.ScreenRect.xMax-126*scale,media.ScreenRect.y+23*scale);
            var click=Click(Plus());while(click.MoveNext())yield return click.Current;
            Check(Mathf.Approximately(Field<float>(media,"previewScale"),1.25f),"Actual + IMGUI button raises preview scale to 1.25 at "+width);
            Check(media.AnimalPreviewRect.height>baselineAnimal.height+1&&media.TrackerVideoRect.height>baselineVideo.height+1,"+ enlarges both animal and tracker images at "+width);Bounds("larger");
            c=Capture(width+"-02-plus");while(c.MoveNext())yield return c.Current;
            click=Click(Plus());while(click.MoveNext())yield return click.Current;
            Check(Mathf.Approximately(Field<float>(media,"previewScale"),1.5f),"Second + reaches maximum scale at "+width);Bounds("maximum");
            c=Capture(width+"-03-maximum");while(c.MoveNext())yield return c.Current;
            Put(media,"showMediaTools",true);for(int i=0;i<6;i++)yield return null;Bounds("maximum with media tools");
            Check(media.AnimalPreviewRect.height>baselineAnimal.height&&media.TrackerVideoRect.height>baselineVideo.height,"Maximum with tools retains larger images than default at "+width);
            c=Capture(width+"-04-maximum-tools");while(c.MoveNext())yield return c.Current;
            Put(media,"showMediaTools",false);for(int i=0;i<3;i++)yield return null;
            click=Click(Minus());while(click.MoveNext())yield return click.Current;click=Click(Minus());while(click.MoveNext())yield return click.Current;
            Check(Mathf.Approximately(Field<float>(media,"previewScale"),1f),"Actual − IMGUI buttons restore default scale at "+width);
            Check(Mathf.Abs(media.AnimalPreviewRect.height-baselineAnimal.height)<.1f&&Mathf.Abs(media.TrackerVideoRect.height-baselineVideo.height)<.1f,"− restores both original image sizes at "+width);
            var rigButton=new Vector2(media.ScreenRect.xMax-173*scale,media.ScreenRect.y+23*scale);
            click=Click(rigButton);while(click.MoveNext())yield return click.Current;
            Check(media.PreviewRig=="VR2","Four-rig camera header button advances exactly one selected rig at "+width);
            click=Click(rigButton);while(click.MoveNext())yield return click.Current;Check(media.PreviewRig=="VR3","Four-rig camera selector remains usable at "+width);
            media.SetRigs(new[]{"VR1"});for(int i=0;i<3;i++)yield return null;
            c=Capture(width+"-05-single-rig");while(c.MoveNext())yield return c.Current;
            cases.Add(new{width,height,backingScale=scale,baselineAnimalHeight=baselineAnimal.height,baselineVideoHeight=baselineVideo.height,header="Four-rig selector and single-rig label captured separately"});
        }
        Check(errors.Count==0,"No rendering, decoding or interaction errors");InputSystem.RemoveDevice(mouse);Finish(null);
    }
    private void Bounds(string name)
    {
        var toolbar=Toolbar(scale);Check(!toolbar.Overlaps(media.ScreenRect),name+" media card stays above transport at "+Screen.width);
        Check(media.AnimalPreviewRect.height>0&&media.TrackerVideoRect.height>0,name+" both image regions are positive at "+Screen.width);
        Check(media.AnimalPreviewRect.yMax<media.TrackerVideoRect.y&&media.TrackerVideoRect.yMax<media.ScreenRect.yMax,name+" images stay distinct inside media card at "+Screen.width);
        states.Add(new{width=Screen.width,name,animalHeight=media.AnimalPreviewRect.height,trackerVideoHeight=media.TrackerVideoRect.height,cardBottom=media.ScreenRect.yMax,transportTop=toolbar.y});
    }
    private IEnumerator Capture(string name)
    {
        for(int i=0;i<3;i++)yield return null;yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();
        if(name.EndsWith("default-four-rig",StringComparison.Ordinal))
        {
            // A second left-aligned VR label used to draw inside this blank
            // margin of the centered selector button on every repaint.
            int ink=0;
            for(int y=Mathf.RoundToInt(28*scale);y<Mathf.RoundToInt(42*scale);y++)
                for(int x=Mathf.RoundToInt(media.ScreenRect.xMax-196*scale);x<Mathf.RoundToInt(media.ScreenRect.xMax-187*scale);x++)
                {var p=image.GetPixel(x,image.height-1-y);if(Mathf.Min(p.r,p.g,p.b)<.70f)ink++;}
            Check(ink==0,"Four-rig header has no duplicate label ink left of centered selector at "+Screen.width);
        }
        File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());Object.Destroy(image);
    }
    private static IEnumerator Guard(IEnumerator routine){while(true){object next;try{if(!routine.MoveNext())yield break;next=routine.Current;}catch(Exception e){Finish(e.ToString());yield break;}yield return next;}}
    private static void Check(bool condition,string text){if(!condition)throw new Exception(text);checks.Add(text);}
    private static void Watchdog(){if(!finished&&EditorApplication.timeSinceStartup>deadline)Finish("Replay preview resize checks timed out.");}
    private static void Log(string text,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!trace.Contains("UnityEditor.Search.SearchDatabase"))errors.Add(text);}
    private static void Finish(string failure)
    {
        if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Watchdog;Application.logMessageReceived-=Log;
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,failure,errors,cases,states,passed=checks,scope="Actual production media Draw and injected IMGUI mouse events in graphics-enabled isolated Unity Game view at Full HD/scale1 and 3024×1898/supplied scale2; recorded native tracker video and actual animal preview. Four-rig selector state supplied for header testing; no claim of native OS event dispatch or four physical recordings."},Formatting.Indented));EditorApplication.Exit(failure==null&&errors.Count==0?0:1);
    }
}
#endif
