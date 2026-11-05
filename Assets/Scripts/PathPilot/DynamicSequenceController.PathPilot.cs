using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public partial class DynamicSequenceController
{
    private sealed class PilotProgress
    {
        public Step current;
        public int completed;
        public bool blockOpen;
        public double started;
    }
    private readonly Dictionary<string, PilotProgress> pilotProgress = new();
    private System.Random pilotRandom;
    private PathGroundAppearance pilotAppearance;
    private bool pilotActive;
    private string pilotLog;
    public bool PilotActive => pilotActive;
    public double PilotDeadlineSeconds => designFile?.sessionSeconds ?? 0;

    private void InitializePilotSession()
    {
        pilotRandom = null;
        pilotAppearance = null;
        pilotProgress.Clear();
        if (designFile.sessionSeconds <= 0) return;
        if (double.IsNaN(designFile.sessionSeconds) || double.IsInfinity(designFile.sessionSeconds) ||
            designFile.repetitions != 1 || designFile.steps == null || designFile.steps.Length == 0)
            throw new ArgumentException("Pilot requires a finite deadline and one complete block per repetition.");
        pilotAppearance = JsonConvert.DeserializeObject<PathGroundAppearance>(
            ExperimentConfigFiles.ReadAllText(designFile.groundAppearance, "pilot-ground-appearance"));
        pilotAppearance.Validate();
        foreach (var step in designFile.steps) step.pathGround?.Validate();
        int seed = designFile.seed < 0 ? Guid.NewGuid().GetHashCode() & int.MaxValue : designFile.seed;
        pilotRandom = new System.Random(seed);
        orderedSteps = BuildOrdered(designFile, null);
        loopSequence = true;
        pilotLog = Path.Combine(MasterDataLogger.Instance.directoryPath, "path_pilot_events.jsonl");
        pilotActive = true;
        PilotEvent("session_start", new { seed, deadlineSeconds = designFile.sessionSeconds,
            appearance = pilotAppearance, conditions = designFile.steps.Select(s => s.name).ToArray(),
            clock = "monotonic elapsed seconds since MainController.StartSequence; includes scene loading and intertrials" });
    }

    private double PilotNow => MainController.Instance?.SessionElapsedSeconds ?? 0;
    private void PilotEvent(string kind, object data)
    {
        File.AppendAllText(pilotLog, JsonConvert.SerializeObject(new {
            kind, elapsedSeconds = PilotNow, utc = DateTime.UtcNow.ToString("O"), data
        }) + "\n");
    }
    private void PilotBlockStarted(RigState state)
    {
        var progress = new PilotProgress { blockOpen = true };
        pilotProgress[state.id] = progress;
        PilotEvent("block_start", new { rig = state.id, block = state.loopCount + 1,
            order = state.steps.Where(s => s != designFile.intertrial).Select(s => s.name).ToArray() });
    }
    private void PilotStepStarted(RigState state, Step step)
    {
        if (!pilotActive) return;
        var progress = pilotProgress[state.id];
        progress.current = step;
        progress.started = PilotNow;
        PilotEvent(step == designFile.intertrial ? "intertrial_start" : "trial_start", new {
            rig = state.id, block = state.loopCount + 1, trialInBlock = progress.completed + 1,
            step = step.name, angle = step.pathGround?.openingAngle,
            forkZCm = step.pathGround == null ? (float?)null : PathGround.ForkZ(step.pathGround.openingAngle),
            cumulativeStep = state.cumulativeStep });
    }
    private void PilotStepEnded(RigState state, Step step, AreaWaitResult result)
    {
        if (!pilotActive) return;
        var progress = pilotProgress[state.id];
        bool intertrial = step == designFile.intertrial;
        PilotEvent(intertrial ? "intertrial_end" : "trial_end", new {
            rig = state.id, block = state.loopCount + 1, trialInBlock = progress.completed + 1,
            step = step.name, durationSeconds = PilotNow - progress.started,
            outcome = intertrial ? "completed" : result?.Triggered == true ? "goal_contact" :
                result?.TimedOut == true ? "no_choice_timeout" : "duration_complete",
            goalObjectIndex = result?.Triggered == true ? result.TriggeredObjectIndex : -1,
            goalSide = result?.Triggered == true && step.objects[result.TriggeredObjectIndex].polar != null
                ? (step.objects[result.TriggeredObjectIndex].polar.angle < 0 ? "left" : "right") : null });
        progress.current = null;
        if (intertrial) return;
        progress.completed++;
        if (progress.completed == designFile.steps.Length)
        {
            progress.blockOpen = false;
            PilotEvent("block_end", new { rig = state.id, block = state.loopCount + 1,
                completedTrials = progress.completed, incomplete = false });
        }
    }

    private void Update() => TickPilotSession();

    // Also called by the editor watchdog while Play mode is paused. Stopwatch is independent of timeScale.
    public bool TickPilotSession()
    {
        if (!pilotActive) return false;
        if (PilotNow < designFile.sessionSeconds && MainController.Instance?.SequenceRunning == true) return false;
        bool deadline = PilotNow >= designFile.sessionSeconds;
        string reason = deadline ? "pilot_session_deadline" : "stopped";
        EndPilotSession(reason);
        if (deadline) MainController.Instance?.StopSequence(reason);
        return true;
    }

    public void EndPilotSession(string reason)
    {
        if (!pilotActive) return;
        pilotActive = false;
        StopAllCoroutines();
        foreach (var state in rigStates.Values)
        {
            if (pilotProgress.TryGetValue(state.id, out var progress))
            {
                if (progress.current != null)
                    PilotEvent(progress.current == designFile.intertrial ? "intertrial_interrupted" : "trial_interrupted", new {
                        rig = state.id, block = state.loopCount + 1, trialInBlock = progress.completed + 1,
                        step = progress.current.name, durationSeconds = PilotNow - progress.started, reason });
                if (progress.blockOpen)
                    PilotEvent("block_end", new { rig = state.id, block = state.loopCount + 1,
                        completedTrials = progress.completed, incomplete = true, reason });
            }
            if (state.container != null) state.container.gameObject.SetActive(false);
            if (state.rig != null)
            {
                foreach (var movement in state.rig.GetComponentsInChildren<ClosedLoop>()) movement.enabled = false;
                foreach (var keyboard in state.rig.GetComponentsInChildren<Keyboard>()) keyboard.enabled = false;
            }
        }
        PilotEvent("session_end", new { reason, deadlineSeconds = designFile.sessionSeconds,
            deadlineOvershootSeconds = Math.Max(0, PilotNow - designFile.sessionSeconds) });
    }
}
