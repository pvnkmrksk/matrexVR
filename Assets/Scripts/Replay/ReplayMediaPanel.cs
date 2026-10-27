using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

/// <summary>Optional camera footage and animal preview. Media I/O never advances or blocks the pose timeline.</summary>
public sealed class ReplayMediaPanel : IDisposable
{
    private readonly MonoBehaviour owner;
    private readonly Action pause;
    private readonly CancellationTokenSource lifetime=new CancellationTokenSource();
    private CancellationTokenSource loadCancellation;
    private CancellationTokenSource operationCancellation;
    private CancellationTokenSource clipCancellation;
    private Task<ReplayMediaArchive> pendingArchive;
    private Task<ReplayAviFrameSource> pendingAvi;
    private Task<ReplayAviFrameSource.Frame> pendingFrame;
    private Task<MediaArchiveReceipt> pendingOperation;
    private Task<string> folderPicker;
    private ReplayMediaArchive archive;
    private ReplayMediaClip clip;
    private ReplayAviFrameSource avi;
    private Texture2D frameTexture;
    private VideoPlayer player;
    private GameObject playerObject;
    private RenderTexture animalPreview;
    private string animalLabel,runDirectory,status="No tracker video was saved.",error;
    private string sourceFolder="",offsetText="",formError;
    private long originTicks,requestedFrame=-1,displayedFrame=-1,failedFrame=-1;
    private int selectedClip,generation,frameGeneration,displayedGeneration=-1,videoSeekGeneration;
    private ReplayMediaFrame? requestedAssociation,displayedAssociation;
    private string pickerRun,operationRun,offsetClipId;
    private long offsetFrameCount;
    private double offsetFps;
    private bool expanded=true,disposed,formOpen,offsetForm,sourceClosed,videoSeeking,videoPrepared;
    private bool playing;
    private bool clearFocus;
    private double seconds,lastSeconds=double.NaN;
    private float speed;
    private float previewScale=1;
    private Rect visibleRect,formRect;
    private readonly List<string> rigs=new List<string>();
    private int selectedRig;
    private GUIStyle textStyle;
    public bool HasModal=>formOpen || folderPicker!=null;
    public bool TextFocused { get; private set; }
    public string PreviewRig=>rigs.Count==0?null:rigs[Mathf.Clamp(selectedRig,0,rigs.Count-1)];
    public bool Expanded=>expanded;
    public IReadOnlyList<string> Warnings=>archive?.Warnings??Array.Empty<string>();
    public string Error=>error;

