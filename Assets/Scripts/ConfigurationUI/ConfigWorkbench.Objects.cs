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
        private bool buildingObjectCard;
        private bool appearancePreviews=true;

        private static VisualElement AddAppearancePreviewSlot(VisualElement parent)
        {
            var slot=new VisualElement();
            slot.AddToClassList("object-preview-slot");
            parent.Add(slot);
            return slot;
        }
        private void BuildObjectCollection(VisualElement target,JObject payload,JObject schema,string[] prefix)
        {
            var section=new VisualElement();section.AddToClassList("object-collection");target.Add(section);
            var header=Row("section-heading");section.Add(header);header.Add(new WorkbenchIcon("object"));header.Add(new Label("Objects"));
            string[] collectionPath=prefix.Concat(new[]{"objects"}).ToArray();var arraySchema=ConfigSchema.Child(schema,"objects");var itemSchema=ConfigSchema.Child(arraySchema,"",true);
            var add=StudioButton("Add object","plus",()=>StudioEdit(selected,root=>{
                if(!(ConfigDocument.At(root,collectionPath) is JArray))AssignAt(root,collectionPath,new JArray());
                var value=ConfigSchema.NewValue(itemSchema) as JObject??new JObject();value["enabled"]=true;value["modelPath"]="";value["type"]="Cube";
                ((JArray)ConfigDocument.At(root,collectionPath)).Add(value);
            }),"quiet");add.name="add-scene-object";header.Add(add);
            var objects=payload["objects"] as JArray??new JArray();
            if(objects.Count==0){var empty=new Label("Add a built-in object or an external GLB.");empty.AddToClassList("quiet-hint");section.Add(empty);}
            for(int index=0;index<objects.Count;index++)
            {
                if(!(objects[index] is JObject owner))continue;
                int item=index;string[] objectPath=collectionPath.Concat(new[]{item.ToString()}).ToArray();
                var card=new VisualElement{name="scene-object-"+item};card.AddToClassList("scene-object-card");section.Add(card);
                var heading=Row("object-heading");card.Add(heading);heading.Add(new WorkbenchIcon("object"));
                var name=new Label("Object "+(item+1));name.AddToClassList("object-name");heading.Add(name);
                var enabled=StateSwitch(ExperimentBlocks.Flag(owner["enabled"],true),on=>StudioEdit(selected,root=>AssignAt(root,objectPath.Concat(new[]{"enabled"}).ToArray(),new JValue(on)),false),"flag-objects-"+item+"-enabled");enabled.text="Enabled";heading.Add(enabled);
                heading.Add(IconButton("copy","Duplicate object",()=>StudioEdit(selected,root=>((JArray)ConfigDocument.At(root,collectionPath)).Insert(item+1,ConfigDocument.At(root,objectPath).DeepClone()))));
                heading.Add(IconButton("trash","Remove object",()=>StudioEdit(selected,root=>((JArray)ConfigDocument.At(root,collectionPath)).RemoveAt(item))));
                var body=new VisualElement();card.Add(body);BindViewState(body,()=>body.SetEnabled(ExperimentBlocks.Flag(ConfigDocument.At(selected.Root,objectPath)?["enabled"],true)));
                BindViewState(enabled,()=>enabled.SetValueWithoutNotify(ExperimentBlocks.Flag(ConfigDocument.At(selected.Root,objectPath)?["enabled"],true)));
                var visual=Row("object-visual-row");body.Add(visual);var controls=new VisualElement();controls.AddToClassList("object-main-controls");visual.Add(controls);
                buildingObjectCard=true;
                try
                {
                    foreach(string key in new[]{"type","modelPath"})controls.Add(Setting(selected,objectPath.Concat(new[]{key}).ToArray(),ConfigSchema.Child(itemSchema,key),owner,itemSchema));
                    BuildObjectAppearance(controls,owner,itemSchema,objectPath);
                }
                finally{buildingObjectCard=false;}
                if(appearancePreviews) AddObjectPreview(AddAppearancePreviewSlot(visual),selected,objectPath.Concat(new[]{"modelPath"}).ToArray());
                var details=new Foldout{name="object-details-"+item,text="Position, size & animation",value=false};details.AddToClassList("editor-disclosure");body.Add(details);
                buildingObjectCard=true;
                try
                {
                    var grid=new VisualElement();grid.AddToClassList("settings-grid");details.Add(grid);
                    foreach(string key in new[]{"position","polar","scale","rotation","animationClip","animationSpeed","groundOnTerrain"})
                        if(owner[key]!=null||itemSchema["properties"]?[key]!=null)grid.Add(Setting(selected,objectPath.Concat(new[]{key}).ToArray(),ConfigSchema.Child(itemSchema,key),owner,itemSchema));
                    var advanced=new Foldout {text="Built-in materials & other parameters",value=false};advanced.AddToClassList("editor-disclosure");details.Add(advanced);
                    var extras=new VisualElement();extras.AddToClassList("settings-grid");advanced.Add(extras);
                    foreach(string key in DisplayKeys(owner,itemSchema).Except(new[]{"enabled","type","modelPath","appearance","position","polar","scale","rotation","animationClip","animationSpeed","groundOnTerrain"}))
                        extras.Add(Setting(selected,objectPath.Concat(new[]{key}).ToArray(),ConfigSchema.Child(itemSchema,key),owner,itemSchema));
                }
                finally{buildingObjectCard=false;}
            }
        }
        private void BuildPopulationAppearance(VisualElement target,JObject payload,JObject schema,string[] prefix)
        {
            bool mirror=selected.SchemaId=="kannadi"&&!string.Equals((string)payload["populationMode"],"Swarm",StringComparison.OrdinalIgnoreCase);
            if(!mirror && payload["swarm"] is JObject nested)
            {
                payload=nested;
                schema=ConfigSchema.Child(schema,"swarm");
                prefix=prefix.Concat(new[]{"swarm"}).ToArray();
            }
            string file=mirror?"kannadiModelPath":"modelPath",builtin=mirror?"kannadiTilePrefab":"agentVisual";
            if(schema["properties"]?[file]==null&&schema["properties"]?[builtin]==null)return;
            var heading=Row("section-heading");heading.Add(new WorkbenchIcon("object"));heading.Add(new Label(mirror?"Tracked animal model":"Generated swarm model"));target.Add(heading);
            var row=Row("object-visual-row");target.Add(row);var controls=new VisualElement();controls.AddToClassList("object-main-controls");row.Add(controls);
            buildingObjectCard=true;
            try
            {
                foreach(string key in new[]{builtin,file,"modelAppearance"})if(schema["properties"]?[key]!=null)controls.Add(Setting(selected,prefix.Concat(new[]{key}).ToArray(),ConfigSchema.Child(schema,key),payload,schema));
                if(!mirror && schema["properties"]?["bogongVisual"]!=null)
                {var details=new Foldout{text="Bogong visual settings",value=false};details.AddToClassList("editor-disclosure");controls.Add(details);details.Add(Setting(selected,prefix.Concat(new[]{"bogongVisual"}).ToArray(),ConfigSchema.Child(schema,"bogongVisual"),payload,schema));}
            }
            finally{buildingObjectCard=false;}
            if(appearancePreviews) AddObjectPreview(AddAppearancePreviewSlot(row),selected,prefix.Concat(new[]{file}).ToArray());
        }
        private void BuildObjectAppearance(VisualElement target,JObject owner,JObject ownerSchema,string[] objectPath)
        {
            var schema=ConfigSchema.Child(ownerSchema,"appearance");if(schema["properties"]==null)return;
            BuildSurfaceControls(target,schema,objectPath.Concat(new[]{"appearance"}).ToArray());
        }
        private void BuildSurfaceControls(VisualElement target,JObject schema,string[] appearancePath)
        {
            JObject Value()=>ConfigDocument.At(selected.Root,appearancePath) as JObject??JObject.FromObject(new ModelAppearanceConfig());
            void Edit(Action<JObject> action)=>StudioEdit(selected,root=>{
                if(!(ConfigDocument.At(root,appearancePath) is JObject))AssignAt(root,appearancePath,JObject.FromObject(new ModelAppearanceConfig()));
                action((JObject)ConfigDocument.At(root,appearancePath));
            },false);
            if(schema["properties"]?["textureVariants"]!=null) BuildTextureVariantPicker(target,selected,appearancePath);
            var row=Row("object-color-row");target.Add(row);
            var enabled=StateSwitch(ExperimentBlocks.Flag(Value()["enabled"]),on=>Edit(value=>value["enabled"]=on),"appearance-enabled-"+string.Join("-",appearancePath.Take(appearancePath.Length-1)));enabled.text="Override color";row.Add(enabled);
            BindViewState(enabled,()=>enabled.SetValueWithoutNotify(ExperimentBlocks.Flag(Value()["enabled"])));
            var color=ColorControl(selected,appearancePath.Concat(new[]{"color"}).ToArray(),Value()["color"] as JObject??JObject.FromObject(new ColorConfig{r=1,g=1,b=1,a=1}));row.Add(color);
            BindViewState(color,()=>color.SetEnabled(ExperimentBlocks.Flag(Value()["enabled"])));
            var detail=new Foldout {text="Surface details",value=false};detail.AddToClassList("surface-details");target.Add(detail);
            var grid=new VisualElement();grid.AddToClassList("settings-grid");detail.Add(grid);
            foreach(string key in new[]{"keepTexture","metallic","smoothness"})grid.Add(Setting(selected,appearancePath.Concat(new[]{key}).ToArray(),ConfigSchema.Child(schema,key),Value(),schema));
            BindViewState(grid,()=>grid.SetEnabled(ExperimentBlocks.Flag(Value()["enabled"])));
        }

        private void BuildTextureVariantPicker(VisualElement target,ConfigDocument document,string[] appearancePath)
        {
            var row=Row("texture-variant-picker");target.Add(row);
            var available=new List<string>();
            string Caption(string value)=>string.IsNullOrEmpty(value)?"Original model texture":
                available.Contains(value,StringComparer.OrdinalIgnoreCase)?value:value+" · unavailable";
            var picker=new DropdownField("Texture variant",new List<string>{""},0) {
                name="model-texture-variant-"+string.Join("-",appearancePath),
                tooltip="A named image replaces the model's base-color texture. Original uses the current Keep texture setting."
            };
            picker.formatSelectedValueCallback=Caption;
            picker.formatListItemCallback=Caption;
            row.Add(picker);
            picker.RegisterValueChangedCallback(change=>Guard(()=>{
                RequireApplied();
                StudioEdit(document,root=>AssignAt(root,appearancePath.Concat(new[]{"selectedTextureVariant"}).ToArray(),new JValue(change.newValue)),false);
            }));
            var edit=StudioButton("Edit variants","image",()=>ShowTextureVariants(document,appearancePath),"quiet");
            edit.name="edit-model-textures-"+string.Join("-",appearancePath);
            row.Add(edit);
            BindViewState(picker,()=>{
                var appearance=ConfigDocument.At(document.Root,appearancePath) as JObject;
                var choices=appearance?["textureVariants"] as JArray;
                available=choices?.OfType<JObject>().Where(entry=>entry["name"]?.Type==JTokenType.String)
                    .Select(entry=>(string)entry["name"]).Where(name=>!string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList()??new List<string>();
                var values=new List<string>{""};
                values.AddRange(available);
                string saved=appearance?["selectedTextureVariant"]?.Type==JTokenType.String?(string)appearance["selectedTextureVariant"]:"";
                string current=available.FirstOrDefault(name=>string.Equals(name,saved,StringComparison.OrdinalIgnoreCase))??saved;
                if(!values.Contains(current))values.Add(current);
                if(!picker.choices.SequenceEqual(values))picker.choices=values;
                picker.SetValueWithoutNotify(current);
                picker.SetEnabled(values.Count>1);
            });
        }

        private void ShowTextureVariants(ConfigDocument document,string[] appearancePath)
        {
            RequireApplied();
            var appearance=ConfigDocument.At(document.Root,appearancePath) as JObject;
            var variants=appearance?["textureVariants"]?.DeepClone() as JArray??new JArray();
            string selectedName=appearance?["selectedTextureVariant"]?.Type==JTokenType.String?(string)appearance["selectedTextureVariant"]:"";
            var selectedEntry=string.IsNullOrEmpty(selectedName)?null:variants.OfType<JObject>().FirstOrDefault(entry=>
                entry["name"]?.Type==JTokenType.String&&string.Equals((string)entry["name"],selectedName,StringComparison.OrdinalIgnoreCase));
            string originalEntryName=(string)selectedEntry?["name"];
            var dialog=OpenModal("Texture variants","Name each PNG or JPEG. Use a path relative to the configuration directory, or browse to a file.");
            dialog.name="texture-variants-dialog";
            var list=new ScrollView {horizontalScrollerVisibility=ScrollerVisibility.Hidden};
            list.AddToClassList("texture-variant-list");dialog.Add(list);
            var actions=Row();actions.AddToClassList("texture-variant-actions");dialog.Add(actions);
            void RenderRows()
            {
                list.Clear();
                for(int index=0;index<variants.Count;index++)
                {
                    int slot=index;
                    var entry=variants[slot] as JObject;
                    if(entry==null){entry=new JObject();variants[slot]=entry;}
                    var item=Row("texture-variant-entry");list.Add(item);
                    var name=new TextField("Name") {name="texture-variant-name-"+slot,value=entry["name"]?.ToString()??""};
                    name.AddToClassList("texture-variant-name");item.Add(name);
                    name.RegisterValueChangedCallback(change=>entry["name"]=change.newValue);
                    var path=new TextField("Image file") {name="texture-variant-path-"+slot,value=entry["texturePath"]?.ToString()??""};
                    path.AddToClassList("texture-variant-path");item.Add(path);
                    path.RegisterValueChangedCallback(change=>entry["texturePath"]=change.newValue);
                    var fileTypes=new List<string>{"png","jpg","jpeg"};
                    int formatIndex=fileTypes.IndexOf(Path.GetExtension(path.value).TrimStart('.').ToLowerInvariant());
                    var format=new DropdownField(fileTypes,Math.Max(0,formatIndex)) {tooltip="Choose the filter for the native image picker."};
                    format.AddToClassList("texture-variant-format");item.Add(format);
                    var browse=IconButton("file","Browse for texture image",()=>BrowseFile("Open model texture",format.value,chosen=>path.value=chosen));
                    browse.name="browse-texture-variant-"+slot;item.Add(browse);
                    item.Add(IconButton("trash","Remove texture variant "+(slot+1),()=>{variants.RemoveAt(slot);RenderRows();}));
                }
            }
            var add=StudioButton("Add variant","plus",()=>{
                variants.Add(new JObject {["name"]="",["texturePath"]=""});RenderRows();
                list.Q<TextField>("texture-variant-name-"+(variants.Count-1))?.Focus();
            },"quiet");
            add.name="add-texture-variant";
            actions.Add(add);
            var apply=StudioButton("Apply variants","next",()=>{
                var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(JObject entry in variants)
                {
                    string name=(string)entry["name"];
                    if(string.IsNullOrWhiteSpace(name)||name!=name.Trim()||!names.Add(name))
                        throw new ArgumentException("Give each texture variant a unique name without leading or trailing spaces.");
                    ModelTextureLibrary.ValidateReference((string)entry["texturePath"]);
                }
                string renamed=variants.Any(entry=>ReferenceEquals(entry,selectedEntry))?(string)selectedEntry["name"]:"";
                StudioEdit(document,root=>{
                    AssignAt(root,appearancePath.Concat(new[]{"textureVariants"}).ToArray(),variants.DeepClone());
                    if(selectedEntry!=null&&renamed!=originalEntryName)
                        AssignAt(root,appearancePath.Concat(new[]{"selectedTextureVariant"}).ToArray(),new JValue(renamed));
                },false);
                CloseModal();
            },"primary");
            apply.name="apply-texture-variants";
            actions.Add(apply);
            RenderRows();
        }
    }
}
