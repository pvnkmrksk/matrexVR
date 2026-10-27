using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private sealed class SequenceStarter
        {
            public readonly string Title, Reference, Description;
            public SequenceStarter(string title,string reference,string description)
            { Title=title;Reference=reference;Description=description; }
        }
        private static readonly SequenceStarter[] SequenceStarters = {
            new SequenceStarter("Terrain · matched relief","Examples/Rugosity/sequence-treatment-matched-terrain.json",
                "Four terrain patterns. Visible relief and navigation follow the same surface."),
            new SequenceStarter("Terrain · visible relief, flat navigation","Examples/Rugosity/sequence-treatment-visual-terrain_navigation-flat.json",
                "Four terrain patterns. Visible relief changes while the animal navigates a flat surface."),
            new SequenceStarter("Terrain · flat appearance, relief navigation","Examples/Rugosity/sequence-treatment-visual-flat_navigation-terrain.json",
                "Four terrain patterns. The world looks flat while navigation follows the terrain relief."),
            new SequenceStarter("Terrain · matched flat","Examples/Rugosity/sequence-treatment-matched-flat.json",
                "Four terrain patterns. Appearance and navigation are both flat."),
            new SequenceStarter("Swarm · entrainment then social coupling","Examples/Sequences/swarm-entrainment-social.sequence.json",
                "Ordered swarm entrainment, followed by the linked Kannadi social scene. Edit durations and clone count in the referenced configs."),
            new SequenceStarter("Social coupling · control","Examples/Sequences/social-coupling-control.sequence.json",
                "The same linked Kannadi social scene without prior swarm entrainment.")
        };
        private void ShowSequenceLibrary()
        {
            RequireApplied();
            var dialog=OpenModal("Load a sequence","Choose a prepared experiment sequence. Your current apparatus and other configuration drafts are retained.");
            dialog.AddToClassList("small-dialog");dialog.AddToClassList("sequence-starter-dialog");
            var chooser=new DropdownField("Sequence",SequenceStarters.Select(p=>p.Title).ToList(),0){name="sequence-starter-choice"};
            chooser.tooltip="Choose a complete sequence. Loading opens its blocks for review before you start.";dialog.Add(chooser);
            var description=SessionLabel("","apparatus-copy");description.name="sequence-starter-description";dialog.Add(description);
            var timing=SessionLabel("","sequence-starter-timing");timing.name="sequence-starter-timing";dialog.Add(timing);
            var reference=SessionLabel("","sequence-starter-reference");reference.name="sequence-starter-reference";dialog.Add(reference);
            var actions=Row("sequence-starter-actions");dialog.Add(actions);
            var custom=StudioButton("Open custom JSON…","file",ShowOpenFile,"quiet");custom.name="sequence-starter-custom";actions.Add(custom);
            var load=StudioButton("Load sequence","sequence",()=>{
                RequireApplied();
                var preset=SequenceStarters[chooser.index];
                Workspace.SelectPrimary("sequence",preset.Reference);
                CloseModal();Select(Workspace.Sequence);
                UpdateStatus("Sequence loaded. Review its blocks, then start when ready.");
            },"primary");load.name="sequence-starter-load";actions.Add(load);
            Action refresh=()=>{
                var preset=SequenceStarters[chooser.index];description.text=preset.Description;reference.text=preset.Reference;reference.tooltip=preset.Reference;
                try
                {
                    var document=Workspace.Open(preset.Reference,"sequence");
                    if(document.LoadError!=null)throw new IOException(document.LoadError);
                    var blocks=document.Root["sequences"] as JArray??new JArray();
                    double seconds=blocks.OfType<JObject>().Sum(block=>ExperimentBlocks.Number(block["duration"]));
                    timing.text=ExperimentBlocks.Time(seconds)+" · "+blocks.Count+(blocks.Count==1?" block":" blocks");
                    load.SetEnabled(true);
                }
                catch(Exception error){timing.text="Unavailable: "+error.Message;load.SetEnabled(false);}
            };
            chooser.RegisterValueChangedCallback(_=>refresh());refresh();
        }
    }
}
