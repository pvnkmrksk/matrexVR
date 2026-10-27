using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

/// <summary>Saved-run viewer with one acquisition timeline, disk-indexed agent frames and a default God's-eye view.</summary>
public partial class ReplayController : MonoBehaviour
{
    public string sessionFolder;
    public string absoluteSessionPath;
    public GameObject rigMarkerPrefab;
    public bool createRigCameras = true;
    public float targetHorizontalFov = 110f;
    public float playbackSpeed = 1f;
    public bool loop = true;
    public Slider scrubSlider;
    public KeyCode togglePlayKey = KeyCode.Space;
    public KeyCode backKey = KeyCode.LeftArrow;
    public KeyCode forwardKey = KeyCode.RightArrow;
    public KeyCode prevStepKey = KeyCode.N;
    public KeyCode nextStepKey = KeyCode.M;
    public float smallStepSeconds = 1f;
    public float largeStepSeconds = 10f;
    public bool pauseOnSeek = true;
    public bool autoResumeAfterSeek = true;
    public RectTransform stepMarkerContainer;
    public GameObject stepMarkerPrefab;
    public ReplayEnvironmentLoader environmentLoader;
    public bool showOverviewOnStart = true;
    public bool loadRecordedAgents = true;
    public bool interpolateAnimalPoses = true;
    [Tooltip("Preserve the saved panel camera projection and letterbox it instead of stretching the animal's view.")]
    public bool matchRecordedCamera = true;
    [Tooltip("Never interpolate a larger position jump; resets and periodic wrapping stay discontinuous.")]
    public float maximumInterpolationDistance = 50f;

    public double ReplayTime => replayTime;
    public double Duration => archive?.Duration ?? 0;
    public bool IsPlaying => isPlaying;
    public bool Ready => archive != null;
    public bool Loading => loading;
    public bool OverviewVisible => selectedView < 0;
    public string LoadError { get; private set; }
    public ReplaySessionArchive Session => archive;
    public int AnimalCount => rigOrder.Count;
    public int PopulationRecordingCount => populations.Count;

    private sealed class Population
    {
        public ReplayAgentStream stream;
        public readonly Dictionary<int,GameObject> visuals = new Dictionary<int,GameObject>();
        public readonly HashSet<int> active = new HashSet<int>();
        public int contextVersion=-1;
    }
    private sealed class LoadedRun
    {
        public ReplaySessionArchive archive;
        public readonly List<ReplayAgentStream> agents = new List<ReplayAgentStream>();
    }
    private static string pendingSessionPath;
    private readonly Dictionary<string,GameObject> markerByRig = new Dictionary<string,GameObject>();
    private readonly Dictionary<string,Camera> camerasByRig = new Dictionary<string,Camera>();
    private sealed class RecordedPath
    {
        public LineRenderer line;
        public int start=-1,end=-1;
        public Bounds bounds;
        public readonly List<Vector3> points=new List<Vector3>(5000);
    }
    private readonly Dictionary<string,RecordedPath> pathsByRig = new Dictionary<string,RecordedPath>();
    private readonly List<string> rigOrder = new List<string>();
    private readonly List<ReplaySessionArchive.Sample> stepChanges = new List<ReplaySessionArchive.Sample>();
    private readonly List<Population> populations = new List<Population>();
    private ReplaySessionArchive archive;
    private Transform generatedRoot, populationRoot;
    private Camera overview;
    private double replayTime;
    private bool isPlaying=true, loading, pathFocused, showDetails;
    private int selectedView=-1;
    private string folderInput="", loadProgress="", cacheDirectory;
    private Task<LoadedRun> pendingLoad;
    private ReplaySessionArchive.Sample referenceSample;
    private Bounds trajectoryBounds;
    private bool pointerTrace;
    private int replayPointerControl;
    private bool timelineMouseCaptured,timelineMouseUpSeen;
    private Rect timelineMouseRect;
    private int timelineMouseFrame,timelineMouseUpFrame;
    private Task<string> folderPicker;
    private int cameraLayoutVersion=-1;
    private Vector3 overviewPivot;
    private float overviewDistance=20,overviewYaw,overviewPitch=90;
    private bool overviewUserAdjusted,overviewPerspective;
    private int overviewDragButton=-1;
    private Vector2 overviewDragPosition;
    private ReplayMediaPanel mediaPanel;
    private RenderTexture animalPreviewTexture;
    private Camera previewCamera;
    private RecordedDashboardLink recordedDashboard;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()=>pendingSessionPath=null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LaunchFromArguments()
    {
        string[] arguments=Environment.GetCommandLineArgs();
        for(int i=1;i<arguments.Length;i++)
        {
            if((arguments[i]=="--replay-session" || arguments[i]=="-replay-session") && i+1<arguments.Length)
            {OpenSession(arguments[i+1]);return;}
        }
    }
    public static void OpenSession(string directory)
    {
        if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException("Choose a saved run folder.");
        pendingSessionPath=Path.GetFullPath(directory.Trim().Trim('"'));
        MainController.Instance?.StopSequence();
        SceneManager.LoadScene("ReplayScene");
    }

