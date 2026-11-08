#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ConfigurationUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Import real exported/downloaded skins and morphs; render colors and restore animation evidence.</summary>
public static class RuntimeModelExamplesValidation
{
    private static readonly List<string> checks = new List<string>();
    private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../runtime-model-examples-validation"));
    private static void Check(bool condition,string label) { if(!condition)throw new Exception(label);checks.Add(label); }
    private static Vector3[] Vertices(GameObject root)
    {
        var result=new List<Vector3>();
        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh=new Mesh();try{skin.BakeMesh(mesh);result.AddRange(mesh.vertices.Select(skin.transform.TransformPoint));}finally{Object.DestroyImmediate(mesh);}
        }
        return result.ToArray();
    }
    private static float Difference(Vector3[] a,Vector3[] b)
    {
        if(a.Length!=b.Length)return float.PositiveInfinity;
        float result=0;for(int i=0;i<a.Length;i++)result=Mathf.Max(result,Vector3.Distance(a[i],b[i]));return result;
    }
    private static void Sample(GameObject root,float phase)
    {
        foreach(var animation in root.GetComponentsInChildren<Animation>(true))
        {
            foreach(AnimationState state in animation) {state.enabled=state.name==animation.clip.name;state.weight=state.enabled?1:0;state.time=state.length*phase;}
            animation.Sample();
        }
    }
    public static async void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Disposable project only.");
        Directory.CreateDirectory(Output);checks.Clear();string failure=null;
        try
        {
            MaterialIsolation();
            foreach(string name in new[]{"SimulatedLocust","RiggedSimple","AnimatedMorphCube"})
            {
                string reference="Models/Examples/"+name+".glb";
                string path=Path.Combine(Application.streamingAssetsPath,reference);byte[] bytes=File.ReadAllBytes(path);
                string hash=RunConfigArchive.Hash(bytes);
                await ExternalModelLibrary.PreloadAsync(reference,bytes);
                var prefab=ExternalModelLibrary.GetPrefab(reference);
                var sourceMaterials=prefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer=>renderer.sharedMaterials).ToArray();
                var materialJson=sourceMaterials.Select(material=>material!=null?EditorJsonUtility.ToJson(material):"").ToArray();
                var instance=ExternalModelLibrary.InstantiatePrefab(prefab);GameObject replay=null;
                try
                {
                    Check(instance.GetComponentsInChildren<Renderer>(true).Length>0,name+": actual runtime import contains visible geometry");
                    Check(instance.GetComponentsInChildren<Animation>(true).Any(animation=>animation.clip!=null),name+": embedded animation imports without a Unity rebuild");
                    Check(instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length>0,name+": skeletal/morph mesh retained");
                    if(name=="AnimatedMorphCube")Check(instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(skin=>skin.sharedMesh.blendShapeCount>=2),"Downloaded morph model retains both morph targets");
                    Sample(instance,.1f);var a=Vertices(instance);var capture=new RecordedAnimationCapture(instance);string evidenceA=capture.Capture();
                    Sample(instance,.65f);var b=Vertices(instance);string evidenceB=capture.Capture();
                    Check(a.Length>0&&Difference(a,b)>.00001f,name+": real deformed vertices change between two animation times");
                    replay=ExternalModelLibrary.InstantiatePrefab(prefab);ReplayEnvironmentLoader.FreezeVisual(replay);var driver=replay.AddComponent<ReplayAnimationDriver>();
                    driver.Sample(evidenceA,0);Check(Difference(a,Vertices(replay))<.001f,name+": original imported animation restores at first saved time");
                    driver.Sample(evidenceB,1);Check(Difference(b,Vertices(replay))<.001f,name+": forward seek restores original deformed geometry");
                    driver.Sample(evidenceA,0);Check(Difference(a,Vertices(replay))<.001f,name+": backward seek restores original deformed geometry");
                    Texture2D red=null,blue=null;
                    try
                    {
                        red=ConfigurationPreviewRenderer.Object(prefab,PreviewVisuals.Animations(prefab),appearance:Appearance(Color.red));
                        blue=ConfigurationPreviewRenderer.Object(prefab,PreviewVisuals.Animations(prefab),appearance:Appearance(Color.blue));
                        File.WriteAllBytes(Path.Combine(Output,name+"-red.png"),red.EncodeToPNG());File.WriteAllBytes(Path.Combine(Output,name+"-blue.png"),blue.EncodeToPNG());
                        Check(red.GetPixels32().Count(pixel=>pixel.r>pixel.b+20)>25,name+": chosen red appearance is visible in the real GPU preview");
                        Check(blue.GetPixels32().Count(pixel=>pixel.b>pixel.r+20)>25,name+": chosen blue appearance is visible in the real GPU preview");
                    }
                    finally { if(red!=null)Object.DestroyImmediate(red);if(blue!=null)Object.DestroyImmediate(blue); }
                    if (name == "SimulatedLocust") TextureVariants(prefab, instance);
                    ModelAppearance.Apply(instance,Appearance(Color.green));
                    var target=instance.AddComponent<RecordedWorldStateTarget>();target.Key="model-appearance-test";var world=target.Capture(true);
                    Check(world!=null&&world["nodes"].Any(node=>node["renderers"].Any()),name+": actual renderer appearance enters extensible world evidence");
                    Check(sourceMaterials.Select((material,index)=>material!=null?EditorJsonUtility.ToJson(material)==materialJson[index]:true).All(value=>value),name+": source materials remain untouched");
                    Check(hash==RunConfigArchive.Hash(File.ReadAllBytes(path)),name+": source GLB remains byte-identical after import, overrides and replay");
                }
                finally { Object.DestroyImmediate(instance);if(replay!=null)Object.DestroyImmediate(replay); }
                Check(ModelAppearance.ActiveMaterialCount==0,name+": preview and instance material copies are all released");
            }
        }
        catch(Exception error){failure=error.ToString();Debug.LogError(failure);}
        File.WriteAllText(Path.Combine(Output,"results.json"),JsonConvert.SerializeObject(new{checks=checks.Count,passed=checks,failure,unity=Application.unityVersion,scope="Actual exported locust plus downloaded Khronos skeletal/morph GLBs through production runtime importer, GPU color previews, original deformed animation forward/backward restore and immutable source assets; no physical hardware."},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
    private static void TextureVariants(GameObject prefab, GameObject instance)
    {
        string red = Path.Combine(Output, "locust-texture-red.png");
        string blue = Path.Combine(Output, "locust-texture-blue.png");
        WriteTexture(red, Color.red);
        WriteTexture(blue, Color.blue);
        var settings = new ModelAppearanceConfig {
            enabled = false, keepTexture = false, selectedTextureVariant = "Red",
            textureVariants = new[] {
                new ModelTextureVariantConfig { name = "Red", texturePath = red },
                new ModelTextureVariantConfig { name = "Blue", texturePath = blue }
            }
        };
        var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
        Material[] original = renderer.sharedMaterials;
        Texture2D redPreview = null, bluePreview = null;
        try
        {
            var clips = PreviewVisuals.Animations(prefab);
            redPreview = ConfigurationPreviewRenderer.Object(prefab, clips, appearance: settings);
            settings.selectedTextureVariant = "blue";
            bluePreview = ConfigurationPreviewRenderer.Object(prefab, clips, appearance: settings);
            Check(redPreview.GetPixels32().Count(pixel => pixel.r > pixel.b + 20) > 25, "Red PNG replaces base-color pixels with tint disabled");
            Check(bluePreview.GetPixels32().Count(pixel => pixel.b > pixel.r + 20) > 25, "Blue PNG replaces base-color pixels with tint disabled");
            File.WriteAllBytes(Path.Combine(Output, "locust-selected-red.png"), redPreview.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(Output, "locust-selected-blue.png"), bluePreview.EncodeToPNG());
            ModelAppearance.Apply(instance, settings, preview: true);
            Check(!renderer.sharedMaterials.SequenceEqual(original), "Selecting an external texture uses instance-owned materials");
            settings.selectedTextureVariant = "";
            ModelAppearance.Apply(instance, settings, preview: true);
            Check(renderer.sharedMaterials.SequenceEqual(original), "Original restores the imported material objects");
        }
        finally
        {
            if (redPreview != null) Object.DestroyImmediate(redPreview);
            if (bluePreview != null) Object.DestroyImmediate(bluePreview);
        }
    }
    private static void WriteTexture(string path, Color color)
    {
        var texture = new Texture2D(8, 8);
        try
        {
            texture.SetPixels(Enumerable.Repeat(color, 64).ToArray());
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(texture); }
    }

    private static ModelAppearanceConfig Appearance(Color color)=>new ModelAppearanceConfig{enabled=true,keepTexture=false,color=new ColorConfig{r=color.r,g=color.g,b=color.b,a=1},metallic=0,smoothness=.4f};
    private static void MaterialIsolation()
    {
        var root=GameObject.CreatePrimitive(PrimitiveType.Cube);var renderer=root.GetComponent<Renderer>();
        var a=new Material(Shader.Find("Standard")){color=Color.red};var b=new Material(Shader.Find("Standard")){color=Color.blue};renderer.sharedMaterials=new[]{a,b};
        try
        {
            for(int i=0;i<10;i++)ModelAppearance.Apply(root,Appearance(i%2==0?Color.green:Color.yellow));
            Check(ModelAppearance.ActiveMaterialCount==2,"Repeated overrides replace both material slots without retaining old copies");
            Check(a.color==Color.red&&b.color==Color.blue,"Per-instance multi-slot appearance leaves both original materials unchanged");
            ModelAppearance.Apply(root,new ModelAppearanceConfig{enabled=false});
            Check(renderer.sharedMaterials[0]==a&&renderer.sharedMaterials[1]==b&&ModelAppearance.ActiveMaterialCount==0,"Disabling appearance restores the original material slots");
            bool rejected=false;try{ModelAppearance.Apply(root,new ModelAppearanceConfig{enabled=true,metallic=2});}catch(ArgumentException){rejected=true;}
            Check(rejected&&ModelAppearance.ActiveMaterialCount==0,"Invalid appearance values fail before allocating partial materials");
            var controllerHost=new GameObject("Surface configuration validation");
            try
            {
                var controller=controllerHost.AddComponent<ChoiceController>();
                var materials=(Dictionary<string,Material>)typeof(ChoiceController).GetField("materialDict",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
                materials["Chosen"]=b;
                var configure=typeof(ChoiceController).GetMethod("ConfigureRegularObjectInstance",BindingFlags.Instance|BindingFlags.NonPublic);
                configure.Invoke(controller,new object[]{root,new SceneObject{type="Cube",material="Chosen"}});
                Check(renderer.sharedMaterial==b,"Built-in objects still use their explicitly chosen built-in material");
                renderer.sharedMaterials=new[]{a,b};
                configure.Invoke(controller,new object[]{root,new SceneObject{type="Cube",material="Chosen",modelPath="model.glb"}});
                Check(renderer.sharedMaterials[0]==a&&renderer.sharedMaterials[1]==b,"A selected GLB ignores retained inactive built-in type/material settings");
            }
            finally{Object.DestroyImmediate(controllerHost);}
        }
        finally{Object.DestroyImmediate(root);Object.DestroyImmediate(a);Object.DestroyImmediate(b);}
    }
}
#endif
