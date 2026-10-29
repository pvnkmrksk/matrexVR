#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using ConfigurationUI;

/// <summary>Names only: runtime setup does not load all prefab meshes and textures to populate a picker.</summary>
public sealed class ChoiceAssetCatalogBuilder:IPreprocessBuildWithReport
{
    public int callbackOrder=>-10;
    public void OnPreprocessBuild(BuildReport report)=>Generate();
    [MenuItem("Tools/Experiment setup/Refresh available choice objects")]
    public static void Generate()
    {
        RetainSky("Panoramic","Skybox/Panoramic");RetainSky("Procedural","Skybox/Procedural");
        var scenes=new JObject();
        foreach(string path in EditorBuildSettings.scenes.Where(scene=>scene.enabled).Select(scene=>scene.path).Distinct().OrderBy(path=>path,StringComparer.Ordinal))
        {
            var preview=EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var objects=new List<GameObject>();var materials=new List<Material>();bool found=false;
                foreach(var root in preview.GetRootGameObjects())
                {
                    foreach(var controller in root.GetComponentsInChildren<ChoiceController>(true))
                    {found=true;objects.AddRange(controller.prefabs??Array.Empty<GameObject>());materials.AddRange(controller.materials??Array.Empty<Material>());}
                    foreach(var controller in root.GetComponentsInChildren<DynamicSequenceController>(true))
                    {found=true;objects.AddRange(controller.prefabs??Array.Empty<GameObject>());materials.AddRange(controller.materials??Array.Empty<Material>());}
                }
                if(!found)continue;
                scenes[Path.GetFileNameWithoutExtension(path)]=new JObject{["objects"]=Rows(objects),["materials"]=Rows(materials)};
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
        var population=Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog");
        if(population!=null)scenes["Kannadi"]=new JObject{["objects"]=Rows(population.prefabs??Array.Empty<GameObject>()),["materials"]=new JArray()};
        AssetDatabase.SaveAssets();
        string output="Assets/Resources/ConfigurationUI/choice-assets.json";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        string json=new JObject{["schemaVersion"]=2,["scenes"]=scenes}.ToString(Formatting.Indented)+"\n";
        if(!File.Exists(output)||File.ReadAllText(output)!=json)File.WriteAllText(output,json);
        AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);
    }
    private static void RetainSky(string name,string shaderName)
    {
        const string folder="Assets/Resources/ConfigurationUI/PreviewSky";
        Directory.CreateDirectory(folder);AssetDatabase.ImportAsset(folder);
        string destination=folder+"/"+name+".mat";
        if(AssetDatabase.LoadAssetAtPath<Material>(destination)!=null)return;
        var shader=Shader.Find(shaderName);
        if(shader==null)throw new BuildFailedException("Sky preview shader is unavailable: "+shaderName);
        AssetDatabase.CreateAsset(new Material(shader){name="Preview "+name},destination);
    }
    private static string Preview(UnityEngine.Object asset)
    {
        string source=AssetDatabase.GetAssetPath(asset);
        if(string.IsNullOrEmpty(source))return null;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,out string guid,out long localId);
        string key=guid+"_"+localId.ToString(System.Globalization.CultureInfo.InvariantCulture).Replace("-","n");
        const string folder="Assets/Resources/ConfigurationUI/AssetPreviews";
        Directory.CreateDirectory(folder);AssetDatabase.ImportAsset(folder);
        string destination=folder+"/"+key+".asset",prefabPath=folder+"/"+key+".prefab";
        string fingerprint="visual-only-v1:"+AssetDatabase.GetAssetDependencyHash(source);
        var definition=AssetDatabase.LoadAssetAtPath<ConfigurationPreviewAsset>(destination);
        if(definition!=null&&definition.fingerprint==fingerprint&&(asset is Material||definition.visual!=null))return "ConfigurationUI/AssetPreviews/"+key;
        if(definition==null){definition=ScriptableObject.CreateInstance<ConfigurationPreviewAsset>();AssetDatabase.CreateAsset(definition,destination);}
        if(asset is GameObject prefab)
        {
            var visual=PreviewVisuals.Clone(prefab);
            try{definition.visual=PrefabUtility.SaveAsPrefabAsset(visual,prefabPath);definition.animations=PreviewVisuals.Animations(prefab);}
            finally{PreviewVisuals.Dispose(visual);}
        }
        else if(asset is Material material)definition.material=material;
        definition.fingerprint=fingerprint;EditorUtility.SetDirty(definition);
        return "ConfigurationUI/AssetPreviews/"+key;
    }
    private static JArray Rows<T>(IEnumerable<T> source) where T:UnityEngine.Object
    {
        return new JArray(source.Where(asset=>asset!=null && (!(asset is GameObject prefab) || !prefab.GetComponentsInChildren<WindZone>(true).Any(wind=>wind.gameObject.activeSelf&&(wind.windMain!=0||wind.windTurbulence!=0||wind.windPulseMagnitude!=0)))).GroupBy(asset=>asset.name,StringComparer.Ordinal).Select(group=>group.Last())
            .OrderBy(asset=>asset.name,StringComparer.Ordinal).Select(asset=>{
                string path=AssetDatabase.GetAssetPath(asset);string folder=Path.GetDirectoryName(path)?.Replace('\\','/')??"";
                string description=asset is Material material?material.shader!=null?material.shader.name:"Material":Path.GetFileName(folder);
                return new JObject{["name"]=asset.name,["description"]=description,["source"]=path,["previewResource"]=Preview(asset)};
            }));
    }
}
#endif
