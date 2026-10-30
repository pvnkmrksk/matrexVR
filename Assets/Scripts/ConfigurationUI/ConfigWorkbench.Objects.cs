using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private bool buildingObjectCard;
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
                var preview=new VisualElement();preview.AddToClassList("object-preview-slot");visual.Add(preview);AddObjectPreview(preview,selected,objectPath.Concat(new[]{"modelPath"}).ToArray());
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
            bool mirror=schema["properties"]?["kannadiModelPath"]!=null&&!string.Equals((string)payload["populationMode"],"Swarm",StringComparison.OrdinalIgnoreCase);
            string file=mirror?"kannadiModelPath":"modelPath",builtin=mirror?"kannadiTilePrefab":"agentVisual";
            if(schema["properties"]?[file]==null&&schema["properties"]?[builtin]==null)return;
            var heading=Row("section-heading");heading.Add(new WorkbenchIcon("object"));heading.Add(new Label("Population model"));target.Add(heading);
            var row=Row("object-visual-row");target.Add(row);var controls=new VisualElement();controls.AddToClassList("object-main-controls");row.Add(controls);
            buildingObjectCard=true;
            try
            {
                foreach(string key in new[]{builtin,file,"modelAppearance"})if(schema["properties"]?[key]!=null)controls.Add(Setting(selected,prefix.Concat(new[]{key}).ToArray(),ConfigSchema.Child(schema,key),payload,schema));
                if(schema["properties"]?["bogongVisual"]!=null)
                {var details=new Foldout{text="Bogong visual settings",value=false};details.AddToClassList("editor-disclosure");controls.Add(details);details.Add(Setting(selected,prefix.Concat(new[]{"bogongVisual"}).ToArray(),ConfigSchema.Child(schema,"bogongVisual"),payload,schema));}
            }
            finally{buildingObjectCard=false;}
            var preview=new VisualElement();preview.AddToClassList("object-preview-slot");row.Add(preview);AddObjectPreview(preview,selected,prefix.Concat(new[]{file}).ToArray());
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
    }
}
