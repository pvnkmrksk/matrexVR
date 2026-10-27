using System;
using System.Linq;
using System.Globalization;
using UnityEngine;

/// <summary>Built-in Unity IMGUI presentation for recorded camera evidence; decoding and time association remain in the core partial.</summary>
public sealed partial class ReplayMediaPanel
{
    private bool showMediaTools;
    private GUIStyle mediaHeading,mediaCaption,mediaFormTitle;
    private int mediaFontSize;
    private void EnsureMediaStyles(GUIStyle source,float scale)
    {
        if(textStyle!=null&&mediaFontSize==source.fontSize&&textStyle.font==source.font)return;
        mediaFontSize=source.fontSize;textStyle=new GUIStyle(source){wordWrap=true};
        mediaHeading=new GUIStyle(source){fontStyle=FontStyle.Bold,wordWrap=false};
        mediaCaption=new GUIStyle(source){wordWrap=true};
        mediaFormTitle=new GUIStyle(source){fontStyle=FontStyle.Bold,fontSize=Mathf.RoundToInt(18*scale),wordWrap=false};
    }
    public Rect ScreenRect=>visibleRect;
    public Rect AnimalPreviewRect { get; private set; }
    public Rect TrackerVideoRect { get; private set; }
    public void Draw(float scale,GUIStyle smallStyle,bool interactable=true,float bottomLimit=0)
    {
        if(disposed || string.IsNullOrEmpty(runDirectory))return;
        if(clearFocus){GUI.FocusControl(null);clearFocus=false;}
        EnsureMediaStyles(smallStyle,scale);
        mediaCaption.normal.textColor=smallStyle.normal.textColor;
        float margin=12*scale,row=28*scale,width=Mathf.Min(350*scale*previewScale,Screen.width*.40f);
        width=Mathf.Max(Mathf.Min(280*scale,Screen.width*.40f),width);
        float inner=width-24*scale;
        bool hasAnimal=animalPreview!=null,hasVideo=clip!=null || pendingArchive!=null;
        string caption=VideoCaption();
        float requestedCaption=Mathf.Clamp(textStyle.CalcHeight(new GUIContent(caption),inner),42*scale,120*scale);
        float limit=bottomLimit>0?bottomLimit:Screen.height-Mathf.Max(150*scale,Screen.height*.14f)-32*scale;
        float availableHeight=Mathf.Max(row+20*scale,limit-margin);
        float chrome=46*scale;
        if(expanded)
        {
            if(hasAnimal)chrome+=34*scale;
            chrome+=24*scale+(hasVideo?8*scale:0)+requestedCaption+row+10*scale;
            if(showMediaTools)chrome+=2*(row+6*scale)+6*scale;
        }
        int imageCount=(hasAnimal?1:0)+(hasVideo?1:0);
        float requestedImageHeight=Mathf.Min(inner*.56f,Screen.height*.19f*previewScale);
        // Grow the actual images with the preview control, then share the
        // remaining viewport budget so neither image can cover the transport.
        float imageHeight=imageCount>0?Mathf.Min(requestedImageHeight,Mathf.Max(0,(availableHeight-chrome)/imageCount)):0;
        float height=Mathf.Min(availableHeight,chrome+(expanded?imageCount*imageHeight:0));
        AnimalPreviewRect=TrackerVideoRect=Rect.zero;
        visibleRect=new Rect(Screen.width-width-margin,margin,width,height);
        GUI.Box(visibleRect,GUIContent.none);float x=visibleRect.x+12*scale,y=visibleRect.y+9*scale,w=inner;
        GUI.enabled=interactable && !HasModal && pendingOperation==null;
        GUI.Label(new Rect(x,y,w-192*scale,row),"Cameras",mediaHeading);
        if(rigs.Count>1)
        {
            if(GUI.Button(new Rect(x+w-188*scale,y,54*scale,row),PreviewRig??"VR1"))selectedRig=(selectedRig+1)%rigs.Count;
        }
        else GUI.Label(new Rect(x+w-188*scale,y,54*scale,row),PreviewRig??"VR1",mediaHeading);
        if(GUI.Button(new Rect(x+w-128*scale,y,28*scale,row),new GUIContent("−","Make previews smaller.")))previewScale=Mathf.Max(.75f,previewScale-.25f);
        if(GUI.Button(new Rect(x+w-94*scale,y,28*scale,row),new GUIContent("+","Make previews larger.")))previewScale=Mathf.Min(1.5f,previewScale+.25f);
        if(GUI.Button(new Rect(x+w-60*scale,y,60*scale,row),expanded?"Hide":"Show"))expanded=!expanded;
        y+=row+10*scale;
        if(expanded)
        {
            if(animalPreview!=null)
            {
                GUI.Label(new Rect(x,y,w,20*scale),animalLabel??"Animal · front view",mediaHeading);y+=24*scale;
                AnimalPreviewRect=new Rect(x,y,w,imageHeight);DrawImage(AnimalPreviewRect,animalPreview);y+=imageHeight+10*scale;
            }
            string title=clip==null?"Tracker video":(clip.RigId??"Animal")+" · tracker video";
            GUI.Label(new Rect(x,y,w-84*scale,20*scale),title,mediaHeading);
            if(archive!=null && archive.Clips.Count(c=>string.Equals(c.RigId,PreviewRig,StringComparison.OrdinalIgnoreCase))>1 && GUI.Button(new Rect(x+w-80*scale,y,80*scale,22*scale),"Next clip"))
            {
                for(int offset=1;offset<=archive.Clips.Count;offset++)
                {int next=(selectedClip+offset)%archive.Clips.Count;if(string.Equals(archive.Clips[next].RigId,PreviewRig,StringComparison.OrdinalIgnoreCase)){ChooseClip(next);break;}}
            }
            y+=24*scale;
            if(hasVideo){TrackerVideoRect=new Rect(x,y,w,imageHeight);DrawImage(TrackerVideoRect,VisibleVideoTexture);y+=imageHeight+8*scale;}
            if(!string.IsNullOrEmpty(error))mediaCaption.normal.textColor=new Color(.66f,.20f,.16f);
            float actionsHeight=(showMediaTools?2*(row+6*scale)+6*scale:0)+row+12*scale;
            float captionHeight=Mathf.Max(24*scale,visibleRect.yMax-y-actionsHeight);
            GUI.Label(new Rect(x,y,w,captionHeight),caption,mediaCaption);
            float buttonsY=visibleRect.yMax-actionsHeight;
            GUI.enabled=interactable && !HasModal && pendingOperation==null && pendingArchive==null;
            if(GUI.Button(new Rect(x,buttonsY,w,row),showMediaTools?"Hide media tools":"Media tools…"))showMediaTools=!showMediaTools;
            if(showMediaTools)
            {
                buttonsY+=row+6*scale;
                if(GUI.Button(new Rect(x,buttonsY,(w-6*scale)*.5f,row),new GUIContent("Import footage…","Copy finished tracker footage and logs into this run. Original files are preserved.")))ChooseImportFolder();
                if(GUI.Button(new Rect(x+w*.5f+3*scale,buttonsY,(w-6*scale)*.5f,row),new GUIContent("Collect media","Collect app-launched footage after the recorder has closed cleanly. Running trackers are never stopped here.")))RunOperation(token=>MediaArchive.CollectPendingAsync(runDirectory,token));
                buttonsY+=row+6*scale;
                if(GUI.Button(new Rect(x,buttonsY,(w-6*scale)*.5f,row),"Refresh"))Load(runDirectory,originTicks);
                GUI.enabled=GUI.enabled && clip!=null && clip.Complete && clip.State=="ready";
                if(GUI.Button(new Rect(x+w*.5f+3*scale,buttonsY,(w-6*scale)*.5f,row),"Set offset…"))
                {offsetClipId=clip.Id;offsetFrameCount=avi!=null?avi.FrameCount:videoPrepared&&player!=null?(long)player.frameCount:clip.FrameCount;offsetFps=avi!=null?avi.NominalFramesPerSecond:videoPrepared&&player!=null?player.frameRate:clip.NominalFps;offsetForm=true;formOpen=true;offsetText="";formError=null;pause?.Invoke();}
            }
        }
        GUI.enabled=true;if(formOpen)DrawImportForm(scale);
    }
    private string VideoCaption()
    {
        ReplayMediaFrame target=default;bool mapped=archive!=null && clip!=null && archive.TryResolveFrame(clip,seconds,out target);
        if(!string.IsNullOrEmpty(error))return error;
        if(clip!=null && mapped)
        {
            if(VisibleVideoTexture==null)return "Seeking linked video frame "+(target.LocalFrame+1)+"…";
            if(displayedAssociation.HasValue)
            {
                var shown=displayedAssociation.Value;double age=Math.Max(0,seconds-shown.AnchorReplaySeconds);
                return shown.AssociationLabel+"\nFrame "+(displayedFrame+1)+" · input "+shown.AnchorReplaySeconds.ToString("0.000",CultureInfo.InvariantCulture)+" s"+(age>.1?" · age "+age.ToString("0.00",CultureInfo.InvariantCulture)+" s":"")+(videoSeeking || pendingFrame!=null?" · seeking":"");
            }
            return "Decoder frame "+(displayedFrame+1)+" · aligning with linked frame "+(target.LocalFrame+1);
        }
        if(clip!=null && clip.Complete && clip.SyncMode=="unavailable")return "Timing link unavailable. Media tools → Set offset adds approximate alignment.";
        if(clip!=null && clip.Complete && pendingAvi==null && pendingArchive==null)return "No camera frame is associated with this replay time.";
        return status;
    }
    private static void DrawImage(Rect rect,Texture image)
    {
        Color previous=GUI.color;GUI.color=new Color(.035f,.045f,.055f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;
        if(image!=null)GUI.DrawTexture(rect,image,ScaleMode.ScaleToFit,false);
    }
    private void DrawImportForm(float scale)
    {
        float width=Mathf.Min(620*scale,Screen.width-32*scale),height=offsetForm?300*scale:390*scale;
        height=Mathf.Min(height,Screen.height-32*scale);formRect=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
        Color tint=GUI.color;GUI.color=new Color(0,0,0,.42f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=tint;
        GUI.Box(formRect,GUIContent.none);float x=formRect.x+20*scale,y=formRect.y+16*scale,w=width-40*scale,row=32*scale;
        GUI.Label(new Rect(x,y,w,row),offsetForm?"Align tracker video":"Import finished footage",mediaFormTitle);y+=row+12*scale;
        GUI.Label(new Rect(x,y,w,offsetForm?42*scale:60*scale),offsetForm?"Approximate alignment: replay time = video time + offset. Use a known event visible in both recordings.":sourceFolder,textStyle);y+=offsetForm?46*scale:64*scale;
        GUI.enabled=pendingOperation==null;
        if(!offsetForm)
        {
            if(GUI.Button(new Rect(x,y,w,row),"Animal: "+(PreviewRig??"VR1")+" (click to change)"))if(rigs.Count>1)selectedRig=(selectedRig+1)%rigs.Count;
            y+=row+6*scale;
            sourceClosed=GUI.Toggle(new Rect(x,y,w,row),sourceClosed,"The tracker has finished saving this recording");y+=row+6*scale;
        }
        GUI.Label(new Rect(x,y,w,row),offsetForm?"Offset in seconds (negative values allowed)":"Optional offset in seconds; leave blank to preserve unknown timing",textStyle);y+=row;
        GUI.SetNextControlName("Replay video offset");offsetText=GUI.TextField(new Rect(x,y,w,row),offsetText);TextFocused=GUI.GetNameOfFocusedControl()=="Replay video offset";y+=row+6*scale;
        GUI.Label(new Rect(x,y,w,46*scale),pendingOperation!=null?"Copying and verifying tracker media…":formError??"Positive offset: video started later. Raw footage and original timestamps are preserved.",textStyle);
        float bottom=formRect.yMax-row-14*scale;
        GUI.enabled=true;
        if(GUI.Button(new Rect(x,bottom,w*.30f,row),"Cancel")){operationCancellation?.Cancel();formOpen=false;TextFocused=false;GUI.FocusControl(null);}
        GUI.enabled=pendingOperation==null && (offsetForm || sourceClosed);
        if(GUI.Button(new Rect(x+w*.58f,bottom,w*.42f,row),offsetForm?"Apply offset":"Import recording"))SubmitForm();
        GUI.enabled=true;
    }
}
