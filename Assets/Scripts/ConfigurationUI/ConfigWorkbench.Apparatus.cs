using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private string unconfiguredSystem,unconfiguredSequence;
        private static readonly string[] InputLabels={"Flying · Rekkefly","Walking · HejjéTrac","Flying · Kinefly (legacy)","Walking · FicTrac (legacy)","Playback · Tirbala"};
        private static readonly string[] InputValues={"Rekkefly","Hejjetrac","Kinefly","FicTrac","Tirbala"};
        private void UseApparatusStyles()
        {
            var sheet=Resources.Load<StyleSheet>("ConfigurationUI/apparatus");
            if(sheet!=null&&!root.styleSheets.Contains(sheet))root.styleSheets.Add(sheet);
        }
        private int ApparatusDisplayFallback => MainController.DefaultTargetDisplay((JObject)ConfigSchema.EffectiveValue(Workspace.System.Root,Workspace.Schemas.Get("system"),true),Environment.GetCommandLineArgs());
        private int ApparatusOutputDisplay(JObject rig) => DesktopApparatusProfile.OutputDisplay(Workspace.System.Root,rig,ApparatusDisplayFallback);
        private bool ApparatusConfigured => DesktopApparatusProfile.IsConfigured(Workspace.System.Root);
        private void EnsureDailyWorkspace()
        {
            if(ApparatusConfigured)return;
            bool quick=DesktopQuickStart.IsQuickStart(Workspace);
            if(quick&&DesktopQuickStart.IsDemo(Workspace))return;
            if(!quick){unconfiguredSystem=Workspace.System.Path;unconfiguredSequence=Workspace.Sequence.Path;}
            DesktopQuickStart.ApplyToWorkspace(Workspace,"flying","night-sky",true);
            selected=Workspace.Sequence;path.Clear();
        }
        private string OwnedApparatusPath => Path.Combine(RuntimePaths.ConfigRoot,"Apparatus","system.json");
        private void OpenApparatusConfiguration()
        { Select(Workspace.System); }
        private void BeginOwnedApparatus(string presentation)
        {
            RequireApplied();
            string destination=OwnedApparatusPath;
            if(!File.Exists(destination))
            {
                string mode=CurrentMovement,world=CurrentWorld;
                var sourceSystem=Workspace.System;var sourceSequence=Workspace.Sequence;
                var system=(JObject)sourceSystem.Root.DeepClone();DesktopApparatusProfile.Begin(system,presentation,true,ApparatusDisplayFallback);
                var fork=ApparatusStarterFiles.Fork((JObject)sourceSequence.Root,RuntimePaths.ConfigRoot,(reference,scene)=>(JObject)Workspace.Open(reference,ConfigSchema.ForScene(scene)).Root,
                    DesktopQuickStart.SequenceDefaults(mode,world,true),DesktopQuickStart.EnvironmentDefaults(mode,world));
                var marker=DesktopApparatusProfile.Marker(system);marker["createdFromDemo"]=true;marker["starterWorld"]=world;
                marker["starterSequence"]=Path.GetRelativePath(RuntimePaths.ConfigRoot,fork.SequencePath).Replace('\\','/');marker["starterSequenceFingerprint"]=fork.SequenceFingerprint;
                marker["starterExperiment"]=fork.ExperimentReference;marker["starterExperimentFingerprint"]=fork.ExperimentFingerprint;marker["factoryStarter"]=fork.FactoryStarter;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using(var file=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var writer=new StreamWriter(file,new System.Text.UTF8Encoding(false)))writer.Write(system.ToString(Formatting.Indented)+"\n");
                // Edits were transferred to the new files. Keep every original demo
                // byte intact when SaveAll later saves this hardware workspace.
                if(sourceSystem.Dirty)sourceSystem.RevertToSaved();if(sourceSequence.Dirty)sourceSequence.RevertToSaved();
                foreach(var path in fork.CopiedSources){var source=Workspace.Documents.FirstOrDefault(d=>d.Path==path);if(source?.Dirty==true)source.RevertToSaved();}
            }
            Workspace.SelectPrimary("system",destination);
            string sequence=Path.Combine(RuntimePaths.ConfigRoot,"Apparatus","sequence.json");
            if(File.Exists(sequence))Workspace.SelectPrimary("sequence",sequence);
            Select(Workspace.System);
        }
        private void ApplyUntouchedStarterMotion()
        {
            var system=(JObject)Workspace.System.Root;var marker=system[DesktopApparatusProfile.MarkerName] as JObject;
            if(marker==null||!DesktopApparatusProfile.Flag(marker["factoryStarter"]))return;
            string expectedSequence=(string)marker["starterSequenceFingerprint"],reference=(string)marker["starterExperiment"];
            string savedSequence=(string)marker["starterSequence"];
            if(string.IsNullOrEmpty(reference)||string.IsNullOrEmpty(savedSequence)||Workspace.Sequence.Path!=Path.GetFullPath(Path.Combine(RuntimePaths.ConfigRoot,savedSequence))||ApparatusStarterFiles.Fingerprint(Workspace.Sequence.Root)!=expectedSequence)return;
            var experiment=Workspace.Documents.FirstOrDefault(d=>d.Path==Path.GetFullPath(ExperimentConfigFiles.Resolve(reference)));
            if(experiment==null||!File.Exists(experiment.Path))return; // Missing/invalid experiment files do not prevent hardware completion.
            if(ApparatusStarterFiles.Fingerprint(experiment.Root)!=(string)marker["starterExperimentFingerprint"])return;
            var modes=DesktopApparatusProfile.Rigs(system).Select(r=>TrackerBackend.TryParse(r["closedLoopMode"]?.ToString(),out var value)?TrackerBackend.IsWalking(value)?"walking":TrackerBackend.IsFlight(value)?"flying":"other":"other").Distinct().ToList();
            if(modes.Count!=1||modes[0]=="other")return;
            var defaults=DesktopQuickStart.EnvironmentDefaults(modes[0],(string)marker["starterWorld"]??"night-sky");
            experiment.Edit(r=>{foreach(string key in new[]{"closedLoopOrientation","closedLoopPosition","walkingPositionGain","walkingOrientationGain","autopilotEnabled","autopilotSpeed","aglHeight"})r[key]=defaults[key].DeepClone();});
            Workspace.System.Edit(r=>DesktopApparatusProfile.Marker((JObject)r)["starterExperimentFingerprint"]=ApparatusStarterFiles.Fingerprint(experiment.Root));
        }
        private void ReviewExistingApparatus(string system,string sequence=null)
        {
            RequireApplied();
            Workspace.SelectPrimary("system",system);
            if(!string.IsNullOrEmpty(sequence)&&File.Exists(sequence))Workspace.SelectPrimary("sequence",sequence);
            Select(Workspace.System);
        }
        private List<string> ApparatusConfigurationErrors()
        {
            var errors=DesktopApparatusProfile.Validate(Workspace.System.Root,ApparatusDisplayFallback);
            if(Workspace.System.LoadError!=null)errors.Add("Repair the system JSON before saving: "+Workspace.System.LoadError);
            errors.AddRange(ConfigSchema.Validate(Workspace.System.Root,Workspace.Schemas.Get("system")));
            return errors;
        }
        private void FinishApparatusConfiguration()
        {
            RequireApplied();
            var errors=ApparatusConfigurationErrors();
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            ApplyUntouchedStarterMotion();
            Workspace.System.Edit(r=>DesktopApparatusProfile.Complete((JObject)r,ApparatusDisplayFallback));Workspace.SaveAll();
            DesktopQuickStart.RememberWorkspace(Workspace.System.Path,Workspace.Sequence.Path);
            ShowSessionHome();UpdateStatus("Configuration saved. Review your experiment sequence before starting.");
        }
        private void BuildApparatusEditor()
        {
            UseApparatusStyles();
            var scroll=new ScrollView {name="apparatus-scroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("apparatus-scroll");body.Add(scroll);
            bool demo=DesktopQuickStart.IsQuickStart(Workspace)&&DesktopQuickStart.IsDemo(Workspace);
            if(demo)
            {
                var intro=new VisualElement();intro.AddToClassList("apparatus-intro");scroll.Add(intro);
                intro.Add(SessionHeading("Configure your apparatus","system"));
                intro.Add(SessionLabel("Define the hardware and tracking inputs here. Experiment sequences and their parameters stay in the everyday workspace.","apparatus-copy"));
                if(File.Exists(OwnedApparatusPath))
                    intro.Add(StudioButton("Review saved apparatus","system",()=>ReviewExistingApparatus(OwnedApparatusPath,Path.Combine(RuntimePaths.ConfigRoot,"Apparatus","sequence.json")),"primary"));
                else
                {
                    var types=Row("apparatus-type-choices");intro.Add(types);
                    types.Add(StudioButton("Configure LED panels","system",()=>BeginOwnedApparatus("led"),"primary"));
                    types.Add(StudioButton("Configure desktop preview","system",()=>BeginOwnedApparatus("desktop"),"quiet"));
                }
                if(!string.IsNullOrEmpty(unconfiguredSystem)&&File.Exists(unconfiguredSystem))
                {
                    var existing=StudioButton("Review existing system JSON","file",()=>ReviewExistingApparatus(unconfiguredSystem,unconfiguredSequence),"quiet");existing.name="apparatus-review-existing";
                    existing.tooltip="Review "+unconfiguredSystem+" without resetting its geometry or native calibration.";intro.Add(existing);
                }
                intro.Add(SessionLabel("LED setups use the existing system JSON, including D/R/B/L/F/U camera order, pixel dimensions, output display and tracker endpoints. A single-monitor preview is available; the multi-face high-resolution monitor wizard is deferred.","apparatus-copy"));
                intro.Add(SessionLabel("Until you save a configuration, Start runs the hardware-free demo.","apparatus-note"));
                return;
            }
            var system=(JObject)Workspace.System.Root;
            scroll.Add(SessionLabel(ApparatusConfigured?"Your apparatus":"Review and configure","apparatus-eyebrow"));
            scroll.Add(SessionLabel("Hardware, species and tracker routing. These settings are kept with the system JSON.","apparatus-copy"));
            var overview=Row("apparatus-overview-controls");scroll.Add(overview);
            overview.Add(ApparatusInteger("VR count","apparatus-vr-count",DesktopApparatusProfile.Rigs(system).Count(),value=>{
                StudioEdit(Workspace.System,r=>DesktopApparatusProfile.ResizeRigs((JObject)r,value,ApparatusDisplayFallback));
            },"Number of real animals/VR rigs. Adding a rig assigns a unique tracker port and a separate LED strip; existing calibration files are retained. Removing rigs is undoable.",1,DesktopApparatusProfile.MaximumRigCount));
            overview.Add(ApparatusInteger("Display count","apparatus-monitor-count",DesktopApparatusProfile.MonitorCount(system,ApparatusDisplayFallback),value=>StudioEdit(Workspace.System,r=>DesktopApparatusProfile.Marker((JObject)r)["monitorCount"]=value),"Displays used by the apparatus, including any operator display. Rig output display numbers below are 1-based here and zero-based in JSON. Unattached outputs can be planned before connecting hardware.",1,32));
            string output=DesktopApparatusProfile.Presentation(system);
            var presentation=new DropdownField("Output",new List<string>{"LED panels","Desktop preview"},output=="led"?0:1){name="apparatus-presentation"};
            presentation.tooltip="LED panels use exact small pixel rectangles. Desktop preview uses one full-screen face per rig. The automatic three-/four-monitor prism/cuboid wizard is deferred.";
            presentation.RegisterValueChangedCallback(e=>StudioEdit(Workspace.System,r=>DesktopApparatusProfile.SetPresentation((JObject)r,e.newValue=="LED panels"?"led":"desktop")));overview.Add(presentation);
            var native=Row("apparatus-native-option");scroll.Add(native);
            native.Add(StateSwitch(DesktopApparatusProfile.Flag((system[DesktopApparatusProfile.MarkerName] as JObject)?["launchBundledTrackers"]),value=>StudioEdit(Workspace.System,r=>DesktopApparatusProfile.Marker((JObject)r)["launchBundledTrackers"]=value),"apparatus-native-trackers"));
            var nativeLabel=SessionLabel("Launch bundled native trackers with Start","apparatus-copy");nativeLabel.tooltip="Only local modern Rekkefly/HejjéTrac rigs without an explicit executable/calibration override are launched. Disable this when sharing an externally started publisher. Remote and legacy endpoints remain externally managed.";native.Add(nativeLabel);
            BuildApparatusDisplayMap(scroll,system);
            var rigs=DesktopApparatusProfile.Rigs(system).ToList();
            for(int i=0;i<rigs.Count;i++)BuildApparatusRig(scroll,rigs[i],i,output);
            var monitorOptions=new Foldout {name="apparatus-operator-options",text="Operator display & monitoring",value=false};monitorOptions.AddToClassList("apparatus-advanced");scroll.Add(monitorOptions);
            var displayNames=DesktopDisplay.DisplayNames.ToList();
            if(displayNames.Count>0)
            {
                var display=new DropdownField("Open MATREX on",displayNames,Mathf.Clamp(DesktopDisplay.SelectedDisplay,0,displayNames.Count-1)){name="application-display"};
                display.tooltip="Select the application window’s monitor. It stays native fullscreen; this is separate from calibrated LED output display routing.";
                display.RegisterValueChangedCallback(e=>DesktopDisplay.SelectDisplay(displayNames.IndexOf(e.newValue)));monitorOptions.Add(display);
            }
            var schema=Workspace.Schemas.Get("system");
            Section(monitorOptions,"Rendering & timing","system",system,schema,Array.Empty<string>(),new[]{"targetDisplay","targetFrameRate","vSyncCount"});
            Section(monitorOptions,"Telemetry & overview","radio",system,schema,Array.Empty<string>(),new[]{"telemetry","statusOverlay","overheadCamera"});
            foreach(string warning in DesktopApparatusProfile.SharedInputWarnings(system))scroll.Add(SessionLabel(warning,"apparatus-validation"));
            if(DesktopApparatusProfile.Rigs(system).Select(RigMode).Distinct().Count()>1)scroll.Add(SessionLabel("Mixed input contracts: review motion settings in each experiment block. A shared block can apply different walking/flight gains, but its movement options are not guessed from one rig.","apparatus-validation"));
            var errors=DesktopApparatusProfile.Validate(system,ApparatusDisplayFallback);
            if(errors.Count>0){var validation=SessionLabel(string.Join("\n",errors),"apparatus-validation");validation.AddToClassList("error");scroll.Add(validation);}
            var complete=StudioButton(ApparatusConfigured?"Save configuration":"Use this configuration","system",FinishApparatusConfiguration,"primary");complete.name="apparatus-complete";scroll.Add(complete);
            complete.tooltip="Validate and save this system JSON, then return to the experiment workspace. Unknown JSON fields and untouched calibration settings are retained.";
            scroll.Add(SessionLabel("Panel cells start at row 0 / column 0. Letters match the runtime cameras: D Down, R Right, B Back, L Left, F Front, U Up.","apparatus-note"));
        }
        private IntegerField ApparatusInteger(string label,string name,int value,Action<int> change,string help,int minimum=0,int maximum=int.MaxValue)
        {
            var field=new IntegerField(label){name=name,value=value,isDelayed=true,tooltip=help};field.AddToClassList("apparatus-input");
            field.RegisterValueChangedCallback(e=>{
                try{if(e.newValue<minimum||e.newValue>maximum)throw new ArgumentException(label+" must be between "+minimum+" and "+maximum+".");change(e.newValue);}
                catch(Exception error){field.SetValueWithoutNotify(e.previousValue);UpdateStatus(error.GetBaseException().Message);status.AddToClassList("error");}
            });return field;
        }
        private TextField ApparatusText(string label,string name,string value,Action<string> change,string help)
        {
            var field=new TextField(label){name=name,value=value??"",isDelayed=true,tooltip=help};field.AddToClassList("apparatus-input");
            field.RegisterValueChangedCallback(e=>{
                try{change(e.newValue);}catch(Exception error){field.SetValueWithoutNotify(e.previousValue);UpdateStatus(error.GetBaseException().Message);status.AddToClassList("error");}
            });return field;
        }
        private void BuildApparatusRig(VisualElement parent,JObject rig,int index,string presentation)
        {
            string id=(string)rig["vrId"]??"VR"+(index+1);string[] RigPath(string key)=>new[]{"configs",index.ToString(),key};
            Action<string,JToken,bool> set=(key,value,rebuild)=>StudioEdit(Workspace.System,r=>r["configs"][index][key]=value,rebuild);
            var fold=new Foldout {name="apparatus-rig-"+id,text=id+" · "+RigMode(rig)+" · "+DesktopApparatusProfile.Order(rig).Length+" faces · "+rig["zmqAddress"]+":"+rig["zmqPort"],value=index==0};fold.AddToClassList("apparatus-rig");parent.Add(fold);
            var identity=Row("apparatus-grid");fold.Add(identity);
            identity.Add(ApparatusText("VR ID","apparatus-id-"+id,id,value=>{
                if(!System.Text.RegularExpressions.Regex.IsMatch(value??"",@"^VR[1-9][0-9]*$")||DesktopApparatusProfile.Rigs(Workspace.System.Root).Where((r,n)=>n!=index).Any(r=>(string)r["vrId"]==value))throw new ArgumentException("Use a unique VR ID, such as VR1 or VR2.");
                set("vrId",value,true);
            },"Hardware routing identity. Animal IDs are entered separately with the experiment recording details."));
            int mode=Array.FindIndex(InputValues,m=>string.Equals(m,rig["closedLoopMode"]?.ToString(),StringComparison.OrdinalIgnoreCase));
            var labels=InputLabels.ToList();var values=InputValues.ToList();
            if(mode<0){labels.Add(RigMode(rig)+" · current JSON value");values.Add(rig["closedLoopMode"]?.ToString()??"FicTrac");mode=labels.Count-1;}
            var movement=new DropdownField("Movement / input",labels,mode){name="apparatus-mode-"+id,tooltip="Choose the animal’s input contract. Existing legacy JSON values remain untouched until changed. This does not rewrite a tracker calibration file."};movement.AddToClassList("apparatus-input");
            movement.RegisterValueChangedCallback(e=>set("closedLoopMode",values[labels.IndexOf(e.newValue)],true));identity.Add(movement);
            identity.Add(ApparatusText("Species","apparatus-species-"+id,(string)rig["speciesName"],value=>set("speciesName",value,false),"Common or scientific species name stored in system JSON and copied to the animal’s recording metadata."));
            var transport=Row("apparatus-grid");fold.Add(transport);
            transport.Add(ApparatusText("Tracker host","apparatus-host-"+id,(string)rig["zmqAddress"]??"localhost",value=>set("zmqAddress",value,true),"Host running the tracker publisher. Use localhost for bundled native apps; a remote host remains externally managed."));
            transport.Add(ApparatusInteger("Tracker port","apparatus-port-"+id,DesktopApparatusProfile.Integer(rig["zmqPort"],9871),value=>set("zmqPort",value,true),"ZeroMQ tracking input port. Each rig on the same host requires its own port.",1,65535));
            transport.Add(ApparatusInteger("Output display","apparatus-display-"+id,ApparatusOutputDisplay(rig)+1,value=>set("targetDisplay",value-1,true),"Physical output display, numbered from1 here. System JSON stores display0 for the first output. The preview below shows every configured rectangle on that display.",1,32));
            if(presentation=="led")
            {
                var pixels=Row("apparatus-grid");fold.Add(pixels);
                pixels.Add(ApparatusInteger("Panel width (px)","apparatus-width-"+id,DesktopApparatusProfile.Integer(rig["ledPanelWidth"],128),value=>set("ledPanelWidth",value,true),"Pixels across one LED panel. Typical panels are64 or128 pixels; any positive integer can be entered.",1));
                pixels.Add(ApparatusInteger("Panel height (px)","apparatus-height-"+id,DesktopApparatusProfile.Integer(rig["ledPanelHeight"],128),value=>set("ledPanelHeight",value,true),"Pixels down one LED panel. Square and rectangular positive dimensions are supported.",1));
                var presets=Row("apparatus-presets");pixels.Add(presets);
                foreach(int size in new[]{64,128}){int chosen=size;presets.Add(StudioButton(size+" × "+size,"system",()=>StudioEdit(Workspace.System,r=>{r["configs"][index]["ledPanelWidth"]=chosen;r["configs"][index]["ledPanelHeight"]=chosen;}),"quiet"));}
                var offsets=Row("apparatus-grid");fold.Add(offsets);
                offsets.Add(ApparatusInteger("Start row","apparatus-row-"+id,DesktopApparatusProfile.Integer(rig["startRow"],0),value=>set("startRow",value,true),"Zero-based panel-cell row from the top of the output. PixelY = startRow × panelHeight.",0));
                offsets.Add(ApparatusInteger("Start column","apparatus-col-"+id,DesktopApparatusProfile.Integer(rig["startCol"],0),value=>set("startCol",value,true),"Zero-based panel-cell column from the left. PixelX = startCol × panelWidth.",0));
                var direction=new DropdownField("Packing",new List<string>{"Horizontal strip","Vertical strip"},rig["horizontal"]==null||DesktopApparatusProfile.Flag(rig["horizontal"])?0:1){name="apparatus-packing-"+id,tooltip="Pack faces from left to right or from top to bottom, in the exact display-order string."};direction.AddToClassList("apparatus-input");direction.RegisterValueChangedCallback(e=>set("horizontal",e.newValue=="Horizontal strip",true));offsets.Add(direction);
                var order=Row("apparatus-order-row");fold.Add(order);
                order.Add(ApparatusText("Face order","apparatus-order-"+id,DesktopApparatusProfile.Order(rig),value=>StudioEdit(Workspace.System,r=>DesktopApparatusProfile.SetOrder((JObject)r["configs"][index],value)),"The exact face sequence sent to the LED output. Valid unique letters: D/R/B/L/F/U. The numbered strip and cube net update together."));
                order.Add(StudioButton("4 walls","system",()=>StudioEdit(Workspace.System,r=>DesktopApparatusProfile.SetOrder((JObject)r["configs"][index],"RBLF")),"quiet"));
                order.Add(StudioButton("6 faces","system",()=>StudioEdit(Workspace.System,r=>DesktopApparatusProfile.SetOrder((JObject)r["configs"][index],"DRBLFU")),"quiet"));
                BuildFaceNet(fold,rig,index,id);
            }
            else fold.Add(SessionLabel("One full-screen F (Front) camera is rendered on this rig’s output. Automatic three-/four-face monitor calibration remains deferred.","apparatus-note"));
            var advanced=new Foldout {name="apparatus-rig-advanced-"+id,text="Calibration & advanced input",value=false};advanced.AddToClassList("apparatus-advanced");fold.Add(advanced);
            var rigSchema=(JObject)Workspace.Schemas.Get("system")["properties"]["configs"]["items"];
            foreach(string key in new[]{"sphereDiameter","manualControls","tracker"})advanced.Add(Setting(Workspace.System,RigPath(key),ConfigSchema.Child(rigSchema,key),rig,rigSchema));
            AddTrackerControls(advanced,rig);
            // A hardware label may change while editing. View identity follows its
            // array slot so renaming VR2 does not close its form or lose focus.
            fold.viewDataKey="apparatus-rig-slot-"+index;
            foreach(var element in fold.Query<VisualElement>().ToList())
                if(!string.IsNullOrEmpty(element.name)&&element.name.Contains("-"+id))
                    element.viewDataKey=element.name.Replace("-"+id,"-slot-"+index);
        }
        private void BuildFaceNet(VisualElement parent,JObject rig,int index,string id)
        {
            string order=DesktopApparatusProfile.Order(rig);
            var previews=Row("apparatus-face-previews");parent.Add(previews);
            var net=new VisualElement {name="apparatus-net-"+id};net.AddToClassList("apparatus-face-net");previews.Add(net);
            for(int row=0;row<3;row++)
            {
                var line=Row("apparatus-net-row");net.Add(line);
                for(int col=0;col<4;col++)
                {
                    char face=row==0&&col==1?'U':row==2&&col==1?'D':row==1?new[]{'L','F','R','B'}[col]:'\0';
                    if(face=='\0'){var blank=new VisualElement();blank.AddToClassList("apparatus-net-blank");line.Add(blank);continue;}
                    int slot=order.IndexOf(face);char chosen=face;
                    var button=StudioButton("",null,()=>StudioEdit(Workspace.System,r=>{
                        var target=(JObject)r["configs"][index];string current=DesktopApparatusProfile.Order(target);
                        DesktopApparatusProfile.SetOrder(target,current.IndexOf(chosen)>=0?current.Replace(chosen.ToString(),""):current+chosen);
                    }),"apparatus-face");button.name="apparatus-face-"+id+"-"+face;button.EnableInClassList("is-enabled",slot>=0);
                    button.Add(new Label(face.ToString()){pickingMode=PickingMode.Ignore});button.Add(SessionLabel(DesktopApparatusProfile.FaceName(face),"apparatus-face-name"));button.Add(SessionLabel(slot<0?"Off":"Slot "+(slot+1),"apparatus-face-slot"));
                    button.tooltip=face+" · "+DesktopApparatusProfile.FaceName(face)+(slot<0?" is not rendered. Click to append it to the output order.":" is output slot "+(slot+1)+". Click to remove this face; at least one face remains required.");line.Add(button);
                }
            }
            var strip=new VisualElement();strip.AddToClassList("apparatus-strip-preview");previews.Add(strip);strip.Add(SessionLabel("Output order","apparatus-eyebrow"));
            var tiles=Row("apparatus-strip-tiles");strip.Add(tiles);
            tiles.style.flexDirection=rig["horizontal"]==null||DesktopApparatusProfile.Flag(rig["horizontal"])?FlexDirection.Row:FlexDirection.Column;
            for(int slot=0;slot<order.Length;slot++){var tile=new VisualElement();tile.AddToClassList("apparatus-strip-tile");tile.Add(new Label(order[slot].ToString()));tile.Add(SessionLabel((slot+1).ToString(),"apparatus-face-slot"));tiles.Add(tile);}
            strip.Add(SessionLabel((rig["horizontal"]==null||DesktopApparatusProfile.Flag(rig["horizontal"])?"Left → right":"Top → bottom")+" · "+rig["ledPanelWidth"]+" × "+rig["ledPanelHeight"]+" pixels per face","apparatus-note"));
        }
        private void BuildApparatusDisplayMap(VisualElement parent,JObject system)
        {
            var section=new VisualElement();section.AddToClassList("apparatus-map-section");parent.Add(section);section.Add(SessionHeading("Display layout","system","Exact stimulus pixels, separate from the operator UI."));
            var rigs=DesktopApparatusProfile.Rigs(system).ToList();
            foreach(int display in rigs.Select(r=>ApparatusOutputDisplay(r)).Distinct().OrderBy(n=>n))
            {
                bool attached=display>=0&&display<Display.displays.Length;
                int width=attached?Display.displays[display].systemWidth:0,height=attached?Display.displays[display].systemHeight:0;
                if(display==0){width=Screen.width;height=Screen.height;}
                bool estimated=width<=0||height<=0;
                if(estimated){width=Math.Max(1920,rigs.Where(r=>ApparatusOutputDisplay(r)==display).Select(r=>PixelBounds(r).Item3).DefaultIfEmpty(1920).Max());height=Math.Max(1080,rigs.Where(r=>ApparatusOutputDisplay(r)==display).Select(r=>PixelBounds(r).Item4).DefaultIfEmpty(1080).Max());}
                section.Add(SessionLabel("Display "+(display+1)+" · "+width+" × "+height+" px"+(attached?"":" · not connected")+(estimated?" · preview extent":""),"apparatus-map-label"));
                var map=new VisualElement {name="apparatus-display-map-"+display};map.AddToClassList("apparatus-display-map");section.Add(map);
                float aspect=(float)width/height;
                map.style.width=210f*aspect;
                map.RegisterCallback<GeometryChangedEvent>(e=>{
                    float scaledHeight=e.newRect.width/aspect;
                    if(Mathf.Abs(e.newRect.height-scaledHeight)>.5f)map.style.height=scaledHeight;
                });
                foreach(var rig in rigs.Where(r=>ApparatusOutputDisplay(r)==display))
                {
                    string id=(string)rig["vrId"];string order=DesktopApparatusProfile.Order(rig);bool horizontal=rig["horizontal"]==null||DesktopApparatusProfile.Flag(rig["horizontal"]);int pw=Math.Max(1,DesktopApparatusProfile.Integer(rig["ledPanelWidth"],128)),ph=Math.Max(1,DesktopApparatusProfile.Integer(rig["ledPanelHeight"],128));
                    bool desktop=DesktopApparatusProfile.Flag(rig["desktopPreview"]);
                    for(int slot=0;slot<order.Length;slot++)
                    {
                        double left=desktop?0d:(DesktopApparatusProfile.Integer(rig["startCol"],0)+(horizontal?slot:0))*(double)pw;
                        double top=desktop?0d:(DesktopApparatusProfile.Integer(rig["startRow"],0)+(horizontal?0:slot))*(double)ph;
                        double panelWidth=desktop?width:pw,panelHeight=desktop?height:ph;
                        var cell=new Label(order[slot].ToString()){name="apparatus-pixel-"+id+"-"+slot};cell.AddToClassList("apparatus-pixel-cell");
                        var colors=new[]{new Color(.20f,.43f,.42f),new Color(.23f,.38f,.57f),new Color(.48f,.37f,.20f),new Color(.39f,.29f,.48f),new Color(.49f,.27f,.28f),new Color(.22f,.43f,.49f)};
                        cell.style.backgroundColor=colors[rigs.IndexOf(rig)%colors.Length];
                        cell.style.left=new Length((float)(left/width*100),LengthUnit.Percent);cell.style.top=new Length((float)(top/height*100),LengthUnit.Percent);cell.style.width=new Length((float)(panelWidth/width*100),LengthUnit.Percent);cell.style.height=new Length((float)(panelHeight/height*100),LengthUnit.Percent);
                        cell.tooltip=id+" · "+order[slot]+" "+DesktopApparatusProfile.FaceName(order[slot])+" · slot "+(slot+1)+"\n"+panelWidth+" × "+panelHeight+" px at ("+left+", "+top+") from the top-left.";map.Add(cell);
                        if(left<0||top<0||left+panelWidth>width||top+panelHeight>height)section.Add(SessionLabel(id+" "+order[slot]+" exceeds this display’s preview bounds. Check panel size and start row/column.","apparatus-validation"));
                    }
                    section.Add(SessionLabel(id+" · "+order+" · "+(desktop?"full display":pw+" × "+ph+" px / face · row "+rig["startRow"]+", column "+rig["startCol"]),"apparatus-map-legend"));
                }
            }
        }
        private static Tuple<int,int,int,int> PixelBounds(JObject rig)
        {
            int width=Math.Max(1,DesktopApparatusProfile.Integer(rig["ledPanelWidth"],128)),height=Math.Max(1,DesktopApparatusProfile.Integer(rig["ledPanelHeight"],128));
            bool horizontal=rig["horizontal"]==null||DesktopApparatusProfile.Flag(rig["horizontal"]);int faces=DesktopApparatusProfile.Order(rig).Length;
            return Tuple.Create(DesktopApparatusProfile.Integer(rig["startCol"],0)*width,DesktopApparatusProfile.Integer(rig["startRow"],0)*height,(DesktopApparatusProfile.Integer(rig["startCol"],0)+(horizontal?faces:1))*width,(DesktopApparatusProfile.Integer(rig["startRow"],0)+(horizontal?1:faces))*height);
        }
    }
}
