using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Phase payloads use the scene's existing experiment schema, in one file.</summary>
public sealed class ExperimentPhase
{
    public string Name { get; private set; }
    public double Duration { get; private set; }
    public JObject Config { get; private set; }
    public HeadingReferenceConfig Heading { get; private set; }

    public static List<ExperimentPhase> Parse(JObject document)
    {
        var phases = new List<ExperimentPhase>();
        foreach (string name in new[] { "preStimulus", "stimulus", "postStimulus" })
        {
            JToken token = document[name];
            if (token == null || token.Type == JTokenType.Null) continue;
            if (!(token is JObject source)) throw new ArgumentException(name + " must be an object.");
            if (!((bool?)source["enabled"] ?? (name == "stimulus"))) continue;
            double seconds = (double?)source["durationSeconds"] ?? 0;
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
                throw new ArgumentException(name + ".durationSeconds must be positive and finite when enabled.");
            var heading = source["headingReference"]?.ToObject<HeadingReferenceConfig>() ?? new HeadingReferenceConfig();
            heading.Validate();
            if (heading.enabled && name != "preStimulus")
                throw new ArgumentException("In a phased experiment, headingReference is measured in preStimulus. Use useHeadingReference on stimulus spawners.");
            var payload = (JObject)source.DeepClone();
            payload.Remove("enabled"); payload.Remove("durationSeconds");
            // The runner owns sampling; controllers must retain the frozen result at transitions.
            payload.Remove("headingReference");
            phases.Add(new ExperimentPhase { Name = name, Config = payload, Heading = heading,
                Duration = heading.enabled ? Math.Max(seconds, heading.startOffsetSeconds + heading.windowSeconds) : seconds });
        }
        if (!phases.Any(p => p.Name == "stimulus"))
            throw new ArgumentException("A phased experiment requires an enabled stimulus object with durationSeconds.");
        if (document.Properties().Any(p => p.Name != "preStimulus" && p.Name != "stimulus" && p.Name != "postStimulus"))
            throw new ArgumentException("Put experiment settings inside preStimulus, stimulus or postStimulus; do not mix flat and phased schemas.");
        return phases;
    }
}

/// <summary>Changes conditions on the existing scene, after movement and heading sampling.</summary>
[DefaultExecutionOrder(1500)]
public sealed class ExperimentPhases : MonoBehaviour
{
    internal const string PayloadKey = "__experimentPhase";
    private List<ExperimentPhase> phases;
    private Dictionary<string, object> parameters;
    private Action<Dictionary<string, object>> apply;
    private Action<Exception> fail;
    private readonly List<StimulusHeadingReference> references = new List<StimulusHeadingReference>();
    private int index;
    private double phaseStarted;
    public bool Active => phases != null;
    public bool Finished { get; private set; }
    public string CurrentPhase => Active ? phases[index].Name : "stimulus";
    public double RemainingPhaseSeconds => Active && !Finished ? Math.Max(0, phases[index].Duration - (Time.timeAsDouble - phaseStarted)) : 0;
    public double RemainingSeconds => Active && !Finished ? RemainingPhaseSeconds + phases.Skip(index + 1).Sum(p => p.Duration) : 0;

    public bool TryBegin(Dictionary<string, object> source, Action<Dictionary<string, object>> applyConfig, Action<Exception> onError)
    {
        Cancel();
        if (source == null) return false;
        object file;
        if (!source.TryGetValue("configFile", out file) && !source.TryGetValue("design", out file)) return false;
        if (file == null) return false;
        JObject document = JObject.Parse(File.ReadAllText(ExperimentConfigFiles.Resolve(file.ToString())));
        if (document["preStimulus"] == null && document["stimulus"] == null && document["postStimulus"] == null) return false;
        phases = ExperimentPhase.Parse(document);
        parameters = new Dictionary<string, object>(source);
        apply = applyConfig; fail = onError; index = 0; Finished = false;
        foreach (ClosedLoop rig in FindObjectsByType<ClosedLoop>(FindObjectsSortMode.None))
        {
            var reference = StimulusHeadingReference.For(rig);
            reference.Cancel(); references.Add(reference);
        }
        Enter();
        return true;
    }

    private void Enter()
    {
        ExperimentPhase phase = phases[index];
        var payload = new Dictionary<string, object>(parameters) { [PayloadKey] = phase.Config };
        apply(payload);
        // Start the observation and phase clock only after sky generation/configuration completes.
        phaseStarted = Time.timeAsDouble;
        if (phase.Heading.enabled)
        {
            if (references.Count == 0) throw new InvalidOperationException("Heading reference requires at least one ClosedLoop rig.");
            foreach (var reference in references) reference.Begin(phase.Heading, null);
        }
        string record = JsonConvert.SerializeObject(new { phase = phase.Name, startedAt = phaseStarted,
            durationSeconds = phase.Duration, trial = MainController.Instance?.currentTrial,
            step = MainController.Instance?.currentStep, skyId = NightSkyController.CurrentId,
            rigs = references.Where(r => r != null).Select(r => new { rig = r.name, reference = r.Result }) });
        Debug.Log("Experiment phase: " + record);
        if (MasterDataLogger.Instance != null)
            File.AppendAllText(Path.Combine(MasterDataLogger.Instance.directoryPath, "experiment_phases.jsonl"), record + "\n");
    }

    private void LateUpdate()
    {
        if (!Active || Finished) return;
        try
        {
            if (references.Any(r => r != null && r.Phase == "error")) throw new InvalidOperationException("Heading reference measurement failed.");
            if (RemainingPhaseSeconds > 0 || references.Any(r => r != null && r.IsObserving)) return;
            if (index + 1 == phases.Count) { Finished = true; return; }
            index++; Enter();
        }
        catch (Exception error) { var report = fail; Cancel(); report?.Invoke(error); }
    }

    public void Cancel()
    {
        foreach (var reference in references) if (reference != null) reference.Cancel();
        references.Clear(); phases = null; apply = null; fail = null; Finished = false;
    }
    private void OnDestroy() => Cancel();

    public static bool IsPhase(Dictionary<string, object> source) => source != null && source.ContainsKey(PayloadKey);
    public static JObject Read(Dictionary<string, object> source, string key = "configFile")
    {
        if (IsPhase(source)) return (JObject)((JObject)source[PayloadKey]).DeepClone();
        return JObject.Parse(File.ReadAllText(ExperimentConfigFiles.Resolve(source[key].ToString())));
    }
}
