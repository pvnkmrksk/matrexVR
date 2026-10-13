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
                    phases.Add(button);
                }
                if(!(selected.Root[activePhase] is JObject))
                {
                    body.Add(new Label("This optional phase is not configured. Viewing it leaves the JSON unchanged."));
                    body.Add(StudioButton("Add "+Pretty(activePhase)+" phase","plus",()=>StudioEdit(selected,r=>{ r[activePhase]=ConfigSchema.NewValue(ConfigSchema.Child(Workspace.Schemas.Get(selected.SchemaId),activePhase)); r[activePhase]["enabled"]=true; }),"primary"));
                    return;
                }
            }
            JObject payload=EditingPayload(); JObject schema=phased?ConfigSchema.Child(Workspace.Schemas.Get(selected.SchemaId),activePhase):Workspace.Schemas.Get(selected.SchemaId);
            string[] prefix=phased?new[]{activePhase}:Array.Empty<string>();
            if(phased)
            {
                var enable=Row("phase-enable"); enable.Add(new Label(Pretty(activePhase)+" phase")); enable.Add(StateSwitch(ExperimentBlocks.Flag(payload["enabled"],activePhase=="stimulus"),v=>StudioEdit(selected,r=>{
                    r[activePhase]["enabled"]=v;
                    if(v && r[activePhase]["durationSeconds"]==null) r[activePhase]["durationSeconds"]=schema["properties"]["durationSeconds"]["default"].DeepClone();
                }),"phase-enabled"));
                var hint=new Label("Disabled phases keep their configuration."); hint.AddToClassList("phase-hint"); enable.Add(hint); body.Add(enable);
            }
            var tabs=Row("settings-tabs"); body.Add(tabs);
            foreach(string name in new[]{"Essentials","Motion","Stimulus","Sky","Tuning","All parameters"})
            { string key=name; tabs.Add(Button(name,()=>{ settingsTab=key; BuildContent(); },settingsTab==name?"tab-active":"quiet")); }
            var scroll=new ScrollView(); scroll.AddToClassList("settings-scroll"); body.Add(scroll);
            AddTimingWarnings(scroll,selected);
            if(settingsTab=="Essentials")
            {
                Section(scroll,"Core controls","motion",payload,schema,prefix,new[]{"durationSeconds","closedLoopOrientation","closedLoopPosition","autopilotEnabled","autopilotSpeed","aglHeight","numberOfLocusts","locustSpeed","dimension","agentVisual","numberOfRings","animateOnMove","loop","repetitions","sync"});
                var features=Row("feature-grid"); scroll.Add(features);
                foreach(string name in new[]{"swarm","nightSky","headingReference","objects","stimuli","steps"})
                    if(payload[name]!=null)
                    {
                        string key=name; var card=new VisualElement(); card.AddToClassList("feature-card"); features.Add(card); var line=Row(); card.Add(line); line.Add(new WorkbenchIcon(key=="nightSky"?"sky":key=="swarm"?"swarm":"settings")); line.Add(new Label(Pretty(key)));
                        JToken value=payload[key]; bool active=value.Type!=JTokenType.Null && (!(value is JObject o) || ExperimentBlocks.Flag(o["enabled"],true));
                        card.Add(Pill(value is JArray a?a.Count+" items":active?"ON":"OFF",active?"accent":"muted-pill"));
                        card.Add(Button("Configure",()=>{ settingsTab=key=="nightSky"?"Sky":key=="headingReference"?"Tuning":"Stimulus"; expandedSections.Add(selected.Path+"|"+string.Join("/",prefix.Concat(new[]{key}))); BuildContent(); },"quiet"));
                    }
            }
            else
            {
                var present=payload.Properties().Select(p=>p.Name).Where(k=>k!="enabled").ToList();
                if(settingsTab=="All parameters")
                {
                    var addMissing=new Toggle("Show omitted fields") { name="show-omitted-settings" }; scroll.Add(addMissing);
                    scroll.Add(Button("Add parameter…",()=>ShowParameterDialog(selected,prefix),"quiet"));
                    var content=new VisualElement(); scroll.Add(content);
                    Action render=()=>{ content.Clear(); var keys=present.ToList(); if(addMissing.value) keys.AddRange(((JObject)schema["properties"]).Properties().Select(p=>p.Name).Where(k=>!keys.Contains(k) && k!="enabled" && !new[]{"preStimulus","stimulus","postStimulus"}.Contains(k)));
                        foreach(string group in new[]{"Motion","Stimulus","Sky","Tuning","Other"}) Section(content,group,GroupIcon(group),payload,schema,prefix,keys.Where(k=>Group(k)==group),false); };
                    addMissing.RegisterValueChangedCallback(_=>render()); render();
                }
                else Section(scroll,settingsTab,GroupIcon(settingsTab),payload,schema,prefix,present.Where(k=>Group(k)==settingsTab));
            }
            if(phased && !ExperimentBlocks.Flag(payload["enabled"],activePhase=="stimulus")) scroll.SetEnabled(false);
        }
        private static string GroupIcon(string group) => group=="Motion"?"motion":group=="Stimulus"?"swarm":group=="Sky"?"sky":"settings";
        private static string Group(string key)
        {
            if(key.StartsWith("initial") || key.StartsWith("reset") || key.StartsWith("closedLoop") || key.StartsWith("autopilot") || key.StartsWith("wind") || new[]{"randomInitialRotation","aglHeight","groundLayerMask","durationSeconds"}.Contains(key)) return "Motion";
            if(new[]{"backgroundColor","uniformSkyColor","nightSky","skyboxPath","skybox"}.Contains(key)) return "Sky";
            if(key.StartsWith("autoTrim") || key=="headingReference" || key=="adaptiveDecision" || key=="useHeadingReference") return "Tuning";
            if(new[]{"preStimulus","stimulus","postStimulus"}.Contains(key)) return "Other";
            if(new[]{"seed","repetitions","sync","loop"}.Contains(key)) return "Tuning";
            if(key=="vrConfigs" || key=="configs") return "Other";
            return "Stimulus";
        }
        private void Section(VisualElement target,string title,string icon,JObject owner,JObject schema,string[] prefix,IEnumerable<string> keys,bool hideInapplicable=true)
        {
            var names=keys.Where(k=>owner[k]!=null || schema["properties"]?[k]!=null).Where(k=>!hideInapplicable || ConfigSchema.Visible(owner,schema,k,Context)).Distinct().ToList();
            if(title=="Motion")
            {
                var priority=new[]{"durationSeconds","closedLoopOrientation","closedLoopPosition","autopilotEnabled","autopilotSpeed","aglHeight","windSpeed","windDirection","initialPosition","initialRotation","randomInitialRotation","resetPositionOnStart","resetRotationOnStart","groundLayerMask"};
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
            var label=Row("setting-label"); setting.Add(label); var caption=new Label(Pretty(key)); caption.AddToClassList("setting-title"); label.Add(caption);
            string unit=(string)schema["x-ui"]?["unit"]; if(!string.IsNullOrEmpty(unit)) label.Add(Pill(unit,"unit-pill"));
            if(value==null) label.Add(Pill("default","muted-pill"));
            setting.tooltip=(string)schema["x-ui"]?["help"]??key;
            Action<JToken,bool> assign=(v,rebuild)=>StudioEdit(doc,root=>{
                var parent=ConfigDocument.At(root,parts.Take(parts.Length-1).ToArray()); if(parent is JObject obj) obj[key]=v; else if(parent is JArray array) array[int.Parse(key)]=v;
            },rebuild);
            JToken shown=value??schema["default"];
            if(shown?.Type==JTokenType.Boolean)
            {
                setting.AddToClassList("boolean-setting");
                setting.Add(StateSwitch((bool)shown,v=>assign(new JValue(v),true),"flag-"+string.Join("-",parts)));
            }
            else if(value is JObject obj && new[]{"x","y","z"}.All(k=>obj[k]!=null) && obj.Properties().All(p=>new[]{"x","y","z"}.Contains(p.Name)))
            {
                setting.AddToClassList("vector-setting");
                var vector=Row("vector-controls"); setting.Add(vector);
                foreach(string axis in new[]{"x","y","z"}) { string component=axis; var field=NumberControl(obj[axis],ConfigSchema.Child(schema,axis),axis,v=>StudioEdit(doc,r=>ConfigDocument.At(r,parts)[component]=v,false),false); field.tooltip="Exact "+axis+" component"; vector.Add(new Label(axis.ToUpperInvariant())); vector.Add(field); }
            }
            else if(value is JObject color && new[]{"r","g","b"}.All(k=>color[k]!=null))
            {
                setting.AddToClassList("color-setting");
                setting.Add(ColorControl(doc,parts,color));
            }
            else if(value is JObject nested)
            {
                string id=doc.Path+"|"+string.Join("/",parts);
                var fold=new Foldout { text="Settings",value=expandedSections.Contains(id) }; setting.Add(fold);
                string flag=schema["properties"]?["enabled"]!=null || nested["enabled"]?.Type==JTokenType.Boolean?"enabled":schema["properties"]?["visible"]!=null || nested["visible"]?.Type==JTokenType.Boolean?"visible":null;
                if(flag!=null)
                {
                    bool on=ExperimentBlocks.Flag(nested[flag],ExperimentBlocks.Flag(schema["properties"]?[flag]?["default"]));
                    setting.Insert(1,StateSwitch(on,v=>StudioEdit(doc,r=>ConfigDocument.At(r,parts)[flag]=v,true),"flag-"+string.Join("-",parts)+"-"+flag));
                }
                Action populate=()=>{
                    fold.Clear();
                    foreach(var property in nested.Properties().Where(p=>p.Name!=flag && ConfigSchema.Visible(nested,schema,p.Name,Context)))
                        fold.Add(Setting(doc,parts.Concat(new[]{property.Name}).ToArray(),ConfigSchema.Child(schema,property.Name),nested,schema));
                    fold.contentContainer.SetEnabled(flag==null || ExperimentBlocks.Flag(nested[flag],ExperimentBlocks.Flag(schema["properties"]?[flag]?["default"],true)));
                };
                if(fold.value) populate();
                fold.RegisterValueChangedCallback(e=>{ if(e.target!=fold) return; if(e.newValue) { expandedSections.Add(id); populate(); } else { expandedSections.Remove(id); fold.Clear(); } });
                setting.AddToClassList("container-setting");
            }
            else if(value is JArray array)
            {
                var controls=Row(); controls.Add(Pill(array.Count+" items","accent")); controls.Add(StudioButton("Edit items","settings",()=>{ selected=doc; advancedMode=true; path.Clear(); path.AddRange(parts); BuildContent(); },"quiet"));
                controls.Add(IconButton("plus","Add item",()=>StudioEdit(doc,r=>((JArray)ConfigDocument.At(r,parts)).Add(ConfigSchema.NewValue(ConfigSchema.Child(schema,"",true)))))); setting.Add(controls);
            }
            else if(shown==null || shown.Type==JTokenType.Null)
            {
                var controls=Row(); controls.Add(Pill(value==null?"Inherited / scene default":"Unset · null","muted-pill"));
                controls.Add(Button("Set value",()=>assign(ConfigSchema.NewValue(schema),true),"quiet")); setting.Add(controls);
            }
            else if(schema["enum"] is JArray options)
            {
                var choices=options.Where(o=>o.Type!=JTokenType.Null).Select(o=>o.ToString()).ToList(); if(!choices.Contains(shown.ToString())) choices.Add(shown.ToString());
                var dropdown=new DropdownField(choices,choices.IndexOf(shown.ToString())); setting.Add(dropdown);
                dropdown.RegisterValueChangedCallback(e=>Guard(()=>{ var chosen=options.FirstOrDefault(o=>o.ToString()==e.newValue); assign(chosen?.DeepClone()??new JValue(e.newValue),true); }));
            }
            else if(LosslessJson.Kind(shown)=="number" || LosslessJson.Kind(shown)=="integer")
            {
                setting.AddToClassList("numeric-setting");
                setting.Add(NumberControl(shown,schema,key,v=>assign(v,false)));
            }
            else
            {
                setting.AddToClassList("text-setting");
                var input=new TextField { value=shown.ToString(),isDelayed=true }; setting.Add(input); input.RegisterValueChangedCallback(e=>Guard(()=>assign(new JValue(e.newValue),false)));
            }
            Action enable=()=>setting.SetEnabled(ConfigSchema.Enabled(ConfigDocument.At(doc.Root,parts.Take(parts.Length-1).ToArray()),ownerSchema,key,Context));
            enableUpdates.Add(enable); setting.RegisterCallback<DetachFromPanelEvent>(_=>enableUpdates.Remove(enable)); enable(); return setting;
        }
        private VisualElement NumberControl(JToken value,JObject schema,string key,Action<JToken> commit,bool allowSlider=true)
        {
            var control=new VisualElement(); control.AddToClassList("number-control");
            var spin=Row("spin-box"); control.Add(spin);
            var input=new TextField { value=value.ToString(),isDelayed=true,name="number-"+key }; input.AddToClassList("exact-number"); spin.Add(input);
            string lastValidText=value.ToString();
            input.style.width=Mathf.Clamp(value.ToString().Length*7+23,64,160);
            Slider slider=null; IVisualElementScheduledItem pending=null;
            JToken draft=null;
            Action flush=()=>{ if(draft==null) return; var number=draft; draft=null; Guard(()=>{commit(number);lastValidText=number.ToString();}); };
            var range=schema["x-ui"]?["slider"] as JObject;
            bool integer=schema["type"] is JArray types?types.Any(t=>t.ToString()=="integer"):schema["type"]?.ToString()=="integer";
            string step=integer?"1":(range?["step"]?.ToString()??"0.1");
            Action<int,int> increment=(direction,power)=>Guard(()=>{
                pending?.Pause();draft=null;
                var current=LosslessJson.Parse(input.value);
                JToken next=JsonNumberStepper.Step(current,step,direction,power);commit(next);input.SetValueWithoutNotify(next.ToString());
                lastValidText=next.ToString();
                if(slider!=null) slider.SetValueWithoutNotify((float)ExperimentBlocks.Number(next));
            });
            var arrows=new VisualElement();arrows.AddToClassList("spin-arrows");spin.Add(arrows);
            var up=new Button(()=>increment(1,0)){text="▴",name="spin-up-"+key};var down=new Button(()=>increment(-1,0)){text="▾",name="spin-down-"+key};arrows.Add(up);arrows.Add(down);
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
                    double chosen=Math.Round(Math.Round(e.newValue/sliderStep)*sliderStep,6); string text=chosen.ToString("0.######",CultureInfo.InvariantCulture); input.SetValueWithoutNotify(text); draft=new JRaw(text); pending?.Pause(); pending=control.schedule.Execute(flush).StartingIn(180);
                });
                slider.RegisterCallback<PointerUpEvent>(_=>flush()); slider.RegisterCallback<FocusOutEvent>(_=>flush());
            }
            input.RegisterValueChangedCallback(e=>Guard(()=>{
                pending?.Pause(); draft=null;
                try { JToken parsed=LosslessJson.Parse(e.newValue); string kind=LosslessJson.Kind(parsed); if(kind!="integer" && kind!="number") throw new ArgumentException("Enter a JSON number."); commit(parsed); lastValidText=parsed.ToString();if(slider!=null) slider.SetValueWithoutNotify((float)ExperimentBlocks.Number(parsed)); }
                catch { input.SetValueWithoutNotify(lastValidText); throw; }
            }));
            // A quick Start, navigation, Undo or Save must flush the last visual slider position first.
            control.RegisterCallback<DetachFromPanelEvent>(_=>numberFlushes.Remove(flush)); numberFlushes.Add(flush);
            return control;
        }
        private readonly List<Action> numberFlushes=new List<Action>();
        private readonly List<Action> enableUpdates=new List<Action>();
        private void FlushNumbers() { foreach(var flush in numberFlushes.ToArray()) flush(); }
        private void RefreshStudioValues()
        {
            var timing=body.Q<VisualElement>("timing-warnings");if(timing!=null) PopulateTimingWarnings(timing,selected);
            var duration=inspector.Q<Label>(className:"overview-duration"); if(duration!=null) duration.text=SequenceDuration();
            var effects=inspector.Q<Label>("config-effects"); if(effects!=null) effects.text=ExperimentBlocks.Describe(EditingPayload());
            foreach(var update in enableUpdates.ToArray()) update();
            var sync=inspector.Q<Label>("system-sync"); if(sync!=null) sync.text=SystemSyncSummary();
            var display=inspector.Q<Label>("system-display"); if(display!=null) display.text=SystemDisplaySummary();
            if(blockList!=null) root.schedule.Execute(()=>blockList?.RefreshItems());
        }
        private void BuildSystemEditor()
        {
            var scroll=new ScrollView(); scroll.AddToClassList("settings-scroll"); body.Add(scroll);
            var obj=(JObject)selected.Root; var schema=Workspace.Schemas.Get("system");
            Section(scroll,"Rendering & timing","system",obj,schema,Array.Empty<string>(),new[]{"targetDisplay","targetFrameRate","vSyncCount"});
            if(obj["configs"] is JArray rigs)
            {
                var title=Row("section-heading"); title.Add(new WorkbenchIcon("system")); title.Add(new Label("VR rigs · "+rigs.Count));
                title.Add(StudioButton("Add rig","plus",()=>StudioEdit(selected,r=>{
                    var array=(JArray)r["configs"]; var rig=ConfigSchema.NewValue((JObject)schema["properties"]["configs"]["items"]) as JObject;
                    int next=1; while(array.OfType<JObject>().Any(o=>(string)o["vrId"]=="VR"+next)) next++; rig["vrId"]="VR"+next; array.Add(rig);
                }),"quiet")); scroll.Add(title);
                for(int i=0;i<rigs.Count;i++)
                {
                    int index=i; var rig=rigs[i] as JObject; if(rig==null) continue; var rigSchema=(JObject)schema["properties"]["configs"]["items"];
                    var fold=new Foldout { text=(string)rig["vrId"]+" · "+(rig["closedLoopMode"]?.ToString()??"FicTrac")+" · "+rig["zmqAddress"]+":"+rig["zmqPort"],value=i==0 }; fold.AddToClassList("rig-card"); scroll.Add(fold);
                    foreach(string key in new[]{"vrId","closedLoopMode","zmqAddress","zmqPort","displayOrder","ledPanelWidth","ledPanelHeight","startRow","startCol","horizontal","sphereDiameter","manualControls"})
                        if(ConfigSchema.Visible(rig,rigSchema,key,Context)) fold.Add(Setting(selected,new[]{"configs",i.ToString(),key},ConfigSchema.Child(rigSchema,key),rig,rigSchema));
                    fold.Add(StudioButton("Remove rig","trash",()=>StudioEdit(selected,r=>((JArray)r["configs"]).RemoveAt(index)),"quiet"));
                }
            }
            Section(scroll,"Monitoring & operator view","settings",obj,schema,Array.Empty<string>(),new[]{"telemetry","statusOverlay","overheadCamera"});
            scroll.Add(new Label("Additional rig rows are editable here. Current scenes/controllers still require authored support beyond VR1–VR4."));
        }
    }
}
