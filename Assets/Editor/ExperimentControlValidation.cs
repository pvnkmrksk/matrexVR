#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ExperimentControlValidation
{
    private const string Active = "ExperimentControlValidation.Active";
    private static double deadline;
    static ExperimentControlValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            deadline = EditorApplication.timeSinceStartup + 60; EditorApplication.update += WaitForSetup;
        };
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a marked disposable project.");
        // This runner owns only its marked disposable project's starter files.
        string backup = Path.GetFullPath(Path.Combine(Application.dataPath, "../control-validation")); Directory.CreateDirectory(backup);
        foreach (string name in new[] { "system_config.json", "sequenceConfig.json" })
            if (!File.Exists(Path.Combine(backup, "original-" + name))) File.Copy(Path.Combine(Application.streamingAssetsPath, name), Path.Combine(backup, "original-" + name));
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "system_config.json"), "{\"configs\":[{\"vrId\":\"VR1\",\"displayOrder\":\"F\",\"closedLoopMode\":\"Kinefly\",\"targetDisplay\":0}],\"telemetry\":{\"enabled\":false}}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "sequenceConfig.json"), "{\"autoStart\":false,\"sequences\":[]}");
        SessionState.SetBool(Active, true);
        EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity"); EditorApplication.isPlaying = true;
    }
    private static void WaitForSetup()
    {
        if (MainController.Instance != null && ConfigurationUI.ConfigWorkbench.Instance?.Workspace != null && Time.frameCount > 8)
        { EditorApplication.update -= WaitForSetup; RunChecks(); return; }
        if (EditorApplication.timeSinceStartup <= deadline) return;
        EditorApplication.update -= WaitForSetup; SessionState.SetBool(Active, false);
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../control-validation")); Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "results.json"), "{\"failure\":\"Timed out waiting for idle setup in PlayMode\"}"); RestoreStarterFiles(); EditorApplication.Exit(1);
    }
    private static void RestoreStarterFiles()
    {
        string backup = Path.GetFullPath(Path.Combine(Application.dataPath, "../control-validation"));
        foreach (string name in new[] { "system_config.json", "sequenceConfig.json" })
            if (File.Exists(Path.Combine(backup, "original-" + name))) File.Copy(Path.Combine(backup, "original-" + name), Path.Combine(Application.streamingAssetsPath, name), true);
    }
    private static void RunChecks()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "../KANNADI_VALIDATION_COPY"))) throw new Exception("Use a marked disposable project.");
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../control-validation")); Directory.CreateDirectory(root);
        int checks = 0; string failure = null;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        try
        {
            var host = new GameObject("Optional control validation"); var endpoint = host.AddComponent<ExperimentControlEndpoint>();
            Check(endpoint.ControlDirectory == null, "Control remains disabled without explicit configuration");
            endpoint.ConfigureDirectory(Path.Combine(root, "mailbox"));
            string id = Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(endpoint.ControlDirectory, id + ".request.json"), new JObject {
                ["id"] = id, ["instance"]=endpoint.ControlInstance, ["expiresUtc"] = DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"), ["method"] = "status" }.ToString());
            endpoint.Poll(); var status = JObject.Parse(File.ReadAllText(Path.Combine(endpoint.ControlDirectory, id + ".response.json")));
            Check((bool)status["ok"] && (int)status["result"]["protocolVersion"] == 2, "Actual Unity endpoint responds to an atomic file status request");
            Check(!File.Exists(Path.Combine(endpoint.ControlDirectory, id + ".request.json")), "Processed request is removed after the response is durable");
            string system = Path.Combine(root, "system.json"), sequence = Path.Combine(root, "sequence.json"), choice = Path.Combine(root, "choice.json");
            File.WriteAllText(system, "{\"apparatusConfiguration\":{\"version\":1,\"configured\":true,\"presentation\":\"desktop\",\"monitorCount\":1},\"configs\":[{\"vrId\":\"VR1\",\"displayOrder\":\"F\",\"desktopPreview\":true,\"ledPanelWidth\":128,\"ledPanelHeight\":128,\"startRow\":0,\"startCol\":0,\"closedLoopMode\":\"Kinefly\",\"zmqAddress\":\"127.0.0.1\",\"zmqPort\":32971,\"targetDisplay\":0}]}");
            File.WriteAllText(choice, "{\"objects\":[]}");
            File.WriteAllText(sequence, new JObject { ["sequences"] = new JArray(new JObject { ["sceneName"] = "Choice", ["duration"] = 1, ["parameters"] = new JObject { ["configFile"] = choice } }) }.ToString());
            string before = RunConfigArchive.Hash(File.ReadAllBytes(system)) + RunConfigArchive.Hash(File.ReadAllBytes(sequence));
            var args = new JObject { ["systemFile"] = system, ["sequenceFile"] = sequence };
            var validation = endpoint.Execute("validate", args);
            Check((bool)validation["valid"], "Endpoint uses the real production schema and configuration validators: " + validation["errors"]);
            File.WriteAllText(system, "{\"configs\":[{\"vrId\":\"VR17\"}]}");
            Check(!(bool)endpoint.Execute("validate", args)["valid"], "Invalid VR configuration is rejected by production validation");
            File.WriteAllText(system, "{\"apparatusConfiguration\":{\"version\":1,\"configured\":true,\"presentation\":\"desktop\",\"monitorCount\":1},\"configs\":[{\"vrId\":\"VR1\",\"displayOrder\":\"F\",\"desktopPreview\":true,\"ledPanelWidth\":128,\"ledPanelHeight\":128,\"startRow\":0,\"startCol\":0,\"closedLoopMode\":\"Kinefly\",\"zmqAddress\":\"127.0.0.1\",\"zmqPort\":32971,\"targetDisplay\":0}]}");
            Check(before == RunConfigArchive.Hash(File.ReadAllBytes(system)) + RunConfigArchive.Hash(File.ReadAllBytes(sequence)), "Status and validation never modify setup source files");
            var gui = ConfigurationUI.ConfigWorkbench.Instance;
            string idleDirectory=MasterDataLogger.Instance.directoryPath;
            MainController.Instance.ConfigureTrackingWhileIdle(system);
            Check(MainController.Instance.SystemConfigs["VR1"].zmqPort==32971&&!MainController.Instance.SequenceRunning&&MasterDataLogger.Instance.directoryPath==idleDirectory,
                "Preflight applies selected saved apparatus/ports without creating an experiment recording");
            gui.SelectFilesFromControl(system,sequence);
            var snapshot=endpoint.Execute("workspace_get",new JObject());
            Check(((JArray)snapshot["documents"]).Count>=3,"Control sees actual GUI workspace references");
            var document=gui.ControlDocument(choice);
            document.Edit(value=>value["agentNote"]="original GUI draft");
            var read=endpoint.Execute("config_get",new JObject{["path"]=choice});
            Check(((string)read["jsonText"]).Contains("original GUI draft"),"GUI edits are visible to the agent without saving");
            string revision=(string)read["revision"],saved=File.ReadAllText(choice);
            var candidate=(JObject)document.Root.DeepClone();candidate["autopilotEnabled"]=false;
            var edited=endpoint.Execute("config_replace",new JObject{["path"]=choice,["revision"]=revision,["jsonText"]=candidate.ToString()});
            Check((bool)document.Root["autopilotEnabled"]==false&&(string)document.Root["agentNote"]=="original GUI draft"&&File.ReadAllText(choice)==saved,
                "Validated MCP replacement updates shared GUI draft, preserves unknown fields and leaves disk untouched");
            bool stale=false;try{endpoint.Execute("config_replace",new JObject{["path"]=choice,["revision"]=revision,["jsonText"]="{}"});}catch(InvalidOperationException){stale=true;}
            Check(stale&&(string)document.Root["agentNote"]=="original GUI draft","Stale concurrent edits cannot overwrite the operator draft");
            string valid=document.Root.ToString();candidate["autopilotEnabled"]="invalid";
            bool invalid=false;try{endpoint.Execute("config_replace",new JObject{["path"]=choice,["revision"]=edited["revision"],["jsonText"]=candidate.ToString()});}catch(ArgumentException){invalid=true;}
            Check(invalid&&document.Root.ToString()==valid,"Invalid replacement is rejected atomically through real validators");
            endpoint.Execute("config_history",new JObject{["path"]=choice,["revision"]=edited["revision"],["action"]="undo"});
            Check(document.Root["autopilotEnabled"]==null&&(string)document.Root["agentNote"]=="original GUI draft","Agent edit is one normal GUI undo transaction; rejected edits leave no history entry");
            read=endpoint.Execute("config_get",new JObject{["path"]=choice});
            var redone=endpoint.Execute("config_history",new JObject{["path"]=choice,["revision"]=read["revision"],["action"]="redo"});
            endpoint.Execute("config_save",new JObject{["path"]=choice,["revision"]=redone["revision"]});
            Check(!document.Dirty&&JObject.Parse(File.ReadAllText(choice))["agentNote"].ToString()=="original GUI draft","Agent save uses the same conflict-safe GUI persistence");
            Check(JToken.DeepEquals(endpoint.Execute("schemas",new JObject{["schemaId"]="choice"})["schema"],gui.Workspace.Schemas.Get("choice")),"Agent and GUI use the exact same schema");
            var held=endpoint.Execute("config_replace",new JObject{["path"]=choice,["revision"]=gui.DescribeControlDocument(document)["revision"],["jsonText"]="{\"objects\":[],\"autopilotEnabled\":\"invalid\"}",["allowInvalidDraft"]=true});
            Check(((JArray)held["validationErrors"]).Count>0&&document.Root["autopilotEnabled"].Type==JTokenType.String,"Explicit invalid drafts remain lossless, visible and un-runnable while multi-file setup is repaired");
            endpoint.Execute("config_history",new JObject{["path"]=choice,["revision"]=held["revision"],["action"]="undo"});
            var systemRead=endpoint.Execute("config_get",new JObject{["path"]=system});
            var invalidSystem=(JObject)gui.Workspace.System.Root.DeepClone();invalidSystem["targetFrameRate"]=0;
            int liveCap=MainController.Instance.TargetFrameRate;
            var repair=endpoint.Execute("config_replace",new JObject{["path"]=system,["revision"]=systemRead["revision"],["jsonText"]=invalidSystem.ToString(),["allowInvalidDraft"]=true});
            Check(((JArray)repair["validationErrors"]).Count>0&&MainController.Instance.TargetFrameRate==liveCap,
                "Invalid system repair drafts are acknowledged with errors without partially applying an invalid live frame cap");
            endpoint.Execute("config_history",new JObject{["path"]=system,["revision"]=repair["revision"],["action"]="undo"});
            var created=endpoint.Execute("config_create",new JObject{["relativePath"]="ControlChecks/new.json",["schemaId"]="choice",["jsonText"]="{\"objects\":[]}"});
            Check(File.Exists((string)created["path"]),"A new agent config is created in the selected config root");
            bool noOverwrite=false;try{endpoint.Execute("config_create",new JObject{["relativePath"]="ControlChecks/new.json",["schemaId"]="choice",["jsonText"]="{}"});}catch(IOException){noOverwrite=true;}
            Check(noOverwrite,"Create never overwrites an existing file");
            bool noEscape=false;try{endpoint.Execute("config_create",new JObject{["relativePath"]="../outside.json",["schemaId"]="choice",["jsonText"]="{}"});}catch(ArgumentException){noEscape=true;}
            Check(noEscape,"Create rejects destinations outside the configuration root");
            var extraHost=new GameObject("Conflicting command owner");var extraEndpoint=extraHost.AddComponent<ExperimentControlEndpoint>();
            bool ownerRefused=false;try{extraEndpoint.ConfigureDirectory(endpoint.ControlDirectory);}catch(IOException){ownerRefused=true;}
            Check(ownerRefused,"A second application endpoint cannot own the same command mailbox");Object.DestroyImmediate(extraHost);
            gui.Workspace.Sequence.Edit(document => document["title"] = "Uncommitted operator draft");
            bool dirtyRefused = false;
            try { endpoint.Execute("start_files", args); } catch (InvalidOperationException) { dirtyRefused = true; }
            Check(dirtyRefused && gui.Workspace.Sequence.Dirty, "AI start_files cannot bypass or discard a dirty GUI draft");
            endpoint.Execute("stop", new JObject()); endpoint.Execute("stop", new JObject());
            Check(EditorApplication.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "ControlScene" && gui.Workspace.Sequence.Dirty,
                "Repeated idle stop keeps the app running and preserves the GUI draft");
            gui.Workspace.Sequence.Undo();
            id = Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(endpoint.ControlDirectory, id + ".request.json"), new JObject { ["id"] = id, ["instance"]=endpoint.ControlInstance, ["expiresUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"), ["method"] = "start_current" }.ToString());
            endpoint.Poll(); var expired = JObject.Parse(File.ReadAllText(Path.Combine(endpoint.ControlDirectory, id + ".response.json")));
            Check(!(bool)expired["ok"] && ((string)expired["error"]).Contains("expired"), "Stale state-changing requests cannot execute after a later launch");
            string recording = Path.Combine(root, "recording"); Directory.CreateDirectory(recording); File.WriteAllText(Path.Combine(recording, "recording_manifest.json"), "{}");
            bool refused = false; try { endpoint.ConfigureDirectory(Path.Combine(recording, "mailbox")); } catch (ArgumentException) { refused = true; }
            Check(refused, "Command transport cannot be placed inside saved recording folders");
            Object.DestroyImmediate(host);
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogError(failure); }
        File.WriteAllText(Path.Combine(root, "results.json"), JsonConvert.SerializeObject(new { checks, failure, unity = Application.unityVersion }, Formatting.Indented));
        SessionState.SetBool(Active, false);
        RestoreStarterFiles();
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
