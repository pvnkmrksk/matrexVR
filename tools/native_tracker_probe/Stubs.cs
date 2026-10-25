// Probe fixture only. Production transport/decoder/telemetry code is copied unchanged.
using System;
using System.Collections.Generic;
using UnityEngine;
public sealed class ManualControlConfig { }
public sealed class TrackerLaunchConfig { }
public class MainController:MonoBehaviour
{
    public static MainController Instance {get;set;}
    public static event Action SystemConfigurationChanged;
    public Dictionary<string,SystemConfig> SystemConfigs=new Dictionary<string,SystemConfig>();
    public Dictionary<string,ClosedLoop> VRClosedLoops=new Dictionary<string,ClosedLoop>();
    public TelemetryConfig TelemetrySettings=new TelemetryConfig();
    public AnimalData CurrentAnimalMetadata=new AnimalData();
    public bool SequenceRunning=true;
    public int currentTrial=2,currentStep=3;
    public float RemainingStepSeconds=>4;
    public float RemainingPhaseSeconds=>2;
    public string SequenceError=>null;
    public static string RecordedExperimentPhase=>"stimulus";
    public SequenceStep step;
    public SequenceStep GetCurrentSequenceStep()=>step;
    public bool TryGetSystemConfigForGameObject(GameObject obj,out SystemConfig config){config=null;return false;}
}
public class SequenceStep {public Dictionary<string,object> parameters;public float duration=30;}
public static class RuntimeRigRegistry {public static int ParseIndex(string id)=>int.Parse(id.Substring(2));}
public class MasterDataLogger {public static MasterDataLogger Instance=new MasterDataLogger();public string timestamp="native-probe-session";}
public static class RuntimeStatusOverlay {public static object RecentErrors()=>Array.Empty<object>();}
public static class fps {public static object Latest=>new{framesPerSecond=60};}
public static class Debugger {public static void Log(string text,int level){Debug.LogWarning(text);}}
public class StimulusHeadingReference:MonoBehaviour {public string Phase=>"disabled";public float RemainingSeconds=>0;public object Result=>null;}
public class ClosedLoop:MonoBehaviour
{
    public ClosedLoopMode GetCurrentMode()=>ClosedLoopMode.Hejjetrac;
    public string MotionMode=>"walking";
    public ZmqListener.RawSample LastConsumedSample=>default;
    public string InputInterpretation=>"walking-pose";
    public bool GetUseForceMode()=>false;
    public bool UsesBogongInput=>false;
    public bool PositionTrackingEnabled=>false;
    public bool OrientationTrackingEnabled=>false;
    public float PositionGain=>1;
    public float OrientationGain=>1;
    public float GetYawGain()=>1;
    public float GetYawDCOffset()=>0;
    public float GetLastYawOutput()=>0;
    public float ForceGain=>1;
    public float TorqueGain=>1;
    public bool AppliedTrackingThisFrame=>false;
    public Vector3 LastAppliedPositionDelta=>Vector3.zero;
    public float LastAppliedYawDeltaDegrees=>0;
    public bool SupportsAutoTrim=>false;
    public KineflyAutoTrim AutoTrim=new KineflyAutoTrim();
}
