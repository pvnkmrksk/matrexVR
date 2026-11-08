#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ConfigurationUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Actual GPU thumbnails, visual isolation, animation sampling and acquisition side effects.</summary>
public static class ConfigurationPreviewValidation
{
    private static readonly List<string> checks=new List<string>();
    private static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"configuration-preview-validation");
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    private static string Hash(Texture2D image)=>NightSkyController.Hash(image.EncodeToPNG());
    private static void Save(Texture2D image,string name){File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());}
    private static long Archives()=>new[]{Application.persistentDataPath,Application.temporaryCachePath}.Select(root=>Path.Combine(root,"NightSkyRuns")).Where(Directory.Exists).Sum(root=>(long)Directory.GetFiles(root,"*",SearchOption.AllDirectories).Length);
    private static byte[] ManyNodes(byte[] original)
    {
        int length=(int)BitConverter.ToUInt32(original,12);var json=JObject.Parse(Encoding.UTF8.GetString(original,20,length));
        json["nodes"]=new JArray(Enumerable.Range(0,2000).Select(index=>new JObject{["name"]="Triangle"+index,["mesh"]=0}));
        json["scenes"][0]["nodes"]=new JArray(Enumerable.Range(0,2000));
        byte[] text=Encoding.UTF8.GetBytes(json.ToString(Formatting.None));int padded=(text.Length+3)/4*4;int binStart=20+length;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(0x46546c67u);writer.Write(2u);writer.Write((uint)(20+padded+original.Length-binStart));writer.Write((uint)padded);writer.Write(0x4e4f534au);writer.Write(text);
            for(int index=text.Length;index<padded;index++)writer.Write((byte)' ');writer.Write(original,binStart,original.Length-binStart);return stream.ToArray();
        }
    }
    public static async void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Use a marked disposable project.");
        Directory.CreateDirectory(Output);checks.Clear();string failure=null;
        var originalSky=RenderSettings.skybox;var originalSun=RenderSettings.sun;bool originalFog=RenderSettings.fog;
        string random=JsonUtility.ToJson(UnityEngine.Random.state);long archives=Archives();
        GameObject source=null;AnimationClip clip=null;Material red=null,blue=null;string panoramaPath=Path.Combine(Output,"input-panorama.png");
        try
        {
            source=new GameObject("Original model");source.AddComponent<LocustMover>();source.transform.position=new Vector3(10,20,30);
            var child=GameObject.CreatePrimitive(PrimitiveType.Cube);child.name="Body";child.transform.SetParent(source.transform,false);
            clip=new AnimationClip{legacy=true};clip.SetCurve("Body",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,0,1,1));
            var copied=PreviewVisuals.Clone(source);PreviewVisuals.Sample(copied,new[]{new PreviewAnimationBinding{root="",clip=clip}});
            Check(copied.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"Preview clone contains no experiment MonoBehaviours");
            Check(copied.GetComponentsInChildren<Collider>(true).Length==0&&copied.GetComponentsInChildren<Camera>(true).Length==0,"Preview clone contains no colliders, cameras or tracking components");
            Check(copied.transform.Find("Body").localPosition.x>.3f&&child.transform.localPosition==Vector3.zero,"Animation samples the visual copy while leaving the original pose unchanged");
            PreviewVisuals.Dispose(copied);
            red=new Material(Shader.Find("Standard")){color=Color.red};blue=new Material(Shader.Find("Standard")){color=Color.blue};
            child.GetComponent<Renderer>().sharedMaterial=red;
            var objectImage=ConfigurationPreviewRenderer.Object(source,new[]{new PreviewAnimationBinding{root="",clip=clip}});
            Check(objectImage.width==256&&objectImage.height==256,"Object image is bounded to256 square pixels");
            var pixels=objectImage.GetPixels32();Check(pixels.Any(pixel=>pixel.r>pixel.b+20)&&pixels.Any(pixel=>pixel.r<30),"Object render contains both mesh and background pixels");Save(objectImage,"animated-object");PreviewVisuals.Dispose(objectImage);
            var originalMaterial=child.GetComponent<Renderer>().sharedMaterial;
            var selectedRed=ConfigurationPreviewRenderer.Object(source,null,red);var selectedBlue=ConfigurationPreviewRenderer.Object(source,null,blue);
            Check(Hash(selectedRed)!=Hash(selectedBlue),"Selected built-in material overrides produce different actual object pixels");
            Check(child.GetComponent<Renderer>().sharedMaterial==originalMaterial,"Built-in material preview overrides leave the original renderer binding unchanged");PreviewVisuals.Dispose(selectedRed);PreviewVisuals.Dispose(selectedBlue);
            var materialRed=ConfigurationPreviewRenderer.Object(null,null,red);var materialBlue=ConfigurationPreviewRenderer.Object(null,null,blue);
            Check(Hash(materialRed)!=Hash(materialBlue),"Selected materials produce visibly different rendered thumbnails");PreviewVisuals.Dispose(materialRed);PreviewVisuals.Dispose(materialBlue);
            var solid=ConfigurationPreviewRenderer.Sky(JObject.Parse("{\"uniformSkyColor\":{\"r\":0.4,\"g\":0.2,\"b\":0.1}}"),"Kannadi",DateTimeOffset.UtcNow);
            Check(solid.width==256&&solid.height==128,"Sky preview is bounded to256 by128 pixels");
            var solidPixels=solid.GetPixels32();Check(solidPixels.All(pixel=>pixel.Equals(solidPixels[0]))&&solidPixels[0].r>solidPixels[0].g,"Uniform sky renders the selected solid color");Save(solid,"uniform-sky");PreviewVisuals.Dispose(solid);
            var image=new Texture2D(256,128,TextureFormat.RGBA32,false);var bands=new Color[256*128];
            for(int y=0;y<128;y++)for(int x=0;x<256;x++)bands[y*256+x]=x<64?Color.red:x<128?Color.green:x<192?Color.blue:Color.yellow;
            image.SetPixels(bands);image.Apply();File.WriteAllBytes(panoramaPath,image.EncodeToPNG());PreviewVisuals.Dispose(image);
            var panorama=new JObject{["skyboxPath"]=panoramaPath,["panoramaRotationDegrees"]=0};
            var first=ConfigurationPreviewRenderer.Sky(panorama,"Kannadi",DateTimeOffset.UtcNow);panorama["panoramaRotationDegrees"]=180;
            var rotated=ConfigurationPreviewRenderer.Sky(panorama,"Kannadi",DateTimeOffset.UtcNow);
            Check(Hash(first)!=Hash(rotated),"Panorama rotation changes the visible sky preview");
            var fullPanorama=first.GetPixels32();
            Check(fullPanorama[32].r>200&&fullPanorama[96].g>200&&fullPanorama[160].b>200&&fullPanorama[224].r>200&&fullPanorama[224].g>200,
                "Custom preview shows all four longitude bands in the same image");Save(first,"custom-sky");Save(rotated,"rotated-sky");PreviewVisuals.Dispose(first);PreviewVisuals.Dispose(rotated);
            var choice=ConfigurationPreviewRenderer.Sky(panorama,"Choice",DateTimeOffset.UtcNow);Check(choice.GetPixels32().Any(pixel=>pixel.r>20||pixel.g>20||pixel.b>20),"Choice displays its complete custom panorama");PreviewVisuals.Dispose(choice);
            var settings=new NightSkyConfig{date="2026-10-29",localTime="22:00:00",utcOffsetHours=0,latitude=47.68,longitude=9.18,exposure=4};
            var instant=settings.ResolveStartUtc(DateTimeOffset.UtcNow);Texture2D map;
            using(var renderer=new NightSkyImageRenderer())
            {
                map=renderer.Render(settings,instant,256);var again=renderer.Render(settings,instant,256);
                Check(Hash(map)==Hash(again),"Identical observer and time produce identical star-map pixels");PreviewVisuals.Dispose(again);
                var shifted=renderer.Render(settings,instant.AddHours(6),256);Check(Hash(map)!=Hash(shifted),"Observer clock changes the projected star-map pixels");PreviewVisuals.Dispose(shifted);
            }
            PreviewVisuals.Dispose(map);
            var starConfig=new JObject{["nightSky"]=JObject.FromObject(settings)};var stars=ConfigurationPreviewRenderer.Sky((JObject)LosslessJson.Parse(starConfig.ToString(Formatting.None)),"Kannadi",instant);Save(stars,"star-sky");PreviewVisuals.Dispose(stars);
            var defaultSky=ConfigurationPreviewRenderer.Sky(new JObject(),"Kannadi",instant);Save(defaultSky,"default-sky");Check(defaultSky.GetPixels32().Any(pixel=>pixel.b>pixel.r),"Default sky renders the Unity blue-sky gradient");PreviewVisuals.Dispose(defaultSky);
            ChoiceAssetCatalogBuilder.Generate();Check(Resources.Load<Material>("ConfigurationUI/PreviewSky/Panoramic")!=null&&Resources.Load<Material>("ConfigurationUI/PreviewSky/Procedural")!=null,"Build generation retains both stock sky shaders through explicit material assets");var catalog=new ChoiceAssetCatalog(JObject.Parse(File.ReadAllText("Assets/Resources/ConfigurationUI/choice-assets.json")));
            var locust=catalog.Entries("Kannadi","type").First(entry=>entry.Name=="SimulatedLocust");
            Check(!string.IsNullOrEmpty(locust.PreviewResource),"Build catalog contains a per-object preview resource key");
            var definition=Resources.Load<ConfigurationPreviewAsset>(locust.PreviewResource);
            Check(definition!=null&&definition.visual!=null&&definition.visual.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"Built-in visual alias loads independently and contains no behaviours");
            var model=ConfigurationPreviewRenderer.Object(definition);Save(model,"builtin-locust");PreviewVisuals.Dispose(model);Resources.UnloadAsset(definition);
            byte[] glb=RigWorldValidation.AnimatedTriangle();string glbHash=RunConfigArchive.Hash(glb);const string reference="preview-cancel.glb";
            await ExternalModelLibrary.PreloadAsync(reference,glb);var cached=ExternalModelLibrary.GetPrefab(reference);
            var importImage=ConfigurationPreviewRenderer.Object(cached,PreviewVisuals.Animations(cached));Save(importImage,"external-glb");PreviewVisuals.Dispose(importImage);
            var cancellation=new CancellationTokenSource();byte[] replacement=ManyNodes(glb);cancellation.CancelAfter(5);var importing=ExternalModelLibrary.PreloadAsync(reference,replacement,cancellation.Token);bool cancelled=false;
            try{await importing;}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"An in-flight GLB preview import responds to cancellation");
            Check(ExternalModelLibrary.GetPrefab(reference)==cached,"Cancelling a replacement import preserves the previously loaded model");
            await ExternalModelLibrary.PreloadAsync(reference,glb);
            var reopened=ConfigurationPreviewRenderer.Object(ExternalModelLibrary.GetPrefab(reference),PreviewVisuals.Animations(cached));PreviewVisuals.Dispose(reopened);
            Check(RunConfigArchive.Hash(glb)==glbHash,"Cancel and reopen leave the source GLB bytes unchanged");cancellation.Dispose();
            Check(source.transform.position==new Vector3(10,20,30)&&child.transform.localPosition==Vector3.zero,"Repeated preview renders never move the source object");
            Check(RenderSettings.skybox==originalSky&&RenderSettings.sun==originalSun&&RenderSettings.fog==originalFog,"Preview restores the scene sky, sun and fog");
            Check(JsonUtility.ToJson(UnityEngine.Random.state)==random,"Preview does not consume experiment random state");
            Check(Archives()==archives,"Preview creates no sky archive or acquisition output");
            Check(!Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(camera=>camera.name=="Preview camera"),"Every temporary preview camera is disposed after rendering");
        }
        catch(Exception error){failure=error.ToString();Debug.LogError(error);}
        finally
        {
            PreviewVisuals.Dispose(source);PreviewVisuals.Dispose(clip);PreviewVisuals.Dispose(red);PreviewVisuals.Dispose(blue);
            File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,unity=Application.unityVersion},Formatting.Indented));
            EditorApplication.Exit(failure==null?0:1);
        }
    }
}
#endif
