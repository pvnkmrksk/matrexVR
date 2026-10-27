using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        // Labels are presentation only. Dropdown values, sequence JSON and
        // recorded scene identities continue to use the actual Unity names.
        private static string SceneCaption(string scene)
        {
            switch(scene)
            {
                case "TerrainChoice_RoughLeft_SmoothRight":return "Rough left, smooth right";
                case "TerrainChoice_SmoothLeft_RoughRight":return "Smooth left, rough right";
                case "TerrainChoice_BothSmooth":return "Both smooth";
                case "TerrainChoice_BothRough":return "Both rough";
                default:return scene;
            }
        }
        private static string SceneOptionCaption(string scene)
        {
            if(scene=="TerrainChoice_RoughLeft_SmoothRight")return "Rough / smooth";
            if(scene=="TerrainChoice_SmoothLeft_RoughRight")return "Smooth / rough";
            return SceneCaption(scene);
        }
        private string BlockCaption(JObject step)
        {
            string scene=(string)step?["sceneName"];
            return scene!=null&&scene.StartsWith("TerrainChoice_",System.StringComparison.Ordinal)
                ?"Terrain choice · "+SceneCaption(scene):ExperimentBlocks.Title(BlockReference(step));
        }
        private static string BlockDescription(JObject step,JObject config)
        {
            JObject presented=config;
            if(config!=null&&step?["parameters"]?["rugosity"] is JObject overrides)
            {
                presented=(JObject)config.DeepClone();var payload=ExperimentBlocks.Payload(presented);
                var terrain=payload["rugosity"] as JObject;
                if(terrain==null){terrain=new JObject();payload["rugosity"]=terrain;}
                terrain.Merge(overrides,new JsonMergeSettings {MergeArrayHandling=MergeArrayHandling.Replace,MergeNullValueHandling=MergeNullValueHandling.Merge});
            }
            return ExperimentBlocks.Describe(presented)
                .Replace("RoughSmooth","rough left / smooth right").Replace("SmoothRough","smooth left / rough right")
                .Replace("SmoothSmooth","both smooth").Replace("RoughRough","both rough");
        }
        private void AddSequenceOverrideNotice(VisualElement parent)
        {
            if(selected==Workspace.Sequence||selected==Workspace.System)return;
            var matches=BuilderSteps.OfType<JObject>().Select((step,index)=>new {step,index})
                .Where(item=>BlockDocument(item.index)?.Path==selected.Path).ToList();
            var context=matches.FirstOrDefault(item=>item.index==activeBlock)??matches.FirstOrDefault();
            if(context==null||!(context.step["parameters"]?["rugosity"] is JObject overrides))return;
            var source=ExperimentBlocks.Payload(selected.Root as JObject)?["rugosity"] as JObject;
            string Role(string key)=>overrides[key]?.ToString()??source?[key]?.ToString()??"Scene default";
            string Friendly(string value)=>value.Replace("RoughSmooth","rough left / smooth right").Replace("SmoothRough","smooth left / rough right").Replace("SmoothSmooth","both smooth").Replace("RoughRough","both rough");
            var note=new VisualElement {name="sequence-terrain-overrides"};note.AddToClassList("sequence-override-note");parent.Add(note);
            note.Add(SessionLabel("Sequence overrides · block "+(context.index+1).ToString("00"),"sequence-override-title"));
            var roles=Row("sequence-override-roles");note.Add(roles);
            var visual=Pill("Visual: "+Friendly(Role("visualSurface")),"accent");visual.name="sequence-override-visual";roles.Add(visual);
            var collision=Pill("Navigation: "+Friendly(Role("collisionSurface")),"accent");collision.name="sequence-override-navigation";roles.Add(collision);
            note.Add(SessionLabel("The controls below edit the shared base file. This sequence block supplies the surface roles shown above, so its overrides take priority over the same fields in the base file.","sequence-override-copy"));
            var edit=StudioButton("Edit sequence overrides","sequence",()=>{
                RequireApplied();activeBlock=context.index;selected=Workspace.Sequence;homeMode=metadataMode=experimentIndexMode=jsonMode=false;advancedMode=true;
                path.Clear();path.AddRange(new[]{"sequences",context.index.ToString(),"parameters","rugosity"});BuildNavigation();BuildContent();
            },"quiet");edit.name="edit-sequence-terrain-overrides";note.Add(edit);
        }
    }
}