    private void Start()
    {
        pointerTrace=Array.IndexOf(Environment.GetCommandLineArgs(),"--replay-pointer-trace")>=0;
        MainController.Instance?.StopSequence();
        generatedRoot=new GameObject("Replay generated visuals").transform;generatedRoot.SetParent(transform,false);
        populationRoot=new GameObject("Recorded populations").transform;populationRoot.SetParent(generatedRoot,false);
        if(environmentLoader==null)environmentLoader=GetComponent<ReplayEnvironmentLoader>()??gameObject.AddComponent<ReplayEnvironmentLoader>();
        overview=Camera.main;
        if(overview==null)overview=new GameObject("Replay overview",typeof(Camera)).GetComponent<Camera>();
        overview.depth=10;overview.rect=CurrentReplayViewport();overview.clearFlags=CameraClearFlags.SolidColor;
        overview.backgroundColor=new Color(.04f,.055f,.075f);overview.orthographic=true;
        if(scrubSlider!=null)scrubSlider.gameObject.SetActive(false); // Runtime controls work without scene wiring.
        cacheDirectory=Path.Combine(Application.temporaryCachePath,"MatrexReplay",Guid.NewGuid().ToString("N"));
        selectedView=showOverviewOnStart?-1:0;
        folderInput=ResolveSessionPath()??"";
        if(!string.IsNullOrEmpty(folderInput))LoadSession(folderInput);
    }

    private string ResolveSessionPath()
    {
        if(!string.IsNullOrEmpty(pendingSessionPath)){string path=pendingSessionPath;pendingSessionPath=null;return path;}
        if(!string.IsNullOrEmpty(absoluteSessionPath) && Directory.Exists(absoluteSessionPath))return absoluteSessionPath;
        string root=RuntimePaths.RunDataRoot;
        if(!string.IsNullOrEmpty(sessionFolder))return Path.Combine(root,sessionFolder);
        return ReplaySessionArchive.NewestRunFolder(root);
    }

