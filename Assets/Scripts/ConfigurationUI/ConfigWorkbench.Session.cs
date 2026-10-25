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
            RequireApplied(); CloseModal(); selected=Workspace.Sequence; path.Clear();
            homeMode=true; experimentIndexMode=false; metadataMode=jsonMode=advancedMode=false;
            BuildNavigation(); BuildContent();
        }
        private void ShowExperimentConfigs()
        {
            RequireApplied(); CloseModal(); path.Clear(); homeMode=false; experimentIndexMode=true;
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
            EnsureMetadata();
            var scroll=new ScrollView {name="start-workspace",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("session-scroll");body.Add(scroll);
            var welcome=new VisualElement();welcome.AddToClassList("start-welcome");scroll.Add(welcome);
            var eyebrow=SessionLabel("YOUR NEXT EXPERIMENT","eyebrow");welcome.Add(eyebrow);
            welcome.Add(SessionLabel("Ready when you are.","start-title"));
            welcome.Add(SessionLabel("Choose flying or walking, pick a world, and start. Your sequence is always yours to change.","start-subtitle"));

            var launch=Row("launch-workspace");scroll.Add(launch);
            var choices=new VisualElement();choices.AddToClassList("launch-choices");launch.Add(choices);
            choices.Add(SessionHeading("How are you moving?","motion"));
            var movements=Row("movement-choices");choices.Add(movements);
            movements.Add(MovementChoice("flying","Flying","Wing-angle control with Rekkefly","flying"));
            movements.Add(MovementChoice("walking","Walking","Ball tracking with HejjéTrac","walking"));
            var sourceRow=Row("tracking-choice");choices.Add(sourceRow);
            bool quick=DesktopQuickStart.IsQuickStart(Workspace);
            bool walkingDemo=quick&&CurrentDemo&&CurrentMovement=="walking";
            var source=new DropdownField("Tracking input",new List<string>{"Demo · no hardware needed","Camera · use my tracker"},CurrentDemo?0:1) {name="quickstart-input"};
            source.tooltip=walkingDemo?DesktopPreviewHud.WalkingDemoTooltip:"Demo publishes clearly marked simulated or prerecorded input so you can try the full loop. Camera uses the native tracker and your calibration. Camera selection and calibration remain in the tracker window.";
            sourceRow.Add(source);
            source.RegisterValueChangedCallback(e=>ChooseQuickStart(CurrentMovement,CurrentWorld,e.newValue.StartsWith("Demo",StringComparison.Ordinal)));
            var explanation=SessionLabel(walkingDemo?DesktopPreviewHud.WalkingDemoHelp:quick?(CurrentDemo?"Explore the complete loop now. No camera, animal or apparatus required.":"Your tracker opens independently. Configure its camera, then watch input freshness in the experiment."):"You are using a saved configuration. Choose a starter world to try the bundled experience; your files stay intact.","tracking-explanation");
            choices.Add(explanation);
            if(quick) AddQuickTrackerControls(choices);
            var links=Row("tracker-links");choices.Add(links);
            var repo=StudioButton(CurrentMovement=="walking"?"About HejjéTrac":"About Rekkefly","external",()=>UnityEngine.Application.OpenURL(CurrentMovement=="walking"?"https://github.com/pvnkmrksk/hejjetrak":"https://github.com/pvnkmrksk/rekkefly"),"text-link");
            repo.tooltip="Open the native tracker’s repository, downloads and calibration guide.";links.Add(repo);
            var plots=StudioButton("Live plots","radio",DesktopDashboard.Open,"text-link");plots.name="open-live-plots";plots.tooltip="Open the optional independent browser dashboard. It starts only when you request it; rendering and tracking do not depend on it.";links.Add(plots);
            var help=StudioButton("How it connects","help",()=>UnityEngine.Application.OpenURL("https://pvnkmrksk.github.io/matrexVR/manual/integrations.html"),"text-link");links.Add(help);
            var plotState=SessionLabel("","viewer-launch-state");choices.Add(plotState);
            plots.clicked+=()=>plotState.text=DesktopDashboard.Status;
            plotState.schedule.Execute(()=>{if(!DesktopDashboard.Status.StartsWith("Optional",StringComparison.Ordinal))plotState.text=DesktopDashboard.Status;}).Every(500);

            var worlds=new VisualElement();worlds.AddToClassList("world-choices");launch.Add(worlds);
            worlds.Add(SessionHeading("Start from a world","sky"));
            var previews=Row("world-previews");worlds.Add(previews);
            previews.Add(WorldChoice("night-sky","Night sky","Terrain beneath an astronomical sky.","night"));
            previews.Add(WorldChoice("forest","Forest","Explore a landscape of trees.","forest"));

            string order=(ExperimentBlocks.Flag(Workspace.Sequence.Root["randomise"])?"Random order":"Listed order")+
                " · "+(ExperimentBlocks.Flag(Workspace.Sequence.Root["loop"],true)?"Repeats":"One pass");
            var planHeading=SessionHeading("Your experiment sequence","sequence",BuilderSteps.Count+(BuilderSteps.Count==1?" block · ":" blocks · ")+SequenceDuration()+" · "+order);
            planHeading.Add(StudioButton("Edit sequence","settings",()=>Select(Workspace.Sequence),"quiet"));scroll.Add(planHeading);
            var summaryIndices=Enumerable.Range(0,BuilderSteps.Count).ToList();
            var plan=new ListView(summaryIndices,64,()=>new VisualElement(),BindSessionBlock)
            {name="session-plan",selectionType=SelectionType.None,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight};
            plan.AddToClassList("session-plan");plan.style.height=Math.Min(5,Math.Max(1,BuilderSteps.Count))*64;scroll.Add(plan);
            if(BuilderSteps.Count==0)scroll.Add(StudioButton("Add your first block","plus",()=>{Select(Workspace.Sequence);ShowBlockLibrary();},"primary"));

            var recording=new Foldout {name="recording-details",text="Animal & recording details",value=sessionDetailsOpen};recording.AddToClassList("recording-details");scroll.Add(recording);
            recording.tooltip="Optional labels, species, experimenter and notes saved with the recording. You can leave these unspecified while exploring.";
            recording.RegisterValueChangedCallback(e=>sessionDetailsOpen=e.newValue);
            BuildSessionDetails(recording);
            var apparatus=Row("apparatus-summary");scroll.Add(apparatus);
            var summary=SessionLabel("Apparatus is remembered between sessions. Change displays, rigs and tracking ports in Setup.","session-hint");summary.style.flexGrow=1;apparatus.Add(summary);
            var system=StudioButton("Apparatus setup","system",()=>Select(Workspace.System),"text-link");system.name="session-system-summary";system.tooltip=SystemHeadline();apparatus.Add(system);
            RefreshStartCaption();UpdateStatus();
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
            if(start==null)return;var label=start.Q<Label>();if(label!=null)label.text=CurrentDemo?"Start demo":DesktopQuickStart.IsQuickStart(Workspace)?"Start experiment":"Start saved experiment";
            start.tooltip=CurrentDemo?"Start this clearly marked demo, including the native tracker companion. No hardware is required.":"Save and validate the current sequence, connect its configured tracking input and start recording.";
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
                var species=SessionField(card,"Species","subject-species-"+id,fly.SpeciesName,v=>{
                    fly.SpeciesName=v; Workspace.System.Edit(r=>((JArray)r["configs"]).OfType<JObject>().First(c=>(string)c["vrId"]==id)["speciesName"]=v); UpdateStatus();
                },"Common or scientific name");
                species.tooltip="Species name saved as speciesName in system config and SpeciesName in this animal’s run metadata.";
                var traits=Row("subject-traits"); card.Add(traits);
                var sexes=new List<string>{"Unspecified","Female","Male"};
                string sex=string.IsNullOrEmpty(fly.Sex)?"Unspecified":fly.Sex;
                if(!sexes.Contains(sex)) sexes.Add(sex); // Preserve labels from existing metadata.
                var sexField=new DropdownField("Sex",sexes,0) { name="subject-sex-"+id,value=sex,tooltip="Subject sex recorded with the run. Unspecified leaves it unclassified." }; traits.Add(sexField);
                sexField.RegisterValueChangedCallback(e=>fly.Sex=e.newValue);
                SessionField(traits,"Age (days)","subject-age-"+id,fly.AgeDays,v=>fly.AgeDays=v,"Unspecified");
                SessionField(card,"Comments","subject-comments-"+id,fly.Comments,v=>fly.Comments=v,"Notes for "+id,true);
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
        private void ShowReplayLauncher()
        {
            RequireApplied();
            if(!UnityEngine.Application.CanStreamedLevelBeLoaded("ReplayScene"))
                throw new InvalidOperationException("ReplayScene is not included in this build.");
            var dialog=OpenModal("Replay a recorded run","Save this setup before leaving, then choose the recorded session folder in replay. Your JSON files and session details will be available when you return to setup.");
            dialog.Add(StudioButton("Save setup and open replay","play",()=>{
                RequireApplied(); Workspace.SaveAll(); SaveMetadata();
                UnityEngine.SceneManagement.SceneManager.LoadScene("ReplayScene");
            },"primary"));
            dialog.Add(StudioButton("Keep editing","settings",CloseModal,"quiet"));
        }
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
