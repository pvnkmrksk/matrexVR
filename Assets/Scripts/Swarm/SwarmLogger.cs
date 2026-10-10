using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SwarmLogger : MonoBehaviour
{
    public LayerMask locustLayerMask;
    private LocustSpawner spawner;
    private AsyncSwarmWriter writer;
    private int cachedRevision = -1, cachedMask;
    private long reportedDrops;
    private readonly List<Transform> agents = new List<Transform>();
    private readonly List<string> names = new List<string>(), layers = new List<string>();
    private static readonly Unity.Profiling.ProfilerMarker captureMarker = new Unity.Profiling.ProfilerMarker("SwarmLogger.Capture");
    public int PendingFrames => writer?.PendingFrames ?? 0;
    public long DroppedFrames => writer?.DroppedFrames ?? 0;
    public string Failure => writer?.Failure;
    public double CaptureMilliseconds { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallShutdown() { Application.quitting -= AsyncSwarmWriter.DrainAll; Application.quitting += AsyncSwarmWriter.DrainAll; }

    private void Start()
    {
        var master = MasterDataLogger.Instance;
        spawner = GetComponent<LocustSpawner>();
        if (master == null || spawner == null || string.IsNullOrEmpty(master.directoryPath)) return;
        string layerNames = "";
        for (int i = 0; i < 32; i++) if ((locustLayerMask.value & (1 << i)) != 0) layerNames += LayerMask.LayerToName(i) + "_";
        string path = Path.Combine(master.directoryPath,
            $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fffffff}_SimulatedLocustData_{layerNames.TrimEnd('_')}_{spawner.numberOfLocusts}_{spawner.spawnAreaSize}_{spawner.mu}_{spawner.kappa}_{spawner.locustSpeed}.csv.gz");
        string header = FormattableString.Invariant($"Timestamp,Name,Layer,X,Y,Z,skyboxId,skyboxSampleUtc,experimentPhase,NumberOfLocusts:{spawner.numberOfLocusts},SpawnAreaSize:{spawner.spawnAreaSize},Mu:{spawner.mu},Kappa:{spawner.kappa},LocustSpeed:{spawner.locustSpeed}");
        writer = new AsyncSwarmWriter(path, header);
    }

    private void LateUpdate()
    {
        if (writer == null || spawner == null || writer.Failure != null) return;
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        using (captureMarker.Auto())
        {
            if (cachedRevision != spawner.SpawnRevision || cachedMask != locustLayerMask.value)
            {
                agents.Clear(); names.Clear(); layers.Clear();
                foreach (var agent in spawner.Spawned)
                    if (agent != null && (locustLayerMask.value & (1 << agent.layer)) != 0)
                    { agents.Add(agent.transform); names.Add(agent.name); layers.Add(LayerMask.LayerToName(agent.layer)); }
                cachedRevision = spawner.SpawnRevision; cachedMask = locustLayerMask.value;
            }
            var frame = writer.Rent(agents.Count);
            frame.timestamp = DateTime.Now;
            frame.skyboxId = NightSkyController.CurrentId;
            frame.skyboxSampleUtc = NightSkyController.CurrentSampleUtc;
            frame.phase = MainController.RecordedExperimentPhase;
            for (int i = 0; i < agents.Count; i++)
            {
                if (agents[i] == null || !agents[i].gameObject.activeInHierarchy) continue;
                Vector3 p = agents[i].position;
                frame.agents[frame.count++] = new AsyncSwarmWriter.Agent { name = names[i], layer = layers[i], x = p.x, y = p.y, z = p.z };
            }
            writer.Submit(frame);
        }
        CaptureMilliseconds = 1000.0 * (System.Diagnostics.Stopwatch.GetTimestamp() - start) / System.Diagnostics.Stopwatch.Frequency;
        if (writer.DroppedFrames > reportedDrops)
        {
            reportedDrops = writer.DroppedFrames;
            if (reportedDrops == 1 || reportedDrops % 120 == 0)
                Debug.LogError($"Swarm log backlog exceeded 240 frames on {name}; {reportedDrops} frames dropped. Check recording disk speed/space.");
        }
    }
    private void OnDestroy() { writer?.Complete(); }
}
