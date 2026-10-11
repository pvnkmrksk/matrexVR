#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Runs the real terrain scene through calibration, CSV playback and cleanup without tracking hardware.</summary>
[InitializeOnLoad]
public static class SwarmPlaybackSceneValidation
{
    private const string Key = "SwarmPlaybackSceneValidation.Active";
    private static readonly List<string> errors = new List<string>();
    private static readonly Dictionary<Camera,float> originalClips = new Dictionary<Camera,float>();
    private static int stage, checks, nextFrame, sceneHandle;
    private static double deadline;
    private static ClosedLoop[] rigs;
    private static int[] rigIds;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static SwarmPlaybackSceneValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
            deadline = EditorApplication.timeSinceStartup + 100;
            SceneManager.sceneLoaded += DisableInputs; Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        };
    }
    private static void DisableInputs(Scene scene, LoadSceneMode mode)
    {
        foreach (var input in Object.FindObjectsByType<ZmqListener>(FindObjectsSortMode.None)) input.enabled = false;
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (stack.Contains("UnityEditor.Search.SearchDatabase") || message == "No backup data file found.") return;
        errors.Add(message + "\n" + stack);
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        string root = Application.streamingAssetsPath;
        var system = JObject.Parse(File.ReadAllText(Path.Combine(root,"Templates/system_config.template.json")));
        system["telemetry"]["enabled"] = false; system["overheadCamera"]["enabled"] = false;
        system["configs"][0]["displayOrder"] = "RBLF";
        File.WriteAllText(Path.Combine(root,"system_config.json"),system.ToString());
        var sequence = JObject.Parse(File.ReadAllText(Path.Combine(root,"Templates/sequence.template.json")));
        sequence["autoStart"] = true;
        sequence["sequences"] = JArray.Parse("[{'sceneName':'Choice_Selwyn','duration':3,'gain':1,'reloadScene':true,'autoTrim':false,'parameters':{'configFile':'Examples/Choice/empty-control.example.json'}},{'sceneName':'Choice_Selwyn','duration':60,'gain':1,'reloadScene':false,'autoTrim':false,'parameters':{'configFile':'Examples/Flight/selwyn-trajectory-playback.example.json'}}]");
        File.WriteAllText(Path.Combine(root,"sequenceConfig.json"),sequence.ToString());
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true); EditorApplication.isPlaying = true;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Scene playback stage " + stage);
            if (Time.frameCount < nextFrame) return;
            if (stage == 0) { SceneManager.LoadScene("ControlScene"); stage=1; nextFrame=Time.frameCount+3; return; }
            var main = MainController.Instance;
            if (stage == 1)
            {
                if (SceneManager.GetActiveScene().name != "Choice_Selwyn" || main == null || main.VRClosedLoops.Count != 4) return;
                rigs = main.VRClosedLoops.OrderBy(k=>k.Key).Select(k=>k.Value).ToArray();
                rigIds = rigs.Select(r=>r.GetInstanceID()).ToArray(); sceneHandle=SceneManager.GetActiveScene().handle;
                Check(Terrain.activeTerrains.Length > 0 && rigs.All(r=>SwarmConfiguration.SceneUnitsPerMetre(r.gameObject)==1),"Terrain supplies a metre scene scale.");
                foreach (var rig in rigs) foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
                    if (camera.name.StartsWith("Main Camera ")) originalClips[camera]=camera.nearClipPlane;
                Check(originalClips.Count > 0,"Captured stimulus camera near planes before playback.");
                stage=2; return;
            }
            if (stage == 2)
            {
                var spawners=Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None);
                if (spawners.Length != 4 || spawners.Any(s=>s.Spawned.Count!=430)) return;
                Check(sceneHandle==SceneManager.GetActiveScene().handle && rigIds.SequenceEqual(rigs.Select(r=>r.GetInstanceID())),"Playback retains terrain and rigs across sequence steps.");
                Check(spawners.All(s=>s.worldUnitsPerMetre==1 && s.Spawned.All(a=>!a.GetComponent<LocustMover>().enabled)),"All four rigs replay metre data without constant-speed movers.");
                Check(Object.FindObjectsByType<BogongSwarmRenderer>(FindObjectsSortMode.None).Length==4,"Four populations use four shared renderers.");
                foreach (var rig in rigs)
                {
                    int index=Kannadi.ParseVRIndex(rig.name); int own=LayerMask.GetMask("SimulatedLocustsVR"+index);
                    int all=LayerMask.GetMask("SimulatedLocustsVR1","SimulatedLocustsVR2","SimulatedLocustsVR3","SimulatedLocustsVR4");
                    var cameras=rig.GetComponentsInChildren<Camera>(true).Where(c=>c.name.StartsWith("Main Camera ")).ToArray();
                    Check(cameras.All(c=>Math.Abs(c.nearClipPlane-.001f)<1e-6),"Metre near field reaches 1mm on VR"+index);
                    Check(cameras.All(c=>(c.cullingMask&all)==own),"Stimulus cameras see only their own population on VR"+index);
                    Check(cameras.Where(c=>c.name.EndsWith("U") || c.name.EndsWith("D")).All(c=>c.enabled==(index!=1)),"Absent system camera letters remain disabled on VR"+index);
                    Check(rig.GetComponent<Keyboard>().AutopilotEnabled && rig.GetComponent<Keyboard>().AutopilotSpeed==5,"Flight autopilot is 5m/s on VR"+index);
                    Check(rig.GetComponent<MigrationMotion>().AglHeight==5,"Flight AGL remains 5m on VR"+index);
                    rig.GetComponent<Keyboard>().enabled=false;
                }
                var replay=spawners[0].GetComponent<SwarmTrajectoryPlayback>();
                replay.Advance(0); var agent=spawners[0].Spawned.First(a=>a.activeSelf); Vector3 before=agent.transform.position;
                var data=SwarmTrajectoryData.Load(Path.Combine(Application.streamingAssetsPath,"SwarmData/selwyn-oct7-unity.csv"));
                var track=data.Tracks.Single(t=>agent.name.EndsWith("_ID_"+t.id));
                track.TrySample(0,.1,out Vector3 p0,out _); Check(track.TrySample(.01,.1,out Vector3 p1,out _),"First track interpolates at 10ms.");
                spawners[0].playbackTarget.position+=Vector3.right*10; replay.Advance(.01);
                Check(Vector3.Distance(agent.transform.position-before,Quaternion.Euler(0,-90,0)*(p1-p0))<.0001 ||
                    Vector3.Distance(agent.transform.position,SwarmTrajectoryPlayback.Wrap(before+Quaternion.Euler(0,-90,0)*(p1-p0),spawners[0].boundaryManager))<.0001,
                    "Movement stays anchored except for deliberate moving-volume wrapping.");
                Object.FindFirstObjectByType<ChoiceController>().AdvanceStep(new Dictionary<string,object>{{"configFile","Examples/Choice/empty-control.example.json"}});
                stage=3; nextFrame=Time.frameCount+3; return;
            }
            if (stage == 3)
            {
                Check(Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).Length==0 && Object.FindObjectsByType<SwarmTrajectoryPlayback>(FindObjectsSortMode.None).Length==0,"Next experiment removes playback and all populations.");
                Check(originalClips.All(e=>Math.Abs(e.Key.nearClipPlane-e.Value)<1e-6),"Next experiment restores original near clipping.");
                main.StopSequence(); SceneManager.LoadScene("ControlScene"); stage=4; nextFrame=Time.frameCount+3; return;
            }
            Check(errors.Count==0,"No scene playback runtime errors: "+string.Join("; ",errors)); Finish(null);
        }
        catch(Exception error) { Finish(error.ToString()); }
    }
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick; SceneManager.sceneLoaded-=DisableInputs; Application.logMessageReceived-=OnLog;
        SessionState.SetBool(Key,false);
        File.WriteAllText(Path.Combine(Application.dataPath,"../swarm-playback-scene-validation.json"),JsonConvert.SerializeObject(new {checks,failure,errors,unity=Application.unityVersion},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
