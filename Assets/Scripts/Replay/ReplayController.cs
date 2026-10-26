using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Saved-run viewer with one acquisition timeline, disk-indexed agent frames and a default God's-eye view.</summary>
public class ReplayController : MonoBehaviour
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
    private Vector2 detailScroll;
    private Task<LoadedRun> pendingLoad;
    private ReplaySessionArchive.Sample referenceSample;
    private Bounds trajectoryBounds;
    private GUIStyle titleStyle, smallStyle;
    private GUISkin replaySkin;
    private Task<string> folderPicker;
    private int cameraLayoutVersion=-1;

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
        MainController.Instance?.StopSequence();
        generatedRoot=new GameObject("Replay generated visuals").transform;generatedRoot.SetParent(transform,false);
        populationRoot=new GameObject("Recorded populations").transform;populationRoot.SetParent(generatedRoot,false);
        if(environmentLoader==null)environmentLoader=GetComponent<ReplayEnvironmentLoader>()??gameObject.AddComponent<ReplayEnvironmentLoader>();
        overview=Camera.main;
        if(overview==null)overview=new GameObject("Replay overview",typeof(Camera)).GetComponent<Camera>();
        overview.depth=10;overview.rect=new Rect(0,.20f,1,.80f);overview.clearFlags=CameraClearFlags.SolidColor;
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
        StartCoroutine(LoadRun(directory));
    }
    private IEnumerator LoadRun(string directory)
    {
        loading=true;LoadError=null;loadProgress="Reading saved configurations and animal logs…";
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
        FitOverview();ApplyView();RenderFrame();
        loading=false;loadProgress="";
    }

    private void BuildMarkers()
    {
        bool first=true;
        foreach(var pair in archive.Rigs.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            string id=pair.Key;rigOrder.Add(id);
            GameObject marker=rigMarkerPrefab!=null?Instantiate(rigMarkerPrefab,generatedRoot):GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.SetParent(generatedRoot,false);marker.name="Recorded animal "+id;ReplayEnvironmentLoader.FreezeVisual(marker);
            SetOverlayLayer(marker);markerByRig[id]=marker;
            foreach(var sample in pair.Value)
            {if(first){trajectoryBounds=new Bounds(sample.position,Vector3.zero);first=false;}else trajectoryBounds.Encapsulate(sample.position);}
            if(createRigCameras)
            {
                var camera=new GameObject("Replay animal view "+id,typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(marker.transform,false);camera.nearClipPlane=.01f;camera.farClipPlane=100000;
                camera.rect=new Rect(0,.20f,1,.80f);camera.depth=11;camera.clearFlags=CameraClearFlags.Skybox;
                camera.fieldOfView=2*Mathf.Atan(Mathf.Tan(targetHorizontalFov*Mathf.Deg2Rad*.5f)/Mathf.Max(.1f,Screen.width/(Screen.height*.8f)))*Mathf.Rad2Deg;
                camera.cullingMask=AnimalCameraMask(id);
                camerasByRig[id]=camera;
            }
        }
        float size=Mathf.Max(.15f,Mathf.Max(trajectoryBounds.size.x,trajectoryBounds.size.z)*.008f);
        foreach(var marker in markerByRig.Values)if(rigMarkerPrefab==null)marker.transform.localScale=Vector3.one*size;
        foreach(var pair in archive.Rigs)
        {
            var path=new GameObject("Recorded path "+pair.Key,typeof(LineRenderer));path.transform.SetParent(generatedRoot,false);
            SetOverlayLayer(path);var line=path.GetComponent<LineRenderer>();line.useWorldSpace=true;
            int stride=Math.Max(1,(pair.Value.Count+4999)/5000);var points=new List<Vector3>();
            for(int i=0;i<pair.Value.Count;i+=stride)points.Add(pair.Value[i].position);
            line.positionCount=points.Count;line.SetPositions(points.ToArray());line.widthMultiplier=Mathf.Max(.015f,size*.12f);
            Shader shader=Shader.Find("Sprites/Default");
            if(shader!=null)
            {
                var sourceMaterial=new Material(shader){name="Replay path material"};
                RuntimeMaterialOwner.Assign(line,sourceMaterial);Destroy(sourceMaterial);
            }
            line.startColor=line.endColor=new Color(.3f,.7f,.85f,.45f);
        }
    }

    public void FitOverview()
    {
        if(overview==null)return;
        float span=Mathf.Max(5,Mathf.Max(trajectoryBounds.size.x,trajectoryBounds.size.z));
        overview.transform.SetPositionAndRotation(trajectoryBounds.center+Vector3.up*(trajectoryBounds.extents.y+span*2+10),Quaternion.Euler(90,0,0));
        float aspect=Mathf.Max(.1f,Screen.width/(Screen.height*.8f));
        overview.orthographicSize=Mathf.Max(2,Mathf.Max(trajectoryBounds.extents.z,trajectoryBounds.extents.x/aspect))*1.15f;
        overview.nearClipPlane=.01f;overview.farClipPlane=span*5+trajectoryBounds.size.y+1000;
    }
    private void ApplyView()
    {
        if(selectedView>=rigOrder.Count || !createRigCameras)selectedView=-1;
        if(overview!=null)overview.enabled=selectedView<0;
        for(int i=0;i<rigOrder.Count;i++)if(camerasByRig.TryGetValue(rigOrder[i],out var camera))camera.enabled=selectedView==i;
    }
    public void ShowOverview(){selectedView=-1;ApplyView();}
    private void CycleView(){selectedView++;if(selectedView>=rigOrder.Count)selectedView=-1;ApplyView();}

    private void Update()
    {
        if(folderPicker!=null && folderPicker.IsCompleted)
        {
            if(folderPicker.IsFaulted)LoadError=folderPicker.Exception?.GetBaseException().Message;
            else if(!string.IsNullOrWhiteSpace(folderPicker.Result)){folderInput=folderPicker.Result;LoadSession(folderInput);}
            folderPicker=null;
        }
        if(archive==null || loading)return;
        if(!pathFocused)
        {
            if(ExperimentInput.Pressed(togglePlayKey,KeyCode.Space,"Pause"))isPlaying=!isPlaying;
            float step=ExperimentInput.Held("SpeedModifier")?largeStepSeconds:smallStepSeconds;
            if(ExperimentInput.Pressed(backKey,KeyCode.LeftArrow,"Back"))Seek(replayTime-step);
            if(ExperimentInput.Pressed(forwardKey,KeyCode.RightArrow,"Forward"))Seek(replayTime+step);
            if(ExperimentInput.Pressed(prevStepKey,KeyCode.N,"PreviousStep"))JumpToStep(-1);
            if(ExperimentInput.Pressed(nextStepKey,KeyCode.M,"NextStep"))JumpToStep(1);
            if(selectedView<0 && overview!=null && !showDetails && ExperimentInput.MousePosition.y>Screen.height*.2f)
                overview.orthographicSize=Mathf.Clamp(overview.orthographicSize*Mathf.Exp(-ExperimentInput.Scroll),.01f,100000);
        }
        if(isPlaying && (environmentLoader==null || !environmentLoader.Loading))
        {
            replayTime+=Time.unscaledDeltaTime*Mathf.Max(.01f,playbackSpeed);
            if(replayTime>Duration){if(loop && Duration>0)replayTime%=Duration;else{replayTime=Duration;isPlaying=false;}}
        }
        RenderFrame();
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
            }
            cameraLayoutVersion=environmentLoader.ContextVersion;
        }
        referenceSample=null;
        ReplaySessionArchive.Sample environmentSample=null;
        foreach(string id in rigOrder)
        {
            var samples=archive.Rigs[id];int cursor=AtOrBefore(samples,replayTime);var a=samples[cursor];
            bool visible=replayTime>=samples[0].time && replayTime<=samples[samples.Count-1].time+.05;
            var marker=markerByRig[id];marker.SetActive(visible);if(!visible)continue;
            var b=cursor+1<samples.Count?samples[cursor+1]:a;
            float amount=interpolateAnimalPoses && a.ContextKey==b.ContextKey && Vector3.Distance(a.position,b.position)<=maximumInterpolationDistance && b.time>a.time?(float)Math.Max(0,Math.Min(1,(replayTime-a.time)/(b.time-a.time))):0;
            Vector3 position=Vector3.Lerp(a.position,b.position,amount);
            Quaternion rotation=Quaternion.Slerp(Quaternion.Euler(a.rotation),Quaternion.Euler(b.rotation),amount);
            marker.transform.SetPositionAndRotation(position,rotation);
            environmentLoader?.DriveRecordedRig(id,position,rotation);
            environmentLoader?.ApplyAsynchronousSample(a);
            if(matchRecordedCamera && camerasByRig.TryGetValue(id,out var animalCamera))environmentLoader?.ConfigureAnimalCamera(id,animalCamera,a);
            if(environmentSample==null)environmentSample=a;
            if(referenceSample==null || (selectedView>=0 && rigOrder[selectedView]==id))referenceSample=a;
        }
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

    private void OnGUI()
    {
        if(OperatorOverlayVisibility.Hidden)return;
        if(replaySkin==null)
        {
            replaySkin=Instantiate(GUI.skin);replaySkin.name="Replay controls";
            replaySkin.font=Resources.Load<Font>("ConfigurationUI/Fonts/Inter-Regular")??GUI.skin.font;
            foreach(var style in new[]{replaySkin.label,replaySkin.button,replaySkin.toggle,replaySkin.textField})
            {style.font=replaySkin.font;style.fontSize=14;}
            replaySkin.button.fixedHeight=30;replaySkin.button.padding=new RectOffset(8,8,5,5);
            replaySkin.textField.fixedHeight=30;replaySkin.textField.padding=new RectOffset(8,8,5,5);
            replaySkin.toggle.fixedHeight=30;replaySkin.toggle.alignment=TextAnchor.MiddleLeft;
            replaySkin.label.alignment=TextAnchor.MiddleLeft;
            titleStyle=new GUIStyle(replaySkin.label){fontSize=19,fontStyle=FontStyle.Bold};
            smallStyle=new GUIStyle(replaySkin.label){fontSize=13,wordWrap=true};
        }
        GUISkin previousSkin=GUI.skin;GUI.skin=replaySkin;
        Matrix4x4 previousMatrix=GUI.matrix;float scale=Mathf.Min(DesktopDisplay.UiScale,Mathf.Max(1,Screen.width)/1120f);
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);
        float screenWidth=Screen.width/scale,screenHeight=Screen.height/scale;
        float height=Mathf.Max(152,screenHeight*.20f);var area=new Rect(8,screenHeight-height,screenWidth-16,height-8);
        GUI.Box(area,GUIContent.none);GUILayout.BeginArea(new Rect(area.x+10,area.y+6,area.width-20,area.height-12));
        GUILayout.BeginHorizontal();GUILayout.Label("Experiment replay",titleStyle,GUILayout.Width(185));
        if(GUILayout.Button(new GUIContent("Back to setup","Return to experiment setup. Shortcut: Esc."),GUILayout.Width(120)))
        {
            GUI.matrix=previousMatrix;GUI.skin=previousSkin;
            if(MainController.Instance!=null)MainController.Instance.HandleEscape();else SceneManager.LoadScene("ControlScene");
            GUIUtility.ExitGUI();
        }
        GUI.SetNextControlName("Replay run folder");folderInput=GUILayout.TextField(folderInput,GUILayout.MinWidth(100));pathFocused=GUI.GetNameOfFocusedControl()=="Replay run folder";
        GUI.enabled=!loading && folderPicker==null;
        if(GUILayout.Button("Browse…",GUILayout.Width(80))){GUI.FocusControl(null);pathFocused=false;folderPicker=ReplayFolderPicker.ChooseAsync();}
        if(GUILayout.Button("Open run folder",GUILayout.Width(125))){GUI.FocusControl(null);pathFocused=false;LoadSession(folderInput);}GUI.enabled=true;
        GUILayout.EndHorizontal();
        if(loading)GUILayout.Label("Indexing recorded frames on disk… "+loadProgress,smallStyle);
        else if(!string.IsNullOrEmpty(LoadError))GUILayout.Label(LoadError,smallStyle);
        else if(archive==null)GUILayout.Label("Paste a saved run folder, or launch with --replay-session <folder>. In Unity Editor, drop a folder anywhere in this window.",smallStyle);
        else
        {
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(isPlaying?"Pause":"Play",GUILayout.Width(64)))isPlaying=!isPlaying;
            if(GUILayout.Button("−10s",GUILayout.Width(52)))Seek(replayTime-10);
            if(GUILayout.Button("+10s",GUILayout.Width(52)))Seek(replayTime+10);
            if(GUILayout.Button("Previous",GUILayout.Width(78)))JumpToStep(-1);
            if(GUILayout.Button("Next",GUILayout.Width(55)))JumpToStep(1);
            GUILayout.Label(Clock(replayTime)+" / "+Clock(Duration),GUILayout.Width(150));
            GUILayout.Label("Speed",GUILayout.Width(56));playbackSpeed=GUILayout.HorizontalSlider(playbackSpeed,.1f,4,GUILayout.Width(80));GUILayout.Label(playbackSpeed.ToString("0.0")+"×",GUILayout.Width(38));
            if(GUILayout.Button(selectedView<0?"God's-eye view":"Animal "+rigOrder[selectedView],GUILayout.Width(130)))CycleView();
            if(GUILayout.Button("Fit",GUILayout.Width(44))){ShowOverview();FitOverview();}
            showDetails=GUILayout.Toggle(showDetails,"Fidelity ("+archive.Warnings.Count+")",GUILayout.Width(106));
            GUILayout.EndHorizontal();
            float normalized=Duration>0?(float)(replayTime/Duration):0;
            float changed=GUILayout.HorizontalSlider(normalized,0,1);
            Rect timeline=GUILayoutUtility.GetLastRect();Color oldColor=GUI.color;GUI.color=new Color(.3f,.8f,.9f,.8f);
            foreach(var step in stepChanges)if(Duration>0)GUI.DrawTexture(new Rect(timeline.x+(float)(step.time/Duration)*timeline.width,timeline.y+2,1,8),Texture2D.whiteTexture);
            GUI.color=oldColor;
            if(Mathf.Abs(changed-normalized)>.000001f)SetScrubNormalized(changed);
            string context=referenceSample==null?"No animal sample at this time":referenceSample.scene+" · trial "+referenceSample.trial+" · sequence "+referenceSample.sequenceStep+" · "+referenceSample.stepName+" · "+referenceSample.phase;
            GUILayout.Label(context+"   |   "+(environmentLoader?.FidelityStatus??""),smallStyle);
            GUILayout.Label("Space: play/pause · ←/→: 1s · Shift+←/→: 10s · N/M: previous/next condition · mouse wheel: zoom · Esc: setup",smallStyle);
        }
        GUILayout.EndArea();
        if(showDetails && archive!=null)
        {
            var details=new Rect(12,12,Mathf.Min(700,screenWidth-24),Mathf.Max(100,screenHeight-height-32));GUI.Box(details,GUIContent.none);
            GUILayout.BeginArea(new Rect(details.x+10,details.y+8,details.width-20,details.height-16));GUILayout.Label("Replay fidelity and saved evidence",titleStyle);
            GUILayout.Label(archive.DirectoryPath,smallStyle);detailScroll=GUILayout.BeginScrollView(detailScroll);
            foreach(string warning in archive.Warnings)GUILayout.Label("• "+warning,smallStyle);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
#if UNITY_EDITOR
        Event evt=Event.current;
        if(evt.type==EventType.DragUpdated || evt.type==EventType.DragPerform)
        {
            string path=UnityEditor.DragAndDrop.paths.FirstOrDefault(Directory.Exists);
            if(path!=null)
            {
                UnityEditor.DragAndDrop.visualMode=UnityEditor.DragAndDropVisualMode.Copy;
                if(evt.type==EventType.DragPerform){UnityEditor.DragAndDrop.AcceptDrag();folderInput=path;LoadSession(path);}
                evt.Use();
            }
        }
#endif
        GUI.matrix=previousMatrix;GUI.skin=previousSkin;
    }
    private static string Clock(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(@"hh\:mm\:ss\.f");
    private void ClearSessionVisuals()
    {
        foreach(var population in populations){foreach(var visual in population.visuals.Values)if(visual!=null)Destroy(visual);population.stream.Dispose();}populations.Clear();
        foreach(Transform child in generatedRoot)if(child!=populationRoot){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        markerByRig.Clear();camerasByRig.Clear();rigOrder.Clear();stepChanges.Clear();archive=null;
    }
    private void OnDestroy()
    {
        if(replaySkin!=null)Destroy(replaySkin);
        foreach(var population in populations)population.stream.Dispose();populations.Clear();
        if(pendingLoad!=null)
            pendingLoad.ContinueWith(task=>{if(task.Status==TaskStatus.RanToCompletion)foreach(var stream in task.Result.agents)stream.Dispose();});
        if(overview!=null && overview.gameObject.name=="Replay overview")Destroy(overview.gameObject);
    }
}
