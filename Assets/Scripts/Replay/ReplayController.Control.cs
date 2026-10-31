using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public partial class ReplayController
{
    /// <summary>The same replay state used by sliders, panel-pixel toggle and overview mouse controls.</summary>
    public JObject ControlOptions(JObject patch=null)
    {
        JObject Snapshot()=>new JObject{["playbackSpeed"]=playbackSpeed,["loop"]=loop,["panelPixels"]=pixelScaleAnimalView,
            ["trailSeconds"]=trailSeconds,["trailProminence"]=trailProminence,["perspective"]=overviewPerspective,
            ["followRig"]=followedRig,["yaw"]=overviewYaw,["pitch"]=overviewPitch,["distance"]=overviewDistance,
            ["zoom"]=overview!=null?overview.orthographicSize:1,["pivot"]=new JObject{["x"]=overviewPivot.x,["y"]=overviewPivot.y,["z"]=overviewPivot.z}};
        if(patch==null)return Snapshot();
        var candidate=Snapshot();
        if(patch.Properties().Any(property=>candidate.Property(property.Name)==null))throw new ArgumentException("Unknown replay option.");
        candidate.Merge(patch,new JsonMergeSettings{MergeNullValueHandling=MergeNullValueHandling.Merge});
        bool Flag(string key)=>candidate[key]?.Type==JTokenType.Boolean?(bool)candidate[key]:throw new ArgumentException(key+" must be a boolean.");
        float Number(JToken value,string key,float min,float max)
        {
            string kind=ConfigurationUI.LosslessJson.Kind(value);
            if((kind!="number"&&kind!="integer")||!double.TryParse(value.ToString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double n))throw new ArgumentException(key+" must be numeric.");
            if(double.IsNaN(n)||double.IsInfinity(n)||n<min||n>max)throw new ArgumentException(key+" is outside the replay control range.");
            return (float)n;
        }
        float speed=Number(candidate["playbackSpeed"],"playbackSpeed",.1f,4),duration=Number(candidate["trailSeconds"],"trailSeconds",.25f,120),prominence=Number(candidate["trailProminence"],"trailProminence",0,3);
        bool repeating=Flag("loop"),pixels=Flag("panelPixels"),perspective=Flag("perspective");
        float yaw=Number(candidate["yaw"],"yaw",-1000000,1000000),pitch=Number(candidate["pitch"],"pitch",-89.9f,90),distance=Number(candidate["distance"],"distance",.01f,10000000),zoom=Number(candidate["zoom"],"zoom",.01f,10000000);
        var pivot=candidate["pivot"] as JObject??throw new ArgumentException("pivot must be an object.");
        if(pivot.Properties().Any(property=>!new[]{"x","y","z"}.Contains(property.Name)))throw new ArgumentException("Unknown pivot coordinate.");
        var point=new Vector3(Number(pivot["x"],"pivot.x",-10000000,10000000),Number(pivot["y"],"pivot.y",-10000000,10000000),Number(pivot["z"],"pivot.z",-10000000,10000000));
        if(candidate["followRig"]?.Type!=JTokenType.Null&&candidate["followRig"]?.Type!=JTokenType.String)throw new ArgumentException("followRig must be a rig ID or null.");
        string follow=(string)candidate["followRig"];
        if(follow!=null&&!markerByRig.ContainsKey(follow))throw new ArgumentException("Unknown recorded rig: "+follow);
        // All validation completes before touching live replay state.
        playbackSpeed=speed;loop=repeating;pixelScaleAnimalView=pixels;trailSeconds=duration;trailProminence=prominence;
        if(perspective!=overviewPerspective)ToggleOverviewProjection();
        if(patch.Property("followRig")!=null)FollowRig(follow);
        if(patch.Property("pivot")!=null)overviewPivot=point;
        if(patch.Property("yaw")!=null)overviewYaw=yaw;
        if(patch.Property("pitch")!=null)overviewPitch=pitch;
        if(patch.Property("distance")!=null)overviewDistance=distance;
        if(overview!=null&&patch.Property("zoom")!=null)overview.orthographicSize=zoom;
        overviewUserAdjusted=true;ApplyOverviewPose();UpdateReplayCameraClipping();return Snapshot();
    }
}
