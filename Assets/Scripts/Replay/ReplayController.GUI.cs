using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Replay controls and native pointer hit testing; scene playback and camera behavior live in the core partial.</summary>
public partial class ReplayController
{
    private Vector2 detailScroll;
    private GUIStyle titleStyle, smallStyle;
    private GUISkin replaySkin;
    private ReplayInterfaceSkin replayInterface;
    private float replaySkinScale;
    private readonly GUIContent playContent=new GUIContent();

    private static float ReplayUiScale(float backingScale,float width)=>Mathf.Min(backingScale,Mathf.Max(1,width)/1280f);
    private static Rect ReplayToolbarRect(float width,float height,float scale)
    {
        float barHeight=Mathf.Max(150*scale,height*.14f);
        return new Rect(12*scale,height-barHeight-12*scale,width-24*scale,barHeight);
    }

    private static bool UsesModernMousePosition(EventType type,PointerType pointerType,bool mouseAvailable)
    {
        return mouseAvailable && pointerType==PointerType.Mouse &&
            (type==EventType.MouseDown || type==EventType.MouseUp || type==EventType.MouseMove ||
             type==EventType.MouseDrag || type==EventType.ScrollWheel || type==EventType.Repaint);
    }
    private static Vector2 ReplayMousePosition(Vector2 position,float height)=>new Vector2(position.x,height-position.y);
    private static void NormalizeReplayPointer(Event evt,UnityEngine.InputSystem.Mouse mouse,float height)
    {
        if(evt==null || mouse==null || !UsesModernMousePosition(evt.type,evt.pointerType,mouse.added && mouse.enabled))return;
        // Input System reports native pixels from bottom-left. IMGUI expects
        // top-left, and its legacy event position can be stale on native input.
        // Repaint uses the same position so hover and click targets agree.
        evt.mousePosition=ReplayMousePosition(mouse.position.ReadValue(),height);
    }

