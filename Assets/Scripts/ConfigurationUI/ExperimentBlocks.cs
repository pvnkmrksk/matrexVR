using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConfigurationUI
{
    public sealed class ExperimentPreset
    {
        public string Reference, Title, Schema, Scene, Category;
        public JObject Config;
        public double Duration => ExperimentBlocks.EffectiveDuration(Config,ExperimentBlocks.FlatDuration(Config));
        public string Description => ExperimentBlocks.Describe(Config);
    }

    /// <summary>Presentation and sequence operations keep the existing JSON shape and reference ownership.</summary>
    public static class ExperimentBlocks
    {
        public static double Number(JToken token,double fallback=0)
        { return token!=null && double.TryParse(token.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out double n) && !double.IsInfinity(n) && !double.IsNaN(n)?n:fallback; }
        public static bool Flag(JToken token,bool fallback=false) => token?.Type==JTokenType.Boolean?(bool)token:fallback;
        public static JObject Payload(JObject config) => config?["stimulus"] as JObject??config;
        public static bool Phased(JObject config) => config!=null && new[]{"preStimulus","stimulus","postStimulus"}.Any(k=>config[k] is JObject);
        public static double FlatDuration(JObject config)
        {
            var stimuli=config?["stimuli"] as JArray;
            return stimuli!=null && stimuli.Count>0?stimuli.Sum(s=>Number(s["duration"],10)):30;
        }
        public static double EffectiveDuration(JObject config,double outer)
        {
            if(config==null) return outer;
            if(!Phased(config))
            {
                var heading=config["headingReference"] as JObject;
                if(!Flag(heading?["enabled"])) return outer;
                double observation=Number(heading?["windowSeconds"],180)+Number(heading?["startOffsetSeconds"]);
                return outer;
            }
            double total=0;
            foreach(string phase in new[]{"preStimulus","stimulus","postStimulus"})
                if(config[phase] is JObject obj && Flag(obj["enabled"],phase=="stimulus"))
                { total+=Number(obj["durationSeconds"]); }
            return total;
        }
        public static List<string> TimingWarnings(JObject config,double duration)
        {
            var warnings=new List<string>();
            if(config==null) return warnings;
            Action<JObject,double,string> inspect=(payload,seconds,prefix)=>{
                var heading=(payload["headingReference"] as JObject)?.ToObject<HeadingReferenceConfig>();
                if(heading==null) return;
                heading.WithinDuration(seconds,out string warning);
                if(warning!=null) warnings.Add(prefix+warning);
            };
            var parsed=JObject.Parse(config.ToString());
            if(!Phased(parsed)) inspect(parsed,duration,"");
            else foreach(string phase in new[]{"preStimulus","stimulus","postStimulus"})
                if(parsed[phase] is JObject obj && Flag(obj["enabled"],phase=="stimulus")) inspect(obj,Number(obj["durationSeconds"]),PrettyPhase(phase)+": ");
            return warnings;
        }
        private static string PrettyPhase(string phase) => phase=="preStimulus"?"Before stimulus":phase=="postStimulus"?"After stimulus":"Stimulus";
        public static string Time(double seconds)
        {
            if(seconds<60) return seconds.ToString("0.###",CultureInfo.InvariantCulture)+" s";
            double hours=Math.Floor(seconds/3600),minutes=Math.Floor((seconds-hours*3600)/60),remaining=seconds-hours*3600-minutes*60;
            var parts=new List<string>(); if(hours>0) parts.Add(hours.ToString("0",CultureInfo.InvariantCulture)+" h"); if(minutes>0) parts.Add(minutes.ToString("0",CultureInfo.InvariantCulture)+" min"); if(remaining>0) parts.Add(remaining.ToString("0.###",CultureInfo.InvariantCulture)+" s");
            return string.Join(" ",parts);
        }
        public static string Title(string reference)
        {
            string name=Path.GetFileNameWithoutExtension(reference??"").Replace(".example","").Replace(".template","").Replace('-',' ').Replace('_',' ');
            return string.IsNullOrEmpty(name)?"Untitled block":char.ToUpperInvariant(name[0])+name.Substring(1);
        }
        public static string Schema(JObject config,string reference)
        {
            JObject p=Payload(config); string path=reference.Replace('\\','/').ToLowerInvariant();
            if(config["configs"] is JArray) return "system";
            if(config["sequences"] is JArray) return "sequence";
            if(config["steps"] is JArray || config["intertrial"]!=null) return "dynamic-choice";
            if(p?["stimuli"] is JArray) return "optomotor";
            if(path.Contains("kannadi") || path.Contains("kinematic") || p?["numberOfRings"]!=null) return "kannadi";
            if(path.Contains("/swarm/") || p?["numberOfLocusts"]!=null || p?["agentVisual"]!=null || p?["vrConfigs"]!=null) return "swarm";
            return "choice";
        }
        public static ExperimentPreset Read(string reference,string fullPath)
        {
            var config=LosslessJson.Parse(RunConfigArchive.Decode(File.ReadAllBytes(fullPath))) as JObject;
            if(config==null) throw new ArgumentException("Experiment config must be an object.");
            string schema=Schema(config,reference); JObject p=Payload(config);
            bool flight=Number(p?["aglHeight"])>0 || reference.Replace('\\','/').Contains("/Flight/");
            string scene=schema=="swarm"?"Swarm":schema=="kannadi"?"Kannadi":schema=="optomotor"?"Optomotor":schema=="dynamic-choice"?"Choice_desync":flight?"Choice_Selwyn":"Choice";
            string category=schema=="swarm"?"Swarm":schema=="optomotor"?"Optomotor":schema=="dynamic-choice"?"Dynamic choice":schema=="kannadi"?"Walking":flight?"Flight":"Choice";
            if(!reference.StartsWith("Templates/",StringComparison.Ordinal) && !reference.StartsWith("Examples/",StringComparison.Ordinal)) category="Custom";
            return new ExperimentPreset { Reference=reference,Title=Title(reference),Schema=schema,Scene=scene,Category=category,Config=config };
        }
        public static List<ExperimentPreset> Catalog(string streaming,bool includeArchive=false)
        {
            var result=new List<ExperimentPreset>();
            foreach(string file in Directory.EnumerateFiles(streaming,"*.json",SearchOption.AllDirectories))
            {
                string reference=Path.GetRelativePath(streaming,file).Replace('\\','/');
                if((!includeArchive && reference.StartsWith("Archive/")) || reference.Contains("/NightSky/") || reference.EndsWith(".metadata.json") || reference.Contains(".before-")) continue;
                try { var preset=Read(reference,file); if(reference.StartsWith("Archive/")) preset.Category="Archive"; if(preset.Schema!="system" && preset.Schema!="sequence") result.Add(preset); }
                catch { /* Malformed files can still be selected explicitly and show their parse error. */ }
            }
            return result.OrderBy(p=>p.Category).ThenBy(p=>p.Title).ThenBy(p=>p.Reference).ToList();
        }
        public static string Describe(JObject config)
        {
            JObject p=Payload(config); if(p==null) return "Configuration unavailable";
            var details=new List<string>();
            if(Flag(p["autopilotEnabled"])) details.Add("Autopilot "+p["autopilotSpeed"]+" units/s");
            else if(p["closedLoopPosition"]!=null) details.Add("Closed loop position "+(Flag(p["closedLoopPosition"],Number(p["closedLoopPosition"],1)!=0)?"ON":"OFF"));
            if(p["closedLoopOrientation"]!=null) details.Add("Closed loop orientation "+(Flag(p["closedLoopOrientation"],Number(p["closedLoopOrientation"],1)!=0)?"ON":"OFF"));
            JObject swarm=p["swarm"] as JObject??(p["numberOfLocusts"]!=null?p:null);
            if(swarm!=null) details.Add(!string.IsNullOrWhiteSpace((string)swarm["trajectoryFile"])?"Recorded swarm":(swarm["numberOfLocusts"]?.ToString()??"Scene default")+" agents · "+(string)(swarm["dimension"]??new JValue("2D")));
            if(p["objects"] is JArray objects && objects.Count>0) details.Add(objects.Count+" objects");
            if(p["stimuli"] is JArray stimuli)
            { details.Add(stimuli.Count+" gratings"); if(stimuli.First is JObject first) { details.Add((first["rotationAxis"]?.ToString()??"Yaw")+" · "+first["speed"]+" deg/s"); if(first["contrast"]!=null) details.Add((Number(first["contrast"])*100).ToString("0.#",CultureInfo.InvariantCulture)+"% contrast"); } }
            if(p["steps"] is JArray steps) details.Add(steps.Count+" decision steps · "+(Flag(p["sync"],true)?"synchronized":"independent"));
            if(Flag(p["adaptiveDecision"]?["enabled"])) details.Add("Adaptive decision ON");
            if(p["uniformSkyColor"] is JObject) details.Add("Uniform sky");
            else if(!string.IsNullOrWhiteSpace((string)p["skyboxPath"])) details.Add("Panorama");
            else if(Flag(p["nightSky"]?["enabled"])) details.Add(Flag(p["nightSky"]?["advanceWithRealTime"],true)?"Live sky":"Fixed sky");
            if(Number(p["aglHeight"])>0) details.Add(p["aglHeight"]+" units AGL");
            return details.Count==0?"Scene defaults · open to inspect":string.Join("  ·  ",details);
        }
        public static JArray Steps(JToken root) => root["sequences"] as JArray??throw new ArgumentException("Sequence must have a sequences array.");
        public static JObject NewBlock(ExperimentPreset preset,string scene=null)
        {
            return new JObject { ["sceneName"]=scene??preset.Scene,["duration"]=new JRaw(Math.Max(.001,preset.Duration).ToString("R",CultureInfo.InvariantCulture)),["gain"]=1,["autoTrim"]=false,["reloadScene"]=true,
                ["parameters"]=new JObject { [preset.Schema=="dynamic-choice"?"design":"configFile"]=preset.Reference } };
        }
        public static void Move(JToken root,int from,int to)
        { var steps=Steps(root); if(from<0 || to<0 || from>=steps.Count || to>=steps.Count || from==to) return; JToken step=steps[from]; steps.RemoveAt(from); steps.Insert(to,step); }
    }
}