    public ReplayMediaPanel(MonoBehaviour owner,Action pause){this.owner=owner;this.pause=pause;}
    public void SetAnimalPreview(RenderTexture texture,string label){animalPreview=texture;animalLabel=label;}
    public void SetRigs(IEnumerable<string> values){rigs.Clear();rigs.AddRange(values);selectedRig=0;}
    public void Load(string directory,long origin)
    {
        if(!string.Equals(runDirectory,directory,StringComparison.Ordinal))
        {operationCancellation?.Cancel();Observe(pendingOperation);pendingOperation=null;folderPicker=null;pickerRun=operationRun=null;}
        runDirectory=directory;originTicks=origin;formOpen=false;TextFocused=false;sourceFolder="";error=null;archive=null;selectedClip=0;
        loadCancellation?.Cancel();Observe(pendingArchive);loadCancellation?.Dispose();loadCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        ResetClip();status="Checking saved camera footage…";pendingArchive=ReplayMediaArchive.LoadAsync(directory,origin,loadCancellation.Token);
    }
    public bool ContainsPointer(Vector2 bottomLeftPosition)
    {
        var point=new Vector2(bottomLeftPosition.x,Screen.height-bottomLeftPosition.y);
        return visibleRect.Contains(point) || (formOpen && formRect.Contains(point));
    }
    public bool CancelModal()
    {
        if(folderPicker!=null)return true; // The native panel owns Escape until its result is consumed.
        if(!formOpen)return false;
        operationCancellation?.Cancel();formOpen=false;TextFocused=false;clearFocus=true;return true;
    }
    public void Tick(double replaySeconds,bool isPlaying,float rate)
    {
        if(disposed)return;seconds=replaySeconds;playing=isPlaying;speed=rate;
        if(folderPicker!=null && folderPicker.IsCompleted)
        {
            if(folderPicker.IsFaulted)error=folderPicker.Exception?.GetBaseException().Message;
            else if(pickerRun==runDirectory && !string.IsNullOrWhiteSpace(folderPicker.Result))
            {sourceFolder=folderPicker.Result;offsetForm=false;formOpen=true;sourceClosed=false;offsetText="";formError=null;pause?.Invoke();}
            folderPicker=null;
        }
        if(pendingOperation!=null && pendingOperation.IsCompleted)
        {
            var completed=pendingOperation;string completedRun=operationRun;pendingOperation=null;formOpen=false;TextFocused=false;
            operationCancellation?.Dispose();operationCancellation=null;
            if(completedRun!=runDirectory){_ = completed.Exception;}
            else if(completed.IsFaulted){error=completed.Exception?.GetBaseException().Message;status="Camera media could not be updated.";}
            else if(completed.IsCanceled)status="Camera import cancelled; original files are unchanged.";
            else {status=completed.Result.Pending?"Some tracker recordings are still pending.":"Camera media updated.";Load(runDirectory,originTicks);}
        }
        if(pendingArchive!=null && pendingArchive.IsCompleted)
        {
            var completed=pendingArchive;pendingArchive=null;
            if(completed.IsFaulted){error=completed.Exception?.GetBaseException().Message;status="Camera archive unavailable.";}
            else if(!completed.IsCanceled)
            {
                archive=completed.Result;selectedClip=0;
                status=archive.Clips.Count==0?"No tracker video was saved.":archive.Clips.Count+" recorded video clip(s).";
                if(archive.Clips.Count>0)ChooseClip(0);
                if(archive.Warnings.Count>0)error=string.Join("\n",archive.Warnings.Take(3));
            }
        }
        if(pendingAvi!=null && pendingAvi.IsCompleted)
        {
            var completed=pendingAvi;pendingAvi=null;
            if(completed.IsFaulted)PreparePlatformVideo();
            else if(!completed.IsCanceled){avi=completed.Result;status="Tracker video ready.";}
        }
        bool jumped=!double.IsNaN(lastSeconds) && (seconds<lastSeconds || Math.Abs(seconds-lastSeconds)>.3 || (!playing && Math.Abs(seconds-lastSeconds)>.000001));
        lastSeconds=seconds;if(jumped)generation++;
        ReplayMediaFrame desired=default;
        bool mapped=archive!=null && clip!=null && archive.TryResolveFrame(clip,seconds,out desired);
        if(pendingFrame!=null && pendingFrame.IsCompleted)
        {
            var completed=pendingFrame;pendingFrame=null;
            if(completed.IsFaulted)
            {if(frameGeneration==generation){error=completed.Exception?.GetBaseException().Message;failedFrame=requestedFrame;status="Tracker frame unavailable.";}else _=completed.Exception;}
            else if(!completed.IsCanceled && frameGeneration==generation && clip!=null)
            {
                try {DisplayFrame(completed.Result);displayedFrame=requestedFrame;displayedAssociation=requestedAssociation;displayedGeneration=frameGeneration;error=null;}
                catch(Exception failure){error=failure.Message;failedFrame=requestedFrame;status="Tracker frame could not be decoded.";}
            }
        }
        if(avi!=null && clip!=null)
        {
            long wanted=mapped?desired.LocalFrame:clip.SyncMode=="unavailable"?0:-1;
            if(wanted==displayedFrame && frameTexture!=null)
            {displayedGeneration=generation;displayedAssociation=mapped?desired:(ReplayMediaFrame?)null;}
            if(wanted>=0 && wanted<avi.FrameCount && pendingFrame==null && wanted!=displayedFrame && wanted!=failedFrame)
            {
                requestedFrame=wanted;frameGeneration=generation;requestedAssociation=mapped?desired:(ReplayMediaFrame?)null;var source=avi;var token=clipCancellation.Token;
                pendingFrame=Task.Run(()=>source.ReadFrame(wanted,token),token);
            }
        }
        else if(player!=null && videoPrepared)
        {
            if(mapped)
            {
                bool sameReadyFrame=!videoSeeking && player.frame==desired.LocalFrame && player.texture!=null;
                bool needsSeek=!sameReadyFrame && (jumped || (!playing && player.frame!=desired.LocalFrame) || Math.Abs(player.time-desired.MediaSeconds)>.20);
                if(needsSeek && !videoSeeking && player.canSetTime)
                {videoSeeking=true;videoSeekGeneration=generation;requestedAssociation=desired;requestedFrame=desired.LocalFrame;player.frame=desired.LocalFrame;}
                if(!videoSeeking && player.frame>=0)
                {displayedFrame=player.frame;displayedGeneration=generation;displayedAssociation=displayedFrame==desired.LocalFrame?desired:(ReplayMediaFrame?)null;}
                if(playing && !videoSeeking && desired.AgeSeconds<.15)
                {if(player.canSetPlaybackSpeed)player.playbackSpeed=Mathf.Max(.1f,speed);if(!player.isPlaying)player.Play();}
                else if(player.isPlaying)player.Pause();
            }
            else if(player.isPlaying)player.Pause();
        }
    }
    private void ChooseClip(int index)
    {
        ResetClip();selectedClip=index;clip=archive?.Clips[index];if(clip==null)return;
        if(!clip.Complete || clip.State!="ready"){status="This camera recording is pending or unavailable.";return;}
        error=null;status="Opening tracker video…";clipCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        if(string.Equals(clip.Container,"avi",StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(clip.AbsolutePath),".avi",StringComparison.OrdinalIgnoreCase))
        {string path=clip.AbsolutePath;var token=clipCancellation.Token;pendingAvi=Task.Run(()=>ReplayAviFrameSource.Open(path,token),token);}
        else PreparePlatformVideo();
    }
    private void PreparePlatformVideo()
    {
        if(disposed || clip==null || string.IsNullOrEmpty(clip.AbsolutePath))return;
        playerObject=new GameObject("Replay tracker video decoder");playerObject.transform.SetParent(owner.transform,false);
        player=playerObject.AddComponent<VideoPlayer>();player.playOnAwake=false;player.renderMode=VideoRenderMode.APIOnly;
        player.audioOutputMode=VideoAudioOutputMode.None;player.source=VideoSource.Url;player.url=new Uri(clip.AbsolutePath).AbsoluteUri;
        player.isLooping=false;player.waitForFirstFrame=true;player.skipOnDrop=true;
        player.prepareCompleted+=Prepared;player.seekCompleted+=SeekCompleted;player.errorReceived+=VideoError;
        status="Preparing platform video decoder…";player.Prepare();
    }
    private void Prepared(VideoPlayer source)
    {
        if(disposed || source!=player)return;videoPrepared=true;status="Platform video decoder ready.";
        // The first frame must be decoded even for footage whose clocks are unknown.
        source.sendFrameReadyEvents=true;source.frameReady+=InitialFrame;source.Play();
    }
    private void InitialFrame(VideoPlayer source,long frame)
    {
        if(source!=player)return;source.Pause();source.frameReady-=InitialFrame;source.sendFrameReadyEvents=false;displayedFrame=frame;displayedGeneration=clip?.SyncMode=="unavailable"?generation:-1;
    }
    private void SeekCompleted(VideoPlayer source)
    {if(source!=player)return;videoSeeking=false;if(videoSeekGeneration!=generation)return;displayedFrame=source.frame;displayedGeneration=generation;displayedAssociation=displayedFrame==requestedFrame?requestedAssociation:null;}
    private void VideoError(VideoPlayer source,string message)
    {if(source!=player)return;videoSeeking=false;videoPrepared=false;error="This platform could not decode the saved video: "+message;status="Pose replay continues. The original footage is preserved.";}
    private void DisplayFrame(ReplayAviFrameSource.Frame frame)
    {
        bool encoded=frame.Encoding==ReplayAviFrameSource.PixelEncoding.Jpeg || frame.Encoding==ReplayAviFrameSource.PixelEncoding.Png;
        if(frameTexture==null || (!encoded && (frameTexture.width!=frame.Width || frameTexture.height!=frame.Height)))
        {if(frameTexture!=null)UnityEngine.Object.Destroy(frameTexture);frameTexture=new Texture2D(encoded?2:frame.Width,encoded?2:frame.Height,TextureFormat.RGB24,false);frameTexture.name="Recorded tracker frame";}
        if(encoded)
        {if(!ImageConversion.LoadImage(frameTexture,frame.Bytes,false))throw new InvalidDataException("The recorded image payload is not decodable.");}
        else
        {
            var rgb=new byte[frame.Width*frame.Height*3];int channels=frame.Encoding==ReplayAviFrameSource.PixelEncoding.Gray8?1:frame.Encoding==ReplayAviFrameSource.PixelEncoding.Bgr24?3:4;
            for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++)
            {
                int row=frame.BottomUp?frame.Height-1-y:y,source=row*frame.RowStride+x*channels,target=((frame.Height-1-y)*frame.Width+x)*3;
                byte luma=frame.Bytes[source];rgb[target]=channels==1?luma:frame.Bytes[source+2];rgb[target+1]=channels==1?luma:frame.Bytes[source+1];rgb[target+2]=luma;
            }
            frameTexture.LoadRawTextureData(rgb);frameTexture.Apply(false,false);
        }
        frameTexture.filterMode=FilterMode.Bilinear;
    }
    public void Draw(float scale,GUIStyle smallStyle,bool interactable=true)
    {
        if(disposed || string.IsNullOrEmpty(runDirectory))return;
        if(clearFocus){GUI.FocusControl(null);clearFocus=false;}
        if(textStyle==null)textStyle=new GUIStyle(smallStyle){wordWrap=true};textStyle.fontSize=smallStyle.fontSize;textStyle.font=smallStyle.font;
        float width=Mathf.Min(420*scale*previewScale,Screen.width*.48f),margin=12*scale,row=28*scale;
        width=Mathf.Max(Mathf.Min(320*scale,Screen.width*.48f),width);
        float imageHeight=Mathf.Min(150*scale*previewScale,Screen.height*.23f);
        float videoHeight=clip!=null || pendingArchive!=null?imageHeight:0;
        float height=expanded?row+videoHeight+(animalPreview!=null?row+imageHeight:0)+170*scale:row+12*scale;
        height=Mathf.Min(height,Screen.height*.8f-margin*2);visibleRect=new Rect(Screen.width-width-margin,margin,width,height);
        GUI.Box(visibleRect,GUIContent.none);float x=visibleRect.x+10*scale,y=visibleRect.y+6*scale,w=width-20*scale;
        GUI.enabled=interactable && !HasModal && pendingOperation==null;
        GUI.Label(new Rect(x,y,w-144*scale,row),"Recording views",textStyle);
        if(GUI.Button(new Rect(x+w-140*scale,y,30*scale,row),new GUIContent("−","Make the animal and tracker previews smaller.")))previewScale=Mathf.Max(.75f,previewScale-.25f);
        if(GUI.Button(new Rect(x+w-106*scale,y,30*scale,row),new GUIContent("+","Make the animal and tracker previews larger.")))previewScale=Mathf.Min(1.5f,previewScale+.25f);
        if(GUI.Button(new Rect(x+w-70*scale,y,70*scale,row),expanded?"Hide":"Show"))expanded=!expanded;
        y+=row+4*scale;
        if(expanded)
        {
            if(animalPreview!=null)
            {
                GUI.Label(new Rect(x,y,w-100*scale,row),animalLabel??"Animal view",textStyle);
                if(rigs.Count>1 && GUI.Button(new Rect(x+w-96*scale,y,96*scale,row),"Next animal"))selectedRig=(selectedRig+1)%rigs.Count;
                y+=row;DrawImage(new Rect(x,y,w,imageHeight),animalPreview);y+=imageHeight+4*scale;
            }
            string title=clip==null?"Tracker video":(clip.RigId??"Animal")+" · video "+(selectedClip+1)+" / "+archive.Clips.Count;
            GUI.Label(new Rect(x,y,w-100*scale,row),title,textStyle);
            if(archive!=null && archive.Clips.Count>1 && GUI.Button(new Rect(x+w-96*scale,y,96*scale,row),"Next video"))ChooseClip((selectedClip+1)%archive.Clips.Count);
            y+=row;
            Texture image=avi!=null?frameTexture:player!=null?player.texture:null;
            ReplayMediaFrame target=default;
            bool mapped=archive!=null && clip!=null && archive.TryResolveFrame(clip,seconds,out target);
            bool show=image!=null && displayedGeneration==generation && (mapped || clip?.SyncMode=="unavailable");
            if(videoHeight>0)DrawImage(new Rect(x,y,w,videoHeight),show?image:null);y+=videoHeight+4*scale;
            string caption=status;
            if(clip!=null && mapped)
            {
                if(!show)caption="Seeking linked video frame "+(target.LocalFrame+1)+"…";
                else if(displayedAssociation.HasValue)
                {
                    var shown=displayedAssociation.Value;double age=Math.Max(0,seconds-shown.AnchorReplaySeconds);
                    caption=shown.AssociationLabel+"\nVideo frame "+(displayedFrame+1)+" · input anchor "+shown.AnchorReplaySeconds.ToString("0.000",CultureInfo.InvariantCulture)+" s"+(age>.1?" · age "+age.ToString("0.00",CultureInfo.InvariantCulture)+" s":"")+(videoSeeking || pendingFrame!=null?" · seeking":"");
                }
                else caption="Decoder frame "+(displayedFrame+1)+" · linked target "+(target.LocalFrame+1)+" · aligning video";
            }
            else if(clip!=null && clip.Complete && clip.SyncMode=="unavailable")caption="Footage has no timing link. Use Set offset for approximate alignment.";
            else if(clip!=null && clip.Complete && pendingAvi==null && pendingArchive==null)caption="No camera frame is associated with this replay time.";
            if(!string.IsNullOrEmpty(error))caption=error;
            float buttonsY=visibleRect.yMax-62*scale,captionHeight=Mathf.Max(20*scale,buttonsY-y-4*scale);
            GUI.Label(new Rect(x,y,w,captionHeight),caption,textStyle);
            GUI.enabled=interactable && !HasModal && pendingOperation==null && pendingArchive==null;
            if(GUI.Button(new Rect(x,buttonsY,w*.48f,row),new GUIContent("Import video folder…","Copy finished tracker footage and its logs into this run. Original files are preserved.")))ChooseImportFolder();
            if(GUI.Button(new Rect(x+w*.50f,buttonsY,w*.50f,row),new GUIContent("Collect media","Collect app-launched tracker footage once its recorder has closed cleanly. Running trackers are never stopped here.")))RunOperation(token=>MediaArchive.CollectPendingAsync(runDirectory,token));
            buttonsY+=row+3*scale;
            if(GUI.Button(new Rect(x,buttonsY,w*.48f,row),"Refresh videos"))Load(runDirectory,originTicks);
            GUI.enabled=GUI.enabled && clip!=null && clip.Complete && clip.State=="ready";
            if(GUI.Button(new Rect(x+w*.50f,buttonsY,w*.50f,row),"Set offset…"))
            {offsetClipId=clip.Id;offsetFrameCount=avi!=null?avi.FrameCount:videoPrepared&&player!=null?(long)player.frameCount:clip.FrameCount;offsetFps=avi!=null?avi.NominalFramesPerSecond:videoPrepared&&player!=null?player.frameRate:clip.NominalFps;offsetForm=true;formOpen=true;offsetText="";formError=null;pause?.Invoke();}
            GUI.enabled=true;
        }
        GUI.enabled=true;if(formOpen)DrawImportForm(scale);
    }
    private void ChooseImportFolder()
    {
        // AppKit runs its native panel modally. Pause before posting it, so the
        // first Unity frame after it closes cannot add the dialog's elapsed time.
        pause?.Invoke();pickerRun=runDirectory;folderPicker=ReplayFolderPicker.ChooseMediaAsync();
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
        GUI.Box(formRect,GUIContent.none);float x=formRect.x+16*scale,y=formRect.y+12*scale,w=width-32*scale,row=30*scale;
        GUI.Label(new Rect(x,y,w,row),offsetForm?"Link footage with an explicit offset":"Import finished tracker footage",textStyle);y+=row+8*scale;
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
    private void SubmitForm()
    {
        double? offset=null;
        if(!string.IsNullOrWhiteSpace(offsetText))
        {
            if(!(double.TryParse(offsetText,NumberStyles.Float,CultureInfo.InvariantCulture,out double value) || double.TryParse(offsetText,NumberStyles.Float,CultureInfo.CurrentCulture,out value)) || double.IsNaN(value) || double.IsInfinity(value))
            {formError="Enter a finite number of seconds.";return;}offset=value;
        }
        if(offsetForm)
        {
            if(!offset.HasValue){formError="Enter an explicit offset; no automatic offset is assumed.";return;}
            string selected=offsetClipId;long count=offsetFrameCount;double fps=offsetFps;
            RunOperation(token=>MediaArchive.SetOperatorOffsetAsync(runDirectory,selected,offset.Value,count,fps,token));
        }
        else
        {
            var request=new MediaImportRequest{RunDirectory=runDirectory,SourceDirectory=sourceFolder,RigId=PreviewRig??"VR1",SourceClosed=sourceClosed,SyncMode=offset.HasValue?"operator-offset":"unavailable",ManualOffsetSeconds=offset,ReplayOriginTicks=originTicks};
            RunOperation(token=>MediaArchive.ImportAsync(request,token));
        }
        TextFocused=false;GUI.FocusControl(null);
    }
    private void RunOperation(Func<CancellationToken,Task<MediaArchiveReceipt>> operation)
    {
        operationCancellation?.Dispose();operationCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        operationRun=runDirectory;
        try {pendingOperation=operation(operationCancellation.Token);}
        catch(Exception failure){formError=failure.Message;error=failure.Message;operationCancellation.Dispose();operationCancellation=null;}
    }
    private void ResetClip()
    {
        generation++;lastSeconds=double.NaN;clipCancellation?.Cancel();clipCancellation?.Dispose();clipCancellation=null;avi?.Dispose();avi=null;clip=null;displayedFrame=requestedFrame=failedFrame=-1;displayedGeneration=-1;displayedAssociation=requestedAssociation=null;videoPrepared=videoSeeking=false;
        if(pendingAvi!=null){pendingAvi.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();else _=t.Exception;});pendingAvi=null;}
        Observe(pendingFrame);pendingFrame=null;
        if(frameTexture!=null){UnityEngine.Object.Destroy(frameTexture);frameTexture=null;}
        if(player!=null){player.prepareCompleted-=Prepared;player.seekCompleted-=SeekCompleted;player.errorReceived-=VideoError;player.frameReady-=InitialFrame;player.Stop();player=null;}
        if(playerObject!=null){UnityEngine.Object.Destroy(playerObject);playerObject=null;}
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;lifetime.Cancel();loadCancellation?.Cancel();operationCancellation?.Cancel();Observe(pendingArchive);Observe(pendingOperation);ResetClip();loadCancellation?.Dispose();operationCancellation?.Dispose();lifetime.Dispose();
    }
    private static void Observe(Task task)
    {task?.ContinueWith(completed=>{_=completed.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);}
}
