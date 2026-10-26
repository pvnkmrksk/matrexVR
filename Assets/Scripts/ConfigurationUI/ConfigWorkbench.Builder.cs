using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private bool advancedMode;
        private int activeBlock;
        private string settingsTab="Essentials",activePhase="stimulus";
        private List<ExperimentPreset> presets;
        private ListView blockList;
        private readonly List<int> blockIndices=new List<int>();
        private VisualElement modal;
        private readonly HashSet<string> expandedSections=new HashSet<string>();
        private static string Pretty(string key)
        {
            var names=new Dictionary<string,string> { ["closedLoopOrientation"]="Closed loop orientation",["closedLoopPosition"]="Closed loop position",["closedLoopMode"]="Input mode",["vrId"]="VR ID",["ledPanelWidth"]="LED panel width",["ledPanelHeight"]="LED panel height",["vSyncCount"]="VSync count",["autoTrim"]="Auto-trim",["reloadScene"]="Reload environment",["autoStart"]="Start automatically",["randomise"]="Shuffle blocks",["loop"]="Loop sequence",["aglHeight"]="Height above terrain",["mu"]="Mean heading",["kappa"]="Heading concentration",["numberOfLocusts"]="Agent count",["agentCount"]="Agent count",["locustSpeed"]="Agent speed",["locoSpeed"]="Agent speed",["agentSpeed"]="Agent speed",["locustSpeedStdDev"]="Agent speed SD",["agentSpeedStdDev"]="Agent speed SD",["speciesName"]="Species name",["nightSky"]="Night sky",["zmqAddress"]="Input host",["zmqPort"]="Input port" };
            if(names.TryGetValue(key,out string label)) return label;
            string text=Human(key).ToLowerInvariant(); return string.IsNullOrEmpty(text)?text:char.ToUpperInvariant(text[0])+text.Substring(1);
        }
        private Button StudioButton(string text,string icon,Action action,string css=null)
        {
            var button=Button("",action,css); button.AddToClassList("studio-button");
            if(!string.IsNullOrEmpty(icon)) button.Add(new WorkbenchIcon(icon));
            if(!string.IsNullOrEmpty(text)) button.Add(new Label(text)); button.tooltip=text; return button;
        }
        private Button IconButton(string icon,string tooltip,Action action)
        { var button=StudioButton("",icon,action,"icon-button"); button.tooltip=tooltip; button.name=tooltip; return button; }
        private Label Pill(string text,string css=null)
        { var pill=new Label(text); pill.AddToClassList("pill"); if(css!=null) pill.AddToClassList(css); return pill; }
        private void BuildStudioNavigation()
        {
            side.Clear();side.AddToClassList("studio-sidebar");
            side.Add(new Label("EXPERIMENT") {name="navigation-title"});
            var session=StudioButton("Start","home",ShowSessionHome,homeMode?"nav-active":null);session.name="back-to-session";session.tooltip="Review and start your experiment sequence, or explore the demonstration before configuring hardware.";side.Add(session);
            var sequence=StudioButton("Sequence","sequence",()=>Select(Workspace.Sequence),!homeMode&&!experimentIndexMode&&!replayLibraryMode&&selected==Workspace.Sequence?"nav-active":null);sequence.name="open-sequence";sequence.tooltip="Arrange blocks, timing and repetitions. Open a block to change its parameters.";side.Add(sequence);
            var experiments=StudioButton("Experiment settings","settings",ShowExperimentConfigs,experimentIndexMode||!homeMode&&!replayLibraryMode&&selected!=Workspace.Sequence&&selected!=Workspace.System?"nav-active":null);experiments.name="open-experiments";experiments.tooltip="Reusable stimulus, movement and scene parameters for the current sequence.";side.Add(experiments);
            var replay=StudioButton("Replay recordings","play",ShowReplayLauncher,replayLibraryMode?"nav-active":null);replay.name="open-recorded-replay";replay.tooltip="Open a recorded session and inspect its timeline and overview.";side.Add(replay);
            var files=new Foldout {name="navigation-files",text="In this sequence",value=!homeMode&&!replayLibraryMode};files.AddToClassList("nav-foldout");side.Add(files);
            var linked=new HashSet<string>(BuilderSteps.OfType<JObject>().Select(BlockReference).Where(r=>!string.IsNullOrEmpty(r)).Select(r=>Path.GetFullPath(ExperimentConfigFiles.Resolve(r))));
            foreach(var doc in Workspace.Documents.Where(d=>d.SchemaId!="sequence"&&d.SchemaId!="system"&&linked.Contains(d.Path)))
            {
                var button=StudioButton(ExperimentBlocks.Title(doc.Path)+(doc.Dirty?" •":""),doc.SchemaId=="swarm"?"swarm":"file",()=>Select(doc),!homeMode&&!experimentIndexMode&&selected==doc?"file-active":null);
                button.AddToClassList("nav-file");button.tooltip="Edit "+doc.Path;files.Add(button);
            }
            var library=new Foldout {name="navigation-library",text="Files & library",value=false};library.AddToClassList("nav-foldout");side.Add(library);
            library.Add(StudioButton("Add a block","plus",()=>ShowBlockLibrary()));library.Add(StudioButton("Open JSON file","file",ShowOpenFile));library.Add(StudioButton("Save a variation","copy",()=>ShowVariation(null)));
            var spacer=new VisualElement();spacer.AddToClassList("nav-spacer");side.Add(spacer);
            side.Add(new Label("APPLICATION"));
            var system=StudioButton("Configure","system",()=>Select(Workspace.System),!homeMode&&!experimentIndexMode&&selected==Workspace.System?"nav-active":null);system.name="open-system";system.tooltip="Configure hardware, panel layouts, tracking inputs and species. Your apparatus is saved between sessions.";side.Add(system);
            side.Add(StudioButton("Documentation","help",()=>Application.OpenURL("https://pvnkmrksk.github.io/matrexVR/manual/"),"quiet"));
            side.Add(StudioButton("Quit","power",()=>Application.Quit(),"quiet"));
            foreach(var heading in side.Children().OfType<Label>())heading.AddToClassList("nav-heading");
        }
        private void BuildStudio()
        {
            body.AddToClassList("studio-body"); inspector.AddToClassList("studio-inspector");
            var top=Row("studio-title-row"); body.Add(top);
            var titles=new VisualElement(); titles.style.flexGrow=1; top.Add(titles);
            var title=new Label(selected==Workspace.Sequence?"Your experiment sequence":selected==Workspace.System?"Configure apparatus":ExperimentBlocks.Title(selected.Path)); title.AddToClassList("title"); titles.Add(title);
            var subtitle=new Label(selected==Workspace.Sequence?"Combine reusable configs. Drag to reorder. Open a block to edit its stimulus.":Path.GetRelativePath(RuntimePaths.ConfigRoot,selected.Path).Replace('\\','/')); subtitle.AddToClassList("studio-subtitle"); titles.Add(subtitle);
            top.Add(StudioButton("More","settings",ShowEditorTools,"quiet"));
            if(selected==Workspace.Sequence) BuildSequenceBuilder();
            else if(selected==Workspace.System) BuildSystemEditor();
            else BuildExperimentEditor();
            BuildStudioOverview(); UpdateStatus();
        }
        private JArray BuilderSteps => Workspace.Sequence.Root["sequences"] as JArray??new JArray();
        private JObject Block(int index) => index>=0 && index<BuilderSteps.Count?BuilderSteps[index] as JObject:null;
        private string BlockReference(JObject step) => (string)(step?["parameters"]?["configFile"]??step?["parameters"]?["design"]);
        private ConfigDocument BlockDocument(int index)
        {
            var step=Block(index); string reference=BlockReference(step); if(string.IsNullOrWhiteSpace(reference)) return null;
            try { return Workspace.Open(reference,ConfigSchema.ForScene((string)step["sceneName"])); } catch { return null; }
        }
        private void BuildSequenceBuilder()
        {
            var stats=Row("sequence-stats"); body.Add(stats);
            stats.Add(Pill(BuilderSteps.Count+" blocks","accent"));
            var total=Pill(SequenceDuration()+" total");total.name="sequence-total-duration";stats.Add(total);
            stats.Add(Pill(ExperimentBlocks.Flag(Workspace.Sequence.Root["randomise"])?"Shuffled order":"Listed order"));
            stats.Add(Pill(ExperimentBlocks.Flag(Workspace.Sequence.Root["loop"],true)?"Repeats":"One pass"));
            body.Add(BuildSequenceFlags());
            blockIndices.Clear(); blockIndices.AddRange(Enumerable.Range(0,BuilderSteps.Count));
            blockList=new ListView(blockIndices,156,()=>new VisualElement(),BindBlockCard) { selectionType=SelectionType.Single,reorderable=true,reorderMode=ListViewReorderMode.Animated,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight };
            blockList.AddToClassList("block-list"); body.Add(blockList);
            blockList.selectionChanged+=values=>{ FlushNumbers(); if(blockList.selectedIndex>=0) { activeBlock=blockList.selectedIndex; BuildStudioOverview(); UpdateStatus(); } };
            blockList.itemIndexChanged+=(from,to)=>{
                FlushNumbers();
                // The ListView reorders only its index list; mutate the original JSON array as one undoable edit.
                Workspace.Sequence.Edit(root=>ExperimentBlocks.Move(root,from,to)); activeBlock=to;
                root.schedule.Execute(()=>{ BuildContent(); BuildNavigation(); });
            };
            if(BuilderSteps.Count>0) blockList.SetSelectionWithoutNotify(new[]{Mathf.Clamp(activeBlock,0,BuilderSteps.Count-1)});
            if(BuilderSteps.Count==0) body.Add(new Label("Your sequence is empty. Add a config from the library to create the first block."));
            var add=Row("builder-add-row"); body.Add(add);
            add.Add(StudioButton("Add experiment block","plus",()=>ShowBlockLibrary(),"primary"));
            add.Add(new Label("Pick a file once — its complete configuration becomes the reusable brick."));
        }
        private void BindBlockCard(VisualElement holder,int index)
        {
            holder.Clear(); holder.AddToClassList("block-holder");
            JObject step=Block(index); if(step==null) { holder.Add(new Label("Invalid block · use All fields to repair it")); return; }
            var card=new VisualElement(); card.AddToClassList("block-card"); holder.Add(card);
            var heading=Row("block-heading"); card.Add(heading);
            var number=new Label((index+1).ToString("00")); number.AddToClassList("block-number"); heading.Add(number);
            string scene=(string)step["sceneName"]??"No scene"; heading.Add(new WorkbenchIcon(scene=="Swarm"?"swarm":scene=="Optomotor"?"settings":"motion"));
            var name=new Label(ExperimentBlocks.Title(BlockReference(step))); name.AddToClassList("block-name"); heading.Add(name);
            heading.Add(Pill(scene));
            heading.Add(IconButton("copy","Duplicate block",()=>DuplicateBlock(index)));
            heading.Add(IconButton("trash","Remove block",()=>RemoveBlock(index)));
            var doc=BlockDocument(index); JObject config=doc?.Root as JObject;
                var description=new Label(ExperimentBlocks.Describe(config)); description.AddToClassList("block-description"); card.Add(description);
            var bottom=Row("block-bottom"); card.Add(bottom);
            bottom.Add(Pill(ExperimentBlocks.Time(ExperimentBlocks.EffectiveDuration(config,ExperimentBlocks.Number(step["duration"])))));
            if(ExperimentBlocks.Phased(config)) bottom.Add(Pill("Phase timeline","muted-pill"));
            if(ExperimentBlocks.Flag(step["autoTrim"])) bottom.Add(Pill("Auto-trim ON","accent"));
            var reference=new Label(BlockReference(step)??"Choose a config"); reference.AddToClassList("block-reference"); bottom.Add(reference);
            bottom.Add(StudioButton("Edit config","settings",()=>EditBlock(index),"quiet"));
            bottom.Add(IconButton("file","Replace config",()=>ShowBlockLibrary(index)));
            card.RegisterCallback<PointerDownEvent>(e=>{ FlushNumbers(); activeBlock=index; BuildStudioOverview(); UpdateStatus(); });
        }
        public void AddPresetBlock(ExperimentPreset preset,string scene=null,int? replace=null)
        {
            RequireApplied();
            Workspace.Sequence.Edit(root=>{
                if(!(root["sequences"] is JArray)) root["sequences"]=new JArray();
                var steps=(JArray)root["sequences"];
                if(replace.HasValue)
                {
                    var step=steps[replace.Value] as JObject??throw new ArgumentException("Block must be an object.");
                    // Retain all tuning and extensions while changing only the selected scene/reference.
                    step["sceneName"]=scene??preset.Scene;
                    var parameters=step["parameters"] as JObject;
                    if(parameters==null) { step["parameters"]=new JObject(); parameters=(JObject)step["parameters"]; }
                    string key=preset.Schema=="dynamic-choice"?"design":"configFile";
                    parameters.Remove(key=="design"?"configFile":"design"); parameters[key]=preset.Reference;
                }
                else steps.Add(ExperimentBlocks.NewBlock(preset,scene));
            });
            activeBlock=replace??BuilderSteps.Count-1; CloseModal(); Select(Workspace.Sequence);
        }
        public void ReorderBlock(int from,int to) { Workspace.Sequence.Edit(root=>ExperimentBlocks.Move(root,from,to)); activeBlock=to; BuildContent(); }
        public void DuplicateBlock(int index)
        { activeBlock=index+1; StudioEdit(Workspace.Sequence,root=>ExperimentBlocks.Steps(root).Insert(index+1,ExperimentBlocks.Steps(root)[index].DeepClone())); }
        public void RemoveBlock(int index)
        { activeBlock=Math.Max(0,Math.Min(index,BuilderSteps.Count-2)); StudioEdit(Workspace.Sequence,root=>ExperimentBlocks.Steps(root).RemoveAt(index)); }
        private void EditBlock(int index)
        { var doc=BlockDocument(index); if(doc==null) throw new IOException("Choose a readable configuration for this block."); activeBlock=index; Select(doc); }
        private void StudioEdit(ConfigDocument doc,Action<JToken> change,bool rebuild=true)
        {
            if(rebuild) FlushNumbers();
            // Capture before changing the document: summaries and rig headings may change.
            var state=rebuild?WorkbenchViewState.Capture(root):null;
            doc.Edit(change);
            if(rebuild)
            {
                // A delayed field commits during the next control's pointer-down.
                // Keep that control alive through pointer-up so its click still runs.
                if(IsEditPointerHeld())DeferPointerEditRebuild();
                else RebuildEditedView(state);
            }
            else { RefreshStudioValues(); UpdateStatus(); }
        }
        private bool editPointerHeld;
        private bool editPointerUsesMouse;
        private int editPointerDownFrame=-1;
        private int pendingPointerGeneration;
        private ConfigDocument pendingPointerSelection;
        private IVisualElementScheduledItem pendingPointerRebuild;
        private IVisualElementScheduledItem pendingBlockRefresh;
        private ListView pendingBlockRefreshList;
        private int pendingBlockRefreshGeneration;
        private ConfigDocument pendingBlockRefreshSelection;
        private void InstallEditPointerTracking()
        {
            root.RegisterCallback<PointerDownEvent>(e=>{
                if(e.button!=0)return;
                editPointerHeld=true;editPointerUsesMouse=e.pointerType==UnityEngine.UIElements.PointerType.mouse;editPointerDownFrame=Time.frameCount;
            },TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(e=>{if(e.button==0)editPointerHeld=false;},TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(_=>editPointerHeld=false,TrickleDown.TrickleDown);
        }
        private bool IsEditPointerHeld()
        {
            // Captured ListView/button releases need not reach the root callback.
            // Read physical state on a later frame; never change input device state.
            if(editPointerHeld&&editPointerUsesMouse&&Time.frameCount>editPointerDownFrame)
            {
                var mouse=UnityEngine.InputSystem.Mouse.current;
                if(mouse!=null&&!mouse.leftButton.isPressed)editPointerHeld=false;
            }
            return editPointerHeld;
        }
        private void RebuildEditedView(WorkbenchViewState state)
        {
            BuildNavigation();BuildContent();
            int version=viewGeneration;state.Restore(root,()=>version==viewGeneration);
        }
        private void DeferPointerEditRebuild()
        {
            if(pendingPointerRebuild!=null&&pendingPointerGeneration==viewGeneration&&pendingPointerSelection==selected)return;
            pendingPointerRebuild?.Pause();pendingPointerGeneration=viewGeneration;pendingPointerSelection=selected;
            pendingPointerRebuild=root.schedule.Execute(()=>{
                if(!isActiveAndEnabled||preparing||root.panel==null||pendingPointerGeneration!=viewGeneration||pendingPointerSelection!=selected)
                {pendingPointerRebuild?.Pause();pendingPointerRebuild=null;return;}
                if(IsEditPointerHeld())return;
                pendingPointerRebuild.Pause();pendingPointerRebuild=null;
                // The clicked control may have opened a foldout or moved focus.
                // Preserve its completed action, rather than the pre-blur state.
                RebuildEditedView(WorkbenchViewState.Capture(root));
            }).Every(1);
        }
        private void ScheduleBlockItemsRefresh()
        {
            var list=blockList;if(list==null)return;
            if(pendingBlockRefresh!=null&&pendingBlockRefreshList==list&&pendingBlockRefreshGeneration==viewGeneration&&pendingBlockRefreshSelection==selected)return;
            pendingBlockRefresh?.Pause();pendingBlockRefreshList=list;
            pendingBlockRefreshGeneration=viewGeneration;pendingBlockRefreshSelection=selected;
            pendingBlockRefresh=root.schedule.Execute(()=>{
                if(!isActiveAndEnabled||preparing||root.panel==null||pendingBlockRefreshGeneration!=viewGeneration||pendingBlockRefreshSelection!=selected||blockList!=list)
                {pendingBlockRefresh?.Pause();pendingBlockRefresh=null;return;}
                if(IsEditPointerHeld())return;
                pendingBlockRefresh.Pause();pendingBlockRefresh=null;list.RefreshItems();
            }).Every(1);
        }
        private Toggle StateSwitch(bool value,Action<bool> changed,string name=null)
        {
            var toggle=new Toggle { name=name }; toggle.AddToClassList("studio-switch"); toggle.SetValueWithoutNotify(value);
            var track=new VisualElement(); track.AddToClassList("switch-track"); var knob=new VisualElement(); knob.AddToClassList("switch-knob"); track.Add(knob);
            var input=toggle.Q<VisualElement>(className:"unity-toggle__input"); (input??toggle).Add(track);
            var indicator=new Label(value?"ON":"OFF"); indicator.AddToClassList("switch-indicator"); (input??toggle).Add(indicator); toggle.EnableInClassList("is-on",value);
            toggle.RegisterValueChangedCallback(e=>{ indicator.text=e.newValue?"ON":"OFF"; toggle.EnableInClassList("is-on",e.newValue); Guard(()=>changed(e.newValue)); }); return toggle;
        }
        private string SequenceDuration()
        {
            double total=0;
            var cache=new Dictionary<string,JObject>();
            foreach(var step in BuilderSteps.OfType<JObject>())
            {
                string reference=BlockReference(step)??"";
                if(!cache.TryGetValue(reference,out JObject config))
                { string full=Path.GetFullPath(ExperimentConfigFiles.Resolve(reference)); config=Workspace.Documents.FirstOrDefault(d=>d.Path==full)?.Root as JObject; cache[reference]=config; }
                total+=ExperimentBlocks.EffectiveDuration(config,ExperimentBlocks.Number(step["duration"]));
            }
            return ExperimentBlocks.Time(total);
        }
        private void BuildStudioOverview()
        {
            if(advancedMode || jsonMode || metadataMode || path.Count>0) return;
            inspector.Clear(); inspector.Add(new Label("RUN OVERVIEW") { name="overview-title" });
            var big=new Label(SequenceDuration()); big.AddToClassList("overview-duration"); inspector.Add(big);
            inspector.Add(new Label(BuilderSteps.Count+" blocks · "+(Workspace.System.Root["configs"] as JArray)?.Count+" VR rigs"));
            inspector.Add(Pill(ExperimentBlocks.Flag(Workspace.Sequence.Root["loop"],true)?"LOOP ON · repeating":"ONE PASS","accent"));
            if(selected==Workspace.Sequence && Block(activeBlock) is JObject step)
            {
                inspector.Add(new Label("SELECTED BLOCK "+(activeBlock+1).ToString("00")));
                inspector.Add(new Label(ExperimentBlocks.Title(BlockReference(step))) { name="selected-block-title" });
                var config=BlockDocument(activeBlock)?.Root as JObject;
                inspector.Add(new Label(ExperimentBlocks.Describe(config)));
                if(ExperimentBlocks.Phased(config)) inspector.Add(new Label("Its phase timeline owns run timing. The outer duration is retained as a fallback."));
                var sceneChoices=ScenesFor(ConfigSchema.ForScene((string)step["sceneName"]));
                string scene=(string)step["sceneName"]??"Choice"; if(!sceneChoices.Contains(scene)) sceneChoices.Add(scene);
                var dropdown=new DropdownField("Environment",sceneChoices,Math.Max(0,sceneChoices.IndexOf(scene))) { tooltip="Unity scene used for this block. Its experiment file controls the stimulus and movement settings." }; inspector.Add(dropdown);
                dropdown.RegisterValueChangedCallback(e=>StudioEdit(Workspace.Sequence,r=>r["sequences"][activeBlock]["sceneName"]=e.newValue));
                var schema=(JObject)Workspace.Schemas.Get("sequence")["properties"]["sequences"]["items"];
                foreach(string key in new[]{"duration","gain","autoTrim","reloadScene"})
                {
                    var control=Setting(Workspace.Sequence,new[]{"sequences",activeBlock.ToString(),key},ConfigSchema.Child(schema,key),step,schema);
                    if(key=="duration" && ExperimentBlocks.Phased(config)) control.SetEnabled(false);
                    inspector.Add(control);
                }
                inspector.Add(StudioButton("Edit stimulus","settings",()=>EditBlock(activeBlock),"primary"));
                inspector.Add(StudioButton("Choose another config","file",()=>ShowBlockLibrary(activeBlock)));
                inspector.Add(StudioButton("Make an independent copy","copy",()=>ShowVariation(activeBlock)));
                inspector.Add(StudioButton("All block settings","settings",()=>{ advancedMode=true; path.Clear(); path.AddRange(new[]{"sequences",activeBlock.ToString()}); BuildContent(); },"quiet"));
            }
            else if(selected!=Workspace.System)
            {
                var payload=EditingPayload(); inspector.Add(new Label("CURRENT CONFIG"));
                bool off=ExperimentBlocks.Phased((JObject)selected.Root) && (!(selected.Root[activePhase] is JObject) || !ExperimentBlocks.Flag(payload["enabled"],activePhase=="stimulus"));
                if(off) inspector.Add(Pill(Pretty(activePhase)+" OFF","muted-pill"));
                inspector.Add(new Label(off?"Saved settings are inactive for this phase.":ExperimentBlocks.Describe(payload)) { name="config-effects" });
                int uses=BuilderSteps.OfType<JObject>().Count(s=>!string.IsNullOrEmpty(BlockReference(s)) && Path.GetFullPath(ExperimentConfigFiles.Resolve(BlockReference(s)))==selected.Path);
                inspector.Add(Pill(uses>1?"SHARED · used by "+uses+" blocks":uses==1?"Used by 1 block":"Not in this sequence",uses>1?"warm-pill":null));
                if(uses>1) inspector.Add(new Label("Edits here affect every block using this file. Make a variation to change only one."));
                inspector.Add(StudioButton("Back to sequence","sequence",()=>Select(Workspace.Sequence)));
            }
            else
            {
                inspector.Add(new Label("SYSTEM OUTPUT"));
                var rigs=(Workspace.System.Root["configs"] as JArray??new JArray()).OfType<JObject>().ToList();
                foreach(var mode in rigs.GroupBy(RigMode)) inspector.Add(Pill(mode.Key+" · "+mode.Count()+" rigs","accent"));
                inspector.Add(new Label(SystemSyncSummary()) { name="system-sync" });
                inspector.Add(new Label(SystemDisplaySummary()) { name="system-display" });
                inspector.Add(Pill("Telemetry "+(ExperimentBlocks.Flag(Workspace.System.Root["telemetry"]?["enabled"],true)?"ON":"OFF")));
                inspector.Add(Pill("Overview camera "+(ExperimentBlocks.Flag(Workspace.System.Root["overheadCamera"]?["enabled"],true)?"ON":"OFF")));
            }
            var health=new VisualElement(); health.AddToClassList("health-card"); inspector.Add(health); health.Add(new Label("CONFIGURATION CHECK")); summary=new Label(); health.Add(summary);
        }
        private string SystemSyncSummary()
        { int sync=(int)ExperimentBlocks.Number(Workspace.System.Root["vSyncCount"]); return sync>0?"vSync /"+sync+" · software FPS target overridden":"Software FPS target: "+(Workspace.System.Root["targetFrameRate"]?.ToString()??"120"); }
        private string SystemDisplaySummary() => "Display "+(Workspace.System.Root["targetDisplay"]?.ToString()??"1")+" · "+(Workspace.System.Root["configs"] as JArray??new JArray()).OfType<JObject>().Sum(r=>(r["displayOrder"]?.ToString()??"").Length)+" configured camera faces";
        private List<string> ScenesFor(string schema)
        {
            var list=new List<string>(); for(int i=0;i<SceneManager.sceneCountInBuildSettings;i++) { string name=Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)); if(name!="ControlScene" && name!="ReplayScene" && ConfigSchema.ForScene(name)==schema) list.Add(name); }
            if(list.Count==0) list.Add(schema=="swarm"?"Swarm":schema=="kannadi"?"Kannadi":schema=="optomotor"?"Optomotor":schema=="dynamic-choice"?"Choice_desync":"Choice"); return list;
        }
        private VisualElement OpenModal(string title,string subtitle)
        {
            CloseModal(); modal=new VisualElement(); modal.AddToClassList("modal-shade"); root.Add(modal);
            var dialog=new VisualElement(); dialog.AddToClassList("studio-dialog"); modal.Add(dialog);
            var head=Row(); dialog.Add(head); var text=new Label(title); text.AddToClassList("title"); head.Add(text); text.style.flexGrow=1;
            head.Add(Button("Close",CloseModal,"quiet")); dialog.Add(new Label(subtitle)); return dialog;
        }
        private void CloseModal() { modal?.RemoveFromHierarchy(); modal=null; }
        private void ShowBlockLibrary(int? replace=null)
        {
            RequireApplied(); presets=ExperimentBlocks.Catalog(RuntimePaths.ConfigRoot);
            foreach(var preset in presets)
            { string full=Path.GetFullPath(ExperimentConfigFiles.Resolve(preset.Reference)); var edited=Workspace.Documents.FirstOrDefault(d=>d.Path==full); if(edited!=null) preset.Config=(JObject)edited.Root.DeepClone(); }
            var dialog=OpenModal(replace.HasValue?"Choose a new config":"Add an experiment block","Complete configurations become reusable blocks. No parameter-by-parameter setup.");
            var controls=Row(); dialog.Add(controls);
            var find=new TextField("Search") { name="preset-search" }; find.style.flexGrow=1; controls.Add(find);
            var category=new DropdownField(new List<string>{"All","Flight","Swarm","Walking","Optomotor","Choice","Dynamic choice","Custom","Archive"},0); controls.Add(category);
            var layout=Row("library-layout"); dialog.Add(layout); var visible=new List<ExperimentPreset>(presets); ExperimentPreset chosen=null;
            var preview=new ScrollView(); preview.AddToClassList("library-preview");
            var scenes=new DropdownField("Environment",ScenesFor("choice"),0); scenes.name="preset-scene";
            var insert=StudioButton(replace.HasValue?"Use this config":"Add block","plus",()=>{ if(chosen!=null) AddPresetBlock(chosen,scenes.value,replace); },"primary"); insert.name="insert-preset"; insert.SetEnabled(false);
            var list=new ListView(visible,82,()=>new VisualElement(),(view,index)=>{
                view.Clear(); var p=visible[index]; view.AddToClassList("preset-card"); var line=Row(); view.Add(line); line.Add(new WorkbenchIcon(p.Schema=="swarm"?"swarm":p.Category=="Flight"?"motion":"file")); line.Add(new Label(p.Title)); line.Add(Pill(p.Category));
                line.Q<Label>().AddToClassList("preset-name");
                var location=new Label(p.Reference); location.AddToClassList("key-hint"); view.Add(location);
            }) { selectionType=SelectionType.Single,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight };
            list.name="preset-list"; list.AddToClassList("preset-list"); layout.Add(list); layout.Add(preview);
            Action update=()=>{ visible.Clear(); visible.AddRange(presets.Where(p=>(category.value=="All" || p.Category==category.value) && (p.Reference+" "+p.Title+" "+p.Description).IndexOf(find.value??"",StringComparison.OrdinalIgnoreCase)>=0)); chosen=null; list.ClearSelection(); list.Rebuild(); preview.Clear(); insert.SetEnabled(false); };
            bool archivesLoaded=false;
            find.RegisterValueChangedCallback(_=>update()); category.RegisterValueChangedCallback(_=>{
                if(category.value=="Archive" && !archivesLoaded) { var archived=ExperimentBlocks.Catalog(RuntimePaths.ConfigRoot,true).Where(p=>p.Category=="Archive"); presets.AddRange(archived); archivesLoaded=true; } update();
            });
            list.selectionChanged+=values=>{
                chosen=values.OfType<ExperimentPreset>().FirstOrDefault(); preview.Clear(); if(chosen==null) return;
                preview.Add(new Label(chosen.Title) { name="preset-title" }); preview.Add(Pill(chosen.Schema)); preview.Add(new Label(chosen.Description));
                preview.Add(new Label(ExperimentBlocks.Time(chosen.Duration)+" suggested duration"));
                preview.Add(new Label(chosen.Reference)); var choices=ScenesFor(chosen.Schema); if(!choices.Contains(chosen.Scene)) choices.Add(chosen.Scene); scenes.choices=choices; scenes.SetValueWithoutNotify(chosen.Scene); preview.Add(scenes);
                if(ExperimentBlocks.Phased(chosen.Config)) foreach(string phase in new[]{"preStimulus","stimulus","postStimulus"}) if(chosen.Config[phase] is JObject obj) preview.Add(Pill(Pretty(phase)+" · "+(ExperimentBlocks.Flag(obj["enabled"],phase=="stimulus")?ExperimentBlocks.Time(ExperimentBlocks.Number(obj["durationSeconds"])):"OFF")));
                insert.SetEnabled(true);
            };
            var custom=Row(); dialog.Add(custom); var source=new TextField("Custom path") { name="custom-preset-path" }; source.style.flexGrow=1; custom.Add(source);
            custom.Add(StudioButton("Load custom path","file",()=>{ string full=ExperimentConfigFiles.Resolve(source.value); var preset=ExperimentBlocks.Read(source.value,full); if(preset.Schema=="system" || preset.Schema=="sequence") throw new ArgumentException("Choose an individual experiment config, not a system/sequence file."); if(presets.All(p=>p.Reference!=preset.Reference)) presets.Add(preset); category.value="All"; find.SetValueWithoutNotify(""); update(); int index=visible.FindIndex(p=>p.Reference==preset.Reference); list.SetSelection(index); list.ScrollToItem(index); }));
            custom.Add(insert); source.tooltip="Relative StreamingAssets path or absolute JSON filename";
            dialog.Add(new Label("Current configs, templates, examples and custom JSON are listed. Choose Archive to include historical files."));
        }
        private void ShowOpenFile()
        {
            RequireApplied(); var dialog=OpenModal("Open configuration","Choose a system, sequence, or experiment JSON file. Paths may be relative to StreamingAssets or absolute.");
            var source=new TextField("File path"); dialog.Add(source);
            var role=new DropdownField("Configuration type",new List<string>{"sequence","system","choice","swarm","kannadi","optomotor","dynamic-choice"},0); dialog.Add(role);
            dialog.Add(StudioButton("Open file","file",()=>{ if(role.value=="sequence" || role.value=="system") { Workspace.SelectPrimary(role.value,source.value); CloseModal(); Select(role.value=="sequence"?Workspace.Sequence:Workspace.System); } else { var doc=Workspace.Open(source.value,role.value); CloseModal(); Select(doc); } },"primary"));
        }
        private void ShowVariation(int? block)
        {
            RequireApplied(); var doc=block.HasValue?BlockDocument(block.Value):selected; if(doc==null) throw new IOException("Configuration is unavailable.");
            var dialog=OpenModal("Create a variation","The complete JSON is copied. Other blocks keep their original config.");
            var destination=new TextField("New path") { value="UserConfigs/"+Path.GetFileNameWithoutExtension(doc.Path)+"-variation.json" }; dialog.Add(destination);
            dialog.Add(StudioButton("Create variation","copy",()=>{
                string target=Path.IsPathRooted(destination.value)?destination.value:Path.Combine(RuntimePaths.ConfigRoot,destination.value);
                if(File.Exists(target)) throw new IOException("Choose a new filename; this one already exists.");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))); File.WriteAllText(target,doc.Root.ToString(Formatting.Indented)+"\n");
                if(block.HasValue) { string reference=Path.IsPathRooted(destination.value)?target:destination.value.Replace('\\','/'); var preset=ExperimentBlocks.Read(reference,target); AddPresetBlock(preset,(string)Block(block.Value)["sceneName"],block); EditBlock(block.Value); }
                else { var copy=Workspace.Open(target,doc.SchemaId); if(doc.SchemaId=="system" || doc.SchemaId=="sequence") Workspace.SelectPrimary(doc.SchemaId,target); CloseModal(); Select(copy); }
            },"primary"));
        }
    }
}