    private static Rect ReplayControlRect(ref float x,float y,float width,float height,float gap=4)
    {
        var rect=new Rect(x,y,width,height);x+=width+gap;return rect;
    }
    private void ClearPathFocus(){GUI.FocusControl(null);pathFocused=false;}
    private bool ReplayButton(Rect rect,string text)
    {
        bool clicked=GUI.Button(rect,text);if(clicked)ClearPathFocus();return clicked;
    }
    private static float TimelinePointerValue(Rect rect,float x)=>Mathf.Clamp01((x-rect.x)/Mathf.Max(1,rect.width));
    private bool UpdateTimelineScrub(UnityEngine.InputSystem.Mouse mouse)
    {
        if(!timelineMouseCaptured)return false;
        if(archive==null || loading || folderPicker!=null || mediaPanel?.HasModal==true || OperatorOverlayVisibility.Hidden || !Application.isFocused ||
            mouse==null || !mouse.added || !mouse.enabled)
        {timelineMouseCaptured=timelineMouseUpSeen=false;return false;}
        bool released=mouse.leftButton.wasReleasedThisFrame || (timelineMouseUpSeen && !mouse.leftButton.isPressed);
        if(!released && ((timelineMouseUpSeen && Time.frameCount>timelineMouseUpFrame+1) ||
            (!timelineMouseUpSeen && !mouse.leftButton.isPressed && !mouse.leftButton.wasPressedThisFrame && Time.frameCount>timelineMouseFrame+1)))
        {timelineMouseCaptured=timelineMouseUpSeen=false;return false;}
        if(mouse.leftButton.isPressed || released)
        {
            float normalized=TimelinePointerValue(timelineMouseRect,mouse.position.ReadValue().x);
            if(Math.Abs(replayTime-normalized*Duration)>.000001)SetScrubNormalized(normalized);
        }
        if(released)timelineMouseCaptured=timelineMouseUpSeen=false;
        return true;
    }
    private float ReplaySlider(Rect rect,float value,float minimum,float maximum,bool timeline=false)
    {
        if(Event.current.type==EventType.MouseDown && rect.Contains(Event.current.mousePosition))ClearPathFocus();
        var mouse=UnityEngine.InputSystem.Mouse.current;
        if(GUI.enabled && timeline && Event.current.pointerType==PointerType.Mouse && Event.current.button==0 && mouse!=null && mouse.added && mouse.enabled)
        {
            if(Event.current.type==EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {timelineMouseCaptured=true;timelineMouseUpSeen=false;timelineMouseRect=rect;timelineMouseFrame=Time.frameCount;}
            if(timelineMouseCaptured && Event.current.rawType==EventType.MouseUp)
            {timelineMouseUpSeen=true;timelineMouseUpFrame=Time.frameCount;}
        }
        return GUI.HorizontalSlider(rect,value,minimum,maximum);
    }

    private void TraceReplayButton(string stage,Rect rect,bool clicked=false)
    {
        var evt=Event.current;
        if(!pointerTrace || evt==null || (evt.rawType!=EventType.MouseDown && evt.rawType!=EventType.MouseUp))return;
        Debug.Log("[Replay button] "+stage+" frame="+Time.frameCount+" type="+evt.type+" raw="+evt.rawType+
            " pointer="+evt.pointerType+" button="+evt.button+" position="+evt.mousePosition+" rect="+rect+
            " contains="+rect.Contains(evt.mousePosition)+" enabled="+GUI.enabled+" hot="+GUIUtility.hotControl+
            " keyboard="+GUIUtility.keyboardControl+" screenOrigin="+GUIUtility.GUIToScreenPoint(Vector2.zero)+" clicked="+clicked);
    }

    private bool showRunPicker,showReplayHelp;
    private GUIStyle replayEyebrow,replayContext,replaySession,replayClock;
    private static Rect ReplayHeaderRect(float width,float height,float scale)
        => new Rect(12*scale,12*scale,Mathf.Min(660*scale,Mathf.Max(280*scale,width-420*scale)),78*scale);
    private static Rect ReplayViewportRect(float width,float height,float scale)
    {
        var toolbar=ReplayToolbarRect(width,height,scale);
        float bottom=Mathf.Clamp01((height-toolbar.y+8*scale)/Mathf.Max(1,height));
        return new Rect(0,bottom,1,1-bottom);
    }
    private bool ContainsReplayChrome(Vector2 bottomLeft)
    {
        float scale=ReplayUiScale(DesktopDisplay.UiScale,Screen.width);
        var p=ReplayMousePosition(bottomLeft,Screen.height);
        if(ReplayToolbarRect(Screen.width,Screen.height,scale).Contains(p)||ReplayHeaderRect(Screen.width,Screen.height,scale).Contains(p)||mediaPanel?.ContainsPointer(bottomLeft)==true)return true;
        if((showRunPicker||archive==null)&&ReplayRunPickerRect(scale).Contains(p))return true;
        return (showDetails||showReplayHelp)&&ReplayInfoRect(scale).Contains(p);
    }
    private Rect ReplayRunPickerRect(float scale)
    {var h=ReplayHeaderRect(Screen.width,Screen.height,scale);return new Rect(h.x,h.yMax+8*scale,h.width,132*scale);}
    private Rect ReplayInfoRect(float scale)
    {
        var h=ReplayHeaderRect(Screen.width,Screen.height,scale);var toolbar=ReplayToolbarRect(Screen.width,Screen.height,scale);
        return new Rect(h.x,h.yMax+8*scale,Mathf.Min(620*scale,Screen.width-24*scale),Mathf.Max(80*scale,Mathf.Min((showReplayHelp?350:Mathf.Min(420,100+(archive?.Warnings.Count??0)*56))*scale,toolbar.y-h.yMax-24*scale)));
    }
    private void EnsureReplayStyles(float scale)
    {
        if(replaySkin==null)
        {
            replayInterface=new ReplayInterfaceSkin(GUI.skin);replaySkin=replayInterface.Skin;
            titleStyle=replayInterface.Title;smallStyle=replayInterface.Small;
        }
        if(Mathf.Approximately(replaySkinScale,scale))return;
        replaySkinScale=scale;replayInterface.ApplyScale(scale);
        int P(float value)=>Mathf.Max(1,Mathf.RoundToInt(value*scale));
        foreach(var style in new[]{replaySkin.label,replaySkin.button,replaySkin.toggle,replaySkin.textField})
        {style.font=replaySkin.font;style.fontSize=P(13);}
        replaySkin.button.fixedHeight=32*scale;replaySkin.button.padding=new RectOffset(P(10),P(10),P(5),P(5));
        replaySkin.textField.fixedHeight=32*scale;replaySkin.textField.padding=new RectOffset(P(10),P(10),P(5),P(5));
        replaySkin.toggle.fixedHeight=32*scale;replaySkin.toggle.alignment=TextAnchor.MiddleLeft;replaySkin.label.alignment=TextAnchor.MiddleLeft;
        replaySkin.horizontalSlider.fixedHeight=22*scale;replaySkin.horizontalSliderThumb.fixedWidth=12*scale;replaySkin.horizontalSliderThumb.fixedHeight=22*scale;
        titleStyle.font=replaySkin.font;titleStyle.fontSize=P(18);titleStyle.fontStyle=FontStyle.Normal;
        smallStyle.font=replaySkin.font;smallStyle.fontSize=P(12);
        replayEyebrow=new GUIStyle(smallStyle){fontSize=P(10),fontStyle=FontStyle.Bold,wordWrap=false};replayEyebrow.normal.textColor=new Color(.12f,.43f,.39f);
        replaySession=new GUIStyle(titleStyle){fontSize=P(16),fontStyle=FontStyle.Bold,clipping=TextClipping.Clip,wordWrap=false};
        replayContext=new GUIStyle(smallStyle){fontSize=P(12),wordWrap=false,clipping=TextClipping.Clip};
        replayClock=new GUIStyle(replaySkin.label){fontSize=P(14),alignment=TextAnchor.MiddleCenter};
    }
    private void OnGUI()
    {
        if(OperatorOverlayVisibility.Hidden)
        {
            if(replayPointerControl!=0 && GUIUtility.hotControl==replayPointerControl)GUIUtility.hotControl=0;
            timelineMouseCaptured=timelineMouseUpSeen=false;replayPointerControl=0;return;
        }
        EventType entryType=Event.current!=null?Event.current.type:EventType.Ignore;
        Vector2 legacyPosition=Event.current!=null?Event.current.mousePosition:Vector2.zero;
        NormalizeReplayPointer(Event.current,UnityEngine.InputSystem.Mouse.current,Screen.height);
        float scale=ReplayUiScale(DesktopDisplay.UiScale,Screen.width);EnsureReplayStyles(scale);
        if(pointerTrace && Event.current!=null && (Event.current.type==EventType.MouseDown || Event.current.type==EventType.MouseUp))
            Debug.Log("[Replay pointer] IMGUI frame="+Time.frameCount+" type="+Event.current.type+" position="+Event.current.mousePosition+" legacyPosition="+legacyPosition+" screen="+Screen.width+"x"+Screen.height+" scale="+scale);
        GUISkin previousSkin=GUI.skin;GUI.skin=replaySkin;
        // Native root coordinates retain the same hit targets on Retina and Full HD.
        Matrix4x4 previousMatrix=GUI.matrix;GUI.matrix=Matrix4x4.identity;
        NormalizeReplayPointer(Event.current,UnityEngine.InputSystem.Mouse.current,Screen.height);
        DrawReplayAgentLabels(scale);
        DrawReplayHeader(scale,previousSkin,previousMatrix);
        DrawReplayTransport(scale);
        if(showRunPicker || archive==null)DrawReplayRunPicker(scale);
        if(showDetails && archive!=null)DrawReplayFidelity(scale);
        if(showReplayHelp)DrawReplayHelp(scale);
#if UNITY_EDITOR
        Event evt=Event.current;
        if(evt.type==EventType.DragUpdated || evt.type==EventType.DragPerform)
        {
            string path=UnityEditor.DragAndDrop.paths.FirstOrDefault(Directory.Exists);
            if(path!=null)
            {
                UnityEditor.DragAndDrop.visualMode=UnityEditor.DragAndDropVisualMode.Copy;
                if(evt.type==EventType.DragPerform){UnityEditor.DragAndDrop.AcceptDrag();folderInput=path;LoadSession(path);showRunPicker=false;}
                evt.Use();
            }
        }
#endif
        mediaPanel?.Draw(scale,smallStyle,!loading && folderPicker==null,ReplayToolbarRect(Screen.width,Screen.height,scale).y-8*scale);
        if(GUIUtility.hotControl==0)replayPointerControl=0;
        else if(entryType==EventType.MouseDown && Event.current.type==EventType.Used)replayPointerControl=GUIUtility.hotControl;
        GUI.matrix=previousMatrix;GUI.skin=previousSkin;
    }
    private void DrawReplayHeader(float scale,GUISkin previousSkin,Matrix4x4 previousMatrix)
    {
        Rect area=ReplayHeaderRect(Screen.width,Screen.height,scale);GUI.Box(area,GUIContent.none);
        float x=area.x+14*scale,y=area.y+10*scale;
        GUI.Label(new Rect(x,y,area.width-246*scale,16*scale),loading?"LOADING RECORDING":isPlaying&&archive!=null?"REPLAY · PLAYING":"REPLAY · PAUSED",replayEyebrow);
        string name=archive!=null?Path.GetFileName(archive.DirectoryPath):"Open a saved experiment";
        GUI.Label(new Rect(x,y+17*scale,area.width-246*scale,24*scale),new GUIContent(name,archive?.DirectoryPath??"Choose a recorded run folder."),replaySession);
        GUI.Label(new Rect(x,y+42*scale,area.width-28*scale,17*scale),archive!=null?rigOrder.Count+" animal"+(rigOrder.Count==1?"":"s")+" · "+stepChanges.Count+" recorded conditions · "+Clock(Duration):"Synchronized animal poses, scene context and tracker video",replayContext);
        GUI.enabled=folderPicker==null && mediaPanel?.HasModal!=true;
        if(ReplayButton(new Rect(area.xMax-230*scale,y+14*scale,92*scale,32*scale),"Open run…")){showRunPicker=!showRunPicker;if(showRunPicker)isPlaying=false;showDetails=showReplayHelp=false;}
        if(GUI.Button(new Rect(area.xMax-130*scale,y+14*scale,116*scale,32*scale),new GUIContent("Back to setup","Return to experiment setup. Shortcut: Esc.")))
        {
            GUI.matrix=previousMatrix;GUI.skin=previousSkin;
            if(MainController.Instance!=null)MainController.Instance.HandleEscape();else SceneManager.LoadScene("ControlScene");GUIUtility.ExitGUI();
        }
        GUI.enabled=true;
    }
    private void DrawReplayTransport(float scale)
    {
        Rect area=ReplayToolbarRect(Screen.width,Screen.height,scale);GUI.Box(area,GUIContent.none);TraceReplayButton("root-toolbar",area);
        float left=area.x+16*scale,top=area.y+12*scale,width=area.width-32*scale,row=32*scale,x=left;
        if(archive==null || loading)
        {
            GUI.Label(new Rect(left,top,width,row),loading?"Preparing the saved experiment…":"Choose a recording to begin",titleStyle);
            GUI.Label(new Rect(left,top+38*scale,width,64*scale),!string.IsNullOrEmpty(LoadError)?LoadError:loading?loadProgress:"Open a recorded run above. Replay reads its saved poses, scene configuration and available tracker footage.",smallStyle);return;
        }
        GUI.enabled=folderPicker==null && mediaPanel?.HasModal!=true;
        playContent.text=isPlaying?"Pause":"Play";Rect playRect=ReplayControlRect(ref x,top,82*scale,row,6*scale);
        TraceReplayButton("pause-before",playRect);bool playClicked=GUI.Button(playRect,playContent,replayInterface.PrimaryButton);TraceReplayButton("pause-after",playRect,playClicked);
        if(playClicked){ClearPathFocus();isPlaying=!isPlaying;}
        if(ReplayButton(ReplayControlRect(ref x,top,52*scale,row,6*scale),"−10 s"))Seek(replayTime-10);
        if(ReplayButton(ReplayControlRect(ref x,top,52*scale,row,6*scale),"+10 s"))Seek(replayTime+10);
        x+=8*scale;
        if(ReplayButton(ReplayControlRect(ref x,top,106*scale,row,6*scale),"Previous trial"))JumpToStep(-1);
        if(ReplayButton(ReplayControlRect(ref x,top,88*scale,row,6*scale),"Next trial"))JumpToStep(1);
        GUI.Label(ReplayControlRect(ref x,top,180*scale,row,6*scale),Clock(replayTime)+" / "+Clock(Duration),replayClock);
        float right=area.xMax-16*scale;
        float fitWidth=44*scale,viewWidth=140*scale,projectionWidth=selectedView<0?112*scale:0;
        if(ReplayButton(new Rect(right-fitWidth,top,fitWidth,row),"Fit")){ShowOverview();FitOverview();}right-=fitWidth+6*scale;
        if(selectedView<0){if(ReplayButton(new Rect(right-projectionWidth,top,projectionWidth,row),overviewPerspective?"Top-down":"Perspective"))ToggleOverviewProjection();right-=projectionWidth+6*scale;}
        if(ReplayButton(new Rect(right-viewWidth,top,viewWidth,row),selectedView<0?"God’s-eye view":"Animal "+rigOrder[selectedView]))CycleView();right-=viewWidth+18*scale;
        GUI.Label(new Rect(right-42*scale,top,42*scale,row),playbackSpeed.ToString("0.0")+"×",replayClock);right-=48*scale;
        playbackSpeed=ReplaySlider(new Rect(right-94*scale,top+5*scale,94*scale,22*scale),playbackSpeed,.1f,4);right-=102*scale;
        GUI.Label(new Rect(right-42*scale,top,42*scale,row),"Speed",smallStyle);
        top+=row+12*scale;
        float normalized=Duration>0?(float)(replayTime/Duration):0;Rect timeline=new Rect(left,top,width,22*scale);
        float changed=ReplaySlider(timeline,normalized,0,1,true);
        Color oldColor=GUI.color;GUI.color=new Color(.17f,.57f,.52f,.60f);
        foreach(var step in stepChanges)if(Duration>0)GUI.DrawTexture(new Rect(timeline.x+(float)(step.time/Duration)*timeline.width,timeline.y+3*scale,Mathf.Max(1,scale),7*scale),Texture2D.whiteTexture);
        GUI.color=oldColor;if(!timelineMouseCaptured && Mathf.Abs(changed-normalized)>.000001f)SetScrubNormalized(changed);
        top+=timeline.height+8*scale;
        string context=referenceSample==null?"No animal sample at this time":string.Join("   ·   ",new[]{referenceSample.scene,"Trial "+(referenceSample.trial+1),"Step "+(referenceSample.sequenceStep+1),referenceSample.stepName,referenceSample.phase}.Where(value=>!string.IsNullOrWhiteSpace(value)));
        GUI.Label(new Rect(left,top,width-336*scale,23*scale),new GUIContent(context,context),replayContext);
        float toolsX=area.xMax-16*scale;
        if(ReplayButton(new Rect(toolsX-82*scale,top,82*scale,28*scale),"Controls")){showReplayHelp=!showReplayHelp;showDetails=showRunPicker=false;}toolsX-=88*scale;
        if(ReplayButton(new Rect(toolsX-100*scale,top,100*scale,28*scale),"Fidelity · "+archive.Warnings.Count)){showDetails=!showDetails;showReplayHelp=showRunPicker=false;}toolsX-=106*scale;
        if(ReplayButton(new Rect(toolsX-115*scale,top,115*scale,28*scale),"Trajectory plots"))recordedDashboard=RecordedDashboardLink.Open(archive.DirectoryPath,archive.OriginTicks);
        if(!string.IsNullOrEmpty(LoadError))GUI.Label(new Rect(left,area.yMax-24*scale,width-336*scale,20*scale),LoadError,smallStyle);
        else if(recordedDashboard!=null)GUI.Label(new Rect(left,area.yMax-24*scale,width-336*scale,20*scale),recordedDashboard.Error??recordedDashboard.Status,smallStyle);
        else GUI.Label(new Rect(left,area.yMax-24*scale,width-336*scale,20*scale),"Space to play/pause   ·   Drag timeline to seek   ·   N / M to change condition",smallStyle);
        GUI.enabled=true;
    }
    private void DrawReplayRunPicker(float scale)
    {
        var area=ReplayRunPickerRect(scale);GUI.Box(area,GUIContent.none);float x=area.x+14*scale,y=area.y+10*scale,w=area.width-28*scale;
        GUI.Label(new Rect(x,y,w,22*scale),"Open a recorded run",replaySession);y+=28*scale;
        GUI.enabled=!loading && folderPicker==null && mediaPanel?.HasModal!=true;
        GUI.SetNextControlName("Replay run folder");folderInput=GUI.TextField(new Rect(x,y,w,32*scale),folderInput);pathFocused=GUI.GetNameOfFocusedControl()=="Replay run folder";y+=40*scale;
        if(ReplayButton(new Rect(x,y,88*scale,30*scale),"Browse…")){isPlaying=false;folderPicker=ReplayFolderPicker.ChooseAsync();}
        if(ReplayButton(new Rect(x+w-130*scale,y,130*scale,30*scale),"Open run folder")){LoadSession(folderInput);showRunPicker=false;ClearPathFocus();}
        GUI.enabled=true;
    }
    private void DrawReplayFidelity(float scale)
    {
        var details=ReplayInfoRect(scale);GUI.Box(details,GUIContent.none);float detailX=details.x+14*scale,detailY=details.y+10*scale,detailWidth=details.width-28*scale;
        GUI.Label(new Rect(detailX,detailY,detailWidth,26*scale),"Replay fidelity",replaySession);detailY+=30*scale;
        string description=environmentLoader?.FidelityStatus??"Saved experiment context";
        float introHeight=smallStyle.CalcHeight(new GUIContent(description),detailWidth);GUI.Label(new Rect(detailX,detailY,detailWidth,introHeight),description,smallStyle);detailY+=introHeight+10*scale;
        var viewport=new Rect(detailX,detailY,detailWidth-22*scale,Mathf.Max(20*scale,details.yMax-detailY-12*scale));float contentHeight=0;
        foreach(string warning in archive.Warnings)contentHeight+=smallStyle.CalcHeight(new GUIContent("• "+warning),viewport.width)+8*scale;
        float maximum=Mathf.Max(0,contentHeight-viewport.height);detailScroll.y=Mathf.Clamp(detailScroll.y,0,maximum);
        if(maximum>0)detailScroll.y=GUI.VerticalScrollbar(new Rect(viewport.xMax+4*scale,viewport.y,18*scale,viewport.height),detailScroll.y,viewport.height,0,contentHeight);
        if(Event.current.type==EventType.ScrollWheel && viewport.Contains(Event.current.mousePosition)){detailScroll.y=Mathf.Clamp(detailScroll.y+Event.current.delta.y*20*scale,0,maximum);Event.current.Use();}
        GUI.BeginClip(viewport);float warningY=-detailScroll.y;
        foreach(string warning in archive.Warnings){string text="• "+warning;float warningHeight=smallStyle.CalcHeight(new GUIContent(text),viewport.width);GUI.Label(new Rect(0,warningY,viewport.width,warningHeight),text,smallStyle);warningY+=warningHeight+8*scale;}
        GUI.EndClip();
    }
    private void DrawReplayHelp(float scale)
    {
        var area=ReplayInfoRect(scale);GUI.Box(area,GUIContent.none);float x=area.x+14*scale,y=area.y+10*scale,w=area.width-28*scale;
        GUI.Label(new Rect(x,y,w,26*scale),"Replay controls",replaySession);y+=34*scale;
        GUI.Label(new Rect(x,y,w,340*scale),@"PLAYBACK
Space   Play / pause
← / →   Seek (Shift uses a larger step)
N / M   Previous / next recorded condition

GOD’S-EYE CAMERA
Right drag   Orbit
Middle drag   Pan
Mouse wheel   Zoom
W A S D / Q E   Translate
I J K L   Look around
Fit   Return to all tracks

TRACKS
Bright tail   Last five seconds
Wire cone   Front-camera field of view
Faint line   Recorded path in this condition",smallStyle);
    }
    private static string Clock(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(seconds>=3600?@"hh\:mm\:ss\.f":@"mm\:ss\.f");
}
