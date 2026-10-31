using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ConfigurationUI;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class ExperimentControlEndpoint
{
    private readonly Dictionary<string,Task<JObject>> operations=new Dictionary<string,Task<JObject>>();
    private static ConfigWorkbench Setup()
    {
        RequireIdle();
        return ConfigWorkbench.Instance??throw new InvalidOperationException("Return to setup before editing configuration.");
    }
    private JObject BeginOperation(Func<Task<JObject>> begin)
    {
        foreach(string key in operations.Where(pair=>pair.Value.IsCompleted).Select(pair=>pair.Key).Take(Math.Max(0,operations.Count-24)).ToArray())operations.Remove(key);
        if(operations.Count>=32)throw new InvalidOperationException("Too many pending operations. Inspect their status before retrying.");
        string id=Guid.NewGuid().ToString("N");operations[id]=begin();return new JObject{["accepted"]=true,["operationId"]=id,["completed"]=operations[id].IsCompleted};
    }
    private static JObject DetailedStatus()
    {
        var main=MainController.Instance;var gui=ConfigWorkbench.Instance;
        var rigs=new JArray();
        if(main!=null)foreach(var pair in main.SystemConfigs)
        {
            var config=pair.Value;main.VRClosedLoops.TryGetValue(pair.Key,out var loop);var listener=loop!=null?loop.GetComponent<ZmqListener>():null;
            rigs.Add(new JObject{["vrId"]=pair.Key,["backend"]=TrackerBackend.DisplayName(config.closedLoopMode),
                ["trackingAddress"]=config.zmqAddress,["trackingPort"]=config.zmqPort,["targetDisplay"]=config.targetDisplay,
                ["inputState"]=listener?.State??"not-initialized",["freshPose"]=listener?.HasFreshPose()??false,
                ["trackerProcess"]=TrackerProcessManager.Status(pair.Key),["recorder"]=TrackerProcessManager.RecordingState(pair.Key)});
        }
        return new JObject{["utc"]=DateTimeOffset.UtcNow,["trial"]=main?.currentTrial??0,["step"]=main?.currentStep??0,
            ["stepDurationSeconds"]=main?.CurrentStepDurationSeconds??0,["elapsedStepSeconds"]=main?.CurrentStepElapsedSeconds??0,
            ["remainingStepSeconds"]=main?.RemainingStepSeconds??0,["remainingPhaseSeconds"]=main?.RemainingPhaseSeconds??0,
            ["runDirectory"]=MasterDataLogger.Instance?.directoryPath,["runFinalizing"]=MasterDataLogger.Instance?.IsFinalizing??false,
            ["setupPreparing"]=gui?.IsPreparing??false,["preparationStatus"]=gui?.ControlPreparationStatus,
            ["rigs"]=rigs,["performance"]=fps.Latest==null?null:JObject.FromObject(fps.Latest)};
    }
    private static JObject Catalog()
    {
        var scenes=new JArray();var assets=ChoiceAssetCatalog.Load();
        for(int i=0;i<SceneManager.sceneCountInBuildSettings;i++)
        {
            string scene=Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            scenes.Add(new JObject{["name"]=scene,["schemaId"]=ConfigSchema.ForScene(scene),
                ["experimentSelectable"]=scene!="ControlScene"&&scene!="ReplayScene"&&scene!="Swarm",
                ["objects"]=new JArray(assets.Entries(scene,"type").Select(entry=>new JObject{["name"]=entry.Name,["description"]=entry.Description,["source"]=entry.Source})),
                ["materials"]=new JArray(assets.Entries(scene,"material").Select(entry=>new JObject{["name"]=entry.Name,["description"]=entry.Description}))});
        }
        // Skip links and archival datasets; the same config root used by the app owns these example files.
        var files=new List<string>();var pending=new Queue<string>();pending.Enqueue(RuntimePaths.ConfigRoot);
        while(pending.Count>0&&files.Count<500)
        {
            string folder=pending.Dequeue();
            files.AddRange(Directory.EnumerateFiles(folder,"*.json").Where(path=>!Path.GetFileName(path).StartsWith(".")).OrderBy(path=>path).Take(500-files.Count));
            foreach(string child in Directory.EnumerateDirectories(folder).OrderBy(path=>path))
                if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0&&!new[]{"Archive","Companions","Backup","RunData"}.Contains(Path.GetFileName(child))&&!Path.GetFileName(child).StartsWith("."))pending.Enqueue(child);
        }
        return new JObject{["configRoot"]=RuntimePaths.ConfigRoot,["runDataRoot"]=RuntimePaths.RunDataRoot,["scenes"]=scenes,
            ["configurationFiles"]=new JArray(files),["configurationFilesTruncated"]=files.Count>=500,
            ["cameraDevices"]=CameraInventory(),
            ["cameraScope"]="Configured selections only. Hardware enumeration, formats and calibration use the native companion API; Unity never opens a camera.",
            ["displays"]=new JArray(DesktopDisplay.DisplayNames),
            ["unsupported"]=new JArray("Native camera calibration uses the companion's own control API.","Physical display/exposure timing requires hardware validation.")};
    }
    private static JArray CameraInventory()
    {
        var source=ConfigWorkbench.Instance?.Workspace?.System?.Root??new JObject();
        return new JArray((source["configs"] as JArray??new JArray()).OfType<JObject>().Select(rig=>new JObject{
            ["vrId"]=rig["vrId"]?.DeepClone(),["device"]=rig["tracker"]?["device"]?.DeepClone(),["backend"]=rig["closedLoopMode"]?.DeepClone(),
            ["scope"]="configured-selection",["hardwareEnumerated"]=false}));
    }
    private JObject ExecuteExtended(string method,JObject args)
    {
        switch(method)
        {
            case "discover":return new JObject{["protocolVersion"]=2,["methods"]=new JArray(ControlMethods),
                ["configuration"]="Shared GUI ConfigWorkspace/ConfigDocument and production schema/parser validation; edits use optimistic revisions.",
                ["asynchronous"]="accepted is not completion; inspect status and operation_get. No automatic mutation retry.",
                ["catalog"]=Catalog()};
            case "schemas":
                var schemas=ConfigSchema.Load();string schema=(string)args["schemaId"];
                if(schema==null)return new JObject{["schemas"]=schemas.Catalog.DeepClone()};
                if(!(schemas.Catalog[schema] is JObject definition))throw new ArgumentException("Unknown schema ID.");
                return new JObject{["schemaId"]=schema,["schema"]=definition.DeepClone()};
            case "workspace_get":
                var workspace=Setup();workspace.RefreshControlFiles();
                return new JObject{["systemFile"]=workspace.Workspace.System.Path,["sequenceFile"]=workspace.Workspace.Sequence.Path,
                    ["documents"]=new JArray(workspace.Workspace.Documents.Select(workspace.DescribeControlDocument)),
                    ["validationErrors"]=new JArray(workspace.Workspace.Validate()),["pendingInput"]=workspace.ControlHasPendingInput};
            case "config_get":return Setup().DescribeControlDocument(Setup().ControlDocument(RequiredPath(args,"path")));
            case "config_replace":return Setup().EditDocumentFromControl(RequiredPath(args,"path"),RequiredText(args,"revision"),"replace",RequiredText(args,"jsonText"),args["allowInvalidDraft"]!=null&&RequiredBoolean(args,"allowInvalidDraft"));
            case "config_create":return Setup().CreateDocumentFromControl(RequiredText(args,"relativePath"),RequiredText(args,"schemaId"),RequiredText(args,"jsonText"));
            case "config_save":return Setup().EditDocumentFromControl(RequiredPath(args,"path"),RequiredText(args,"revision"),"save");
            case "config_history":return Setup().EditDocumentFromControl(RequiredPath(args,"path"),RequiredText(args,"revision"),RequiredText(args,"action"));
            case "setup_select":
                var validation=Execute("validate",args);if(!(bool)validation["valid"])throw new ArgumentException("Invalid saved setup: "+validation["errors"]);
                Setup().SelectFilesFromControl(RequiredPath(args,"systemFile"),RequiredPath(args,"sequenceFile"));return ExecuteExtended("workspace_get",new JObject());
            case "tracking_initialize":Setup().InitializeTrackingFromControl();return new JObject{["accepted"]=true,["note"]="Tracking preparation requested independently; inspect status.diagnostics.setupPreparing and per-rig freshPose. No experiment was started."};
            case "tracker_control":
                var gui=Setup();gui.RequireControlEditReady();string id=RequiredText(args,"vrId"),action=RequiredText(args,"action");
                var rig=(gui.Workspace.System.Root["configs"] as JArray)?.OfType<JObject>().SingleOrDefault(value=>(string)value["vrId"]==id)??throw new ArgumentException("Unknown configured rig.");
                if(action=="launch")TrackerProcessManager.Instance.Launch(LosslessJson.ToRuntime<SystemConfig>(rig));
                else if(action=="stop")TrackerProcessManager.Instance.Stop(id);
                else throw new ArgumentException("Tracker action must be launch or stop.");
                return new JObject{["vrId"]=id,["processRunning"]=TrackerProcessManager.IsRunning(id),["status"]=TrackerProcessManager.Status(id),["inputAcknowledged"]=false};
            case "recorder_stop":
                string vr=RequiredText(args,"vrId");return BeginOperation(()=>TrackerProcessManager.StopRecorderForOperatorAsync(vr));
            case "operation_get":
                if(!operations.TryGetValue(RequiredText(args,"operationId"),out var operation))throw new ArgumentException("Unknown operation in this application instance.");
                return new JObject{["completed"]=operation.IsCompleted,["successful"]=operation.Status==TaskStatus.RanToCompletion,
                    ["result"]=operation.Status==TaskStatus.RanToCompletion?operation.Result:null,["error"]=operation.Exception?.GetBaseException().Message};
            case "metadata_get":return Setup().ControlMetadata();
            case "metadata_set":return Setup().ControlMetadata(RequiredText(args,"jsonText"),RequiredText(args,"revision"),args["save"]!=null&&RequiredBoolean(args,"save"));
            case "recordings_list":
                string root=args["directory"]==null?RuntimePaths.RunDataRoot:RequiredPath(args,"directory");
                return new JObject{["directory"]=root,["entries"]=new JArray(RecordingLibrary.Discover(root).Take(100).Select(entry=>new JObject{
                    ["id"]=entry.Id,["directory"]=entry.DirectoryPath,["title"]=entry.Title,["state"]=entry.RecordingState,["indexed"]=entry.Indexed,["error"]=entry.Error})),
                    ["note"]="Discovery is a preview; replay_open performs full archive loading and validation."};
            case "runtime_control":
                var main=MainController.Instance??throw new InvalidOperationException("No experiment controller.");
                bool? auto=args["autoTrim"]!=null?(bool?)RequiredBoolean(args,"autoTrim"):null;
                bool? flight=args["flightCheck"]!=null?(bool?)RequiredBoolean(args,"flightCheck"):null;
                int? target=args["targetFrameRate"]==null?null:args["targetFrameRate"].Type==JTokenType.Integer?(int?)args["targetFrameRate"]:throw new ArgumentException("targetFrameRate must be an integer.");
                if(target.HasValue&&(target.Value!=-1&&target.Value<=0||main.VSyncCount>0))throw new InvalidOperationException("Use a positive cap or -1, with VSync disabled, as in the live frame-rate editor.");
                if(target.HasValue)main.SetRuntimeFrameRate(target.Value);
                if(auto.HasValue)main.SetCurrentAutoTrim(auto.Value);if(flight.HasValue)main.SetCurrentFlightCheck(flight.Value);
                return new JObject{["autoTrim"]=main.CurrentAutoTrimEnabled,["flightCheck"]=main.CurrentFlightCheckEnabled,["targetFrameRate"]=main.TargetFrameRate};
            case "replay_options":
                var replayOptions=FindFirstObjectByType<ReplayController>();
                if(replayOptions==null||!replayOptions.Ready||replayOptions.Loading)throw new InvalidOperationException("Load a valid replay first.");
                return replayOptions.ControlOptions(args["jsonText"]==null?null:LosslessJson.Parse(RequiredText(args,"jsonText")) as JObject??throw new ArgumentException("Replay options must be an object."));
            case "replay_view":
                var replay=FindFirstObjectByType<ReplayController>();if(replay==null||!replay.Ready||replay.Loading)throw new InvalidOperationException("Load a valid replay first.");
                if(args["vrId"]==null)replay.ShowOverview();else replay.ShowAnimalView(RequiredText(args,"vrId"));
                return new JObject{["overview"]=replay.OverviewVisible};
            default:throw new ArgumentException("Unknown control method: "+method);
        }
    }
    private static string RequiredText(JObject args,string key)=>args[key]?.Type==JTokenType.String?(string)args[key]:throw new ArgumentException(key+" must be a string.");
    private static bool RequiredBoolean(JObject args,string key)=>args[key]?.Type==JTokenType.Boolean?(bool)args[key]:throw new ArgumentException(key+" must be a boolean.");
    public static readonly string[] ControlMethods={"status","discover","schemas","workspace_get","config_get","config_create","config_replace","config_save","config_history","setup_select","validate","tracking_initialize","tracker_control","metadata_get","metadata_set","start_current","start_files","stop","recorder_stop","operation_get","recordings_list","replay_open","replay_play","replay_seek","replay_options","replay_view","runtime_control"};
}
