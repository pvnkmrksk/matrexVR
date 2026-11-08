using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private JObject EditingPayload()
        { return ExperimentBlocks.Phased((JObject)selected.Root)?selected.Root[activePhase] as JObject??new JObject():ExperimentBlocks.Payload((JObject)selected.Root); }
        private void BuildExperimentEditor()
        {
            bool phased=ExperimentBlocks.Phased((JObject)selected.Root);
            var editToolbar=Row("experiment-actions");body.Add(editToolbar);
            if(!phased && selected.SchemaId!="dynamic-choice")
            {
                var addPhases=StudioButton("Add phases","sequence",()=>{
                    double seconds=BuilderSteps.OfType<JObject>().Where(s=>BlockReference(s)!=null&&System.IO.Path.GetFullPath(ExperimentConfigFiles.Resolve(BlockReference(s)))==selected.Path)
                        .Select(s=>ExperimentBlocks.Number(s["duration"],30)).FirstOrDefault();
                    if(seconds<=0)seconds=ExperimentBlocks.FlatDuration(selected.Root as JObject);
                    StudioEdit(selected,r=>ExperimentBlocks.AddPhaseTimeline((JObject)r,seconds));
                },"quiet");addPhases.name="add-phase-timeline";
                addPhases.tooltip="Keep the existing stimulus and add optional pre/post phases. An existing heading observation moves into pre-stimulus with its own duration. Shuffling and looping keep the timeline together. This change is undoable.";editToolbar.Add(addPhases);
            }
            if(phased)
            {
                var phases=Row("phase-tabs"); body.Add(phases);
                foreach(string name in new[]{"preStimulus","stimulus","postStimulus"})
                {
                    var phase=selected.Root[name] as JObject; string key=name;
                    bool on=phase!=null && ExperimentBlocks.Flag(phase["enabled"],name=="stimulus");
                    var button=StudioButton(Pretty(name)+"  ·  "+(on?ExperimentBlocks.Time(ExperimentBlocks.Number(phase["durationSeconds"])):"OFF"),name=="stimulus"?"play":"settings",()=>{
                        activePhase=key; BuildContent();
                    },activePhase==name?"tab-active":null);
                    phases.Add(button); BindViewState(button,()=>{var current=selected.Root[key] as JObject;bool enabled=current!=null&&ExperimentBlocks.Flag(current["enabled"],key=="stimulus");button.Q<Label>().text=Pretty(key)+" · "+(enabled?ExperimentBlocks.Time(ExperimentBlocks.Number(current["durationSeconds"])):"Off");});
                }
                if(!(selected.Root[activePhase] is JObject))
                {
                    body.Add(new Label("This optional phase is not configured. Viewing it leaves the JSON unchanged."));
                    body.Add(StudioButton("Add "+Pretty(activePhase)+" phase","plus",()=>StudioEdit(selected,r=>{ r[activePhase]=ConfigSchema.NewValue(ConfigSchema.Child(Workspace.Schemas.Get(selected.SchemaId),activePhase)); r[activePhase]["enabled"]=true; }),"primary"));
                    return;
                }
            }
            JObject payload=EditingPayload(); JObject schema=phased?ConfigSchema.Child(Workspace.Schemas.Get(selected.SchemaId),activePhase):Workspace.Schemas.Get(selected.SchemaId);
            AddChoiceCatalogAction(editToolbar);
            string[] prefix=phased?new[]{activePhase}:Array.Empty<string>();
            if(phased)
            {
                var enable=Row("phase-enable"); enable.Add(new Label(Pretty(activePhase)+" phase")); enable.Add(StateSwitch(ExperimentBlocks.Flag(payload["enabled"],activePhase=="stimulus"),v=>StudioEdit(selected,r=>{
                    r[activePhase]["enabled"]=v;
                    if(v && r[activePhase]["durationSeconds"]==null) r[activePhase]["durationSeconds"]=schema["properties"]["durationSeconds"]["default"].DeepClone();
                },false),"phase-enabled"));
                var hint=new Label("Disabled phases keep their configuration."); hint.AddToClassList("phase-hint"); enable.Add(hint); body.Add(enable);
                var continuity=Row("phase-continuity");body.Add(continuity);
                bool preserve=new[]{"stimulus","postStimulus"}.Select(k=>selected.Root[k] as JObject).Where(p=>p!=null)
                    .All(p=>!ExperimentBlocks.Flag(p["resetPositionOnStart"],true)&&!ExperimentBlocks.Flag(p["resetRotationOnStart"],true));
                continuity.Add(new Label("Keep pose between phases"));
                var keep=StateSwitch(preserve,on=>StudioEdit(selected,r=>{
                    foreach(string phase in new[]{"stimulus","postStimulus"})if(r[phase] is JObject item){item["resetPositionOnStart"]=!on;item["resetRotationOnStart"]=!on;}
                },false),"phase-keep-pose");
                keep.tooltip="ON continues the animal's position and heading into stimulus and post-stimulus. OFF resets both to each phase's configured initial pose. Individual reset switches remain available under Motion.";continuity.Add(keep);
            }
            AddSequenceOverrideNotice(body);
            string category=SettingsCategory(settingsTab);
            var tabs=Row("settings-tabs"); body.Add(tabs);
            foreach(string name in new[]{"Basics","Appearance","Motion","Advanced"})
            { string key=name;tabs.Add(StudioButton(name,name=="Basics"?"home":name=="Appearance"?"palette":name=="Motion"?"motion":"settings",()=>{settingsTab=key;BuildContent();},category==name?"tab-active":"quiet")); }
            var scroll=new ScrollView {name="settings-editor-scroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("settings-scroll");body.Add(scroll);
            AddTimingWarnings(scroll,selected);
            var present=DisplayKeys(payload,schema).Where(key=>key!="enabled"&&!new[]{"preStimulus","stimulus","postStimulus"}.Contains(key)).ToList();
            if(category=="Basics")
            {
                bool swarm = selected.SchemaId=="swarm" || selected.SchemaId=="kannadi" &&
                    string.Equals(payload["populationMode"]?.ToString(),"Swarm",StringComparison.OrdinalIgnoreCase);
                Section(scroll,"Experiment","sequence",payload,schema,prefix,selected.SchemaId=="kannadi"
                    ? new[]{"durationSeconds","populationMode"} : new[]{"durationSeconds"});
                if(swarm && payload["swarm"]?.Type!=JTokenType.Null)
                {
                    bool nested=payload["swarm"] is JObject;
                    JObject population=nested?(JObject)payload["swarm"]:payload;
                    JObject populationSchema=nested?ConfigSchema.Child(schema,"swarm"):schema;
                    string[] populationPath=nested?prefix.Concat(new[]{"swarm"}).ToArray():prefix;
                    var populationFields=new List<string>{"dimension","spawnType","spawnRelativeToAnimal","spawnAreaSize","spawnVolumeSize","spawnCenter","agentGroups"};
                    if(!(population["agentGroups"] is JArray groups) || groups.Count==0)
                        populationFields.Insert(0,AgentCountKey(population));
                    Section(scroll,"Generated swarm and spawn","swarm",population,populationSchema,populationPath,populationFields);
                    Section(scroll,"Periodic arena","navigation",population,populationSchema,populationPath,
                        new[]{"periodicBoundary","boundaryLengthX","boundaryLengthZ","followAnimalPosition","wrapY","boundaryHeight","volumeHalfExtentMetres"});
                    Section(scroll,"Camera distance","visibility",population,populationSchema,populationPath,new[]{"farClipMetres","nearClipMetres"});
                }
                else if(selected.SchemaId=="kannadi")
                {
                    Section(scroll,"Kannadi multiplayer","swarm",payload,schema,prefix,new[]{"mirrorSpawn","numberOfRings","hexRadius"});
                    var peers=new Foldout {name="multiplayer-visibility",text="Peer visibility · all other rigs by default",value=false};
                    peers.AddToClassList("editor-disclosure");scroll.Add(peers);
                    Section(peers,"Visibility","visibility",payload,schema,prefix,new[]{ExperimentVisibility.Field});
                    Section(scroll,"Periodic arena","navigation",payload,schema,prefix,new[]{"periodicBoundary","boundaryLengthX","boundaryLengthZ"});
                }
                Section(scroll,"Tracking","navigation",payload,schema,prefix,new[]{"closedLoopOrientation","closedLoopPosition"});
                var shortcuts=Row("editor-shortcuts");scroll.Add(shortcuts);
                shortcuts.Add(StudioButton("Objects & sky","palette",()=>{settingsTab="Appearance";BuildContent();},"quiet"));
                shortcuts.Add(StudioButton("Motion & resets","motion",()=>{settingsTab="Motion";BuildContent();},"quiet"));
            }
            else if(category=="Appearance")
            {
                var previews=StateSwitch(appearancePreviews,enabled=>{
                    appearancePreviews=enabled;
                    RebuildPreservingView();
                },"show-appearance-previews");
                previews.text="Show previews";
                previews.tooltip="Show visual-only model and sky previews while setting up the experiment.";
                editToolbar.Add(previews);
                BuildPopulationAppearance(scroll,payload,schema,prefix);
                if(schema["properties"]?["objects"]!=null||payload["objects"] is JArray)BuildObjectCollection(scroll,payload,schema,prefix);
                var sky=new Foldout {name="appearance-sky",text="Sky & light",value=settingsTab=="Sky"||!(payload["objects"] is JArray objects)||objects.Count==0};sky.AddToClassList("editor-disclosure");scroll.Add(sky);
                var skyRow=Row("object-visual-row");sky.Add(skyRow);
                var skyControls=new VisualElement();skyControls.AddToClassList("object-main-controls");skyControls.AddToClassList("sky-main-controls");skyRow.Add(skyControls);
                BuildSkyChoices(skyControls,payload,schema,prefix);
                var skyKeys=new[]{"uniformSkyColor","skyboxPath","panoramaRotationDegrees","nightSky","sun"};
                Section(skyControls,"Background & sun","sky",payload,schema,prefix,skyKeys);
                if(appearancePreviews) AddSkyPreview(AddAppearancePreviewSlot(skyRow),selected,prefix);
            }
            else if(category=="Motion")
            {
                Section(scroll,"Tracking","navigation",payload,schema,prefix,present.Where(key=>key.StartsWith("closedLoop")||key.StartsWith("walking")));
                Section(scroll,"Movement","motion",payload,schema,prefix,present.Where(key=>key.StartsWith("autopilot")||new[]{"aglHeight",AgentSpeedKey(payload),"animateOnMove"}.Contains(key)));
                var pose=new Foldout {name="motion-pose-details",text="Starting pose & resets",value=false};pose.AddToClassList("editor-disclosure");scroll.Add(pose);
                var poseKeys=present.Where(key=>key.StartsWith("initial")||key.StartsWith("reset")||key=="randomInitialRotation");
                if(selected.SchemaId=="kannadi") poseKeys=poseKeys.Concat(new[]{"vrConfigs"});
                Section(pose,"Pose","settings",payload,schema,prefix,poseKeys);
                var environment=new Foldout {name="motion-environment-details",text="Wind & terrain",value=false};environment.AddToClassList("editor-disclosure");scroll.Add(environment);
                Section(environment,"Environment","motion",payload,schema,prefix,present.Where(key=>key.StartsWith("wind")||key=="groundLayerMask"));
            }
            else
            {
                var visibility=new Foldout {name="advanced-visibility",text="Who sees whom",value=settingsTab=="Visibility"};visibility.AddToClassList("editor-disclosure");scroll.Add(visibility);
                Section(visibility,"Visibility","visibility",payload,schema,prefix,new[]{ExperimentVisibility.Field});
                var tuning=new Foldout {name="advanced-tuning",text="Timing & behavior",value=settingsTab=="Tuning"};tuning.AddToClassList("editor-disclosure");scroll.Add(tuning);
                Section(tuning,"Behavior","settings",payload,schema,prefix,present.Where(key=>Group(key)=="Tuning"));
                var all=new Foldout {name="advanced-all-parameters",text="All parameters",value=settingsTab=="All parameters"||settingsTab=="Stimulus"};all.AddToClassList("editor-disclosure");scroll.Add(all);
                var addMissing=new Toggle("Show optional fields"){name="show-omitted-settings",tooltip="Show omitted optional fields without writing defaults into JSON."};all.Add(addMissing);
                var addParameter=StudioButton("Add parameter","plus",()=>ShowParameterDialog(selected,prefix),"quiet");addParameter.AddToClassList("add-parameter-action");all.Add(addParameter);
                var content=new VisualElement();all.Add(content);
                Action render=()=>{content.Clear();var keys=present.ToList();if(addMissing.value)keys.AddRange(((JObject)schema["properties"]).Properties().Select(property=>property.Name).Where(key=>!keys.Contains(key)&&key!="enabled"&&!new[]{"preStimulus","stimulus","postStimulus"}.Contains(key)));
                    foreach(string group in new[]{"Motion","Stimulus","Sky","Visibility","Tuning","Other"})Section(content,group,GroupIcon(group),payload,schema,prefix,keys.Where(key=>Group(key)==group),false);};
                addMissing.RegisterValueChangedCallback(_=>render());render();
            }
            if(phased) BindViewState(scroll,()=>scroll.SetEnabled(ExperimentBlocks.Flag(selected.Root[activePhase]?["enabled"],activePhase=="stimulus")));
        }
        private static string SettingsCategory(string tab)=>tab=="Essentials"||tab=="Basics"?"Basics":tab=="Sky"||tab=="Appearance"?"Appearance":tab=="Motion"?"Motion":"Advanced";
        private bool EditingSky=>SettingsCategory(settingsTab)=="Appearance";
        private static bool HasValue(JObject payload,string key) => payload[key]!=null && payload[key].Type!=JTokenType.Null;
        private static string AgentCountKey(JObject payload) => HasValue(payload,"agentCount")?"agentCount":HasValue(payload,"numberOfLocusts")?"numberOfLocusts":"agentCount";
        private static string AgentSpeedKey(JObject payload) => HasValue(payload,"agentSpeed")?"agentSpeed":HasValue(payload,"locustSpeed")?"locustSpeed":HasValue(payload,"locoSpeed")?"locoSpeed":"agentSpeed";
        private static string GroupIcon(string group) => group=="Motion"?"motion":group=="Stimulus"?"swarm":group=="Sky"?"sky":group=="Visibility"?"visibility":"settings";
        private static IEnumerable<string> DisplayKeys(JObject owner,JObject schema)
        {
            return owner.Properties().Select(p=>p.Name).Concat((schema["properties"] as JObject??new JObject()).Properties()
                .Where(p=>p.Value["default"]!=null && p.Value["default"].Type!=JTokenType.Null).Select(p=>p.Name)).Where(k=>k!="title"&&k!="comments"&&k!="description").Distinct();
        }
        private static string Group(string key)
        {
            if(key==ExperimentVisibility.Field)return "Visibility";
            if(key.StartsWith("initial") || key.StartsWith("reset") || key.StartsWith("closedLoop") || key.StartsWith("walking") || key.StartsWith("autopilot") || key.StartsWith("wind") || new[]{"randomInitialRotation","aglHeight","groundLayerMask","durationSeconds"}.Contains(key)) return "Motion";
            if(new[]{"backgroundColor","uniformSkyColor","nightSky","skyboxPath","skybox","panoramaRotationDegrees","sun","modelAppearance"}.Contains(key)) return "Sky";
            if(key.StartsWith("autoTrim") || key=="headingReference" || key=="adaptiveDecision" || key=="useHeadingReference") return "Tuning";
            if(new[]{"preStimulus","stimulus","postStimulus"}.Contains(key)) return "Other";
            if(new[]{"seed","repetitions","sync","loop"}.Contains(key)) return "Tuning";
            if(key=="vrConfigs" || key=="configs") return "Other";
            return "Stimulus";
        }
        private void Section(VisualElement target,string title,string icon,JObject owner,JObject schema,string[] prefix,IEnumerable<string> keys,bool hideInapplicable=true)
        {
            var names=keys.Where(k=>owner[k]!=null || schema["properties"]?[k]!=null).Distinct().ToList();
            if(title=="Motion" || title=="Forward motion & altitude" || title=="Starting pose & resets")
            {
                var priority=new[]{"durationSeconds","closedLoopOrientation","closedLoopPosition","autopilotEnabled","autopilotSpeed","aglHeight","windSpeed","windDirection","groundLayerMask","resetPositionOnStart","resetRotationOnStart","randomInitialRotation","initialPosition","initialRotation"};
                names=names.OrderBy(k=>{ int i=Array.IndexOf(priority,k); return i<0?99:i; }).ToList();
            }
            if(names.Count==0) return;
            var section=new VisualElement(); section.AddToClassList("settings-section"); target.Add(section); var head=Row("section-heading"); section.Add(head); head.Add(new WorkbenchIcon(icon)); head.Add(new Label(title));
            var grid=new VisualElement(); grid.AddToClassList("settings-grid"); section.Add(grid);
            foreach(string key in names) grid.Add(Setting(selected,prefix.Concat(new[]{key}).ToArray(),ConfigSchema.Child(schema,key),owner,schema));
        }
        private VisualElement Setting(ConfigDocument doc,string[] parts,JObject schema,JObject owner,JObject ownerSchema)
        {
            string key=parts.Last(); JToken value=ConfigDocument.At(doc.Root,parts);
            var setting=new VisualElement(); setting.AddToClassList("studio-setting"); setting.name="setting-"+string.Join("-",parts);
            var label=Row("setting-label"); setting.Add(label); var caption=new Label(key=="mirrorSpawn"?"Tracked start layout":Pretty(key)); caption.AddToClassList("setting-title"); label.Add(caption);
            string unit=(string)schema["x-ui"]?["unit"]; if(!string.IsNullOrEmpty(unit)) label.Add(Pill(unit,"unit-pill"));
            if(schema["default"]!=null && schema["default"].Type!=JTokenType.Null)
            { var inherited=Pill("Inherited","muted-pill"); inherited.tooltip="Not explicitly set in this file. The displayed value comes from the runtime/schema default."; inherited.AddToClassList("inherited-indicator"); label.Add(inherited); BindViewState(inherited,()=>inherited.style.visibility=ConfigDocument.At(doc.Root,parts)==null?Visibility.Visible:Visibility.Hidden); }
            setting.tooltip=ConfigParameterHelp.Description(key,parts,schema); caption.tooltip=setting.tooltip;
            Action<JToken,bool> assign=(v,rebuild)=>StudioEdit(doc,root=>AssignAt(root,parts,v),rebuild);
            JToken shown=ConfigSchema.EffectiveValue(value,schema);
            if(EditingSky && (shown==null || shown.Type==JTokenType.Null))
            {
                if(key=="uniformSkyColor")shown=new JObject{["r"]=.12,["g"]=.12,["b"]=.12,["a"]=1};
                else if(key=="nightSky")shown=ConfigSchema.NewValue(schema);
                else if(key=="skyboxPath")shown=new JValue("");
            }
            if(value!=null && ConfigSchema.Validate(value,schema).Count>0)
            {
                var fallback=Pill("Check JSON","muted-pill");fallback.tooltip="This file contains an invalid value. The control displays its default; edit the value to repair it. Starting stays disabled until validation passes.";label.Add(fallback);BindViewState(fallback,()=>fallback.style.visibility=ConfigSchema.Validate(ConfigDocument.At(doc.Root,parts),schema).Count>0?Visibility.Visible:Visibility.Hidden);
            }
            if(parts.Length==1&&key==StimulusResolutionConfig.Field)BuildStimulusResolutionControls(setting,doc,parts,schema);
            else if(key=="mirrorSpawn" && schema["properties"] is JObject)
            {
                if(shown is JObject preset)
                {
                    setting.AddToClassList("container-setting");
                    foreach(string field in new[]{"layout","width","depth"})
                        setting.Add(Setting(doc,parts.Concat(new[]{field}).ToArray(),ConfigSchema.Child(schema,field),preset,schema));
                    setting.Add(Button("Use authored poses",()=>assign(JValue.CreateNull(),true),"quiet"));
                }
                else
                {
                    var controls=Row();controls.Add(Pill("Shared / per-rig poses","muted-pill"));
                    controls.Add(Button("Choose a spawn layout",()=>assign(ConfigSchema.NewValue(schema),true),"quiet"));
                    setting.Add(controls);
                }
            }
            else if(key=="agentGroups"&&schema["items"] is JObject) BuildSwarmGroups(setting,doc,parts,schema);
            else if(key=="modelAppearance"&&schema["properties"]!=null){setting.AddToClassList("surface-setting");BuildSurfaceControls(setting,schema,parts);}
            else if(TryVisibilityControl(doc,parts,out var visibilityControl)){setting.AddToClassList("visibility-setting");setting.Add(visibilityControl);}
            else if(TryChoiceCatalogControl(parts,value,v=>assign(v,false),out var catalogControl))setting.Add(catalogControl);
            else if(BooleanControl(shown,schema))
            {
                setting.AddToClassList("boolean-setting");
                var check=StateSwitch(ExperimentBlocks.Flag(value,ExperimentBlocks.Flag(shown)),v=>assign(BooleanValue(v,ConfigDocument.At(doc.Root,parts),schema),false),"flag-"+string.Join("-",parts)); setting.Add(check);
                BindViewState(check,()=>check.SetValueWithoutNotify(ExperimentBlocks.Flag(ConfigDocument.At(doc.Root,parts),ExperimentBlocks.Flag(shown))));
            }
            else if(shown is JObject obj && new[]{"x","y","z"}.All(k=>obj[k]!=null) && obj.Properties().All(p=>new[]{"x","y","z"}.Contains(p.Name)))
            {
                setting.AddToClassList("vector-setting");
                var vector=Row("vector-controls"); setting.Add(vector);
                foreach(string axis in new[]{"x","y","z"})
                {
                    var component=Row("vector-axis"); vector.Add(component);
                    var axisSchema=ConfigSchema.Child(schema,axis);
                    var field=NumberControl(obj[axis],axisSchema,axis,v=>StudioEdit(doc,r=>{
                        if(!(ConfigDocument.At(r,parts) is JObject)) AssignAt(r,parts,obj.DeepClone());
                        ConfigDocument.At(r,parts)[axis]=v;
                    },false),false);
                    field.tooltip=ConfigParameterHelp.Description(axis,parts.Concat(new[]{axis}).ToArray(),axisSchema);
                    component.Add(new Label(axis.ToUpperInvariant())); component.Add(field);
                }
            }
            else if(shown is JObject color && new[]{"r","g","b"}.All(k=>color[k]!=null))
            {
                setting.AddToClassList("color-setting");
                setting.Add(ColorControl(doc,parts,color));
            }
            else if(shown is JObject nested)
            {
                string id=doc.Path+"|"+string.Join("/",parts);
                var fold=new Foldout { text="Parameters",value=expandedSections.Contains(id),tooltip=setting.tooltip }; setting.Add(fold);
                var disclosure=StudioButton("Parameters","next",()=>fold.value=!fold.value,"disclosure-button");
                var foldIcon=disclosure.Q<WorkbenchIcon>();foldIcon.style.rotate=new Rotate(new Angle(fold.value?90:0,AngleUnit.Degree));
                disclosure.tooltip="Show or hide the parameters for "+Pretty(key)+". This does not enable the feature or change JSON.";
                string flag=schema["properties"]?["enabled"]!=null || nested["enabled"]?.Type==JTokenType.Boolean?"enabled":schema["properties"]?["visible"]!=null || nested["visible"]?.Type==JTokenType.Boolean?"visible":null;
                if(flag!=null)
                {
                    bool on=ExperimentBlocks.Flag(nested[flag],ExperimentBlocks.Flag(schema["properties"]?[flag]?["default"]));
                    var toggle=StateSwitch(on,v=>StudioEdit(doc,r=>AssignAt(r,parts.Concat(new[]{flag}).ToArray(),new JValue(v)),false),"flag-"+string.Join("-",parts)+"-"+flag);
                    toggle.tooltip=ConfigParameterHelp.Description(flag,parts.Concat(new[]{flag}).ToArray(),ConfigSchema.Child(schema,flag)); label.Add(toggle);
                    BindViewState(toggle,()=>toggle.SetValueWithoutNotify(ExperimentBlocks.Flag(ConfigDocument.At(doc.Root,parts)?[flag],ExperimentBlocks.Flag(schema["properties"]?[flag]?["default"]))));
                }
                label.Add(disclosure);
                Action populate=()=>{
                    fold.Clear();
                    if(key=="nightSky")
                    {
                        var currentTime=Row("night-sky-current-time");currentTime.Add(new Label("Use current time"));
                        bool now=string.IsNullOrWhiteSpace((string)nested["localTime"])||string.Equals((string)nested["localTime"],"now",StringComparison.OrdinalIgnoreCase);
                        var timeSwitch=StateSwitch(now,on=>StudioEdit(doc,r=>AssignAt(r,parts.Concat(new[]{"localTime"}).ToArray(),new JValue(on?"now":"22:00:00")),false),"night-sky-use-current-time");
                        timeSwitch.tooltip="ON samples the current UTC instant at experiment start. OFF uses the configured local date, time and UTC offset.";currentTime.Add(timeSwitch);fold.Add(currentTime);
                        BindViewState(timeSwitch,()=>{string time=ConfigDocument.At(doc.Root,parts)?["localTime"]?.ToString();timeSwitch.SetValueWithoutNotify(string.IsNullOrWhiteSpace(time)||string.Equals(time,"now",StringComparison.OrdinalIgnoreCase));});
                    }
                    IEnumerable<string> optional = key=="swarm" && schema["properties"] is JObject fields
                        ? fields.Properties().Select(p=>p.Name)
                        : key=="nightSky"?new[]{"utcOffsetHours"}:Array.Empty<string>();
                    foreach(string property in DisplayKeys(nested,schema).Concat(optional).Distinct().Where(p=>p!=flag))
                        fold.Add(Setting(doc,parts.Concat(new[]{property}).ToArray(),ConfigSchema.Child(schema,property),nested,schema));
                };
                BindViewState(fold,()=>fold.contentContainer.SetEnabled(flag==null || ExperimentBlocks.Flag(ConfigDocument.At(doc.Root,parts)?[flag],ExperimentBlocks.Flag(schema["properties"]?[flag]?["default"],true))));
                if(fold.value) populate();
                fold.RegisterValueChangedCallback(e=>{ if(e.target!=fold) return; setting.EnableInClassList("parameters-open",e.newValue);foldIcon.style.rotate=new Rotate(new Angle(e.newValue?90:0,AngleUnit.Degree)); if(e.newValue) { expandedSections.Add(id); populate(); } else { expandedSections.Remove(id); fold.Clear(); } });
                setting.AddToClassList("container-setting");
                setting.EnableInClassList("parameters-open",fold.value);
            }
            else if(shown is JArray array)
            {
                var controls=Row(); controls.Add(Pill(array.Count+" items","accent")); controls.Add(StudioButton("Edit items","settings",()=>{ selected=doc; advancedMode=true; path.Clear(); path.AddRange(parts); BuildContent(); },"quiet"));
                controls.Add(IconButton("plus","Add item",()=>StudioEdit(doc,r=>{if(!(ConfigDocument.At(r,parts) is JArray))AssignAt(r,parts,array.DeepClone());((JArray)ConfigDocument.At(r,parts)).Add(ConfigSchema.NewValue(ConfigSchema.Child(schema,"",true)));}))); setting.Add(controls);
            }
            else if(key=="utcOffsetHours")
            {
                double hours=ExperimentBlocks.Number(shown);var offsets=Enumerable.Range(-56,113).Select(i=>i/4d).ToList();
                if(!offsets.Any(v=>Math.Abs(v-hours)<.000001))offsets.Add(hours);offsets.Sort();
                Func<double,string> labelOffset=offset=>"UTC"+(offset>=0?"+":"−")+TimeSpan.FromHours(Math.Abs(offset)).ToString(@"hh\:mm",CultureInfo.InvariantCulture);
                var choices=offsets.Select(labelOffset).ToList();choices.Insert(0,"Use named time zone");var offsetPicker=new DropdownField(choices,shown==null||shown.Type==JTokenType.Null?0:Math.Max(0,offsets.FindIndex(v=>Math.Abs(v-hours)<.000001))+1){name="night-sky-utc-offset"};
                offsetPicker.RegisterValueChangedCallback(e=>Guard(()=>assign(choices.IndexOf(e.newValue)==0?JValue.CreateNull():new JValue(offsets[choices.IndexOf(e.newValue)-1]),false)));setting.Add(offsetPicker);
            }
            else if(shown==null || shown.Type==JTokenType.Null)
            {
                var controls=Row(); controls.Add(Pill(value==null?"Inherited / scene default":"Unset · null","muted-pill"));
                controls.Add(Button("Set value",()=>assign(NewSettingValue(key,owner,schema),true),"quiet")); setting.Add(controls);
            }
            else if(key=="closedLoopMode")
            {
                setting.AddToClassList("choice-setting");setting.AddToClassList("backend-setting");
                var modes=new List<ClosedLoopMode>{ClosedLoopMode.Hejjetrac,ClosedLoopMode.Rekkefly,ClosedLoopMode.Tirbala};
                var current=TrackerBackend.Parse(shown.ToString());
                if(!modes.Contains(current)) modes.Add(current);
                Func<ClosedLoopMode,string> title=mode=>TrackerBackend.DisplayName(mode)+(mode==ClosedLoopMode.FicTrac?" · legacy FicTrac":mode==ClosedLoopMode.Kinefly?" · legacy Kinefly":TrackerBackend.IsWalking(mode)?" · walking":TrackerBackend.IsFlight(mode)?" · flight":" · force/torque");
                var choices=modes.Select(title).ToList();var dropdown=new DropdownField(choices,modes.IndexOf(current)) {name="tracker-backend-"+string.Join("-",parts)};setting.Add(dropdown);
                dropdown.RegisterValueChangedCallback(e=>Guard(()=>assign(new JValue(modes[choices.IndexOf(e.newValue)].ToString()),false)));
            }
            else if(key=="agentVisual")
            {
                var choices=new List<string>{"Built-in / GLB model","Bogong visual"};var current=shown?.ToString()??"ScenePrefab";if(current!="ScenePrefab"&&current!="Bogong")choices.Add(current);
                var picker=new DropdownField(choices,current=="Bogong"?1:current=="ScenePrefab"?0:2);picker.RegisterValueChangedCallback(evt=>assign(new JValue(evt.newValue==choices[0]?"ScenePrefab":evt.newValue==choices[1]?"Bogong":evt.newValue),false));setting.AddToClassList("choice-setting");setting.Add(picker);
            }
            else if(key=="populationMode")
            {
                var choices=new List<string>{"Kannadi multiplayer","Generated swarm"};
                var picker=new DropdownField(choices,string.Equals(shown.ToString(),"Swarm",StringComparison.OrdinalIgnoreCase)?1:0);
                picker.RegisterValueChangedCallback(evt=>assign(new JValue(evt.newValue==choices[1]?"Swarm":"Mirror"),true));
                setting.AddToClassList("choice-setting");setting.Add(picker);
            }
            else if(schema["enum"] is JArray options)
            {
                setting.AddToClassList("choice-setting");
                var dropdown=EnumControl(options,shown,v=>assign(v,key=="populationMode")); setting.Add(dropdown);
            }
            else if(LosslessJson.Kind(shown)=="number" || LosslessJson.Kind(shown)=="integer")
            {
                setting.AddToClassList("numeric-setting");
                setting.Add(NumberControl(shown,schema,key,v=>assign(v,false)));
            }
            else
            {
                setting.AddToClassList("text-setting");
                if(IsPathSetting(key)) setting.AddToClassList("path-setting");
                var input=new TextField { value=shown.ToString(),isDelayed=true,multiline=key=="comments" }; setting.Add(input); BindDelayedText(input,text=>assign(new JValue(text),false));
                BindViewState(input,()=>RefreshBoundText(input,ConfigSchema.EffectiveValue(ConfigDocument.At(doc.Root,parts),schema)?.ToString()));
            }
            Action enable=()=>{
                var currentOwner=ConfigSchema.EffectiveValue(ConfigDocument.At(doc.Root,parts.Take(parts.Length-1).ToArray()),ownerSchema,true);
                bool active=ConfigSchema.Enabled(currentOwner,ownerSchema,key,Context)&&ConfigSchema.Visible(currentOwner,ownerSchema,key,Context);
                if((key=="agentCount"||key=="numberOfLocusts"||key=="density") &&
                    currentOwner?["agentGroups"] is JArray groups && groups.Count>0)active=false;
                if(key=="localTime"&&parts.Contains("nightSky"))
                {var local=ConfigDocument.At(doc.Root,parts.Take(parts.Length-1).ToArray())?["localTime"]?.ToString();active&=!string.IsNullOrWhiteSpace(local)&&!string.Equals(local,"now",StringComparison.OrdinalIgnoreCase);}
                if(EditingSky)
                {
                    if(key=="uniformSkyColor")active&=selectedSkyMode==1;
                    else if(key=="skyboxPath" || key=="panoramaRotationDegrees")active&=selectedSkyMode==2;
                    else if(key=="nightSky")active&=selectedSkyMode==3;
                }
                if((key=="modelAppearance"||key=="modelPath")&&string.Equals((string)currentOwner?["agentVisual"],"Bogong",StringComparison.OrdinalIgnoreCase))active=false;
                if(key=="kannadiTilePrefab"&&!string.IsNullOrWhiteSpace((string)currentOwner?["kannadiModelPath"]))active=false;
                if(ChoiceAssetCatalog.IsObjectField(parts)&&!string.IsNullOrWhiteSpace((string)currentOwner?["modelPath"]))active=false;
                setting.SetEnabled(active);
            };
            if(!buildingObjectCard)AddObjectPreview(setting,doc,parts);
            BindViewState(setting,enable); return setting;
        }

        private void BuildSwarmGroups(VisualElement setting,ConfigDocument document,string[] groupPath,JObject schema)
        {
            JArray Groups(JToken root)=>ConfigDocument.At(root,groupPath) as JArray;
            var groups=Groups(document.Root);
            var itemSchema=ConfigSchema.Child(schema,"",true);
            var countSchema=ConfigSchema.Child(itemSchema,"count");
            var appearanceSchema=ConfigSchema.Child(itemSchema,"appearance");
            var offsetSchema=ConfigSchema.Child(itemSchema,"spawnOffset");
            var toolbar=Row("swarm-group-toolbar"); setting.Add(toolbar);
            var add=StudioButton("Add group","plus",()=>StudioEdit(document,root=>{
                if(Groups(root)==null) AssignAt(root,groupPath,new JArray());
                Groups(root).Add(ConfigSchema.NewValue(itemSchema));
            }),"quiet");
            add.name="add-swarm-group";
            toolbar.Add(add);
            if(groups==null || groups.Count==0)return;

            setting.AddToClassList("swarm-group-setting");
            var summary=new Label {name="swarm-group-total"};
            summary.AddToClassList("quiet-hint");
            toolbar.Add(summary);
            BindViewState(summary,()=>{
                var current=Groups(document.Root);
                if(current==null){summary.text="";return;}
                long total=0;
                foreach(var item in current)
                {
                    JToken count=(item as JObject)?["count"]??countSchema["default"];
                    if(!(item is JObject)||!int.TryParse(count?.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)||n<=0)
                    { summary.text=current.Count+" groups · check counts";return; }
                    total+=n;
                }
                summary.text=current.Count+(current.Count==1?" group":" groups")+" · "+total+" agents per rig";
            });
            var advanced=StudioButton("Advanced list","settings",()=>{
                RequireApplied(); selected=document; advancedMode=true; path.Clear(); path.AddRange(groupPath); BuildContent();
            },"quiet");
            toolbar.Add(advanced);

            var cards=Row("swarm-group-grid"); setting.Add(cards);
            for(int index=0;index<groups.Count;index++)
            {
                int slot=index;
                string[] itemPath=groupPath.Concat(new[]{slot.ToString(CultureInfo.InvariantCulture)}).ToArray();
                var item=groups[slot] as JObject;
                var card=new VisualElement {name="swarm-group-"+slot};
                card.AddToClassList("swarm-group-card"); cards.Add(card);
                var header=Row("swarm-group-header"); card.Add(header);
                header.Add(new Label("Group "+(slot+1)));
                header.Add(IconButton("copy","Duplicate group "+(slot+1),()=>StudioEdit(document,root=>Groups(root).Insert(slot+1,Groups(root)[slot].DeepClone()))));
                header.Add(IconButton("trash","Remove group "+(slot+1),()=>StudioEdit(document,root=>Groups(root).RemoveAt(slot))));
                void SetValue(string[] keys,JToken value)=>StudioEdit(document,root=>{
                    if(!(Groups(root)[slot] is JObject)) Groups(root)[slot]=new JObject();
                    AssignAt(root,itemPath.Concat(keys).ToArray(),value);
                },false);

                var values=Row("swarm-group-values"); card.Add(values);
                JToken shownCount=ConfigSchema.EffectiveValue(item?["count"],countSchema)??new JValue(128);
                AddSwarmGroupField(values,"Count",NumberControl(shownCount,countSchema,"count",v=>SetValue(new[]{"count"},v),false));
                JToken shownAppearance=ConfigSchema.EffectiveValue(item?["appearance"],appearanceSchema)??new JValue("Stock");
                if(shownAppearance.Type==JTokenType.Raw&&int.TryParse(shownAppearance.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out int choice))
                    shownAppearance=new JValue(choice);
                if(appearanceSchema["enum"] is JArray options)
                {
                    var appearance=EnumControl(options,shownAppearance,v=>SetValue(new[]{"appearance"},v));
                    appearance.name="swarm-group-appearance-"+slot;
                    AddSwarmGroupField(values,"Appearance",appearance);
                }

                var offsetLabel=new Label("Spawn offset · scene units");
                offsetLabel.tooltip=(string)offsetSchema["x-ui"]?["help"];
                offsetLabel.AddToClassList("swarm-group-label");card.Add(offsetLabel);
                var offsets=Row("swarm-group-values");card.Add(offsets);
                foreach(string axis in new[]{"x","y","z"})
                {
                    var axisSchema=ConfigSchema.Child(offsetSchema,axis);
                    JToken axisValue=ConfigSchema.EffectiveValue((item?["spawnOffset"] as JObject)?[axis],axisSchema)??new JValue(0);
                    var field=NumberControl(axisValue,axisSchema,axis,v=>SetValue(new[]{"spawnOffset",axis},v),false,84);
                    AddSwarmGroupField(offsets,axis.ToUpperInvariant(),field);
                }
            }
        }

        private static void AddSwarmGroupField(VisualElement target,string label,VisualElement control)
        {
            var field=new VisualElement();field.AddToClassList("swarm-group-field");
            var caption=new Label(label);caption.AddToClassList("swarm-group-label");field.Add(caption);
            field.Add(control);target.Add(field);
        }

        private static bool IsPathSetting(string key) => key.EndsWith("File",StringComparison.Ordinal) ||
            key.EndsWith("Path",StringComparison.Ordinal) || key=="design";

        private static JToken NewSettingValue(string key,JObject owner,JObject schema)
        {
            if(key=="nearClipMetres") return new JValue(.001);
            if(key=="farClipMetres") return new JValue(10);
            if(key=="boundaryLengthX" || key=="boundaryLengthZ") return new JValue(200);
            if(key!="spawnAreaSize" && key!="spawnVolumeSize" && key!="boundaryHeight") return ConfigSchema.NewValue(schema);
            double x=ExperimentBlocks.Number(owner["boundaryLengthX"],200);
            double z=ExperimentBlocks.Number(owner["boundaryLengthZ"],200);
            double y=ExperimentBlocks.Number(owner["boundaryHeight"],Math.Min(x,z));
            double side=Math.Min(1,Math.Min(x,Math.Min(y,z)));
            if(key=="boundaryHeight") return new JValue(ExperimentBlocks.Number((owner["spawnVolumeSize"] as JObject)?["y"],
                ExperimentBlocks.Number(owner["spawnAreaSize"],Math.Min(200,Math.Min(x,z)))));
            if(key=="spawnAreaSize") return new JValue(side);
            if(key=="spawnVolumeSize") return new JObject { ["x"]=side,["y"]=side,["z"]=side };
            return ConfigSchema.NewValue(schema);
        }
        private int selectedSkyMode;
        private void BuildSkyChoices(VisualElement target,JObject payload,JObject schema,string[] prefix)
        {
            var choices=new List<string>{"Default sky","Solid color","Custom panorama","Starry night sky"};
            int mode=payload["uniformSkyColor"] is JObject?1:!string.IsNullOrWhiteSpace((string)payload["skyboxPath"])?2:ExperimentBlocks.Flag(payload["nightSky"]?["enabled"])?3:0;
            selectedSkyMode=mode;
            var chooser=new DropdownField("Sky background",choices,mode){name="sky-background-mode",tooltip="Choose the sky shown by this phase. Default uses the scene's sky. A panorama uses a local image; starry night uses location and time."};target.Add(chooser);
            Action<int,string> apply=(next,path)=>{
                selectedSkyMode=next;
                StudioEdit(selected,r=>{
                var owner=prefix.Length==0?(JObject)r:(JObject)ConfigDocument.At(r,prefix);
                owner["uniformSkyColor"]=next==1?(owner["uniformSkyColor"] is JObject old?old.DeepClone():new JObject{["r"]=.12,["g"]=.12,["b"]=.12,["a"]=1}):JValue.CreateNull();
                owner["skyboxPath"]=next==2?path:"";
                var sky=owner["nightSky"] as JObject;
                if(next==3&&sky==null){sky=ConfigSchema.NewValue(ConfigSchema.Child(schema,"nightSky")) as JObject??new JObject();owner["nightSky"]=sky;}
                if(sky!=null)sky["enabled"]=next==3;
                },false);
            };
            chooser.RegisterValueChangedCallback(e=>Guard(()=>{
                RequireApplied();int next=choices.IndexOf(e.newValue);
                if(next!=2){apply(next,null);return;}
                var dialog=OpenModal("Choose panorama","Select a 2:1 PNG or JPEG image. MATREX archives the image with the run.");
                dialog.AddToClassList("small-dialog");
                var input=new TextField("Image path"){name="panorama-path",value=(string)payload["skyboxPath"]??"",isDelayed=true};
                dialog.Add(input);
                var formats=new List<string>{"png","jpg","jpeg"};
                int formatIndex=formats.IndexOf(System.IO.Path.GetExtension(input.value).TrimStart('.').ToLowerInvariant());
                var format=new DropdownField(formats,Math.Max(0,formatIndex)){tooltip="Filter the native image picker by file format."};
                var browse=Row();browse.Add(format);
                browse.Add(StudioButton("Browse…","file",()=>BrowseFile("Open sky panorama",format.value,chosen=>input.value=chosen),"quiet"));
                dialog.Add(browse);
                dialog.Add(StudioButton("Use panorama","sky",()=>{
                    RequireApplied();string reference=input.value.Trim();if(string.IsNullOrEmpty(reference)||!System.IO.File.Exists(ExperimentConfigFiles.Resolve(reference)))throw new ArgumentException("Choose an existing panorama image.");
                    apply(2,reference);CloseModal();
                },"primary"));
            }));
        }
        private VisualElement NumberControl(JToken value,JObject schema,string key,Action<JToken> commit,bool allowSlider=true,float inputWidth=112)
        {
            var control=new VisualElement(); control.AddToClassList("number-control");
            var spin=Row("spin-box"); control.Add(spin);
            var input=new TextField { value=value.ToString(),isDelayed=true,name="number-"+key }; input.AddToClassList("exact-number"); spin.Add(input);
            string lastValidText=value.ToString();
            input.style.width=inputWidth;
            Slider slider=null; IVisualElementScheduledItem pending=null;
            JToken draft=null;
            Action flush=()=>{ if(draft==null) return; var number=draft; draft=null;numberDrafts.Remove(control); Guard(()=>{commit(number);lastValidText=number.ToString();MarkEditorCommitted(input,lastValidText);});RefreshSaveAvailability(); };
            var range=schema["x-ui"]?["slider"] as JObject;
            bool integer=ConfigSchema.Types(schema).Contains("integer") ||
                (schema["type"]==null && LosslessJson.Kind(value)=="integer");
            string step=integer?"1":(range?["step"]?.ToString()??"0.1");
            Action<int,int> increment=(direction,power)=>Guard(()=>{
                pending?.Pause();draft=null;numberDrafts.Remove(control);textDrafts.Remove(input);
                FlushEditorDrafts();var current=LosslessJson.Parse(input.value);
                JToken next=JsonNumberStepper.Step(current,step,direction,power);commit(next);input.SetValueWithoutNotify(next.ToString());
                lastValidText=next.ToString();MarkEditorCommitted(input,lastValidText);
                if(slider!=null) slider.SetValueWithoutNotify((float)ExperimentBlocks.Number(next));
            });
            var arrows=new VisualElement();arrows.AddToClassList("spin-arrows");spin.Add(arrows);
            var up=IconButton("up","Increase "+Pretty(key),()=>increment(1,0));up.name="spin-up-"+key;var down=IconButton("down","Decrease "+Pretty(key),()=>increment(-1,0));down.name="spin-down-"+key;arrows.Add(up);arrows.Add(down);
            input.RegisterCallback<KeyDownEvent>(evt=>{
                if(evt.keyCode!=KeyCode.UpArrow && evt.keyCode!=KeyCode.DownArrow) return;
                increment(evt.keyCode==KeyCode.UpArrow?1:-1,evt.shiftKey?1:evt.altKey&&!integer?-1:0);evt.StopImmediatePropagation();evt.PreventDefault();
            },TrickleDown.TrickleDown);
            spin.RegisterCallback<WheelEvent>(evt=>{
                if(input.panel?.focusController.focusedElement!=input && !input.Contains(input.panel?.focusController.focusedElement as VisualElement)) return;
                increment(evt.delta.y<0?1:-1,evt.shiftKey?1:0);evt.StopPropagation();evt.PreventDefault();
            });
            control.tooltip="Exact number · ↑/↓ or arrows to step · Shift ×10 · Alt ÷10 · wheel when focused";
            bool usefulSlider=new[]{"gain","autopilotSpeed","locustSpeed","locustSpeedStdDev","windSpeed","kappa","kappaElevation","angularSizeDegrees","contrast","wingOpacity","exposure","faintDetailCutoff","aggressiveness","flickerDutyCycle"}.Contains(key);
            if(allowSlider && usefulSlider && !integer && range!=null && double.TryParse(value.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out double n) && !double.IsInfinity(n))
            {
                control.AddToClassList("with-slider");
                double min=(double)range["min"],max=(double)range["max"],sliderStep=(double?)range["step"]??.01;
                slider=new Slider((float)min,(float)max) { name="slider-"+key }; slider.SetValueWithoutNotify((float)Math.Max(min,Math.Min(max,n))); control.Add(slider);
                if(n<min || n>max) { var outside=new Label("Outside slider range · exact value retained"); outside.AddToClassList("key-hint"); control.Add(outside); }
                slider.RegisterValueChangedCallback(e=>{
                    double chosen=Math.Round(Math.Round(e.newValue/sliderStep)*sliderStep,6); string text=chosen.ToString("0.######",CultureInfo.InvariantCulture); input.SetValueWithoutNotify(text); draft=new JRaw(text);numberDrafts.Add(control);RefreshSaveAvailability(); pending?.Pause(); pending=control.schedule.Execute(flush).StartingIn(180);
                });
                slider.RegisterCallback<PointerUpEvent>(_=>flush()); slider.RegisterCallback<FocusOutEvent>(_=>flush());
            }
            BindDelayedText(input,text=>{
                pending?.Pause(); draft=null;numberDrafts.Remove(control);textDrafts.Remove(input);
                try { JToken parsed=LosslessJson.Parse(text); string kind=LosslessJson.Kind(parsed); if(kind!="integer" && kind!="number") throw new ArgumentException("Enter a JSON number."); commit(parsed); lastValidText=parsed.ToString();if(slider!=null) slider.SetValueWithoutNotify((float)ExperimentBlocks.Number(parsed)); }
                catch { input.SetValueWithoutNotify(lastValidText); throw; }
            });
            // A quick Start, navigation, Undo or Save must flush the last visual slider position first.
            control.RegisterCallback<DetachFromPanelEvent>(_=>{numberFlushes.Remove(flush);numberDrafts.Remove(control);}); numberFlushes.Add(flush);
            return control;
        }
        private readonly List<Action> numberFlushes=new List<Action>();
        private readonly List<Action> enableUpdates=new List<Action>();
        private void FlushNumbers() { foreach(var flush in numberFlushes.ToArray()) flush(); }
        private void RefreshStudioValues()
        {
            var timing=body.Q<VisualElement>("timing-warnings");if(timing!=null) PopulateTimingWarnings(timing,selected);
            var duration=inspector.Q<Label>(className:"overview-duration");
            var total=body.Q<Label>("sequence-total-duration");
            if(duration!=null||total!=null)
            {
                string value=SequenceDuration();if(duration!=null)duration.text=value;if(total!=null)total.text=value+" total";
            }
            var effects=inspector.Q<Label>("config-effects"); if(effects!=null) effects.text=ExperimentBlocks.Describe(EditingPayload());
            foreach(var update in enableUpdates.ToArray()) update();
            var sync=inspector.Q<Label>("system-sync"); if(sync!=null) sync.text=SystemSyncSummary();
            var display=inspector.Q<Label>("system-display"); if(display!=null) display.text=SystemDisplaySummary();
            ScheduleBlockItemsRefresh();
        }
        private void BuildSystemEditor() { BuildApparatusEditor(); }
    }
}
