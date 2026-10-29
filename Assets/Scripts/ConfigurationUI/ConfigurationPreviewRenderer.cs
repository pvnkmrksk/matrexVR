using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConfigurationUI
{
    /// <summary>Single-frame, bounded setup render. Owns every camera, light and texture it creates.</summary>
    public static class ConfigurationPreviewRenderer
    {
        public const int Width=256;
        public static bool Idle => MainController.Instance?.SequenceRunning!=true&&MainController.Instance?.SequencePreparing!=true&&MainController.Instance?.SequenceStopping!=true&&MasterDataLogger.Instance?.IsFinalizing!=true;
        public static Texture2D Object(ConfigurationPreviewAsset asset,string clip=null)
        {
            if(asset==null)throw new ArgumentException("This asset has no visual preview in this build.");
            return Object(asset.visual,asset.animations,asset.material,clip);
        }
        public static Texture2D Object(GameObject source,PreviewAnimationBinding[] clips,Material material=null,string clip=null)
        {
            if(!Idle)throw new InvalidOperationException("Previews pause while an experiment is running.");
            GameObject visual=null;
            try
            {
                if(source!=null){visual=PreviewVisuals.Clone(source);PreviewVisuals.Sample(visual,clips,clip);}
                else if(material!=null)
                {
                    visual=GameObject.CreatePrimitive(PrimitiveType.Sphere);var collider=visual.GetComponent<Collider>();collider.enabled=false;PreviewVisuals.Dispose(collider);visual.GetComponent<Renderer>().sharedMaterial=material;
                }
                else throw new ArgumentException("Procedural objects do not have a static visual preview.");
                var renderers=visual.GetComponentsInChildren<Renderer>(false);
                bool any=false;var bounds=new Bounds();
                foreach(var renderer in renderers)
                    if(renderer.enabled&&(renderer is MeshRenderer||renderer is SkinnedMeshRenderer))
                    {if(!any){bounds=renderer.bounds;any=true;}else bounds.Encapsulate(renderer.bounds);}
                if(!any)throw new ArgumentException("This object has no authored visible meshes. Its procedural behaviour runs only in the experiment.");
                float radius=bounds.extents.magnitude;
                if(float.IsNaN(radius)||float.IsInfinity(radius)||radius<=0)throw new ArgumentException("Object bounds are empty or invalid.");
                foreach(var node in visual.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
                using(var stage=new Stage(Width,Width))
                {
                    visual.transform.SetParent(stage.root.transform,true);
                    stage.camera.clearFlags=CameraClearFlags.SolidColor;stage.camera.backgroundColor=new Color(.055f,.065f,.08f,1);
                    stage.camera.fieldOfView=32;stage.camera.nearClipPlane=Mathf.Max(.0001f,radius*.005f);stage.camera.farClipPlane=radius*20;
                    float distance=radius/Mathf.Sin(stage.camera.fieldOfView*Mathf.Deg2Rad/2)*1.15f;
                    stage.camera.transform.position=bounds.center+new Vector3(1,.5f,1).normalized*distance;
                    stage.camera.transform.LookAt(bounds.center);
                    stage.sun.transform.rotation=Quaternion.Euler(40,315,0);stage.sun.intensity=1.2f;
                    return stage.Render();
                }
            }
            finally{PreviewVisuals.Dispose(visual);}
        }
        public static Texture2D Sky(JObject config,string scene,DateTimeOffset now)
        {
            if(!Idle)throw new InvalidOperationException("Previews pause while an experiment is running.");
            // Workspace numbers are lossless JRaw tokens. Normalize only this read-only copy
            // before typed deserialization; never replace the editable document.
            config=JObject.Parse((config??new JObject()).ToString(Newtonsoft.Json.Formatting.None));
            Texture2D panorama=null;Material sky=null;
            try
            {
                using(var stage=new Stage(Width,Width/2))
                {
                    var uniform=config?["uniformSkyColor"] as JObject;
                    string path=(string)config?["skyboxPath"];
                    var stars=config?["nightSky"] as JObject;
                    stage.camera.transform.rotation=Quaternion.identity;stage.camera.fieldOfView=75;
                    stage.camera.cullingMask=0;
                    if(uniform!=null)
                    {
                        stage.camera.clearFlags=CameraClearFlags.SolidColor;
                        stage.camera.backgroundColor=new Color((float?)uniform["r"]??0,(float?)uniform["g"]??0,(float?)uniform["b"]??0,1);
                    }
                    else
                    {
                        bool choice=scene=="Choice"||scene=="Choice_desync";
                        if(!string.IsNullOrWhiteSpace(path))
                        {
                            string resolved=Path.IsPathRooted(path)?path:Path.Combine(RuntimePaths.ConfigRoot,path);
                            if(new FileInfo(resolved).Length>64*1024*1024)throw new ArgumentException("Image preview is limited to 64 MiB. The full image can still be used in an experiment.");
                            panorama=new Texture2D(2,2,TextureFormat.RGBA32,false,choice);
                            if(!panorama.LoadImage(File.ReadAllBytes(resolved))||panorama.width!=panorama.height*2)throw new ArgumentException("Choose a 2:1 panorama PNG or JPEG.");
                            sky=choice?StockSky("Panoramic"):new Material(Resources.Load<Shader>("NightSky/Panorama"));
                        }
                        else if(stars!=null&&ExperimentBlocks.Flag(stars["enabled"],true))
                        {
                            var settings=stars.ToObject<NightSkyConfig>();var sample=settings.ResolveStartUtc(now);if(settings.roundToInterval)sample=settings.SampleUtc(sample);
                            using(var bake=new NightSkyImageRenderer())panorama=bake.Render(settings,sample,Width*4);
                            sky=new Material(Resources.Load<Shader>("NightSky/Panorama"));
                        }
                        else
                        {
                            sky=StockSky("Procedural");
                        }
                        if(panorama!=null){panorama.wrapModeU=TextureWrapMode.Repeat;panorama.wrapModeV=TextureWrapMode.Clamp;panorama.filterMode=FilterMode.Bilinear;sky.mainTexture=panorama;}
                        if(sky.HasProperty("_Rotation"))sky.SetFloat("_Rotation",ExperimentSky.Rotation(config));
                        sky.hideFlags=HideFlags.HideAndDontSave;stage.camera.gameObject.AddComponent<Skybox>().material=sky;stage.camera.clearFlags=CameraClearFlags.Skybox;
                    }
                    if(config?["sun"] is JObject sun)
                    {
                        var settings=sun.ToObject<SunConfig>();settings.Validate();stage.sun.enabled=settings.enabled;
                        stage.sun.intensity=settings.intensity;stage.sun.color=new Color(settings.color.r,settings.color.g,settings.color.b,1);
                        stage.sun.transform.rotation=Quaternion.Euler(settings.rotation.ToVector3());
                    }
                    else{stage.sun.transform.rotation=Quaternion.Euler(50,330,0);stage.sun.intensity=1;}
                    return stage.Render();
                }
            }
            finally{PreviewVisuals.Dispose(panorama);PreviewVisuals.Dispose(sky);}
        }
        private static Material StockSky(string name)
        {
            var retained=Resources.Load<Material>("ConfigurationUI/PreviewSky/"+name);
            if(retained!=null)return new Material(retained);
            var shader=Shader.Find("Skybox/"+name);
            if(shader==null)throw new InvalidOperationException("The sky preview shader is missing. Refresh available choice objects before building.");
            return new Material(shader);
        }
        private sealed class Stage:IDisposable
        {
            public readonly GameObject root;
            public readonly Camera camera;
            public readonly Light sun;
            private readonly RenderTexture target;
            public Stage(int width,int height)
            {
                root=new GameObject("Configuration preview (temporary)"){hideFlags=HideFlags.HideAndDontSave};
                var cameraObject=new GameObject("Preview camera");cameraObject.transform.SetParent(root.transform,false);camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
                camera.cullingMask=1<<31;camera.allowHDR=false;camera.allowMSAA=false;camera.aspect=(float)width/height;camera.useOcclusionCulling=false;
                target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=target;
                var light=new GameObject("Preview sun");light.transform.SetParent(root.transform,false);sun=light.AddComponent<Light>();sun.type=LightType.Directional;sun.cullingMask=1<<31;sun.shadows=LightShadows.None;
            }
            public Texture2D Render()
            {
                // The local Skybox component owns the image. Restore global lighting immediately;
                // no DynamicGI update, file writer or acquisition controller is involved.
                var previousTarget=RenderTexture.active;var previousSun=RenderSettings.sun;var previousMode=RenderSettings.ambientMode;var previousAmbient=RenderSettings.ambientLight;bool fog=RenderSettings.fog;
                var otherLights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                var disabled=new System.Collections.Generic.List<Light>();
                Texture2D image=null;
                try
                {
                    foreach(var light in otherLights)if(light!=sun&&light.enabled){light.enabled=false;disabled.Add(light);}
                    RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.42f,.42f,.42f);RenderSettings.fog=false;
                    camera.Render();RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                    image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply(false,false);var result=image;image=null;return result;
                }
                finally{RenderTexture.active=previousTarget;RenderSettings.sun=previousSun;RenderSettings.ambientMode=previousMode;RenderSettings.ambientLight=previousAmbient;RenderSettings.fog=fog;foreach(var light in disabled)if(light!=null)light.enabled=true;PreviewVisuals.Dispose(image);}
            }
            public void Dispose(){camera.targetTexture=null;PreviewVisuals.Dispose(root);RenderTexture.ReleaseTemporary(target);}
        }
    }
}
