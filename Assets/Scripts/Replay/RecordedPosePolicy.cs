using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

public enum RecordedPoseExclusion { None, SetupOrReplay, VerifiedIdleOrHandoff }

/// <summary>One read-only exclusion policy for replay, recording summaries and the local library.</summary>
public sealed class RecordedPosePolicy
{
    private sealed class Columns
    {
        public int Scene,Logical,Phase,Step,Frame;
        public Columns(string[] header)
        {Scene=Array.IndexOf(header,"Scene");Logical=Array.IndexOf(header,"CurrentSequenceScene");Phase=Array.IndexOf(header,"experimentPhase");Step=Array.IndexOf(header,"CurrentStep");Frame=Array.IndexOf(header,"unityFrame");}
        public bool Modern => Scene>=0&&Logical>=0&&Phase>=0&&Step>=0&&Frame>=0;
    }
    private readonly string[] steps;
    private readonly HashSet<string> scenes;
    private readonly Dictionary<string[],Columns> columns=new Dictionary<string[],Columns>();
    private RecordedPosePolicy(JObject sequence,bool checksumVerified)
    {
        if(checksumVerified && sequence?["sequences"] is JArray source)
        {
            steps=source.Select(item=>(item as JObject)?["sceneName"]?.ToString()).ToArray();
            scenes=new HashSet<string>(steps.Where(scene=>!string.IsNullOrEmpty(scene)),StringComparer.Ordinal);
        }
    }
    public static RecordedPosePolicy FromSavedSequence(JObject sequence,bool checksumVerified) => new RecordedPosePolicy(sequence,checksumVerified);
    public RecordedPoseExclusion Classify(string file,string[] header,string[] cells)
    {
        if(!columns.TryGetValue(header,out var map)){map=new Columns(header);columns.Add(header,map);}
        string Cell(int index)=>index>=0&&index<cells.Length?cells[index]:"";
        string scene=Cell(map.Scene);
        if(IsNonExperimentScene(scene,file))return RecordedPoseExclusion.SetupOrReplay;
        if(steps==null||!map.Modern||ReplaySessionArchive.Integer(Cell(map.Frame),-1)<0)return RecordedPoseExclusion.None;
        int step=ReplaySessionArchive.Integer(Cell(map.Step),-1);
        if(step<0||step>=steps.Length||string.IsNullOrEmpty(steps[step]))return RecordedPoseExclusion.None;
        string logical=Cell(map.Logical),phase=Cell(map.Phase);
        if(string.IsNullOrEmpty(phase)||logical!=steps[step]||!scenes.Contains(scene))return RecordedPoseExclusion.None;
        return scene!=logical||phase=="idle"?RecordedPoseExclusion.VerifiedIdleOrHandoff:RecordedPoseExclusion.None;
    }
    public static bool IsNonExperimentScene(string scene,string file)
    {
        scene=(scene??"").Trim();
        if(string.Equals(scene,"ControlScene",StringComparison.OrdinalIgnoreCase)||string.Equals(scene,"ReplayScene",StringComparison.OrdinalIgnoreCase))return true;
        if(scene.Length>0)return false;
        string name=Path.GetFileName(file);
        return name.IndexOf("_ControlScene_",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("_ReplayScene_",StringComparison.OrdinalIgnoreCase)>=0;
    }
}
