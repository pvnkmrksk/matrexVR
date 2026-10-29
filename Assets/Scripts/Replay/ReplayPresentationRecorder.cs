using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class ReplayPresentationRecorder
{
    public const string ContractName = ReplayPresentationJournal.ContractName;
    public const string FileName = ReplayPresentationJournal.FileName;
    public const int SchemaVersion = ReplayPresentationJournal.SchemaVersion;
    public static string CurrentPresentationId { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => CurrentPresentationId = null;

    public static JObject Begin(MainController main, JObject document, Dictionary<string,object> parameters)
    {
        var logger = MasterDataLogger.Instance;
        if (logger == null || !logger.AcceptingWrites) return null;
        string contract = Path.Combine(logger.directoryPath, ContractName);
        if (!File.Exists(contract))
        {
            var value = new JObject { ["schemaVersion"] = SchemaVersion, ["contract"] = "same-build-controller-inputs-v1",
                ["player"] = PlayerBuildIdentity.Capture(), ["createdUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["policy"] = "Replay with this exact build. Archived controller inputs, RNG checkpoint and realized state are authoritative; other builds are not promised compatible." };
            using (var stream = new FileStream(contract, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream)) writer.Write(value.ToString(Formatting.Indented));
        }
        CurrentPresentationId = Guid.NewGuid().ToString("N");
        return new JObject { ["id"] = CurrentPresentationId, ["scene"] = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            ["authoredRow"] = main.executionOrder[main.currentStep], ["executionStep"] = main.currentStep, ["trial"] = main.currentTrial,
            ["phase"] = main.ExperimentPhase, ["config"] = document?.DeepClone() ?? new JObject(),
            ["parameters"] = parameters == null ? new JObject() : JObject.FromObject(parameters),
            ["initializationRandom"] = ExperimentRandom.CaptureCheckpoint(), ["onsetUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["rigsBeforeApply"] = Rigs(main) };
    }

    private static JArray Rigs(MainController main)
    {
        return new JArray(RuntimeRigRegistry.Rigs(UnityEngine.SceneManagement.SceneManager.GetActiveScene()).Select(rig => new JObject {
            ["id"] = "VR" + RuntimeRigRegistry.ParseIndex(rig.name), ["position"] = new JArray(rig.transform.position.x, rig.transform.position.y, rig.transform.position.z),
            ["rotation"] = new JArray(rig.transform.rotation.x, rig.transform.rotation.y, rig.transform.rotation.z, rig.transform.rotation.w),
            ["headingReference"] = rig.GetComponent<StimulusHeadingReference>()?.Result == null ? null : JToken.FromObject(rig.GetComponent<StimulusHeadingReference>().Result) }));
    }

    public static void Commit(JObject entry, MainController main)
    {
        if (entry == null) return;
        var logger = MasterDataLogger.Instance;
        if (logger == null || !logger.AcceptingWrites) throw new InvalidOperationException("Presentation recording ended during configuration.");
        ReplayEvidenceSupport.RequireSupportedScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        entry["firstUnityFrame"] = Time.frameCount; entry["configuredAtUnityTime"] = Time.timeAsDouble;
        entry["rigsAfterApply"] = Rigs(main);
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var visuals=new JArray();
        foreach(var rig in UnityEngine.Object.FindObjectsByType<Kannadi>(FindObjectsSortMode.None).Where(rig=>rig.gameObject.scene==scene))
            visuals.Add(new JObject{["family"]="Kannadi",["kind"]=rig.IsSwarm?"Swarm":"Kannadi",["rig"]="VR"+rig.VRIndex,
                ["layer"]=RuntimeRigRegistry.LayerLabel(RuntimeRigRegistry.StimulusLayer(rig.VRIndex)),["prefab"]=rig.tilePrefab!=null?rig.tilePrefab.name:null});
        foreach(var spawner in UnityEngine.Object.FindObjectsByType<LocustSpawner>(FindObjectsSortMode.None).Where(spawner=>spawner.gameObject.scene==scene))
            visuals.Add(new JObject{["family"]="Swarm",["kind"]="Swarm",["layer"]=RuntimeRigRegistry.LayerLabel(spawner.ResolvedLayer),
                ["prefab"]=spawner.locustPrefab!=null?spawner.locustPrefab.name:null,["agentVisual"]=spawner.agentVisual,
                ["worldUnitsPerMetre"]=spawner.worldUnitsPerMetre,["bogongVisual"]=spawner.bogongVisual!=null?ReplayConfigurationJson.Capture(spawner.bogongVisual):null});
        entry["populationVisuals"]=visuals;
        entry["sha256"] = ReplayPresentationJournal.HashEntry(entry);
        File.AppendAllText(Path.Combine(logger.directoryPath, FileName), entry.ToString(Formatting.None) + "\n");
        if (main.GetComponent<RecordedWorldStateWriter>() == null) main.gameObject.AddComponent<RecordedWorldStateWriter>();
        foreach (var target in UnityEngine.Object.FindObjectsByType<RecordedWorldStateTarget>(FindObjectsSortMode.None)) target.Invalidate();
    }
    public static void End() => CurrentPresentationId = null;

}
