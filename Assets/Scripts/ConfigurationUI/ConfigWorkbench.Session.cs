using System;
using System.Collections.Generic;
using System.IO;
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
                var item=Row("sequence-option");flags.Add(item);
                item.Add(new WorkbenchIcon(key=="autoStart"?"auto":key=="loop"?"loop":"shuffle"));
                string text=key=="autoStart"?"Auto start":key=="loop"?"Loop":"Shuffle";
                var toggle=StateSwitch(ExperimentBlocks.Flag(Workspace.Sequence.Root[key],key=="loop"),
                    on=>StudioEdit(Workspace.Sequence,r=>r[flag]=on,false),"sequence-"+key);
                toggle.text=text;item.Add(toggle);
                toggle.tooltip=key=="autoStart"?"Start the saved sequence on the next application launch. This session starts with Start experiment.":key=="loop"?"Repeat the sequence from the chosen block.":"Shuffle complete blocks or phase groups, retaining their internal order.";
            }
            var loopStart=new IntegerField("Repeat from block") {
                name="sequence-loop-start", value=(int?)Workspace.Sequence.Root["loopStartIndex"]+1??1,isDelayed=true,
                tooltip="1 repeats every block. 2 runs the first block once, then repeats from the second block. Available with looping and listed order." };
            BindDelayedInteger(loopStart,number=>StudioEdit(Workspace.Sequence,r=>r["loopStartIndex"]=Math.Max(0,number-1),false));
            flags.Add(loopStart);
            BindViewState(loopStart,()=>loopStart.SetEnabled(ExperimentBlocks.Flag(Workspace.Sequence.Root["loop"],true)&&!ExperimentBlocks.Flag(Workspace.Sequence.Root["randomise"])));
            var reproducibility=Row("sequence-seed");flags.Add(reproducibility);
            string selectedMode=(string)Workspace.Sequence.Root["randomSeedMode"]??"Fixed";
            bool randomMode=string.Equals(selectedMode,"Random",StringComparison.OrdinalIgnoreCase);
            var mode=new DropdownField("Random seed",new List<string>{"Fixed","Random"},randomMode?1:0){name="sequence-seed-mode"};
            mode.tooltip="Fixed repeats the configured seed. Random creates a new seed for each run and records it for replay.";
            mode.RegisterValueChangedCallback(e=>StudioEdit(Workspace.Sequence,r=>r["randomSeedMode"]=e.newValue,false));reproducibility.Add(mode);
            var seed=new IntegerField("Seed") {name="sequence-random-seed",value=(int?)Workspace.Sequence.Root["randomSeed"]??1,isDelayed=true};
            seed.tooltip="Explicit seed for experiment random choices, including shuffled block order. The effective seed is saved with the run.";
            BindViewState(seed,()=>seed.SetEnabled(!string.Equals((string)Workspace.Sequence.Root["randomSeedMode"],"Random",StringComparison.OrdinalIgnoreCase)));
            BindDelayedInteger(seed,number=>StudioEdit(Workspace.Sequence,r=>r["randomSeed"]=number,false));reproducibility.Add(seed);
            return flags;
        }
        private TextField SessionField(VisualElement parent,string label,string name,string value,Action<string> change,string placeholder,bool multiline=false)
        {
            var field=new TextField(label) { name=name,value=value??"",multiline=multiline };
            field.tooltip=label+". "+placeholder+". Saved in this run's subject/session metadata.";
            field.textEdition.placeholder=placeholder; field.AddToClassList("session-field");
            if(multiline) field.AddToClassList("session-comments");
            field.RegisterValueChangedCallback(e=>{if(e.target!=field)return;change(e.newValue);UpdateStatus();}); parent.Add(field); return field;
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
            VisualElement sequenceParent=scroll;
            if(ApparatusConfigured)BuildDailyRunHeader(scroll);
            else
            {
                var overview=Row("home-overview");scroll.Add(overview);
                var launcher=new VisualElement();launcher.AddToClassList("home-launcher");overview.Add(launcher);BuildDemoLauncher(launcher);
                sequenceParent=new VisualElement();sequenceParent.AddToClassList("home-sequence");overview.Add(sequenceParent);
            }
            string order=(ExperimentBlocks.Flag(Workspace.Sequence.Root["randomise"])?"Random order":"Listed order")+
                " · "+(ExperimentBlocks.Flag(Workspace.Sequence.Root["loop"],true)?"Repeats":"One pass");
            var planHeading=SessionHeading(ExperimentBlocks.Title(Workspace.Sequence.Root as JObject,Workspace.Sequence.Path),"sequence",BuilderSteps.Count+(BuilderSteps.Count==1?" block · ":" blocks · ")+SequenceDuration()+" · "+order);
            var load=StudioButton("Load","file",ShowSequenceLibrary,"quiet");load.name="session-load-sequence";load.tooltip="Choose a prepared terrain treatment or swarm/social sequence. The current apparatus and other drafts are retained.";planHeading.Add(load);
            var edit=StudioButton("Edit","settings",()=>Select(Workspace.Sequence),"quiet");edit.name="session-edit-sequence";planHeading.Add(edit);sequenceParent.Add(planHeading);
            var sequencePath=SessionLabel(System.IO.Path.GetRelativePath(RuntimePaths.ConfigRoot,Workspace.Sequence.Path).Replace('\\','/'),"config-reference");sequencePath.name="session-sequence-reference";sequenceParent.Add(sequencePath);
            var summaryIndices=Enumerable.Range(0,BuilderSteps.Count).ToList();
            var plan=new ListView(summaryIndices,64,()=>new VisualElement(),BindSessionBlock)
            {name="session-plan",selectionType=SelectionType.None,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight};
            plan.AddToClassList("session-plan");plan.style.height=Math.Min(5,Math.Max(1,BuilderSteps.Count))*64;sequenceParent.Add(plan);
            if(BuilderSteps.Count==0)sequenceParent.Add(StudioButton("Add your first block","plus",()=>{Select(Workspace.Sequence);ShowBlockLibrary();},"primary"));
            BuildRecordingSummary(scroll);
            var recording=new Foldout {name="recording-details",text="Animal & recording details",value=sessionDetailsOpen};recording.AddToClassList("recording-details");scroll.Add(recording);
            recording.tooltip="Animal labels, experimenter and notes saved with the recording. Species and tracking hardware are configured separately.";
            recording.RegisterValueChangedCallback(e=>sessionDetailsOpen=e.newValue);BuildSessionDetails(recording);
            RefreshStartCaption();UpdateStatus();
        }
        private void BuildRecordingSummary(VisualElement parent)
        {
            var summary=Row("recording-summary");summary.name="recording-summary";parent.Add(summary);
            summary.Add(new WorkbenchIcon("camera"));
            var title=SessionLabel("Recording","recording-summary-title");summary.Add(title);
            var state=SessionLabel("","recording-summary-state");state.name="recording-summary-state";summary.Add(state);
            Action refresh=()=>{
                var videoRigs=DesktopApparatusProfile.Rigs(Workspace.System.Root).Where(r=>TrackerBackend.TryParse(r["closedLoopMode"]?.ToString(),out var mode)&&(TrackerBackend.IsFlight(mode)||TrackerBackend.IsWalking(mode))).ToList();
                int videoCount=videoRigs.Count(r=>{JToken value=(r["tracker"] as JObject)?["saveVideo"];return value?.Type!=JTokenType.Boolean||(bool)value;});
                if(!ExperimentBlocks.Flag(Workspace.System.Root["saveTrackerVideo"],true))videoCount=0;
                state.text=videoCount==0?"Data only":videoCount==videoRigs.Count?"Data + tracker video":"Data + video · "+videoCount+" / "+videoRigs.Count;
            };refresh();state.schedule.Execute(refresh).Every(500);
            summary.tooltip="Tracking data and configuration snapshots are saved with each experiment. Tracker video follows each rig’s saveVideo setting. Change recording options in Animal & recording details before initializing tracking.";
            var swarm=StudioButton("Save swarm data","radio",()=>StudioEdit(Workspace.System,r=>{
                r["saveSwarmData"]=!ExperimentBlocks.Flag(r["saveSwarmData"],true);
            },false),"quiet");
            swarm.name="save-swarm-data";
            swarm.tooltip="Toggle virtual swarm recording for this run. Animal tracking and behavior are always recorded.";
            BindViewState(swarm,()=>{
                bool savePopulation=ExperimentBlocks.Flag(Workspace.System.Root["saveSwarmData"],true);
                swarm.Q<Label>().text=savePopulation?"Save swarm data":"Skip swarm data";
                swarm.EnableInClassList("is-on",savePopulation);
            });
            summary.Add(swarm);
            var video=StudioButton("Save tracker video","camera",()=>StudioEdit(Workspace.System,r=>{
                r["saveTrackerVideo"]=!ExperimentBlocks.Flag(r["saveTrackerVideo"],true);
            },false),"quiet");
            video.name="save-tracker-video";
            video.tooltip="Permit automatic camera video for this experiment. Each rig’s video and recorder-control settings still apply.";
            BindViewState(video,()=>{
                bool enabled=ExperimentBlocks.Flag(Workspace.System.Root["saveTrackerVideo"],true);
                video.Q<Label>().text=enabled?"Save tracker video":"Skip tracker video";
                video.EnableInClassList("is-on",enabled);
                refresh();
            });
            summary.Add(video);
            var options=StudioButton("Options","settings",()=>{
                sessionDetailsOpen=true;var details=root.Q<Foldout>("recording-details");if(details!=null){details.value=true;details.schedule.Execute(()=>details.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(details));}
            },"quiet");options.name="recording-summary-options";summary.Add(options);
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
            var options=new Foldout {name="demo-options",text="Demo options",value=false};options.AddToClassList("apparatus-advanced");parent.Add(options);
            var movement=new DropdownField("Demonstration",new List<string>{"Flying · simulated input","Walking · keyboard controls"},CurrentMovement=="walking"?1:0){name="quickstart-demo-mode"};
            movement.tooltip="This changes the hardware-free demo only. Configure real walking/flying inputs with the apparatus.";
            movement.RegisterValueChangedCallback(e=>ChooseQuickStart(e.newValue.StartsWith("Walking",StringComparison.Ordinal)?"walking":"flying",CurrentWorld,true));options.Add(movement);
            if(CurrentMovement=="walking")options.Add(SessionLabel(DesktopPreviewHud.WalkingDemoHelp,"apparatus-note"));
            if(!CurrentDemo)
            {
                foreach(var rig in DesktopApparatusProfile.Rigs(Workspace.System.Root))AddTrackerControls(options,rig);
                var preparation=StudioButton("Open tracker","camera",OpenQuickTracker,"quiet");
                preparation.name="initialize-tracking";
                preparation.tooltip="Open tracking to check input and calibration before the experiment starts.";
                parent.Add(preparation);
            }
            var links=Row("tracker-links");options.Add(links);AddSessionPlotLink(links);
            links.Add(StudioButton(CurrentMovement=="walking"?"About HejjéTrac":"About Rekkefly","external",()=>UnityEngine.Application.OpenURL(CurrentMovement=="walking"?"https://github.com/pvnkmrksk/hejjetrak":"https://github.com/pvnkmrksk/rekkefly"),"text-link"));
            var configure=Row("demo-configure");parent.Add(configure);configure.Add(new WorkbenchIcon("system"));
            var copy=SessionLabel("Have an apparatus? Configure its VRs, panels and tracker inputs.","apparatus-copy");copy.style.flexGrow=1;configure.Add(copy);
            var button=StudioButton("Configure","settings",OpenApparatusConfiguration,"quiet");button.name="demo-configure";configure.Add(button);
        }
        private void BuildDailyRunHeader(VisualElement parent)
        {
            var welcome=new VisualElement();welcome.AddToClassList("daily-welcome");parent.Add(welcome);
            welcome.Add(SessionLabel("Ready to run","start-title"));
            var rigs=DesktopApparatusProfile.Rigs(Workspace.System.Root).ToList();
            var summary=Row("daily-apparatus-summary");parent.Add(summary);summary.Add(new WorkbenchIcon("system"));
            var text=SessionLabel(rigs.Count+(rigs.Count==1?" VR":" VRs")+" · "+(DesktopApparatusProfile.Presentation(Workspace.System.Root)=="led"?"LED panels":"Desktop preview")+" · "+string.Join(" / ",rigs.Select(RigMode).Distinct()),"apparatus-copy");text.name="configured-apparatus-summary";text.tooltip=SystemHeadline();summary.Add(text);
            var links=Row("tracker-links");parent.Add(links);AddSessionPlotLink(links);
            if(rigs.Any(DesktopApparatusProfile.LocalNativeTracker))
            {
                var open=StudioButton("Open tracker","camera",OpenQuickTracker,"quiet");open.name="initialize-tracking";open.tooltip="Open or show the independent native tracker windows to check input before starting. This does not start the experiment or create a run recording. Camera calibration stays in those windows.";links.Add(open);
            }
            var state=new Foldout {name="session-tracker-windows",text="Trackers",value=false};state.AddToClassList("apparatus-advanced");parent.Add(state);
            foreach(var rig in rigs)
            {
                var panel=new Foldout {text=(string)rig["vrId"]+" · "+RigMode(rig),value=rigs.Count==1};
                state.Add(panel);AddTrackerControls(panel,rig);
            }
        }
        private void AddSessionPlotLink(VisualElement parent)
        {
            var plots=StudioButton("Plots","radio",DesktopDashboard.Open,"text-link");plots.name="open-live-plots";plots.tooltip="Open the optional independent live trajectory dashboard. Plotting is separate from stimulus rendering.";parent.Add(plots);
            var state=SessionLabel("","viewer-launch-state");parent.Add(state);
            state.schedule.Execute(()=>{if(!DesktopDashboard.Status.StartsWith("Optional",StringComparison.Ordinal))state.text=DesktopDashboard.Status;}).Every(500);
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
            start.tooltip=ApparatusDraft?"Validate and save the apparatus, then return to the experiment workspace. The experiment does not start.":CurrentDemo?"Start this clearly marked demo, with keyboard controls and simulated movement. No hardware is required.":"Save and validate the current sequence, connect its configured tracking input and start recording.";
        }
        private void BuildSessionDetails(VisualElement parent)
        {
            var details=Row("session-details"); parent.Add(details);
            SessionField(details,"Experiment name","session-experiment-name",metadata.ExperimentName,v=>metadata.ExperimentName=v,"Name this experiment");
            SessionField(details,"Experimenter","session-experimenter",metadata.ExperimenterName,v=>metadata.ExperimenterName=v,"Your name");
            SessionField(details,"Overall comments","session-comments",metadata.Comments,v=>metadata.Comments=v,"Notes for this session",true);

            var animalHeading=SessionHeading("Animals","person","Names and IDs can be any text. Empty age means unspecified.");parent.Add(animalHeading);
            var increment=StudioButton("Next animal IDs","next",()=>{
                var active=(Workspace.System.Root["configs"] as JArray??new JArray()).OfType<JObject>().Select(r=>(string)r["vrId"]).ToList();
                previousAnimalIds=metadata.Animals.Where(a=>active.Contains(a.VR)).ToDictionary(a=>a.VR,a=>a.AnimalID);
                for(int index=0;index<active.Count;index++)
                {var animal=metadata.Animals.First(a=>a.VR==active[index]);animal.AnimalID=ExperimentBlocks.NextAnimalId(animal.AnimalID,active.Count,index+1);}
                BuildContent();UpdateStatus("Animal IDs advanced by "+active.Count+". Labels without a trailing number are retained.");
            },"quiet");increment.name="increment-animal-ids";increment.tooltip="Advance every numeric or numbered animal ID by the current VR count: 1–16 becomes 17–32 with 16 VRs. Empty IDs start at 1; text labels keep their prefix.";animalHeading.Add(increment);
            if(previousAnimalIds!=null)
                animalHeading.Add(StudioButton("Undo ID increment","back",()=>{
                    foreach(var animal in metadata.Animals)if(previousAnimalIds.TryGetValue(animal.VR,out string id))animal.AnimalID=id;
                    previousAnimalIds=null;BuildContent();
                },"quiet"));
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
                sexField.RegisterValueChangedCallback(e=>{fly.Sex=e.newValue;UpdateStatus();});
                SessionField(traits,"Age (days)","subject-age-"+id,fly.AgeDays,v=>fly.AgeDays=v,"Unspecified");
                SessionField(card,"Comments","subject-comments-"+id,fly.Comments,v=>fly.Comments=v,"Notes for "+id,true);
                if(TrackerBackend.TryParse(rig["closedLoopMode"]?.ToString(),out var videoMode)&&(TrackerBackend.IsFlight(videoMode)||TrackerBackend.IsWalking(videoMode)))
                {
                    var videoRow=Row("recording-video-option");videoRow.style.marginTop=8;videoRow.style.marginBottom=8;
                    var videoLabel=SessionLabel("Save tracker video","session-hint");videoLabel.style.flexGrow=1;videoLabel.style.flexBasis=0;videoRow.Add(videoLabel);
                    JToken videoSetting=(rig["tracker"] as JObject)?["saveVideo"];
                    var video=StateSwitch(videoSetting?.Type==JTokenType.Boolean?(bool)videoSetting:true,enabled=>StudioEdit(Workspace.System,r=>{
                        var current=(r["configs"] as JArray).OfType<JObject>().First(row=>(string)row["vrId"]==id);
                        if(!(current["tracker"] is JObject tracker)){tracker=new JObject();current["tracker"]=tracker;}tracker["saveVideo"]=enabled;
                    },false),"recording-save-video-"+id);
                    video.tooltip="Save native camera video alongside tracker frame/configuration logs. Automatic requests video from compatible native endpoints. Unavailable recording produces a warning and the VR continues. Manual leaves recording to the operator or external system.";
                    videoLabel.tooltip=video.tooltip;videoLabel.RegisterCallback<ClickEvent>(_=>video.value=!video.value);videoRow.Add(video);card.Add(videoRow);
                    var recordingControls=new VisualElement {name="recording-control-"+id};recordingControls.AddToClassList("recording-control-mode");card.Add(recordingControls);
                    var controlModes=new List<string>{"Automatic · experiment","Manual · external"};
                    var recordingMode=new DropdownField("Recorder control",controlModes,ManualRecorder(rig["tracker"]?["recordingControl"])?1:0){name="recording-control-mode-"+id};
                    recordingMode.tooltip="Automatic starts and stops the recorder for each experiment and verifies acknowledgement. Manual never sends recorder commands and does not promise automatic media collection.";recordingControls.Add(recordingMode);
                    recordingMode.RegisterValueChangedCallback(evt=>StudioEdit(Workspace.System,r=>{
                        var current=(r["configs"] as JArray).OfType<JObject>().First(row=>(string)row["vrId"]==id);
                        if(!(current["tracker"] is JObject tracker)){tracker=new JObject();current["tracker"]=tracker;}
                        bool manual=controlModes.IndexOf(evt.newValue)==1;tracker["recordingControl"]=tracker["recordingControl"]?.Type==JTokenType.Integer?new JValue((int)(manual?TrackerRecordingControlMode.Manual:TrackerRecordingControlMode.Experiment)):new JValue(manual?"Manual":"Experiment");
                    },false));
                    BindViewState(recordingControls,()=>recordingControls.SetEnabled(ExperimentBlocks.Flag((Workspace.System.Root["configs"] as JArray).OfType<JObject>().First(row=>(string)row["vrId"]==id)["tracker"]?["saveVideo"],true)));
                    var controlHelp=SessionLabel("","recording-control-help");recordingControls.Add(controlHelp);
                    BindViewState(controlHelp,()=>{controlHelp.text=ManualRecorder((Workspace.System.Root["configs"] as JArray).OfType<JObject>().First(row=>(string)row["vrId"]==id)["tracker"]?["recordingControl"])?"Recorder controlled externally. VR start/stop does not change it.":"Automatic video when a compatible tracker is available. The VR can start without it.";controlHelp.tooltip=controlHelp.text;});
                    var recordingState=SessionLabel("","recording-control-status");recordingState.name="recording-control-status-"+id;card.Add(recordingState);
                    Action refreshRecording=()=>{recordingState.text=TrackerProcessManager.RecordingStatus(id);recordingState.tooltip=recordingState.text;};
                    refreshRecording();recordingState.schedule.Execute(refreshRecording).Every(300);
                }
                AddTrackerControls(card,rig);
            }

        }
        private Dictionary<string,string> previousAnimalIds;
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
            if(string.Equals(value,"Kinefly",StringComparison.OrdinalIgnoreCase))return "Kinefly (legacy)";
            if(string.Equals(value,"FicTrac",StringComparison.OrdinalIgnoreCase))return "FicTrac (legacy)";
            return TrackerBackend.TryParse(value,out ClosedLoopMode mode)?TrackerBackend.DisplayName(mode):value;
        }
        private void AddTrackerControls(VisualElement parent,JObject rig)
        {
            string id=(string)rig["vrId"];
            if(string.IsNullOrWhiteSpace(id))return;
            var container=new VisualElement();container.AddToClassList("tracker-controls");parent.Add(container);
            var state=SessionLabel("","apparatus-note");container.Add(state);
            var row=Row();container.Add(row);
            var start=StudioButton("Launch tracker","play",()=>OpenTracker(id),"quiet");start.name="tracker-launch-"+id;row.Add(start);
            var show=StudioButton("Show","external",()=>TrackerProcessManager.Instance.Show(id),"quiet");row.Add(show);
            var stop=StudioButton("Stop tracker","stop",()=>{
                TrackerProcessManager.Instance.Stop(id);UpdateStatus(id+" tracker stopped.");
            },"quiet");stop.name="tracker-stop-"+id;row.Add(stop);
            stop.tooltip="Stop only the tracker MATREX launched for this rig. Save calibration in its window first.";
            bool supportedBackend=TrackerBackend.TryParse(rig["closedLoopMode"]?.ToString(),out var mode);
            bool canConfigure=!CurrentDemo&&supportedBackend&&
                (TrackerBackend.IsFlight(mode)||TrackerBackend.IsWalking(mode))&&DesktopApparatusProfile.LocalAddress((string)rig["zmqAddress"]);
            JObject CurrentRig(JToken root)=>DesktopApparatusProfile.Rigs(root).SingleOrDefault(r=>(string)r["vrId"]==id);
            string TrackerValue(string key)=>(string)CurrentRig(Workspace.System.Root)?["tracker"]?[key]??"";
            void SetTrackerValue(string key,string value)
            {
                StudioEdit(Workspace.System,root=>{
                    var current=CurrentRig(root);
                    if(current==null)throw new ArgumentException("The selected rig changed. Reopen its tracker settings.");
                    if(!(current["tracker"] is JObject tracker)){tracker=new JObject();current["tracker"]=tracker;}
                    tracker[key]=value;
                },false);
            }
            string detected=canConfigure?TrackerInstallation.Find(mode):"";
            string EffectiveLauncher()
            {
                string configured=TrackerValue("executablePath");
                return string.IsNullOrWhiteSpace(configured)?detected:configured;
            }
            Action refresh=()=>{
                bool running=TrackerProcessManager.IsRunning(id);
                bool hasLauncher=canConfigure&&!string.IsNullOrWhiteSpace(EffectiveLauncher());
                bool hasCalibration=canConfigure&&!string.IsNullOrWhiteSpace(TrackerValue("configFile"));
                state.text=running?TrackerProcessManager.Status(id):!canConfigure?"Tracking input is managed independently.":
                    !hasLauncher?"Tracker launcher was not detected. Install "+TrackerBackend.DisplayName(mode)+" or browse for its executable.":
                    !hasCalibration?"Select the tracker calibration file for "+id+".":"Installed tracker and calibration are ready.";
                start.SetEnabled(hasLauncher&&hasCalibration&&!running);show.SetEnabled(running);stop.SetEnabled(running);
            };
            refresh();row.schedule.Execute(refresh).Every(500);
            if(!canConfigure)return;

            var launcherRow=Row();container.Add(launcherRow);
            var launcher=ApparatusText("Tracker launcher","tracker-executable-"+id,EffectiveLauncher(),value=>
                SetTrackerValue("executablePath",string.IsNullOrWhiteSpace(value)?"":TrackerInstallation.ResolveSelection(value,mode)),
                "This rig uses the detected installation when no override is set. Browse for the executable or complete macOS app to select a different build.");
            launcher.style.flexGrow=1;launcherRow.Add(launcher);
            launcherRow.Add(StudioButton("Browse…","file",()=>BrowseFile("Choose "+TrackerBackend.DisplayName(mode)+" launcher",null,path=>
                SetTrackerValue("executablePath",TrackerInstallation.ResolveSelection(path,mode))),"quiet"));
            launcherRow.Add(StudioButton("Detect","search",()=>{
                detected=TrackerInstallation.Find(mode);
                if(string.IsNullOrWhiteSpace(TrackerValue("executablePath")))RefreshBoundText(launcher,detected);
                refresh();
                UpdateStatus(string.IsNullOrEmpty(detected)?"No installed tracker was detected. Browse for its launcher.":
                    string.IsNullOrWhiteSpace(TrackerValue("executablePath"))?"Detected "+detected:"Detected "+detected+". The configured override remains selected.");
            },"quiet"));
            BindViewState(launcher,()=>{RefreshBoundText(launcher,EffectiveLauncher());refresh();});

            var calibrationRow=Row();container.Add(calibrationRow);
            var calibration=ApparatusText("Tracker calibration","tracker-calibration-"+id,TrackerValue("configFile"),
                value=>SetTrackerValue("configFile",value),"Choose the calibration/profile for this rig. MATREX passes it to the tracker without modifying it.");
            calibration.style.flexGrow=1;calibrationRow.Add(calibration);
            calibrationRow.Add(StudioButton("Browse…","file",()=>BrowseFile("Choose tracker calibration",null,path=>
                SetTrackerValue("configFile",Path.GetFullPath(path))),"quiet"));
            BindViewState(calibration,()=>RefreshBoundText(calibration,TrackerValue("configFile")));
        }
        private void BindSessionBlock(VisualElement holder,int index)
        {
            holder.Clear(); holder.AddToClassList("session-plan-holder"); var block=Block(index); if(block==null) return;
            var row=Row("session-plan-row"); holder.Add(row);
            var number=new Label((index+1).ToString("00")); number.AddToClassList("block-number"); row.Add(number);
            row.Add(new WorkbenchIcon((string)block["sceneName"]=="Swarm"?"swarm":"motion"));
            var text=new VisualElement(); text.style.flexGrow=1; text.style.flexBasis=0; row.Add(text);
            var name=new Label(BlockCaption(block)); name.AddToClassList("session-plan-title"); text.Add(name);
            var config=BlockDocument(index)?.Root as JObject;
            var description=new Label(BlockDescription(block,config)); description.AddToClassList("session-plan-description"); text.Add(description);
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
                text.Add(SessionLabel(ExperimentBlocks.Title(doc.Root as JObject,doc.Path),"session-plan-title"));
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
            dialog.Add(StudioButton("Effective JSON & defaults","file",ShowEffectiveConfiguration));
            dialog.Add(StudioButton("Keyboard shortcuts","help",ShowKeyboardHelp));
            dialog.Add(StudioButton("Open another file","file",()=>{ CloseModal(); ShowOpenFile(); }));
            dialog.Add(StudioButton("Save a variation","copy",()=>{ CloseModal(); ShowVariation(null); }));
            var revert=StudioButton("Revert to saved JSON","loop",()=>{ selected.RevertToSaved(); CloseModal(); BuildNavigation(); BuildContent(); });
            revert.tooltip="Restore this file’s last saved values. Undo restores your draft, including custom fields.";
            dialog.Add(revert);
        }
    }
}
