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
                var modelRow=new VisualElement();modelRow.AddToClassList("external-model-control");modelPicker.AddToClassList("model-file-menu");
                var modelInput=new TextField {value=modelCurrent,isDelayed=true,name="external-model-path-"+string.Join("-",parts),tooltip="Model path relative to StreamingAssets, or an existing absolute GLB path. The original model is archived with the run."};modelInput.textEdition.placeholder="Optional .glb path";modelRow.Add(modelInput);modelRow.Add(modelPicker);
                BindDelayedText(modelInput,text=>assign(new JValue(text)));
                modelPicker.RegisterValueChangedCallback(e=>Guard(()=>{RequireApplied();string chosen=modelPaths[modelLabels.IndexOf(e.newValue)];assign(new JValue(chosen));RefreshBoundText(modelInput,chosen);}));control=modelRow;return true;
            }
            bool kannadi=parts.Last()=="kannadiTilePrefab";
            if(!kannadi&&!ChoiceAssetCatalog.IsObjectField(parts))return false;
            var entries=ChoiceAssetCatalog.Load().Entries(kannadi?"Kannadi":ChoiceCatalogScene(),kannadi?"type":parts.Last());if(entries.Count==0)return false;
            string current=value?.Type==JTokenType.String?value.ToString():"";
            var labels=entries.Select(entry=>entry.Label).ToList();labels.Insert(0,"Choose object…");
            const string custom="Custom name…";labels.Add(custom);
            string LabelFor(string name)
            {
                var entry=entries.FirstOrDefault(item=>item.Name==name);if(entry!=null)return entry.Label;
                if(string.IsNullOrEmpty(name))return labels[0];
                string label=name+" · custom";if(!labels.Contains(label))labels.Insert(labels.Count-1,label);return label;
            }
            var document=selected;
            var row=Row("asset-picker-control");
            var picker=new DropdownField(labels,0){name="choice-asset-"+string.Join("-",parts)};picker.SetValueWithoutNotify(LabelFor(current));row.Add(picker);
            void EditName()
            {
                RequireApplied();var dialog=OpenModal("Object name","Exact registered name. External files use the Custom GLB field.");
                var input=new TextField("JSON name"){value=ConfigDocument.At(document.Root,parts)?.ToString()??"",isDelayed=true,name="choice-custom-"+string.Join("-",parts)};dialog.Add(input);
                dialog.Add(StudioButton("Use name","next",()=>{RequireApplied();assign(new JValue(input.value));CloseModal();},"primary"));
                dialog.RegisterCallback<DetachFromPanelEvent>(_=>picker.SetValueWithoutNotify(LabelFor(ConfigDocument.At(document.Root,parts)?.ToString()??"")));
            }
            picker.RegisterValueChangedCallback(evt=>Guard(()=>{
                RequireApplied();if(evt.newValue==custom){EditName();return;}
                var chosen=entries.FirstOrDefault(entry=>entry.Label==evt.newValue);if(chosen!=null)assign(new JValue(chosen.Name));
            }));
            var edit=IconButton("code","Enter an exact JSON object name",EditName);edit.name="edit-choice-name-"+string.Join("-",parts);row.Add(edit);
            BindViewState(picker,()=>picker.SetValueWithoutNotify(LabelFor(ConfigDocument.At(document.Root,parts)?.ToString()??"")));
            control=row;return true;
        }
        private void AddChoiceCatalogAction(VisualElement target)
        {
            if(selected.SchemaId!="choice"&&selected.SchemaId!="dynamic-choice")return;
            var action=StudioButton("Browse assets","file",ShowChoiceCatalog,"quiet");action.name="show-choice-assets";
            action.tooltip="Browse the exact object and material names registered by the selected scene in this build.";
            var actions=Row();actions.Add(action);target.Add(actions);
        }
        private void ShowChoiceCatalog()
        {
            string scene=ChoiceCatalogScene();var catalog=ChoiceAssetCatalog.Load();
            var dialog=OpenModal("Browse assets","Registered by "+SceneCaption(scene)+" in this build. Choosing an object or material in its field writes the exact name to the same JSON.");
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
