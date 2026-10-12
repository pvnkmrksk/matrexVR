using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ConfigurationUI
{
    public sealed class ConfigWorkspace
    {
        public ConfigSchema Schemas { get; }
        public ConfigDocument System { get; private set; }
        public ConfigDocument Sequence { get; private set; }
        public List<ConfigDocument> Documents { get; } = new List<ConfigDocument>();
        public List<string> ReferenceErrors { get; } = new List<string>();
        private readonly HashSet<string> active=new HashSet<string>(StringComparer.Ordinal);
        public ConfigWorkspace(string system,string sequence,ConfigSchema schemas)
        { Schemas=schemas; System=Open(system,"system"); Sequence=Open(sequence,"sequence"); RefreshReferences(); }
        public ConfigDocument Open(string reference,string schema)
        {
            string path=ExperimentConfigFiles.Resolve(reference);
            path=Path.GetFullPath(path);
            var existing=Documents.FirstOrDefault(d=>d.Path==path);
            if(existing!=null)
            {
                if(existing.SchemaId!=schema) throw new ArgumentException("The same file is referenced with incompatible schemas: "+reference);
                return existing;
            }
            var doc=new ConfigDocument(path,schema); Documents.Add(doc); return doc;
        }
        public void SelectPrimary(string role,string reference)
        { if(role=="system") System=Open(reference,role); else Sequence=Open(reference,"sequence"); RefreshReferences(); }
        public void RefreshReferences()
        {
            ReferenceErrors.Clear(); active.Clear(); active.Add(System.Path); active.Add(Sequence.Path);
            var resolved=new Dictionary<string,string>(StringComparer.Ordinal);
            if(!(Sequence.Root["sequences"] is JArray steps)) return;
            foreach(var step in steps.OfType<JObject>())
            {
                string schema=ConfigSchema.ForScene((string)step["sceneName"]);
                string key=schema=="dynamic-choice"?"design":"configFile";
                var value=step["parameters"]?[key];
                if(value?.Type!=JTokenType.String || string.IsNullOrWhiteSpace((string)value))
                { ReferenceErrors.Add("Sequence "+step["sceneName"]+": parameters."+key+" is required."); continue; }
                string cacheKey=schema+"|"+(string)value;
                if(!resolved.TryGetValue(cacheKey,out string path))
                {
                    try { path=Open((string)value,schema).Path; }
                    catch(Exception error) { ReferenceErrors.Add(error.Message); path=null; }
                    resolved[cacheKey]=path;
                }
                if(path!=null) active.Add(path);
            }
        }
        public List<string> Validate(bool scenes=true)
        {
            RefreshReferences(); var errors=new List<string>(ReferenceErrors);
            foreach(var doc in Documents.Where(d=>active.Contains(d.Path)))
            {
                string label=Path.GetFileName(doc.Path);
                errors.AddRange(ConfigSchema.Validate(doc.Root,Schemas.Get(doc.SchemaId)).Select(e=>label+": "+e));
                try
                {
                    // Use the production parser too, including cross-field constraints.
                    JObject json=JObject.Parse(doc.Root.ToString());
                    if(doc.SchemaId=="system")
                    {
                        var ids=new HashSet<string>();
                        foreach(var rig in (json["configs"] as JArray??new JArray()).OfType<JObject>())
                        {
                            string id=(string)rig["vrId"];
                            if(string.IsNullOrWhiteSpace(id) || !ids.Add(id)) errors.Add(label+": unique nonempty vrId required.");
                            string order=(string)rig["displayOrder"]??"";
                            if(order.Any(c=>"DRBLFU".IndexOf(c)<0) || order.Distinct().Count()!=order.Length) errors.Add(label+": displayOrder uses each D/R/B/L/F/U at most once.");
                        }
                        int fps=(int?)json["targetFrameRate"]??120;
                        if(fps!=-1 && fps<=0) errors.Add(label+": targetFrameRate must be positive or -1.");
                        json["telemetry"]?.ToObject<TelemetryConfig>()?.Validate();
                    }
                    else if(doc.SchemaId=="sequence")
                    {
                        var sequence=json.ToObject<SequenceConfig>();
                        if(sequence.sequences==null || sequence.sequences.Length==0) errors.Add(label+": add at least one sequence step.");
                        foreach(var step in sequence.sequences??Array.Empty<SequenceItem>())
                        {
                            if(step==null) { errors.Add(label+": null sequence step."); continue; }
                            if(scenes && !Application.CanStreamedLevelBeLoaded(step.sceneName)) errors.Add(label+": scene unavailable in this build: "+step.sceneName);
                            step.autoTrimSettings?.Validate();
                        }
                    }
                    else if(doc.SchemaId!="dynamic-choice")
                    {
                        bool phased=json["stimulus"]!=null || json["preStimulus"]!=null || json["postStimulus"]!=null;
                        var payloads=phased?ExperimentPhase.Parse(json).Select(p=>p.Config):new[]{json}.AsEnumerable();
                        foreach(var payload in payloads)
                        {
                            payload["autoTrimSettings"]?.ToObject<AutoTrimConfig>()?.Validate();
                            var sky=payload["nightSky"]?.ToObject<NightSkyConfig>();
                            if(sky?.enabled==true && string.IsNullOrWhiteSpace((string)payload["skyboxPath"])) sky.Validate();
                            if(doc.SchemaId=="swarm" || doc.SchemaId=="kannadi")
                                KannadiConfig.LoadSwarm(new Dictionary<string,object> { [ExperimentPhases.PayloadKey]=payload }).Validate();
                            else if(doc.SchemaId=="choice")
                            {
                                var config=payload.ToObject<SceneConfig>(); config.headingReference?.Validate(); config.swarm?.ValidateSwarm();
                            }
                        }
                    }
                    CheckFiles(json,"$",errors,label,true);
                }
                catch(Exception e) { errors.Add(label+": "+e.GetBaseException().Message); }
                if(doc.Conflict) errors.Add(label+": external edit conflict.");
            }
            return errors;
        }
        private static void CheckFiles(JToken token,string path,List<string> errors,string label,bool enabled)
        {
            if(token is JObject obj)
            {
                enabled &= (bool?)obj["enabled"]??true;
                foreach(var p in obj.Properties())
                {
                    if(enabled && new[]{"trajectoryFile","skyboxPath"}.Contains(p.Name) && p.Value.Type==JTokenType.String && !string.IsNullOrWhiteSpace((string)p.Value) && !File.Exists(ExperimentConfigFiles.Resolve((string)p.Value))) errors.Add(label+": "+path+"."+p.Name+" file does not exist.");
                    CheckFiles(p.Value,path+"."+p.Name,errors,label,enabled);
                }
            }
            else if(token is JArray array) foreach(var value in array) CheckFiles(value,path+"[]",errors,label,enabled);
        }
        public void SaveAll()
        {
            foreach(var doc in Documents.Where(d=>d.Dirty)) doc.EnsureSaveable();
            foreach(var doc in Documents.Where(d=>d.Dirty)) doc.Save();
        }
    }
}