    public void LoadSession(string directory)
    {
        if(loading)return;
        timelineMouseCaptured=timelineMouseUpSeen=false;
        StartCoroutine(LoadRun(directory));
    }
    private IEnumerator LoadRun(string directory)
    {
        loading=true;LoadError=null;loadProgress="Reading saved configurations and animal logs…";
        var saving=MasterDataLogger.Instance;
        string selectedPath=null;
        try {selectedPath=Path.GetFullPath(directory.Trim().Trim('"'));}catch(Exception failure){LoadError=failure.Message;}
        if(LoadError!=null){loading=false;yield break;}
        if(saving!=null && saving.IsFinalizing && string.Equals(selectedPath,Path.GetFullPath(saving.directoryPath),Application.platform==RuntimePlatform.WindowsPlayer || Application.platform==RuntimePlatform.OSXPlayer?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
        {
            loadProgress="Finishing this recording before replay…";
            while(saving!=null && saving.IsFinalizing)yield return null;
        }
        string cache=cacheDirectory??Path.Combine(Application.temporaryCachePath,"MatrexReplay",Guid.NewGuid().ToString("N"));
        bool includeAgents=loadRecordedAgents;
        pendingLoad=Task.Run(()=>
        {
            var run=new LoadedRun();
            try
            {
                run.archive=new ReplaySessionArchive(directory);run.archive.LoadRigSamples();
                if(includeAgents)
                    foreach(string file in ReplaySessionArchive.CsvFiles(run.archive.DirectoryPath))
                    {
                        try
                        {
                            var stream=ReplayAgentStream.TryOpen(file,cache,run.archive.OriginTicks);
                            if(stream==null)continue;
                            run.agents.Add(stream);
                            if(!stream.HasRotations)run.archive.Warn(Path.GetFileName(file)+": historical agent positions have no orientation; heading/wing animation cannot be reconstructed exactly.");
                            if(stream.Kind=="Band" && !stream.HasVisibility)run.archive.Warn(Path.GetFileName(file)+": historical visibility phase did not capture the actual per-agent timer; exact blinking cannot be recovered and bodies are shown continuously.");
                            if(stream.HasDuplicateNames)run.archive.Warn(Path.GetFileName(file)+": duplicate agent names are distinguished by row order within each frame.");
                        }
                        catch(Exception error){run.archive.Warn("Population recording unavailable: "+Path.GetFileName(file)+" — "+error.Message);}
                    }
                if(run.agents.Count==0)run.archive.Warn("No recorded population tracks were loaded. Unrecorded agents are not resimulated as if they were the original experiment.");
                return run;
            }
            catch {foreach(var stream in run.agents)stream.Dispose();throw;}
        });
        while(!pendingLoad.IsCompleted)yield return null;
        if(pendingLoad.IsFaulted)
        {LoadError=pendingLoad.Exception?.GetBaseException().Message;loading=false;pendingLoad=null;yield break;}
        var loaded=pendingLoad.Result;pendingLoad=null;
        // Finish the preceding additive scene operation before replacing its archive.
        while(environmentLoader!=null && environmentLoader.Loading)yield return null;
        ClearSessionVisuals();archive=loaded.archive;
        foreach(var stream in loaded.agents)populations.Add(new Population{stream=stream});
        absoluteSessionPath=archive.DirectoryPath;folderInput=absoluteSessionPath;replayTime=archive.FirstExperimentTime;referenceSample=null;
        BuildMarkers();BuildStepTimeline();
        environmentLoader.BeginSession(archive,rigOrder);
        mediaPanel=new ReplayMediaPanel(this,()=>isPlaying=false);mediaPanel.SetRigs(rigOrder);mediaPanel.Load(archive.DirectoryPath,archive.OriginTicks);
        FitOverview();ApplyView();RenderFrame();
        loading=false;loadProgress="";
    }

    private void BuildMarkers()
    {
        foreach(var pair in archive.Rigs.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            string id=pair.Key;rigOrder.Add(id);
            var marker=new GameObject("Recorded animal "+id);marker.transform.SetParent(generatedRoot,false);
            GameObject body=rigMarkerPrefab!=null?Instantiate(rigMarkerPrefab,marker.transform):GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(marker.transform,false);body.name="Operator marker body "+id;
            ReplayEnvironmentLoader.FreezeVisual(marker);
            SetOverlayLayer(marker);markerByRig[id]=marker;
            var annotation=marker.AddComponent<ReplayAgentVisuals>();
            annotation.Initialize(Mathf.Max(1,RuntimeRigRegistry.ParseIndex(ReplayEnvironmentLoader.CanonicalRig(id))),body.GetComponentInChildren<Renderer>());
            agentAnnotations[id]=annotation;
            if(createRigCameras)
            {
                var camera=new GameObject("Replay animal view "+id,typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(marker.transform,false);camera.nearClipPlane=.01f;camera.farClipPlane=100000;
                camera.rect=CurrentReplayViewport();camera.depth=11;camera.clearFlags=CameraClearFlags.Skybox;
                camera.fieldOfView=2*Mathf.Atan(Mathf.Tan(targetHorizontalFov*Mathf.Deg2Rad*.5f)/Mathf.Max(.1f,camera.pixelWidth/(float)Mathf.Max(1,camera.pixelHeight)))*Mathf.Rad2Deg;
                camera.cullingMask=AnimalCameraMask(id);
                camerasByRig[id]=camera;
            }
        }
        foreach(var pair in archive.Rigs)
        {
            var path=new GameObject("Recorded path "+pair.Key,typeof(LineRenderer));path.transform.SetParent(generatedRoot,false);
            SetOverlayLayer(path);var line=path.GetComponent<LineRenderer>();line.useWorldSpace=true;
            Shader shader=Shader.Find("Sprites/Default");
            if(shader!=null)
            {
                var sourceMaterial=new Material(shader){name="Replay path material"};
                RuntimeMaterialOwner.Assign(line,sourceMaterial);Destroy(sourceMaterial);
            }
            Color color=ReplayAgentVisuals.RigColor(Mathf.Max(1,RuntimeRigRegistry.ParseIndex(ReplayEnvironmentLoader.CanonicalRig(pair.Key))));
            line.startColor=line.endColor=new Color(color.r,color.g,color.b,.2f);
            var recordedPath=new RecordedPath{line=line};pathsByRig[pair.Key]=recordedPath;
            line.enabled=replayTime>=pair.Value[0].time && replayTime<=pair.Value[pair.Value.Count-1].time+.05;
            UpdateRecordedPath(recordedPath,pair.Value,AtOrBefore(pair.Value,replayTime));
        }
        RefreshTrajectoryBounds();
    }

    private static void ReplayPathRange(List<ReplaySessionArchive.Sample> samples,int cursor,float maximumJump,out int start,out int end)
    {
        start=end=-1;if(samples==null || samples.Count==0)return;
        start=end=Math.Max(0,Math.Min(samples.Count-1,cursor));
        string context=samples[start].ContextKey;
        bool Connected(int a,int b)=>samples[b].ContextKey==context &&
            (maximumJump<=0 || Vector3.Distance(samples[a].position,samples[b].position)<=maximumJump);
        while(start>0 && Connected(start,start-1))start--;
        while(end+1<samples.Count && Connected(end,end+1))end++;
    }
    private bool UpdateRecordedPath(RecordedPath path,List<ReplaySessionArchive.Sample> samples,int cursor)
    {
        if(path.start>=0 && cursor>=path.start && cursor<=path.end)return false;
        ReplayPathRange(samples,cursor,maximumInterpolationDistance,out path.start,out path.end);
        path.points.Clear();path.bounds=new Bounds(samples[path.start].position,Vector3.zero);
        for(int i=path.start;i<=path.end;i++)path.bounds.Encapsulate(samples[i].position);
        int stride=Math.Max(1,(path.end-path.start+4998)/4999);
        for(int i=path.start;i<=path.end;i+=stride)path.points.Add(samples[i].position);
        if((path.end-path.start)%stride!=0)path.points.Add(samples[path.end].position);
        path.line.positionCount=path.points.Count;path.line.SetPositions(path.points.ToArray());
        return true;
    }
    private void RefreshTrajectoryBounds()
    {
        bool first=true;
        foreach(var path in pathsByRig.Values)
        {
            if(!path.line.enabled || path.start<0)continue;
            if(first){trajectoryBounds=path.bounds;first=false;}
            else{trajectoryBounds.Encapsulate(path.bounds.min);trajectoryBounds.Encapsulate(path.bounds.max);}
        }
        if(first)trajectoryBounds=new Bounds(Vector3.zero,Vector3.one);
        float size=Mathf.Max(.15f,Mathf.Max(trajectoryBounds.size.x,trajectoryBounds.size.z)*.008f);
        foreach(var path in pathsByRig.Values)path.line.widthMultiplier=Mathf.Max(.015f,size*.12f);
    }

    public void FitOverview()
    {
        if(overview==null)return;
        float span=Mathf.Max(5,Mathf.Max(trajectoryBounds.size.x,trajectoryBounds.size.z));
        overviewPivot=trajectoryBounds.center;overviewYaw=0;overviewPitch=90;
        overviewDistance=trajectoryBounds.extents.y+span*2+10;overviewUserAdjusted=false;overviewDragButton=-1;
        overview.rect=CurrentReplayViewport();
        float aspect=Mathf.Max(.1f,overview.pixelWidth/(float)Mathf.Max(1,overview.pixelHeight));
        overview.orthographicSize=Mathf.Max(2,Mathf.Max(trajectoryBounds.extents.z,trajectoryBounds.extents.x/aspect))*1.15f;
        if(overviewPerspective)overviewDistance=overview.orthographicSize/Mathf.Tan(25*Mathf.Deg2Rad);
        ApplyOverviewPose();
        UpdateReplayCameraClipping();
    }
    private void ApplyView()
    {
        if(selectedView>=rigOrder.Count || !createRigCameras)selectedView=-1;
        if(overview!=null)overview.enabled=selectedView<0;
        if(selectedView<0 && !overviewUserAdjusted)FitOverview();
        if(selectedView>=0)overviewDragButton=-1;
        for(int i=0;i<rigOrder.Count;i++)if(camerasByRig.TryGetValue(rigOrder[i],out var camera)){camera.targetTexture=null;FitReplayAnimalViewport(camera);camera.enabled=selectedView==i;}
    }
    public void ShowOverview(){selectedView=-1;ApplyView();}
    private void CycleView(){selectedView++;if(selectedView>=rigOrder.Count)selectedView=-1;ApplyView();}

    private void ApplyOverviewPose()
    {
        if(overview==null)return;
        overview.orthographic=!overviewPerspective;overview.fieldOfView=50;
        var rotation=Quaternion.Euler(overviewPitch,overviewYaw,0);
        overview.transform.SetPositionAndRotation(overviewPivot-rotation*Vector3.forward*overviewDistance,rotation);
        UpdateReplayCameraClipping();
    }
    private void ToggleOverviewProjection()
    {
        if(overview==null)return;
        overviewPerspective=!overviewPerspective;
        if(overviewPerspective)overviewDistance=Mathf.Max(.01f,overview.orthographicSize/Mathf.Tan(25*Mathf.Deg2Rad));
        else {overview.orthographicSize=Mathf.Max(.01f,overviewDistance*Mathf.Tan(25*Mathf.Deg2Rad));overviewPitch=90;overviewYaw=0;}
        overviewUserAdjusted=true;ApplyOverviewPose();
    }
    private void UpdateOverviewControls(UnityEngine.InputSystem.Mouse mouse)
    {
        if(selectedView>=0 || overview==null || !Application.isFocused || pathFocused || showDetails || folderPicker!=null || mediaPanel?.HasModal==true)
        {overviewDragButton=-1;return;}
        if(mouse==null)overviewDragButton=-1;
        Vector2 position=mouse!=null?mouse.position.ReadValue():Vector2.zero;
        bool inImage=mouse!=null && overview.pixelRect.Contains(position) && !ContainsReplayChrome(position);
        if(overviewDragButton<0 && inImage)
        {
            if(mouse.middleButton.wasPressedThisFrame)overviewDragButton=2;
            else if(mouse.rightButton.wasPressedThisFrame)overviewDragButton=1;
            if(overviewDragButton>=0)overviewDragPosition=position;
        }
        if(overviewDragButton>=0)
        {
            bool held=overviewDragButton==2?mouse.middleButton.isPressed:mouse.rightButton.isPressed;
            Vector2 delta=position-overviewDragPosition;overviewDragPosition=position;
            if(delta.sqrMagnitude>0)
            {
                if(overviewDragButton==1)
                {overviewYaw+=delta.x*.2f;overviewPitch=Mathf.Clamp(overviewPitch-delta.y*.2f,10,90);}
                else
                {
                    float height=overviewPerspective?2*overviewDistance*Mathf.Tan(25*Mathf.Deg2Rad):2*overview.orthographicSize;
                    overviewPivot-=(overview.transform.right*delta.x+overview.transform.up*delta.y)*(height/Mathf.Max(1,overview.pixelHeight));
                }
                overviewUserAdjusted=true;ApplyOverviewPose();
            }
            if(!held)overviewDragButton=-1;
        }
        if(inImage && Mathf.Abs(ExperimentInput.Scroll)>.00001f)
        {
            float factor=Mathf.Exp(-ExperimentInput.Scroll);
            if(overviewPerspective)overviewDistance=Mathf.Clamp(overviewDistance*factor,.01f,1000000);
            else overview.orthographicSize=Mathf.Clamp(overview.orthographicSize*factor,.01f,100000);
            overviewUserAdjusted=true;ApplyOverviewPose();
        }
        var keyboard=UnityEngine.InputSystem.Keyboard.current;
        if(keyboard!=null)
        {
            float Horizontal(UnityEngine.InputSystem.Controls.KeyControl positive,UnityEngine.InputSystem.Controls.KeyControl negative)=>(positive.isPressed?1:0)-(negative.isPressed?1:0);
            Vector3 movement=new Vector3(Horizontal(keyboard.dKey,keyboard.aKey),Horizontal(keyboard.eKey,keyboard.qKey),Horizontal(keyboard.wKey,keyboard.sKey));
            float yaw=Horizontal(keyboard.lKey,keyboard.jKey),pitch=Horizontal(keyboard.kKey,keyboard.iKey);
            if(movement.sqrMagnitude>0 || yaw!=0 || pitch!=0)
            {
                float dt=Mathf.Min(.1f,Time.unscaledDeltaTime),height=overviewPerspective?2*overviewDistance*Mathf.Tan(25*Mathf.Deg2Rad):2*overview.orthographicSize;
                Vector3 right=Quaternion.Euler(0,overviewYaw,0)*Vector3.right,forward=Quaternion.Euler(0,overviewYaw,0)*Vector3.forward;
                float boost=keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed?3:1;
                overviewPivot+=(right*movement.x+Vector3.up*movement.y+forward*movement.z)*(height*.5f*boost*dt);
                overviewYaw+=yaw*45*dt;overviewPitch=Mathf.Clamp(overviewPitch+pitch*45*dt,10,90);
                overviewUserAdjusted=true;ApplyOverviewPose();
            }
        }
    }

    private void Update()
    {
        // Retain the blocked state for the frame that consumes a native picker.
        bool modalOpen=folderPicker!=null || mediaPanel?.HasModal==true;
        if(modalOpen)isPlaying=false;
        var mouse=UnityEngine.InputSystem.Mouse.current;
        if(pointerTrace && mouse!=null && (mouse.leftButton.wasPressedThisFrame || mouse.leftButton.wasReleasedThisFrame))
            Debug.Log("[Replay pointer] InputSystem frame="+Time.frameCount+" position="+mouse.position.ReadValue()+" down="+mouse.leftButton.wasPressedThisFrame);
        if(folderPicker!=null && folderPicker.IsCompleted)
        {
            if(folderPicker.IsFaulted)LoadError=folderPicker.Exception?.GetBaseException().Message;
            else if(!string.IsNullOrWhiteSpace(folderPicker.Result)){folderInput=folderPicker.Result;LoadSession(folderInput);}
            folderPicker=null;
        }
        bool scrubbing=UpdateTimelineScrub(mouse);
        if(archive==null || loading)return;
        ApplyRecordedDashboardCommands(modalOpen);
        if(!pathFocused && !modalOpen && mediaPanel?.TextFocused!=true)
        {
            if(ExperimentInput.Pressed(togglePlayKey,KeyCode.Space,"Pause"))isPlaying=!isPlaying;
            float step=ExperimentInput.Held("SpeedModifier")?largeStepSeconds:smallStepSeconds;
            if(ExperimentInput.Pressed(backKey,KeyCode.LeftArrow,"Back"))Seek(replayTime-step);
            if(ExperimentInput.Pressed(forwardKey,KeyCode.RightArrow,"Forward"))Seek(replayTime+step);
            if(ExperimentInput.Pressed(prevStepKey,KeyCode.N,"PreviousStep"))JumpToStep(-1);
            if(ExperimentInput.Pressed(nextStepKey,KeyCode.M,"NextStep"))JumpToStep(1);
            UpdateOverviewControls(mouse);
        }
        else overviewDragButton=-1;
        if(isPlaying && !scrubbing && !modalOpen && (environmentLoader==null || !environmentLoader.Loading))
        {
            replayTime+=Time.unscaledDeltaTime*Mathf.Max(.01f,playbackSpeed);
            if(replayTime>Duration){if(loop && Duration>0)replayTime%=Duration;else{replayTime=Duration;isPlaying=false;}}
        }
        RenderFrame();
    }
    private void ApplyRecordedDashboardCommands(bool modalOpen)
    {
        if(recordedDashboard==null)return;
        while(recordedDashboard.TryTakeCommand(out var command))
        {
            // Taking the command acknowledges it. Discard blocked requests so a
            // browser click during an import cannot resume or seek after Cancel.
            if(modalOpen)continue;
            if(command.Kind=="seek")Seek(command.Seconds);
            else if(command.Kind=="play")isPlaying=true;
            else if(command.Kind=="pause")isPlaying=false;
        }
    }
    private void RenderFrame()
    {
        if(archive==null)return;
        if(environmentLoader!=null && !environmentLoader.Loading && cameraLayoutVersion!=environmentLoader.ContextVersion)
        {
            foreach(var pair in camerasByRig)
            {
                pair.Value.cullingMask=AnimalCameraMask(pair.Key);
                if(matchRecordedCamera)environmentLoader.ConfigureAnimalCamera(pair.Key,pair.Value);
                FitReplayAnimalViewport(pair.Value);
            }
            cameraLayoutVersion=environmentLoader.ContextVersion;
        }
        referenceSample=null;
        ReplaySessionArchive.Sample environmentSample=null;
        bool pathsChanged=false;
        foreach(string id in rigOrder)
        {
            var samples=archive.Rigs[id];int cursor=AtOrBefore(samples,replayTime);var a=samples[cursor];
            bool visible=replayTime>=samples[0].time && replayTime<=samples[samples.Count-1].time+.05;
            var marker=markerByRig[id];marker.SetActive(visible);
            if(pathsByRig.TryGetValue(id,out var path))
            {
                if(path.line.enabled!=visible){path.line.enabled=visible;pathsChanged=true;}
                if(visible)pathsChanged|=UpdateRecordedPath(path,samples,cursor);
            }
            if(!visible)continue;
            var b=cursor+1<samples.Count?samples[cursor+1]:a;
            float amount=interpolateAnimalPoses && a.ContextKey==b.ContextKey && Vector3.Distance(a.position,b.position)<=maximumInterpolationDistance && b.time>a.time?(float)Math.Max(0,Math.Min(1,(replayTime-a.time)/(b.time-a.time))):0;
            Vector3 position=Vector3.Lerp(a.position,b.position,amount);
            Quaternion rotation=Quaternion.Slerp(Quaternion.Euler(a.rotation),Quaternion.Euler(b.rotation),amount);
            marker.transform.SetPositionAndRotation(position,rotation);
            environmentLoader?.DriveRecordedRig(id,position,rotation);
            environmentLoader?.ApplyAsynchronousSample(a);
            if(matchRecordedCamera && camerasByRig.TryGetValue(id,out var animalCamera))
            {environmentLoader?.ConfigureAnimalCamera(id,animalCamera,a);FitReplayAnimalViewport(animalCamera);}
            if(environmentSample==null)environmentSample=a;
            if(referenceSample==null || (selectedView>=0 && rigOrder[selectedView]==id))referenceSample=a;
        }
        if(pathsChanged){RefreshTrajectoryBounds();if(selectedView<0 && !overviewUserAdjusted)FitOverview();}
        if(environmentSample!=null)environmentLoader?.ApplyRecordedSample(environmentSample);
        float fallbackSize=Mathf.Max(.1f,Mathf.Max(trajectoryBounds.size.x,trajectoryBounds.size.z)*.002f);
        foreach(var population in populations)
        {
            if(environmentLoader.Loading){foreach(var visual in population.visuals.Values)if(visual!=null)visual.SetActive(false);continue;}
            if(population.contextVersion!=environmentLoader.ContextVersion)
            {
                foreach(var visual in population.visuals.Values)if(visual!=null){visual.SetActive(false);Destroy(visual);}
                population.visuals.Clear();population.contextVersion=environmentLoader.ContextVersion;
            }
            population.active.Clear();
            foreach(var pose in population.stream.Sample(replayTime))
            {
                if(!population.visuals.TryGetValue(pose.id,out var visual) || visual==null)
                {
                    visual=environmentLoader.CreateAgentVisual(population.stream.Kind,pose.name,pose.layer,populationRoot,fallbackSize);
                    population.visuals[pose.id]=visual;
                }
                visual.SetActive(pose.visible);visual.transform.SetPositionAndRotation(pose.position,Quaternion.Euler(pose.rotation));population.active.Add(pose.id);
            }
            foreach(var pair in population.visuals)if(pair.Value!=null && !population.active.Contains(pair.Key))pair.Value.SetActive(false);
        }
        if(scrubSlider!=null && Duration>0)scrubSlider.SetValueWithoutNotify((float)(replayTime/Duration));
        UpdateAnimalPreview();
        RefreshReplayAnnotations();
        mediaPanel?.Tick(replayTime,isPlaying && !timelineMouseCaptured && environmentLoader?.Loading!=true,playbackSpeed);
        if(mediaPanel!=null)
        {
            foreach(string warning in mediaPanel.Warnings)archive.Warn("Tracker video: "+warning);
            if(!string.IsNullOrEmpty(mediaPanel.Error))archive.Warn("Tracker video: "+mediaPanel.Error);
        }
        recordedDashboard?.UpdateClock(replayTime,isPlaying && environmentLoader?.Loading!=true,playbackSpeed,Duration);
    }
    private void UpdateAnimalPreview()
    {
        if(mediaPanel==null)return;
        if(selectedView>=0 || !mediaPanel.Expanded || OperatorOverlayVisibility.Hidden)
        {
            if(previewCamera!=null){previewCamera.targetTexture=null;if(selectedView<0)previewCamera.enabled=false;previewCamera=null;}
            mediaPanel.SetAnimalPreview(null,null);return;
        }
        string id=mediaPanel.PreviewRig;
        if(id==null || !camerasByRig.TryGetValue(id,out var camera))return;
        if(previewCamera!=null && previewCamera!=camera){previewCamera.targetTexture=null;previewCamera.enabled=false;}
        var saved=(archive?.SystemConfiguration?["configs"] as JArray)?.OfType<JObject>().FirstOrDefault(config=>(string)config["vrId"]==id);
        bool desktop=(bool?)saved?["desktopPreview"]==true;
        int width=desktop?512:Mathf.Clamp((int?)saved?["ledPanelWidth"]??128,1,1024);
        int height=desktop?Mathf.Max(1,Mathf.RoundToInt(width/Mathf.Max(.01f,camera.aspect))):Mathf.Clamp((int?)saved?["ledPanelHeight"]??128,1,1024);
        height=Mathf.Clamp(height,1,1024);
        if(animalPreviewTexture==null || animalPreviewTexture.width!=width || animalPreviewTexture.height!=height)
        {
            if(previewCamera!=null)previewCamera.targetTexture=null;
            if(animalPreviewTexture!=null){animalPreviewTexture.Release();Destroy(animalPreviewTexture);}
            animalPreviewTexture=new RenderTexture(width,height,24){name="Replay front panel preview",filterMode=desktop?FilterMode.Bilinear:FilterMode.Point};animalPreviewTexture.Create();
        }
        previewCamera=camera;camera.targetTexture=animalPreviewTexture;camera.rect=new Rect(0,0,1,1);camera.enabled=environmentLoader?.Loading!=true;
        mediaPanel.SetAnimalPreview(animalPreviewTexture,"Animal "+id+" · front"+(environmentLoader?.Loading==true?" · restoring scene":""));
    }
    public static int AtOrBefore(List<ReplaySessionArchive.Sample> samples,double time)
    {
        int lo=0,hi=samples.Count-1;
        while(lo<hi){int mid=(lo+hi+1)/2;if(samples[mid].time<=time)lo=mid;else hi=mid-1;}return lo;
    }
    public static int AnimalCameraMask(string id)
    {
        int isolated=RuntimeRigRegistry.StimulusMask|LayerMask.GetMask("ChoiceVR1","ChoiceVR2","ChoiceVR3","ChoiceVR4");
        int index=RuntimeRigRegistry.ParseIndex(ReplayEnvironmentLoader.CanonicalRig(id));
        int own=RuntimeRigRegistry.StimulusLayer(index),objects=ReplayEnvironmentLoader.ObjectLayerForRig(id);
        int visible=(own>=0?1<<own:0)|(objects>=0?1<<objects:0);
        return (~isolated|visible)&~LayerMask.GetMask("OverheadCameraMarkers","UI");
    }
    private static void SetOverlayLayer(GameObject target)
    {
        int layer=LayerMask.NameToLayer("OverheadCameraMarkers");
        if(layer<0)return;
        target.layer=layer;foreach(var child in target.GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;
    }
    private void Seek(double seconds)
    {
        replayTime=Math.Max(0,Math.Min(Duration,seconds));
        if(pauseOnSeek && !autoResumeAfterSeek)isPlaying=false;
        RenderFrame();
    }
    public void SeekSeconds(double seconds)=>Seek(seconds);
    public void SetScrubNormalized(float normalized){if(archive!=null)Seek(Mathf.Clamp01(normalized)*Duration);}
    public void SetPlaying(bool playing)=>isPlaying=playing;
    public bool TryCloseModal()=>folderPicker!=null || mediaPanel?.CancelModal()==true;
    private void BuildStepTimeline()
    {
        stepChanges.Clear();if(rigOrder.Count==0)return;
        foreach(string rig in rigOrder)
        {
            string key=null;
            foreach(var sample in archive.Rigs[rig])if(sample.ContextKey!=key){stepChanges.Add(sample);key=sample.ContextKey;}
        }
        var union=stepChanges.GroupBy(s=>s.time.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+s.ContextKey)
            .Select(g=>g.First()).OrderBy(s=>s.time).ToArray();
        stepChanges.Clear();stepChanges.AddRange(union);
    }
    private void JumpToStep(int direction)
    {
        if(stepChanges.Count==0)return;int index=0;
        while(index+1<stepChanges.Count && stepChanges[index+1].time<=replayTime)index++;
        Seek(stepChanges[Mathf.Clamp(index+direction,0,stepChanges.Count-1)].time);
    }

    private void ClearSessionVisuals()
    {
        timelineMouseCaptured=timelineMouseUpSeen=false;
        overviewUserAdjusted=false;overviewDragButton=-1;
        mediaPanel?.Dispose();mediaPanel=null;recordedDashboard?.CloseSession();recordedDashboard=null;
        if(previewCamera!=null){previewCamera.targetTexture=null;previewCamera=null;}
        if(animalPreviewTexture!=null){animalPreviewTexture.Release();Destroy(animalPreviewTexture);animalPreviewTexture=null;}
        foreach(var population in populations){foreach(var visual in population.visuals.Values)if(visual!=null)Destroy(visual);population.stream.Dispose();}populations.Clear();
        foreach(Transform child in generatedRoot)if(child!=populationRoot){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        markerByRig.Clear();camerasByRig.Clear();pathsByRig.Clear();agentAnnotations.Clear();rigOrder.Clear();stepChanges.Clear();archive=null;
        clippingContextVersion=-1;clippingEnvironment=null;
    }
    private void OnDestroy()
    {
        mediaPanel?.Dispose();recordedDashboard?.CloseSession();
        if(previewCamera!=null)previewCamera.targetTexture=null;
        if(animalPreviewTexture!=null){animalPreviewTexture.Release();Destroy(animalPreviewTexture);}
        replayInterface?.Dispose();
        foreach(var population in populations)population.stream.Dispose();populations.Clear();
        if(pendingLoad!=null)
            pendingLoad.ContinueWith(task=>{if(task.Status==TaskStatus.RanToCompletion)foreach(var stream in task.Result.agents)stream.Dispose();});
        if(overview!=null && overview.gameObject.name=="Replay overview")Destroy(overview.gameObject);
    }
}
