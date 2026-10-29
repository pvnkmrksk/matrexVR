using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private string ChoiceCatalogScene()
        {
            string fallback=selected.SchemaId=="dynamic-choice"?"Choice_desync":"Choice";
            var matches=BuilderSteps.OfType<JObject>().Where(step=>{
                string reference=BlockReference(step);if(string.IsNullOrEmpty(reference))return false;
                try{return Path.GetFullPath(ExperimentConfigFiles.Resolve(reference))==selected.Path;}catch{return false;}
            }).ToArray();
            var current=Block(activeBlock);
            return (string)(matches.FirstOrDefault(step=>ReferenceEquals(step,current))??matches.FirstOrDefault())?["sceneName"]??fallback;
        }
        private bool TryChoiceCatalogControl(string[] parts,JToken value,Action<JToken> assign,out VisualElement control)
        {
            control=null;
            if(parts.Last()=="modelPath"||parts.Last()=="kannadiModelPath")
            {
                string modelCurrent=value?.Type==JTokenType.String?value.ToString():"";
                var modelPaths=new System.Collections.Generic.List<string>{""};
                try
                {
                    if(Directory.Exists(RuntimePaths.ConfigRoot))modelPaths.AddRange(Directory.EnumerateFiles(RuntimePaths.ConfigRoot,"*.glb",SearchOption.AllDirectories)
                        .Take(4096).Select(path=>Path.GetRelativePath(RuntimePaths.ConfigRoot,path).Replace('\\','/')).OrderBy(path=>path,StringComparer.OrdinalIgnoreCase));
                }
                catch(IOException){}catch(UnauthorizedAccessException){}
                if(!modelPaths.Contains(modelCurrent))modelPaths.Add(modelCurrent);
                var modelLabels=modelPaths.Select(path=>string.IsNullOrEmpty(path)?"Built-in model":path).ToList();
                var modelPicker=new DropdownField(modelLabels,modelPaths.IndexOf(modelCurrent)){name="external-model-"+string.Join("-",parts),tooltip="Choose an embedded GLB file under StreamingAssets. Drop new .glb files there and reopen this editor to refresh the list."};
                var modelRow=new VisualElement();modelRow.Add(modelPicker);
                modelPicker.RegisterValueChangedCallback(e=>Guard(()=>assign(new JValue(modelPaths[modelLabels.IndexOf(e.newValue)]))));
                var modelInput=new TextField {value=modelCurrent,isDelayed=true,name="external-model-path-"+string.Join("-",parts),tooltip="Model path relative to StreamingAssets, or an existing absolute GLB path. The original model is archived with the run."};modelRow.Add(modelInput);
                modelInput.RegisterValueChangedCallback(e=>Guard(()=>assign(new JValue(e.newValue))));control=modelRow;return true;
            }
            if(!ChoiceAssetCatalog.IsObjectField(parts))return false;
            var entries=ChoiceAssetCatalog.Load().Entries(ChoiceCatalogScene(),parts.Last());if(entries.Count==0)return false;
            string current=value?.Type==JTokenType.String?value.ToString():"";
            var labels=entries.Select(entry=>entry.Label).ToList();labels.Insert(0,"Select from available assets…");
            const string custom="Custom / existing JSON value…";labels.Add(custom);
            int index=entries.ToList().FindIndex(entry=>entry.Name==current);index=index>=0?index+1:current.Length>0?labels.Count-1:0;
            var row=new VisualElement();row.AddToClassList("number-control");
            var picker=new DropdownField(labels,index){name="choice-asset-"+string.Join("-",parts)};row.Add(picker);
            var input=new TextField{value=current,isDelayed=true,name="choice-custom-"+string.Join("-",parts),tooltip="Exact JSON name. Existing custom values are retained until you explicitly change them."};
            input.style.display=index==labels.Count-1?DisplayStyle.Flex:DisplayStyle.None;row.Add(input);
            picker.RegisterValueChangedCallback(evt=>Guard(()=>{
                int chosen=labels.IndexOf(evt.newValue);input.style.display=chosen==labels.Count-1?DisplayStyle.Flex:DisplayStyle.None;
                if(chosen>0&&chosen<=entries.Count){input.SetValueWithoutNotify(entries[chosen-1].Name);assign(new JValue(entries[chosen-1].Name));}
            }));
            input.RegisterValueChangedCallback(evt=>Guard(()=>assign(new JValue(evt.newValue))));control=row;return true;
        }
        private void AddChoiceCatalogAction(VisualElement target)
        {
            if(selected.SchemaId!="choice"&&selected.SchemaId!="dynamic-choice")return;
            var action=StudioButton("Available objects & materials","file",ShowChoiceCatalog,"quiet");action.name="show-choice-assets";
            action.tooltip="Browse the exact object and material names registered by the selected scene in this build.";
            var actions=Row();actions.Add(action);target.Add(actions);
        }
        private void ShowChoiceCatalog()
        {
            string scene=ChoiceCatalogScene();var catalog=ChoiceAssetCatalog.Load();
            var dialog=OpenModal("Available objects & materials","Registered by "+SceneCaption(scene)+" in this build. Choosing an object or material in its field writes the exact name to the same JSON.");
            var scroll=new ScrollView{name="choice-asset-catalog"};scroll.style.maxHeight=420;dialog.Add(scroll);
            var columns=Row();columns.style.alignItems=Align.FlexStart;scroll.Add(columns);
            foreach(string role in new[]{"type","material"})
            {
                var column=new VisualElement();column.style.flexGrow=1;column.style.flexBasis=0;column.style.marginRight=12;columns.Add(column);
                var entries=catalog.Entries(scene,role);var heading=new Label((role=="type"?"Objects":"Materials")+" · "+entries.Count);heading.AddToClassList("section-heading");column.Add(heading);
                foreach(var entry in entries)
                {
                    var name=new Label(entry.Label){tooltip=entry.Source+"\n"+entry.Description};name.AddToClassList("setting-title");name.style.marginBottom=6;column.Add(name);
                }
                if(entries.Count==0)column.Add(new Label("This scene has no registered "+(role=="type"?"configurable objects":"material overrides")+". Authored scenery stays in its scene or shared prefab."));
            }
        }
    }
}
