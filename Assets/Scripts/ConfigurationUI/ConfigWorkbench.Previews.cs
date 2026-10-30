using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private void AddObjectPreview(VisualElement target,ConfigDocument document,string[] path)
        {
            string key=path.Last();
            if(!new[]{"type","material","modelPath","kannadiModelPath","kannadiTilePrefab","agentVisual"}.Contains(key))return;
            if((key=="type"||key=="material")&&!ChoiceAssetCatalog.IsObjectField(path))return;
            string scene=key=="kannadiTilePrefab"||key=="agentVisual"||key=="kannadiModelPath"?"Kannadi":ChoiceCatalogScene();
            if(key=="modelPath"&&selected.SchemaId=="swarm")scene="Kannadi";
            JObject Owner()=>ConfigDocument.At(document.Root,path.Take(path.Length-1).ToArray()) as JObject??new JObject();
            var pane=new EditPreviewPane(false,()=>new JObject{["value"]=ConfigDocument.At(document.Root,path)?.DeepClone(),["model"]=Owner()["modelPath"]?.DeepClone(),["agentVisual"]=Owner()["agentVisual"]?.DeepClone(),["tile"]=Owner()["kannadiTilePrefab"]?.DeepClone(),["material"]=Owner()["material"]?.DeepClone(),["clip"]=Owner()["animationClip"]?.DeepClone(),["appearance"]=(Owner()["appearance"]??Owner()["modelAppearance"])?.DeepClone()}.ToString(Formatting.None),async cancellation=>{
                var owner=(JObject)Owner().DeepClone();string value=ConfigDocument.At(document.Root,path)?.ToString()??"";
                ModelAppearanceConfig appearance=(owner["appearance"]??owner["modelAppearance"]) is JObject surface?Newtonsoft.Json.JsonConvert.DeserializeObject<ModelAppearanceConfig>(surface.ToString(Formatting.None)):null;
                string external=key=="modelPath"||key=="kannadiModelPath"?value:key=="type"?(string)owner["modelPath"]:null;
                if(!string.IsNullOrWhiteSpace(external))
                {
                    string file=Path.IsPathRooted(external)?external:Path.Combine(RuntimePaths.ConfigRoot,external);
                    if(new FileInfo(file).Length>64*1024*1024)throw new ArgumentException("Model preview is limited to 64 MiB. The complete model can still be used in an experiment.");
                    // A read-only preload: no run archive or acquisition controller is involved.
                    await ExternalModelLibrary.PreloadAsync(external,File.ReadAllBytes(file),cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    if(!ConfigurationPreviewRenderer.Idle)throw new InvalidOperationException("Preview paused for the experiment.");
                    var prefab=ExternalModelLibrary.GetPrefab(external);
                    return ConfigurationPreviewRenderer.Object(prefab,PreviewVisuals.Animations(prefab),null,(string)owner["animationClip"],appearance);
                }
                if(key=="modelPath"||key=="kannadiModelPath")value=key=="kannadiModelPath"?(string)owner["kannadiTilePrefab"]??"SimulatedLocust":(string)owner["type"]??"SimulatedLocust";
                if(key=="modelPath"&&string.IsNullOrEmpty(external)&&string.Equals((string)owner["agentVisual"],"Bogong",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Generated Bogong visual · choose its shape and color in Bogong visual settings.");
                if(key=="agentVisual")value=value=="ScenePrefab"?"SimulatedLocust":value;
                var entry=ChoiceAssetCatalog.Load().Entries(scene,key=="material"?"material":"type").FirstOrDefault(item=>item.Name==value);
                if(entry==null||string.IsNullOrEmpty(entry.PreviewResource))throw new ArgumentException("No authored mesh preview is available for this selection.");
                var definition=await LoadPreviewAsset(entry.PreviewResource,cancellation);ConfigurationPreviewAsset selectedMaterial=null;
                try
                {
                    if(key!="material"&&!string.IsNullOrWhiteSpace((string)owner["material"]))
                    {
                        var materialEntry=ChoiceAssetCatalog.Load().Entries(scene,"material").FirstOrDefault(item=>item.Name==(string)owner["material"]);
                        if(!string.IsNullOrEmpty(materialEntry?.PreviewResource))selectedMaterial=await LoadPreviewAsset(materialEntry.PreviewResource,cancellation);
                    }
                    cancellation.ThrowIfCancellationRequested();
                    if(!ConfigurationPreviewRenderer.Idle)throw new InvalidOperationException("Preview paused for the experiment.");
                    if(definition==null)throw new ArgumentException("This asset has no visual preview in this build.");
                    return ConfigurationPreviewRenderer.Object(definition.visual,definition.animations,selectedMaterial?.material??definition.material,(string)owner["animationClip"],appearance);
                }
                finally{if(selectedMaterial!=null)Resources.UnloadAsset(selectedMaterial);if(definition!=null)Resources.UnloadAsset(definition);}
            });
            pane.name="object-preview-"+string.Join("-",path);target.Add(pane);BindViewState(pane,pane.Refresh);
        }
        private static async Task<ConfigurationPreviewAsset> LoadPreviewAsset(string key,CancellationToken cancellation)
        {
            var resource=Resources.LoadAsync<ConfigurationPreviewAsset>(key);var ready=new TaskCompletionSource<bool>();resource.completed+=_=>ready.TrySetResult(true);if(resource.isDone)ready.TrySetResult(true);await ready.Task;
            var asset=resource.asset as ConfigurationPreviewAsset;
            try{cancellation.ThrowIfCancellationRequested();return asset;}
            catch{if(asset!=null)Resources.UnloadAsset(asset);throw;}
        }
        private void AddSkyPreview(VisualElement target,ConfigDocument document,string[] prefix)
        {
            JObject Payload()=>ConfigDocument.At(document.Root,prefix) as JObject??new JObject();
            var pane=new EditPreviewPane(true,()=>{
                var payload=Payload();return new JObject{["uniformSkyColor"]=payload["uniformSkyColor"]?.DeepClone(),["skyboxPath"]=payload["skyboxPath"]?.DeepClone(),["nightSky"]=payload["nightSky"]?.DeepClone(),["sun"]=payload["sun"]?.DeepClone(),[ExperimentSky.RotationField]=payload[ExperimentSky.RotationField]?.DeepClone()}.ToString(Formatting.None);
            },cancellation=>{cancellation.ThrowIfCancellationRequested();return Task.FromResult(ConfigurationPreviewRenderer.Sky((JObject)Payload().DeepClone(),ChoiceCatalogScene(),DateTimeOffset.UtcNow));});
            pane.name="sky-rendered-preview";target.Add(pane);BindViewState(pane,pane.Refresh);
        }
        /// <summary>Fixed geometry; only edit-time visible panes render. Stale async results are disposed.</summary>
        private sealed class EditPreviewPane:VisualElement
        {
            private readonly Func<string> fingerprint;
            private readonly Func<CancellationToken,Task<Texture2D>> render;
            private readonly Image image;
            private readonly Label caption;
            private readonly string readyCaption;
            private string pendingKey,renderedKey;
            private long changedAt;
            private int generation;
            private bool attached,busy;
            private static bool rendering;
            private Texture2D texture;
            private CancellationTokenSource cancellation;
            public EditPreviewPane(bool sky,Func<string> fingerprint,Func<CancellationToken,Task<Texture2D>> render)
            {
                this.fingerprint=fingerprint;this.render=render;AddToClassList("edit-rendered-preview");
                image=new Image{scaleMode=ScaleMode.ScaleToFit};image.AddToClassList("edit-preview-image");Add(image);
                caption=new Label("Preparing preview…");caption.AddToClassList("edit-preview-caption");Add(caption);
                readyCaption=sky?"Sky preview · Unity +Z, 256 px":"Object preview · rendered sample";
                tooltip=sky?"Rendered only while editing. The camera looks along Unity +Z. The experiment uses the full panorama at its configured resolution.":"A visual-only copy of the selected mesh and materials. Experiment scripts, tracking and recording never run in a thumbnail.";
                RegisterCallback<AttachToPanelEvent>(_=>{attached=true;Refresh();});
                RegisterCallback<DetachFromPanelEvent>(_=>{attached=false;generation++;cancellation?.Cancel();Release();});
                schedule.Execute(Tick).Every(200);
            }
            public void Refresh()
            {
                string key=fingerprint();if(key==pendingKey)return;
                cancellation?.Cancel();pendingKey=key;changedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();generation++;
                caption.text="Updating preview…";
            }
            private bool Visible()
            {
                if(!attached||panel==null||resolvedStyle.display==DisplayStyle.None||worldBound.width<=0)return false;
                for(var parent=hierarchy.parent;parent!=null;parent=parent.hierarchy.parent)
                    if(parent is ScrollView scroll&&!scroll.contentViewport.worldBound.Overlaps(worldBound))return false;
                return true;
            }
            private async void Tick()
            {
                if(!ConfigurationPreviewRenderer.Idle){generation++;cancellation?.Cancel();renderedKey=null;Release();caption.text="Preview paused during experiment";return;}
                if(busy||rendering||!Visible()||renderedKey==pendingKey||DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-changedAt<220)return;
                busy=true;rendering=true;cancellation=new CancellationTokenSource();var requestCancellation=cancellation;int request=generation;string key=pendingKey;Texture2D next=null;
                try
                {
                    next=await render(requestCancellation.Token);
                    if(!attached||request!=generation||!ConfigurationPreviewRenderer.Idle)return;
                    Release();texture=next;next=null;image.image=texture;caption.text=readyCaption;renderedKey=key;
                }
                catch(OperationCanceledException){}
                catch(Exception error){if(attached&&request==generation){Release();caption.text=error.GetBaseException().Message;caption.tooltip=caption.text;renderedKey=key;}}
                finally{PreviewVisuals.Dispose(next);if(cancellation==requestCancellation)cancellation=null;requestCancellation.Dispose();busy=false;rendering=false;}
            }
            private void Release(){image.image=null;PreviewVisuals.Dispose(texture);texture=null;}
        }
    }
}
