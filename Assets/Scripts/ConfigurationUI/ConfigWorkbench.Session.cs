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
        private void BuildSessionHome()
        {
            EnsureMetadata();
            var scroll=new ScrollView { horizontalScrollerVisibility=ScrollerVisibility.Hidden }; scroll.AddToClassList("session-scroll"); body.Add(scroll);
            var welcome=Row("session-welcome"); scroll.Add(welcome);
            var titles=new VisualElement(); titles.style.flexGrow=1; welcome.Add(titles);
            var title=new Label("Session"); title.AddToClassList("title"); titles.Add(title);
            var hint=new Label("Label your subjects, check the plan, and you're ready to run."); hint.AddToClassList("session-hint"); titles.Add(hint);

            var details=Row("session-details"); scroll.Add(details);
            SessionField(details,"Experiment name","session-experiment-name",metadata.ExperimentName,v=>metadata.ExperimentName=v,"Name this experiment");
            SessionField(details,"Experimenter","session-experimenter",metadata.ExperimenterName,v=>metadata.ExperimenterName=v,"Your name");
            SessionField(details,"Overall comments","session-comments",metadata.Comments,v=>metadata.Comments=v,"Notes for this session",true);

            scroll.Add(SessionHeading("Subjects","person","Names and IDs can be any text. Empty age means unspecified."));
            var rigs=Row("subject-grid"); scroll.Add(rigs);
            foreach(var rig in (Workspace.System.Root["configs"] as JArray??new JArray()).OfType<JObject>())
            {
                string id=(string)rig["vrId"]; var fly=metadata.Flies.First(f=>f.VR==id);
                var card=new VisualElement { name="subject-"+id }; card.AddToClassList("subject-card"); rigs.Add(card);
                var heading=Row("subject-heading"); card.Add(heading); heading.Add(new WorkbenchIcon("person"));
                var label=new Label(id); label.AddToClassList("subject-name"); heading.Add(label);
                heading.Add(Pill(RigMode(rig),"accent"));
                heading.Add(IconButton("settings","More subject details for "+id,()=>ShowSubjectDetails(fly)));
                SessionField(card,"Name / ID","subject-id-"+id,fly.FlyID,v=>fly.FlyID=v,"Any name, label or ID");
                var traits=Row("subject-traits"); card.Add(traits);
                var sexes=new List<string>{"Unspecified","Female","Male"};
                string sex=string.IsNullOrEmpty(fly.Sex)?"Unspecified":fly.Sex;
                if(!sexes.Contains(sex)) sexes.Add(sex); // Preserve labels from existing metadata.
                var sexField=new DropdownField("Sex",sexes,0) { name="subject-sex-"+id,value=sex,tooltip="Subject sex recorded with the run. Unspecified leaves it unclassified." }; traits.Add(sexField);
                sexField.RegisterValueChangedCallback(e=>fly.Sex=e.newValue);
                SessionField(traits,"Age (days)","subject-age-"+id,fly.AgeDays,v=>fly.AgeDays=v,"Unspecified");
                SessionField(card,"Comments","subject-comments-"+id,fly.Comments,v=>fly.Comments=v,"Notes for "+id,true);
            }

            var system=StudioButton("","system",()=>Select(Workspace.System),"session-system"); system.name="session-system-summary";
            system.Add(new Label(SystemHeadline()) { name="session-system-headline" });
            system.Add(new Label("System details  ›")); scroll.Add(system);

            string order=(ExperimentBlocks.Flag(Workspace.Sequence.Root["randomise"])?"Random order":"Listed order")+
                " · "+(ExperimentBlocks.Flag(Workspace.Sequence.Root["loop"],true)?"Repeats from block "+((int?)Workspace.Sequence.Root["loopStartIndex"]+1??1):"One pass");
            var planHeading=SessionHeading("Coming up","sequence",BuilderSteps.Count+" blocks · "+SequenceDuration()+" first pass · "+order);
            planHeading.Add(StudioButton("Edit sequence","settings",()=>Select(Workspace.Sequence),"quiet")); scroll.Add(planHeading);
            var summaryIndices=Enumerable.Range(0,BuilderSteps.Count).ToList();
            var plan=new ListView(summaryIndices,50,()=>new VisualElement(),BindSessionBlock)
            { name="session-plan",selectionType=SelectionType.None,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight };
            plan.AddToClassList("session-plan"); plan.style.height=Math.Min(6,Math.Max(1,BuilderSteps.Count))*50; scroll.Add(plan);
            if(BuilderSteps.Count==0) scroll.Add(StudioButton("Add your first block","plus",()=>{ Select(Workspace.Sequence); ShowBlockLibrary(); },"primary"));
            UpdateStatus();
        }
        private void ShowSubjectDetails(Fly fly)
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
            var telemetry=Workspace.System.Root["telemetry"];
            string output=ExperimentBlocks.Flag(telemetry?["enabled"],true)?"Telemetry → "+(telemetry?["bindAddress"]?.ToString()??"127.0.0.1")+":"+(telemetry?["port"]?.ToString()??"9880"):"Telemetry off";
            return modes+" · "+rigs.Count+" VRs · "+panels+"\nClosed loop input: "+inputs+"\n"+output;
        }
        private static string RigMode(JObject rig)
        {
            string value=rig["closedLoopMode"]?.ToString()??"FicTrac";
            return Enum.TryParse(value,true,out ClosedLoopMode mode) && Enum.IsDefined(typeof(ClosedLoopMode),mode)?mode.ToString():value;
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
        private void ShowEditorTools()
        {
            var dialog=OpenModal("Config tools","Every JSON value remains accessible, including custom extensions.");
            dialog.Add(StudioButton("All parameters","settings",()=>{ CloseModal(); advancedMode=true; BuildContent(); }));
            dialog.Add(StudioButton("Edit JSON","file",()=>{ CloseModal(); jsonMode=true; BuildContent(); }));
            dialog.Add(StudioButton("Open another file","file",()=>{ CloseModal(); ShowOpenFile(); }));
            dialog.Add(StudioButton("Save a variation","copy",()=>{ CloseModal(); ShowVariation(null); }));
        }
    }
}
