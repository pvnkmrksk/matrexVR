using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(1200)]
public class BandLogger : MonoBehaviour
{
    public LayerMask targetLayerMask;
    private string directoryPath;
    private string logPath;
    private AsyncSwarmWriter writer;
    private readonly List<CapturedAgent> capture = new List<CapturedAgent>();

    private sealed class CapturedAgent
    {
        public GameObject instance;
        public Transform transform;
        public string name, layer;
        public DirectionalMovement movement;
        public VisibilityScript visibility;
        public RecordedAnimationCapture animation;
    }
    private bool closedForArchive;
    private BandSpawner bandSpawner;

    void Start()
    {
        if (closedForArchive) return;
        // Access directoryPath from MasterDataLogger
        MasterDataLogger masterDataLogger = FindObjectOfType<MasterDataLogger>();
        if (masterDataLogger != null)
        {
            if (!masterDataLogger.AcceptingWrites || !masterDataLogger.SaveSwarmData) { closedForArchive = true; return; }
            directoryPath = masterDataLogger.directoryPath;
        }
        else
        {
            Debugger.Log("MasterDataLogger not found.", 1);
            return;
        }

        // Get a reference to the BandSpawner script
        bandSpawner = GetComponent<BandSpawner>();
        if (bandSpawner == null)
        {
            Debugger.Log("BandSpawner script not found on this GameObject.", 1);
            return;
        }

        // Initialize the log file
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss-fffffff");
        string layerNames = GetLayerNames(targetLayerMask);

        string prefabName = bandSpawner.instancePrefab.name;
        string bandName = gameObject.name; // Get the name of the GameObject this script is attached to

        // Construct the base filename without the counter
        string baseFileName = $"{date}_{time}_SimulatedData_{prefabName}_{bandName}";
        string filenameSuffix = $"{layerNames}_{bandSpawner.numberOfInstances}_{bandSpawner.spawnLengthX}_{bandSpawner.spawnLengthZ}_{bandSpawner.mu}_{bandSpawner.kappa}_{bandSpawner.speed}.matpop";

        // Ensure unique file name
        string uniqueFileName = GetUniqueFileName(directoryPath, baseFileName, filenameSuffix);

        logPath = Path.Combine(directoryPath, uniqueFileName);

        writer = new AsyncSwarmWriter(logPath, FormattableString.Invariant($"Timestamp,Name,Layer,X,Y,Z,RotationX,RotationY,RotationZ,Speed,VisibilityPhase,experimentPhase,Visible,UnityFrame,AnimationState,ScaleX,ScaleY,ScaleZ,SpawnLengthX:{bandSpawner.spawnLengthX},SpawnLengthZ:{bandSpawner.spawnLengthZ},GridType:{bandSpawner.gridType},Mu:{bandSpawner.mu},Kappa:{bandSpawner.kappa},Speed:{bandSpawner.speed},VisibleOffDuration:{bandSpawner.visibleOffDuration},VisibleOnDuration:{bandSpawner.visibleOnDuration},BoundaryLengthX:{bandSpawner.boundaryLengthX},BoundaryLengthZ:{bandSpawner.boundaryLengthZ},RotationAngle:{bandSpawner.rotationAngle},VisibilityPhaseClock:local-timer"), AsyncSwarmWriter.RowLayout.Band);
    }

    void LateUpdate()
    {
        if (closedForArchive || bandSpawner == null || writer == null) return;
        if (writer.Failure != null) { StopForEvidenceFailure(writer.Failure); return; }

        var instances = bandSpawner.Instances;
        if (capture.Count > instances.Count) capture.RemoveRange(instances.Count, capture.Count - instances.Count);
        while (capture.Count < instances.Count) capture.Add(null);
        var frame = writer.Rent(instances.Count);
        frame.timestamp = RecordedFrameClock.Now;
        frame.unityFrame = Time.frameCount;
        frame.phase = MainController.RecordedExperimentPhase;
        for (int i = 0; i < instances.Count; i++)
        {
            GameObject instance = instances[i];
            if (instance == null || !instance.activeInHierarchy || (targetLayerMask.value & (1 << instance.layer)) == 0)
                continue;

            var agent = capture[i];
            if (agent == null || agent.instance != instance)
            {
                capture[i] = agent = new CapturedAgent { instance = instance, transform = instance.transform,
                    name = instance.name, layer = RuntimeRigRegistry.LayerLabel(instance.layer),
                    movement = instance.GetComponent<DirectionalMovement>(), visibility = instance.GetComponent<VisibilityScript>(),
                    animation = new RecordedAnimationCapture(instance) };
            }
            Vector3 position = agent.transform.position;
            Vector3 rotation = agent.transform.eulerAngles;
            Vector3 scale = agent.transform.lossyScale;
            var animation = frame.agents[frame.count].animationSnapshot as RecordedAnimationSnapshot ?? new RecordedAnimationSnapshot();
            agent.animation.CaptureSnapshot(animation);
            frame.agents[frame.count++] = new AsyncSwarmWriter.Agent { index = i, name = agent.name, layer = agent.layer,
                x = position.x, y = position.y, z = position.z, rx = rotation.x, ry = rotation.y, rz = rotation.z,
                sx = scale.x, sy = scale.y, sz = scale.z, animationSnapshot = animation,
                speed = agent.movement ? agent.movement.GetSpeed() : 0f,
                visibilityPhase = agent.visibility ? agent.visibility.GetCurrentPhase() : 0f,
                visible = agent.visibility == null || agent.visibility.IsVisible };
        }
        if (!writer.Submit(frame)) StopForEvidenceFailure("The bounded population recording queue could not accept the rendered frame.");
    }

    private void StopForEvidenceFailure(string reason)
    {
        CloseForArchive();
        var error = new IOException("Band recording evidence could not be preserved: " + reason);
        if (MainController.Instance != null) MainController.Instance.FailRecordingEvidence(error);
        else Debug.LogException(error);
    }

    string GetLayerNames(LayerMask layerMask)
    {
        string layerNames = "";
        for (int i = 0; i < 32; i++)
        {
            if ((layerMask.value & (1 << i)) != 0)
            {
                layerNames += RuntimeRigRegistry.LayerLabel(i) + "_";
            }
        }
        return layerNames.TrimEnd('_');
    }

    private string GetUniqueFileName(string directory, string baseFileName, string suffix)
    {
        int counter = 0;
        string fileName;
        do
        {
            fileName = $"{baseFileName}_{counter}_{suffix}";
            counter++;
        } while (File.Exists(Path.Combine(directory, fileName)));

        return fileName;
    }

    void OnDestroy() => CloseForArchive();

    public void CloseForArchive()
    {
        closedForArchive = true;
        writer?.Complete();
    }
}
