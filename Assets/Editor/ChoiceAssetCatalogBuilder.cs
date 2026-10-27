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

/// <summary>Names only: runtime setup does not load all prefab meshes and textures to populate a picker.</summary>
public sealed class ChoiceAssetCatalogBuilder:IPreprocessBuildWithReport
{
    public int callbackOrder=>-10;
    public void OnPreprocessBuild(BuildReport report)=>Generate();
    [MenuItem("Tools/Experiment setup/Refresh available choice objects")]
    public static void Generate()
    {
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
        string output="Assets/Resources/ConfigurationUI/choice-assets.json";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        string json=new JObject{["schemaVersion"]=1,["scenes"]=scenes}.ToString(Formatting.Indented)+"\n";
        if(!File.Exists(output)||File.ReadAllText(output)!=json)File.WriteAllText(output,json);
        AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);
    }
    private static JArray Rows<T>(IEnumerable<T> source) where T:UnityEngine.Object
    {
        return new JArray(source.Where(asset=>asset!=null).GroupBy(asset=>asset.name,StringComparer.Ordinal).Select(group=>group.Last())
            .OrderBy(asset=>asset.name,StringComparer.Ordinal).Select(asset=>{
                string path=AssetDatabase.GetAssetPath(asset);string folder=Path.GetDirectoryName(path)?.Replace('\\','/')??"";
                string description=asset is Material material?material.shader!=null?material.shader.name:"Material":Path.GetFileName(folder);
                return new JObject{["name"]=asset.name,["description"]=description,["source"]=path};
            }));
    }
}
#endif
