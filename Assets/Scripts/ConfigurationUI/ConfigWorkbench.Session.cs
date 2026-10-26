using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private void ShowSessionHome()
        {
            RequireApplied(); CloseModal(); LeaveRecordingLibrary(); selected=Workspace.Sequence; path.Clear();
            homeMode=true; experimentIndexMode=false; metadataMode=jsonMode=advancedMode=false;
            BuildNavigation(); BuildContent();
        }
        private void ShowExperimentConfigs()
        {
            RequireApplied(); CloseModal(); LeaveRecordingLibrary(); path.Clear(); homeMode=false; experimentIndexMode=true;
            metadataMode=jsonMode=advancedMode=false; BuildNavigation(); BuildContent();
        }
        private VisualElement BuildSequenceFlags()
        {
            var flags=Row("sequence-flags");
            foreach(string key in new[]{"autoStart","loop","randomise"})
            {
                string flag=key;
                bool on=ExperimentBlocks.Flag(Workspace.Sequence.Root[key],key=="loop");
                string text=key=="autoStart"?"Auto start":key=="loop"?"Loop":"Randomize";
                var button=StudioButton(text,key=="autoStart"?"auto":key=="loop"?"loop":"shuffle",
                    ()=>StudioEdit(Workspace.Sequence,r=>r[flag]=!on),"sequence-flag");
                button.name="sequence-"+key; button.EnableInClassList("is-on",on);
                button.tooltip=text+": "+(on?"ON":"OFF")+". Click to turn "+(on?"off":"on")+
                    (key=="autoStart"?". Applies to the next application launch; this session starts with Start experiment.":".");
                var state=new Label(on?"ON":"OFF"); state.AddToClassList("flag-state"); button.Add(state);
                flags.Add(button);
            }
            if(ExperimentBlocks.Flag(Workspace.Sequence.Root["loop"],true))
            {
                var loopStart=new IntegerField("Repeat from block") {
                    name="sequence-loop-start", value=(int?)Workspace.Sequence.Root["loopStartIndex"]+1??1,
                    tooltip="1 repeats every block. 2 runs the first block once, then repeats from the second block. Requires listed order." };
                loopStart.RegisterValueChangedCallback(evt=>StudioEdit(Workspace.Sequence,r=>r["loopStartIndex"]=Math.Max(0,evt.newValue-1)));
                flags.Add(loopStart);
            }
            return flags;
        }
        private TextField SessionField(VisualElement parent,string label,string name,string value,Action<string> change,string placeholder,bool multiline=false)
        {
            var field=new TextField(label) { name=name,value=value??"",multiline=multiline };
            field.tooltip=label+". "+placeholder+". Saved in this run's subject/session metadata.";
            field.textEdition.placeholder=placeholder; field.AddToClassList("session-field");
            if(multiline) field.AddToClassList("session-comments");
            field.RegisterValueChangedCallback(e=>change(e.newValue)); parent.Add(field); return field;
        }
        private VisualElement SessionHeading(string title,string icon,string detail=null)
        {
            var row=Row("session-section-heading"); row.Add(new WorkbenchIcon(icon));
            var label=new Label(title); label.AddToClassList("session-section-title"); row.Add(label);
            if(detail!=null) { var hint=new Label(detail); hint.AddToClassList("session-hint"); row.Add(hint); }
            return row;
        }
        private bool sessionDetailsOpen;
        private string CurrentMovement => DesktopQuickStart.IsQuickStart(Workspace) ? DesktopQuickStart.Mode(Workspace) :
            (Workspace.System.Root["configs"] as JArray??new JArray()).OfType<JObject>().All(r=>TrackerBackend.TryParse(r["closedLoopMode"]?.ToString(),out var mode)&&TrackerBackend.IsWalking(mode))?"walking":"flying";
        private string CurrentWorld => DesktopQuickStart.IsQuickStart(Workspace)?DesktopQuickStart.Scene(Workspace):"night-sky";
        private bool CurrentDemo => DesktopQuickStart.IsQuickStart(Workspace)&&DesktopQuickStart.IsDemo(Workspace);
        private bool ApparatusDraft => !ApparatusConfigured&&!DesktopQuickStart.IsQuickStart(Workspace);
        private List<string> PrimaryActionErrors()
        {
            return ApparatusDraft?ApparatusConfigurationErrors():Workspace.Validate();
        }
        private void RunPrimaryAction()
        {
            if(replayLibraryMode)OpenLibrarySelection();else if(ApparatusDraft)FinishApparatusConfiguration();else StartExperiment();
        }
        private void ChooseQuickStart(string mode,string world,bool demo)
        {
            RequireApplied();
            DesktopQuickStart.ApplyToWorkspace(Workspace,mode,world,demo);
            selected=Workspace.Sequence;path.Clear();homeMode=true;experimentIndexMode=metadataMode=jsonMode=advancedMode=false;
            BuildNavigation();BuildContent();
            UpdateStatus((demo?"Demo":"Camera setup")+" ready. Your other configuration files have been kept.");
        }
        private void BuildSessionHome()
        {
            EnsureDailyWorkspace();EnsureMetadata();UseApparatusStyles();
            var scroll=new ScrollView {name="start-workspace",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("session-scroll");body.Add(scroll);
            if(ApparatusConfigured)BuildDailyRunHeader(scroll);else BuildDemoLauncher(scroll);
            string order=(ExperimentBlocks.Flag(Workspace.Sequence.Root["randomise"])?"Random order":"Listed order")+
                " · "+(ExperimentBlocks.Flag(Workspace.Sequence.Root["loop"],true)?"Repeats":"One pass");
            var planHeading=SessionHeading("Experiment sequence","sequence",BuilderSteps.Count+(BuilderSteps.Count==1?" block · ":" blocks · ")+SequenceDuration()+" · "+order);
            var edit=StudioButton("Edit sequence","settings",()=>Select(Workspace.Sequence),"quiet");edit.name="session-edit-sequence";planHeading.Add(edit);scroll.Add(planHeading);
            var summaryIndices=Enumerable.Range(0,BuilderSteps.Count).ToList();
            var plan=new ListView(summaryIndices,64,()=>new VisualElement(),BindSessionBlock)
            {name="session-plan",selectionType=SelectionType.None,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight};
            plan.AddToClassList("session-plan");plan.style.height=Math.Min(5,Math.Max(1,BuilderSteps.Count))*64;scroll.Add(plan);
            if(BuilderSteps.Count==0)scroll.Add(StudioButton("Add your first block","plus",()=>{Select(Workspace.Sequence);ShowBlockLibrary();},"primary"));
            var recording=new Foldout {name="recording-details",text="Animal & recording details",value=sessionDetailsOpen};recording.AddToClassList("recording-details");scroll.Add(recording);
            recording.tooltip="Animal labels, experimenter and notes saved with the recording. Species and tracking hardware are configured separately.";
            recording.RegisterValueChangedCallback(e=>sessionDetailsOpen=e.newValue);BuildSessionDetails(recording);
            RefreshStartCaption();UpdateStatus();
        }
        private void BuildDemoLauncher(VisualElement parent)
        {
            var welcome=new VisualElement();welcome.AddToClassList("start-welcome");parent.Add(welcome);
            welcome.Add(SessionLabel("DEMO","eyebrow"));welcome.Add(SessionLabel("Explore a world.","start-title"));
            welcome.Add(SessionLabel("Press Start demo to try the complete loop. No animal, camera or LED hardware is required.","start-subtitle"));
            var worlds=new VisualElement();worlds.AddToClassList("demo-worlds");parent.Add(worlds);
            var previews=Row("world-previews");worlds.Add(previews);
            previews.Add(WorldChoice("night-sky","Night sky","Terrain beneath an astronomical sky.","night"));
            previews.Add(WorldChoice("forest","Forest","Explore a landscape of trees.","forest"));
            var options=new Foldout {name="demo-options",text="Demo options & tracker windows",value=false};options.AddToClassList("apparatus-advanced");parent.Add(options);
            var movement=new DropdownField("Demonstration",new List<string>{"Flying · simulated input","Walking · recorded sample"},CurrentMovement=="walking"?1:0){name="quickstart-demo-mode"};
            movement.tooltip="This changes the hardware-free demo only. Configure real walking/flying inputs with the apparatus.";
            movement.RegisterValueChangedCallback(e=>ChooseQuickStart(e.newValue.StartsWith("Walking",StringComparison.Ordinal)?"walking":"flying",CurrentWorld,true));options.Add(movement);
            if(CurrentMovement=="walking")options.Add(SessionLabel(DesktopPreviewHud.WalkingDemoHelp,"apparatus-note"));
            AddQuickTrackerControls(options);
            var links=Row("tracker-links");options.Add(links);AddSessionPlotLink(links);
            links.Add(StudioButton(CurrentMovement=="walking"?"About HejjéTrac":"About Rekkefly","external",()=>UnityEngine.Application.OpenURL(CurrentMovement=="walking"?"https://github.com/pvnkmrksk/hejjetrak":"https://github.com/pvnkmrksk/rekkefly"),"text-link"));
            var configure=Row("demo-configure");parent.Add(configure);configure.Add(new WorkbenchIcon("system"));
            var copy=SessionLabel("Have an apparatus? Configure its VRs, panels and tracker inputs.","apparatus-copy");copy.style.flexGrow=1;configure.Add(copy);
            var button=StudioButton("Configure","settings",OpenApparatusConfiguration,"quiet");button.name="demo-configure";configure.Add(button);
        }
        private void BuildDailyRunHeader(VisualElement parent)
        {
            var welcome=new VisualElement();welcome.AddToClassList("daily-welcome");parent.Add(welcome);
            welcome.Add(SessionLabel("EXPERIMENT","eyebrow"));welcome.Add(SessionLabel("Ready to run.","start-title"));
            welcome.Add(SessionLabel("Review the sequence, adjust its parameters, and start.","start-subtitle"));
            var rigs=DesktopApparatusProfile.Rigs(Workspace.System.Root).ToList();
            var summary=Row("daily-apparatus-summary");parent.Add(summary);summary.Add(new WorkbenchIcon("system"));
            var text=SessionLabel(rigs.Count+(rigs.Count==1?" VR":" VRs")+" · "+(DesktopApparatusProfile.Presentation(Workspace.System.Root)=="led"?"LED panels":"Desktop preview")+" · "+string.Join(" / ",rigs.Select(RigMode).Distinct()),"apparatus-copy");text.name="configured-apparatus-summary";text.tooltip=SystemHeadline();summary.Add(text);
            var links=Row("tracker-links");parent.Add(links);AddSessionPlotLink(links);
            if(DesktopApparatusProfile.ShouldPrepareCompanions(Workspace.System.Root))
            {
                var open=StudioButton("Open tracker windows","camera",OpenQuickTracker,"text-link");open.name="session-open-trackers";open.tooltip="Open or show the independent native tracker windows for eligible local rigs. Camera calibration stays in those windows.";links.Add(open);
            }
            var state=new Foldout {name="session-tracker-windows",text="Tracker windows",value=false};state.AddToClassList("apparatus-advanced");parent.Add(state);
            foreach(var rig in rigs)
            {
                string id=(string)rig["vrId"];
                var row=Row("session-native-row");state.Add(row);
                var label=SessionLabel(id+" · "+RigMode(rig),"apparatus-copy");label.style.flexGrow=1;row.Add(label);
                var show=StudioButton("Show","external",()=>TrackerProcessManager.Instance.Show(id),"quiet");row.Add(show);
                var stop=StudioButton("Stop","stop",()=>TrackerProcessManager.Instance.Stop(id),"quiet");row.Add(stop);
                Action refresh=()=>{bool running=TrackerProcessManager.IsRunning(id);label.text=id+" · "+(running?TrackerProcessManager.Status(id):rig["zmqAddress"]+":"+rig["zmqPort"]+" · input configured");show.SetEnabled(running);stop.SetEnabled(running);};refresh();row.schedule.Execute(refresh).Every(500);
            }
        }
        private void AddSessionPlotLink(VisualElement parent)
        {
            var plots=StudioButton("Live plots","radio",DesktopDashboard.Open,"text-link");plots.name="open-live-plots";plots.tooltip="Open the optional independent live trajectory dashboard. Plotting is separate from stimulus rendering.";parent.Add(plots);
            var state=SessionLabel("","viewer-launch-state");parent.Add(state);
            state.schedule.Execute(()=>{if(!DesktopDashboard.Status.StartsWith("Optional",StringComparison.Ordinal))state.text=DesktopDashboard.Status;}).Every(500);
        }
        private void AddQuickTrackerControls(VisualElement parent)
        {
            var rig=(Workspace.System.Root["configs"] as JArray)?.OfType<JObject>().FirstOrDefault();if(rig==null)return;
            string id=(string)rig["vrId"];
            var row=Row("quick-tracker-status");parent.Add(row);
            var state=SessionLabel("Tracker ready to open","tracker-app-state");row.Add(state);
            var open=StudioButton("Open tracker","external",()=>{
                if(TrackerProcessManager.IsRunning(id))TrackerProcessManager.Instance.Show(id);else OpenQuickTracker();
            },"text-link");open.name="quickstart-open-tracker";row.Add(open);
            open.tooltip="Open the independent tracker or bring its existing window forward for camera selection and calibration.";
            var stop=StudioButton("Stop tracker","stop",()=>TrackerProcessManager.Instance.Stop(id),"text-link");stop.name="quickstart-stop-tracker";row.Add(stop);
            stop.tooltip="Explicitly stop the tracker opened for this rig. Closing MATREX does not close independent tracker windows.";
            Action refresh=()=>{bool running=TrackerProcessManager.IsRunning(id);state.text=running?TrackerProcessManager.Status(id):"Independent tracker · opens with Start";stop.SetEnabled(running);open.SetEnabled(true);var caption=open.Q<Label>();if(caption!=null)caption.text=running?"Show tracker":"Open tracker";};
            refresh();row.schedule.Execute(refresh).Every(500);
        }
        private Button MovementChoice(string mode,string title,string detail,string icon)
        {
            var button=StudioButton("",null,()=>ChooseQuickStart(mode,CurrentWorld,DesktopQuickStart.IsQuickStart(Workspace)?CurrentDemo:true),"movement-card");button.name="quickstart-mode-"+mode;
            button.EnableInClassList("is-selected",CurrentMovement==mode);button.Add(new WorkbenchIcon(icon));
            var text=new VisualElement();text.style.flexGrow=1;button.Add(text);text.Add(SessionLabel(title,"movement-title"));text.Add(SessionLabel(detail,"movement-detail"));
            button.tooltip=mode=="flying"?"Flying: Rekkefly measures the difference between left and right wing angles to steer. Use demo input to explore without a camera.":"Walking: HejjéTrac follows a tracked ball and supplies movement and heading. Use demo input to explore without a camera.";return button;
        }
        private Button WorldChoice(string world,string title,string detail,string icon)
        {
            var button=StudioButton("",null,()=>ChooseQuickStart(CurrentMovement,world,DesktopQuickStart.IsQuickStart(Workspace)?CurrentDemo:true),"world-card");button.name="quickstart-world-"+world;
            button.EnableInClassList("is-selected",DesktopQuickStart.IsQuickStart(Workspace)&&CurrentWorld==world);
            var image=new Image {image=UnityEngine.Resources.Load<UnityEngine.Texture2D>("ConfigurationUI/Previews/"+world),scaleMode=UnityEngine.ScaleMode.ScaleAndCrop};image.AddToClassList("world-image");button.Add(image);
            var caption=Row("world-caption");caption.Add(new WorkbenchIcon(icon));caption.Add(SessionLabel(title,"world-title"));button.Add(caption);
            button.Add(SessionLabel(detail,"world-detail"));button.tooltip="Open the "+title+" starter sequence. Each movement/input/world combination keeps its own editable JSON files; switching back retains your edits.";return button;
        }
        private void RefreshStartCaption()
        {
            if(replayLibraryMode){RefreshLibraryFooter();return;}
            if(start==null)return;var label=start.Q<Label>();if(label!=null)label.text=ApparatusDraft?"Use configuration":CurrentDemo?"Start demo":"Start experiment";
            start.tooltip=ApparatusDraft?"Validate and save the apparatus, then return to the experiment workspace. The experiment does not start.":CurrentDemo?"Start this clearly marked demo, including the native tracker companion. No hardware is required.":"Save and validate the current sequence, connect its configured tracking input and start recording.";
        }
        private void BuildSessionDetails(VisualElement parent)
        {
            var details=Row("session-details"); parent.Add(details);
            SessionField(details,"Experiment name","session-experiment-name",metadata.ExperimentName,v=>metadata.ExperimentName=v,"Name this experiment");
            SessionField(details,"Experimenter","session-experimenter",metadata.ExperimenterName,v=>metadata.ExperimenterName=v,"Your name");
            SessionField(details,"Overall comments","session-comments",metadata.Comments,v=>metadata.Comments=v,"Notes for this session",true);

            parent.Add(SessionHeading("Animals","person","Names and IDs can be any text. Empty age means unspecified."));
            var rigs=Row("subject-grid"); parent.Add(rigs);
            foreach(var rig in (Workspace.System.Root["configs"] as JArray??new JArray()).OfType<JObject>())
            {
                string id=(string)rig["vrId"]; var fly=metadata.Animals.First(f=>f.VR==id);
                var card=new VisualElement { name="subject-"+id }; card.AddToClassList("subject-card"); rigs.Add(card);
                var heading=Row("subject-heading"); card.Add(heading); heading.Add(new WorkbenchIcon("person"));
                var label=new Label(id); label.AddToClassList("subject-name"); heading.Add(label);
                heading.Add(Pill(RigMode(rig),"accent"));
                heading.Add(IconButton("settings","More subject details for "+id,()=>ShowSubjectDetails(fly)));
                SessionField(card,"Animal ID","subject-id-"+id,fly.AnimalID,v=>fly.AnimalID=v,"Any name, label or ID");
                var species=SessionLabel("Species · "+(string.IsNullOrWhiteSpace((string)rig["speciesName"])?"Unspecified":(string)rig["speciesName"]),"session-hint");
                species.tooltip="Species is part of the apparatus configuration. Change it in Configure.";card.Add(species);
                var traits=Row("subject-traits"); card.Add(traits);
                var sexes=new List<string>{"Unspecified","Female","Male"};
                string sex=string.IsNullOrEmpty(fly.Sex)?"Unspecified":fly.Sex;
                if(!sexes.Contains(sex)) sexes.Add(sex); // Preserve labels from existing metadata.
                var sexField=new DropdownField("Sex",sexes,0) { name="subject-sex-"+id,value=sex,tooltip="Subject sex recorded with the run. Unspecified leaves it unclassified." }; traits.Add(sexField);
                sexField.RegisterValueChangedCallback(e=>fly.Sex=e.newValue);
                SessionField(traits,"Age (days)","subject-age-"+id,fly.AgeDays,v=>fly.AgeDays=v,"Unspecified");
                SessionField(card,"Comments","subject-comments-"+id,fly.Comments,v=>fly.Comments=v,"Notes for "+id,true);
                if(TrackerBackend.TryParse(rig["closedLoopMode"]?.ToString(),out var videoMode)&&(videoMode==ClosedLoopMode.Rekkefly||videoMode==ClosedLoopMode.Hejjetrac))
                {
                    var videoRow=Row("recording-video-option");videoRow.style.marginTop=8;videoRow.style.marginBottom=8;
                    var videoLabel=SessionLabel("Save tracker video","session-hint");videoLabel.style.flexGrow=1;videoLabel.style.flexBasis=0;videoRow.Add(videoLabel);
                    var video=StateSwitch(DesktopApparatusProfile.Flag(rig["tracker"]?["saveVideo"]),enabled=>StudioEdit(Workspace.System,r=>{
                        var current=(r["configs"] as JArray).OfType<JObject>().First(row=>(string)row["vrId"]==id);
                        if(!(current["tracker"] is JObject tracker)){tracker=new JObject();current["tracker"]=tracker;}tracker["saveVideo"]=enabled;
                    },false),"recording-save-video-"+id);
                    video.tooltip="Save native camera video alongside tracker frame/configuration logs. Uses additional disk space. Recording options preserve calibration. A running tracker using another profile must be closed explicitly before changing this option. Media remains pending until the native recorder closes cleanly; archival never stops your tracker.";
                    videoLabel.tooltip=video.tooltip;videoLabel.RegisterCallback<ClickEvent>(_=>video.value=!video.value);videoRow.Add(video);card.Add(videoRow);
                }
                AddTrackerControls(card,rig);
            }

        }
        private void ShowSubjectDetails(AnimalMetadata fly)
        {
            var details=OpenModal(fly.VR+" · subject details","Additional metadata for this subject.");
            SessionField(details,"Starved since (hours)","subject-starved-"+fly.VR,fly.StarvedSinceHours,v=>fly.StarvedSinceHours=v,"Unspecified");
        }
        private string SystemHeadline()
        {
            var rigs=(Workspace.System.Root["configs"] as JArray??new JArray()).OfType<JObject>().ToList();
            string modes=string.Join(" / ",rigs.Select(RigMode).Distinct());
            string panels=string.Join(" / ",rigs.Select(r=>((string)r["displayOrder"]??"").Length+" panels per VR · "+(r["ledPanelWidth"]?.ToString()??"128")+" × "+(r["ledPanelHeight"]?.ToString()??"128")+" · "+r["displayOrder"]).Distinct());
            string inputs=string.Join(", ",rigs.Select(r=>r["vrId"]+" "+(r["zmqAddress"]?.ToString()??"localhost")+":"+(r["zmqPort"]?.ToString()??"9872")));
            var telemetry=Workspace.System.Root["telemetry"] as JObject;
            string output=ExperimentBlocks.Flag(telemetry?["enabled"],true)?"Telemetry → "+(telemetry?["bindAddress"]?.ToString()??"127.0.0.1")+":"+(telemetry?["port"]?.ToString()??"9880"):"Telemetry off";
            return modes+" · "+rigs.Count+" VRs · "+panels+"\nClosed loop input: "+inputs+"\n"+output;
        }
        private static string RigMode(JObject rig)
        {
            string value=rig["closedLoopMode"]?.ToString()??"FicTrac";
            return TrackerBackend.TryParse(value,out ClosedLoopMode mode)?TrackerBackend.DisplayName(mode):value;
        }
        private void AddTrackerControls(VisualElement parent,JObject rig)
        {
            string id=(string)rig["vrId"];
            if(string.IsNullOrWhiteSpace(id)) return;
            var launch=rig["tracker"] as JObject;
            bool configured=!string.IsNullOrWhiteSpace((string)launch?["executablePath"]);
            // A running independent tracker keeps its explicit Stop control even
            // after optional launch configuration is cleared.
            if(!configured && !TrackerProcessManager.IsRunning(id)) return;
            var container=new VisualElement();container.AddToClassList("tracker-controls");parent.Add(container);
            var state=new Label();state.style.fontSize=12;state.style.whiteSpace=WhiteSpace.Normal;container.Add(state);
            var row=Row();container.Add(row);
            var start=StudioButton("Launch tracker","play",()=>{
                RequireApplied();
                TrackerProcessManager.Instance.Launch(LosslessJson.ToRuntime<SystemConfig>(rig));
                UpdateStatus(id+" tracker launched. Verify tracking input before starting the experiment.");
            },"quiet");start.name="tracker-launch-"+id;row.Add(start);
            var stop=StudioButton("Stop tracker","stop",()=>{
                TrackerProcessManager.Instance.Stop(id);UpdateStatus(id+" tracker stopped.");
            },"quiet");stop.name="tracker-stop-"+id;row.Add(stop);
            stop.tooltip="Stop only the tracker MATREX launched for this rig. Save calibration edits in the tracker before stopping; an unresponsive process is terminated.";
            Action refresh=()=>{ bool running=TrackerProcessManager.IsRunning(id);state.text=running?TrackerProcessManager.Status(id):configured?"Tracker app · ready to launch":"Tracker launch is not configured";state.tooltip=TrackerProcessManager.Status(id);start.SetEnabled(configured&&!running);stop.SetEnabled(running); };
            refresh();row.schedule.Execute(refresh).Every(500);
        }
        private void BindSessionBlock(VisualElement holder,int index)
        {
            holder.Clear(); holder.AddToClassList("session-plan-holder"); var block=Block(index); if(block==null) return;
            var row=Row("session-plan-row"); holder.Add(row);
            var number=new Label((index+1).ToString("00")); number.AddToClassList("block-number"); row.Add(number);
            row.Add(new WorkbenchIcon((string)block["sceneName"]=="Swarm"?"swarm":"motion"));
            var text=new VisualElement(); text.style.flexGrow=1; text.style.flexBasis=0; row.Add(text);
            var name=new Label(ExperimentBlocks.Title(BlockReference(block))); name.AddToClassList("session-plan-title"); text.Add(name);
            var config=BlockDocument(index)?.Root as JObject;
            var description=new Label(ExperimentBlocks.Describe(config)); description.AddToClassList("session-plan-description"); text.Add(description);
            row.Add(Pill((string)block["sceneName"]));
            row.Add(Pill(ExperimentBlocks.Time(ExperimentBlocks.EffectiveDuration(config,ExperimentBlocks.Number(block["duration"])))));
            row.Add(IconButton("settings","Edit experiment "+(index+1),()=>EditBlock(index)));
        }
        private void BuildExperimentConfigs()
        {
            body.Add(SessionLabel("Experiment configs","title"));
            body.Add(SessionLabel("The reusable configs in the current sequence. Changes apply to every block using the same file.","studio-subtitle"));
            var references=BuilderSteps.OfType<JObject>().Select((step,i)=>new { step,i,doc=BlockDocument(i) }).Where(x=>x.doc!=null).ToList();
            var scroll=new ScrollView(); scroll.AddToClassList("settings-scroll"); body.Add(scroll);
            foreach(var group in references.GroupBy(x=>x.doc.Path))
            {
                var doc=group.First().doc; var card=StudioButton("","file",()=>Select(doc),"experiment-link"); scroll.Add(card);
                var text=new VisualElement(); text.style.flexGrow=1; card.Add(text);
                text.Add(SessionLabel(ExperimentBlocks.Title(doc.Path),"session-plan-title"));
                text.Add(SessionLabel(ExperimentBlocks.Describe(doc.Root as JObject),"session-plan-description"));
                card.Add(Pill(group.Count()+" block"+(group.Count()==1?"":"s"))); card.Add(new Label("Configure  ›")); card.tooltip=doc.Path;
            }
            if(references.Count==0) scroll.Add(new Label("No referenced experiment configs yet. Add a block in Sequence config."));
            UpdateStatus();
        }
        private Label SessionLabel(string text,string css) { var label=new Label(text); label.AddToClassList(css); return label; }
        private void ShowReplayLauncher()=>ShowRecordingLibrary();
        private void ShowEditorTools()
        {
            var dialog=OpenModal("Config tools","Every JSON value remains accessible, including custom extensions.");
            dialog.Add(StudioButton("All parameters","settings",()=>{ CloseModal(); advancedMode=true; BuildContent(); }));
            dialog.Add(StudioButton("Edit JSON","file",()=>{ CloseModal(); jsonMode=true; BuildContent(); }));
            dialog.Add(StudioButton("Open another file","file",()=>{ CloseModal(); ShowOpenFile(); }));
            dialog.Add(StudioButton("Save a variation","copy",()=>{ CloseModal(); ShowVariation(null); }));
            var revert=StudioButton("Revert to saved JSON","loop",()=>{ selected.RevertToSaved(); CloseModal(); BuildNavigation(); BuildContent(); });
            revert.tooltip="Restore this file’s last saved values. Undo restores your draft, including custom fields.";
            dialog.Add(revert);
        }
    }
}
