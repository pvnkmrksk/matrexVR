using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ConfigurationUI
{
    /// <summary>Build-time metadata for the exact object names registered by each shipped scene.</summary>
    public sealed class ChoiceAssetCatalog
    {
        public sealed class Entry
        {
            public readonly string Name, Description, Source;
            public Entry(string name,string description,string source){Name=name;Description=description;Source=source;}
            public string Label=>Name;
        }
        private readonly JObject catalog;
        private static ChoiceAssetCatalog current;
        public ChoiceAssetCatalog(JObject value){catalog=value??new JObject();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){current=null;}
        public static ChoiceAssetCatalog Load()
        {
            if(current==null)
            {
                var asset=Resources.Load<TextAsset>("ConfigurationUI/choice-assets");
                current=new ChoiceAssetCatalog(asset!=null?JObject.Parse(asset.text):null);
            }
            return current;
        }
        public IReadOnlyList<Entry> Entries(string scene,string role)
        {
            string key=role=="type"?"objects":role=="material"?"materials":null;
            if(key==null||!(catalog["scenes"]?[scene]?[key] is JArray values))return Array.Empty<Entry>();
            return values.OfType<JObject>().Where(value=>!string.IsNullOrEmpty((string)value["name"]))
                .Select(value=>new Entry((string)value["name"],(string)value["description"],(string)value["source"]))
                .GroupBy(value=>value.Name,StringComparer.Ordinal).Select(group=>group.Last()).OrderBy(value=>value.Name,StringComparer.Ordinal).ToArray();
        }
        public static bool IsObjectField(string[] path)
        {
            return path!=null&&path.Length>=3&&path[path.Length-3]=="objects"&&int.TryParse(path[path.Length-2],out int index)&&index>=0&&
                (path[path.Length-1]=="type"||path[path.Length-1]=="material");
        }
    }
}
