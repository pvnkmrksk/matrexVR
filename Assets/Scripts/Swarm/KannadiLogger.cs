using UnityEngine;
using System.IO;
using System;

/// <summary>
/// Logs positions and orientations of Kannadi clones with VR index information.
/// </summary>
[DefaultExecutionOrder(1300)]
public class KannadiLogger : MonoBehaviour
{
    private string directoryPath;
    private string logPath, recordedLayer;
    private AsyncSwarmWriter writer;
    private bool closedForArchive;
    private Kannadi kannadi;
    private int vrIndex = 0; // Will be extracted from GameObject name (VR1, VR2, etc.)
    private GameObject[] capturedClones;
    private RecordedAnimationCapture[] animationCaptures;
    private string[] names;
    public double CaptureMilliseconds { get; private set; }
    public int PendingFrames => writer?.PendingFrames ?? 0;
    public long DroppedFrames => writer?.DroppedFrames ?? 0;
    public string Failure => writer?.Failure;

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

        if (string.IsNullOrEmpty(directoryPath)) return;

        // Get Kannadi component
        kannadi = GetComponent<Kannadi>();
        if (kannadi == null)
        {
            Debugger.Log("Kannadi script not found on this GameObject.", 1);
            return;
        }

        // Extract VR index from GameObject name (e.g., "VR1 Kannadi" -> 1)
        vrIndex = RuntimeRigRegistry.ParseIndex(gameObject.name);
        recordedLayer = $"SimulatedLocustsVR{vrIndex}";

        // Initialize the log file
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string time = DateTime.Now.ToString("HH-mm-ss-fffffff");
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        logPath = Path.Combine(
            directoryPath,
            $"{date}_{time}_{sceneName}_Kannadi_VR{vrIndex}_Clones.matpop"
        );

        writer=new AsyncSwarmWriter(logPath,
            "Timestamp,VRIndex,CloneIndex,CloneName,PositionX,PositionY,PositionZ,RotationX,RotationY,RotationZ,NumberOfRings,hexRadius,skyboxId,skyboxSampleUtc,experimentPhase,AnimationState,UnityFrame,ScaleX,ScaleY,ScaleZ",
            AsyncSwarmWriter.RowLayout.Kannadi);
    }

    void LateUpdate()
    {
        long captureStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        CaptureFrame();
        CaptureMilliseconds = 1000.0 * (System.Diagnostics.Stopwatch.GetTimestamp() - captureStarted) / System.Diagnostics.Stopwatch.Frequency;
    }

    private void CaptureFrame()
    {
        if (closedForArchive || kannadi == null || writer == null)
            return;
        if(writer.Failure!=null) { StopForEvidenceFailure(writer.Failure);return; }

        GameObject[] clones = kannadi.Clones;
        if (clones == null)
            return;
        if (!ReferenceEquals(capturedClones, clones))
        {
            capturedClones = clones; animationCaptures = new RecordedAnimationCapture[clones.Length];names=new string[clones.Length];
            for (int i = 0; i < clones.Length; i++) if (clones[i] != null) {animationCaptures[i] = new RecordedAnimationCapture(clones[i]);names[i]=clones[i].name;}
        }
        var frame=writer.Rent(clones.Length);
        frame.timestamp=RecordedFrameClock.Now;frame.unityFrame=Time.frameCount;frame.vrIndex=vrIndex;
        frame.numberOfRings=kannadi.numberOfRings;frame.hexRadius=kannadi.hexRadius;
        frame.skyboxId=NightSkyController.CurrentId;frame.skyboxSampleUtc=NightSkyController.CurrentSampleUtc;
        frame.phase=MainController.RecordedExperimentPhase;

        for (int i = 0; i < clones.Length; i++)
        {
            if (clones[i] != null)
            {
                Vector3 position = clones[i].transform.position;
                Vector3 rotation = clones[i].transform.rotation.eulerAngles;

                Vector3 scale=clones[i].transform.lossyScale;
                var animation=frame.agents[frame.count].animationSnapshot as RecordedAnimationSnapshot??new RecordedAnimationSnapshot();
                if(animationCaptures[i]!=null)animationCaptures[i].CaptureSnapshot(animation);else animation.CopyFrom(null);
                frame.agents[frame.count++]=new AsyncSwarmWriter.Agent {index=i,name=names[i],layer=recordedLayer,x=position.x,y=position.y,z=position.z,
                    rx=rotation.x,ry=rotation.y,rz=rotation.z,sx=scale.x,sy=scale.y,sz=scale.z,
                    animationSnapshot=animation};
            }
        }
        if(!writer.Submit(frame))StopForEvidenceFailure("The bounded population recording queue could not accept the rendered frame.");
    }

    void OnDestroy() => CloseForArchive();

    public void CloseForArchive()
    {
        closedForArchive = true;
        writer?.Complete();
    }
    private void StopForEvidenceFailure(string reason)
    {
        CloseForArchive();
        var error=new IOException("Kannadi recording evidence could not be preserved: "+reason);
        if(MainController.Instance!=null)MainController.Instance.FailRecordingEvidence(error);else Debug.LogException(error);
    }
}
