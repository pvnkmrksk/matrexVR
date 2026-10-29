#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Shared sky options through an actual Optomotor acquisition, in a disposable copy.</summary>
[InitializeOnLoad]
public static class SharedSkyRuntimeValidation
{
    private const string Key = "SharedSkyRuntimeValidation.Active";
    private static readonly List<string> checks = new List<string>();
    private static int stage;
    private static double deadline;
    private static string recording, panoramaId;
    static SharedSkyRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            stage = 0; checks.Clear(); deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        };
    }
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks.Add(label); }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Disposable project only.");
        var root = Application.streamingAssetsPath;
        File.WriteAllText(Path.Combine(root, "system_config.json"), new JObject {
            ["configs"] = new JArray(new JObject {
                ["vrId"]="VR1",["closedLoopMode"]="Hejjetrac",["zmqAddress"]="127.0.0.1",["zmqPort"]=59991,
                ["displayOrder"]="F",["targetDisplay"]=0,["ledPanelWidth"]=16,["ledPanelHeight"]=16,
                ["tracker"]=new JObject{["saveVideo"]=false,["recordingControl"]="Manual"}}),
            ["telemetry"]=new JObject{["enabled"]=false},["statusOverlay"]=new JObject{["visible"]=false},
            ["overheadCamera"]=new JObject{["enabled"]=false}
        }.ToString());
        var texture = new Texture2D(8,4,TextureFormat.RGBA32,false);
        var colors = new Color[32]; for (int i=0;i<colors.Length;i++) colors[i]=i%8<4?Color.red:Color.green;
        texture.SetPixels(colors);texture.Apply();File.WriteAllBytes(Path.Combine(root,"__shared-sky.png"),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        JObject Phase() => new JObject { ["enabled"]=true,["durationSeconds"]=1,["stimuli"]=new JArray(new JObject{["duration"]=10,["speed"]=0,["frequency"]=4}) };
        var pre=Phase();pre["uniformSkyColor"]=new JObject{["r"]=1,["g"]=0,["b"]=0,["a"]=1};
        var stimulus=Phase();stimulus["skyboxPath"]="__shared-sky.png";stimulus["panoramaRotationDegrees"]=90;
        var post=(JObject)stimulus.DeepClone();post["panoramaRotationDegrees"]=270;
        File.WriteAllText(Path.Combine(root,"__shared-sky.json"),new JObject{["preStimulus"]=pre,["stimulus"]=stimulus,["postStimulus"]=post}.ToString());
        File.WriteAllText(Path.Combine(root,"sequenceConfig.json"),new JObject{
            ["autoStart"]=false,["loop"]=false,["randomise"]=false,
            ["sequences"]=new JArray(new JObject{["sceneName"]="Optomotor",["duration"]=3,["reloadScene"]=true,["parameters"]=new JObject{["configFile"]="__shared-sky.json"}})
        }.ToString());
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Shared sky validation Main").AddComponent<MainController>();
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Shared sky stage "+stage);
            var main=MainController.Instance;if(main==null||MasterDataLogger.Instance==null)return;
            if(main.SequenceError!=null)throw new Exception(main.SequenceError);
            if(stage==0){main.StartSequence();recording=MasterDataLogger.Instance.directoryPath;stage=1;return;}
            if(!main.SequenceRunning||SceneManager.GetActiveScene().name!="Optomotor")
            {
                if(stage==4&&!main.SequencePreparing&&!main.SequenceStopping&&!MasterDataLogger.Instance.IsFinalizing)
                {
                    Check(string.IsNullOrEmpty(main.RecordingTransitionError),"Shared-sky run finalizes without native recording errors");
                    Check(Directory.GetFiles(Path.Combine(recording,"Skyboxes"),"*.png").Length>=2,"Exact uniform and panorama images archived with the acquisition");
                    Finish(null);
                }
                return;
            }
            if(stage==1&&main.ExperimentPhase=="preStimulus")
            {
                Check(!string.IsNullOrEmpty(NightSkyController.CurrentId),"Optomotor uses the shared managed sky");
                var pixel=((Texture2D)RenderSettings.skybox.mainTexture).GetPixel(0,0);
                Check(pixel.r>.99f&&pixel.g<.01f,"Shared uniform color is the requested red");stage=2;
            }
            else if(stage==2&&main.ExperimentPhase=="stimulus")
            {
                panoramaId=NightSkyController.CurrentId;
                Check(!string.IsNullOrEmpty(panoramaId)&&RenderSettings.skybox.GetFloat("_Rotation")==90,"Custom panorama and 90 degree azimuth applied through Main");stage=3;
            }
            else if(stage==3&&main.ExperimentPhase=="postStimulus")
            {
                Check(NightSkyController.CurrentId==panoramaId,"Panorama image identity remains independent of azimuth");
                Check(RenderSettings.skybox.GetFloat("_Rotation")==270,"Same image switches to 270 degrees in the next phase");stage=4;
            }
        }
        catch(Exception error){Finish(error.ToString());}
    }
    private static void Finish(string failure)
    {
        EditorApplication.update-=Tick;SessionState.SetBool(Key,false);
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../shared-sky-runtime-validation"));Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,recording,scope="Actual Optomotor MainController phases, managed sky images/rotation and recording finalization; video explicitly disabled, no physical tracker."},Formatting.Indented));
        if(failure!=null)Debug.LogError(failure);EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(failure==null?0:1);
    }
}
#endif
