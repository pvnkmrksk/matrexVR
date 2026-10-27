using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ConfigurationUI
{
    /// <summary>The shipped schema subset: type, enum, required, properties, items and numeric bounds.</summary>
    public sealed class ConfigSchema
    {
        public JObject Catalog { get; }
        public ConfigSchema(JObject catalog) { Catalog=catalog; }
        public static ConfigSchema Load() => new ConfigSchema(JObject.Parse(Resources.Load<TextAsset>("ConfigurationUI/schemas").text));
        public JObject Get(string id) => Catalog[id] as JObject ?? new JObject();
        public static JObject Child(JObject schema,string key,bool array=false) => (array?schema?["items"]:schema?["properties"]?[key]) as JObject ?? new JObject();
        public static JToken NewValue(JObject schema)
        {
            if(schema["default"]!=null && schema["default"].Type!=JTokenType.Null) return schema["default"].DeepClone();
            string type=Types(schema).FirstOrDefault(t=>t!="null")??"string";
            switch(type)
            {
                case "object":
                    var obj=new JObject();
                    foreach(var prop in (schema["properties"] as JObject ?? new JObject()).Properties()) obj[prop.Name]=prop.Value["default"]?.DeepClone() ?? NewValue((JObject)prop.Value);
                    return obj;
                case "array": return new JArray();
                case "boolean": return new JValue(false);
                case "number": case "integer": return new JRaw("0");
                default: return schema["enum"]?.First?.DeepClone()??new JValue("");
            }
        }
        public static IEnumerable<string> Types(JObject schema) => schema?["type"] is JArray a?a.Select(v=>v.ToString()):schema?["type"]!=null?new[]{schema["type"].ToString()}:Array.Empty<string>();
        // Resolve a separate display/export value. The lossless source DOM is never
        // changed by inspecting defaults, and invalid explicit input remains a
        // validation error until the operator edits or replaces it.
        public static JToken EffectiveValue(JToken value,JObject schema,bool documentRoot=false)
        {
            var types=Types(schema).ToList();
            string kind=LosslessJson.Kind(value);
            bool valid=value!=null && (types.Count==0 || types.Contains(kind) || kind=="integer" && types.Contains("number"));
            if(valid && schema["enum"] is JArray options) valid=options.Any(v=>LosslessJson.Equivalent(v,value));
            if(valid && (kind=="number" || kind=="integer"))
            {
                valid=double.TryParse(value.ToString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double n) && !double.IsNaN(n) && !double.IsInfinity(n);
                if(valid && schema["minimum"]!=null)valid=n>=(double)schema["minimum"];
                if(valid && schema["maximum"]!=null)valid=n<=(double)schema["maximum"];
                if(valid && schema["exclusiveMinimum"]!=null)valid=n>(double)schema["exclusiveMinimum"];
            }
            JToken shown=valid?value:!documentRoot?schema["default"]:null;
            if(shown==null && documentRoot && types.Contains("object"))shown=new JObject();
            if(shown is JObject obj)
            {
                var result=(JObject)obj.DeepClone();
                foreach(var property in (schema["properties"] as JObject??new JObject()).Properties())
                {
                    var child=(JObject)property.Value;
                    // A top-level template describes a starter file, not runtime
                    // defaults. Only declared per-field defaults are inherited.
                    if(obj[property.Name]==null && child["default"]==null)continue;
                    JToken resolved=EffectiveValue(obj[property.Name],child);
                    if(resolved!=null)result[property.Name]=resolved;
                }
                // Omitted rig outputs inherit the system/command-line display at
                // the runtime boundary. SystemConfig's field initializer is not
                // that contextual default; writing it would change output routing.
                if(documentRoot && (string)schema["$id"]=="urn:matrex:config:system" &&
                    obj["configs"] is JArray sourceRigs && result["configs"] is JArray effectiveRigs)
                    for(int i=0;i<sourceRigs.Count;i++)
                        if(sourceRigs[i] is JObject sourceRig && sourceRig.Property("targetDisplay")==null && effectiveRigs[i] is JObject effectiveRig)
                            effectiveRig.Remove("targetDisplay");
                return result;
            }
            if(shown is JArray array)return new JArray(array.Select(item=>EffectiveValue(item,Child(schema,"",true))??item.DeepClone()));
            return shown?.DeepClone();
        }
        public static bool RuleMatches(JToken owner,JToken rule,JObject context=null)
        {
            if(!(rule is JObject r)) return true;
            JToken value=null;
            if(r["contextProperty"]!=null)
            { if(context==null) return true; value=context[r["contextProperty"].ToString()]; }
            else if(r["ancestorProperty"]!=null)
            { for(JToken p=owner;p!=null;p=p.Parent) if(p is JObject o && o[r["ancestorProperty"].ToString()]!=null) { value=o[r["ancestorProperty"].ToString()]; break; } }
            else value=owner?[r["property"]?.ToString()??""];
            // Missing flags follow their schema/runtime defaults; callers add a temporary effective owner.
            if(r["nonEmpty"]!=null) return value?.Type==JTokenType.String && !string.IsNullOrWhiteSpace(value.ToString());
            if(r["oneOf"] is JArray options) return options.Any(o=>LosslessJson.Equivalent(value,o));
            if(r["notEquals"]!=null) return !LosslessJson.Equivalent(value,r["notEquals"]);
            return LosslessJson.Equivalent(value,r["equals"]);
        }
        public static bool Enabled(JToken owner,JObject ownerSchema,string key,JObject context=null)
        {
            if(owner is JObject obj)
            {
                bool? enabled=Flag(obj,ownerSchema,"enabled");
                bool? visible=Flag(obj,ownerSchema,"visible");
                if(key!="enabled" && enabled==false) return false;
                if(key!="visible" && visible==false) return false;
            }
            var child=Child(ownerSchema,key);
            return (bool?)child["x-ui"]?["readOnly"] != true && MatchesWithDefaults(owner,ownerSchema,child["x-ui"]?["enabledWhen"],context);
        }
        private static bool? Flag(JObject owner,JObject schema,string key)
        {
            JToken value=EffectiveValue(owner[key],Child(schema,key));
            return value?.Type==JTokenType.Boolean?(bool?)value:null;
        }
        public static bool Visible(JToken owner,JObject schema,string key,JObject context=null) => MatchesWithDefaults(owner,schema,Child(schema,key)["x-ui"]?["visibleWhen"],context);
        private static bool MatchesWithDefaults(JToken owner,JObject schema,JToken rule,JObject context)
        {
            if(rule?["all"] is JArray conditions) return conditions.All(r=>MatchesWithDefaults(owner,schema,r,context));
            if(rule?["property"]!=null && owner is JObject obj && obj[rule["property"].ToString()]==null)
            {
                var copy=(JObject)obj.DeepClone(); string key=rule["property"].ToString(); copy[key]=Child(schema,key)["default"]?.DeepClone();
                return RuleMatches(copy,rule,context);
            }
            return RuleMatches(owner,rule,context);
        }
        public static List<string> Validate(JToken document,JObject schema)
        { var errors=new List<string>(); Visit(document,schema,"$",errors); return errors; }
        private static void Visit(JToken value,JObject schema,string path,List<string> errors)
        {
            string kind=LosslessJson.Kind(value); var types=Types(schema).ToList();
            if(types.Count>0 && !types.Contains(kind) && !(kind=="integer" && types.Contains("number"))) { errors.Add(path+": expected "+string.Join(" / ",types)+", got "+kind); return; }
            if(value.Type==JTokenType.Null) return;
            if(schema["enum"] is JArray choices && !choices.Any(v=>LosslessJson.Equivalent(v,value))) errors.Add(path+": choose "+string.Join(", ",choices));
            if((kind=="number" || kind=="integer") && types.Count>0)
            {
                double n;
                if(!double.TryParse(value.ToString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out n))
                { errors.Add(path+": number exceeds the experiment runtime range"); return; }
                if(types.Count>0 && (double.IsNaN(n) || double.IsInfinity(n))) errors.Add(path+": number exceeds the experiment runtime range");
                if(schema["minimum"]!=null && n<(double)schema["minimum"]) errors.Add(path+": below minimum "+schema["minimum"]);
                if(schema["maximum"]!=null && n>(double)schema["maximum"]) errors.Add(path+": above maximum "+schema["maximum"]);
                if(schema["exclusiveMinimum"]!=null && n<=(double)schema["exclusiveMinimum"]) errors.Add(path+": must be greater than "+schema["exclusiveMinimum"]);
            }
            if(value is JObject obj)
            {
                if(schema["required"] is JArray required) foreach(var key in required) if(obj[key.ToString()]==null) errors.Add(path+": missing "+key);
                foreach(var prop in obj.Properties()) Visit(prop.Value,Child(schema,prop.Name),path+"."+prop.Name,errors);
            }
            if(value is JArray array)
            {
                if(schema["minItems"]!=null && array.Count<(int)schema["minItems"]) errors.Add(path+": needs at least "+schema["minItems"]+" item(s)");
                for(int i=0;i<array.Count;i++) Visit(array[i],Child(schema,"",true),path+"["+i+"]",errors);
            }
        }
        public static string ForScene(string scene) => scene=="Choice_desync"?"dynamic-choice":scene=="Swarm"?"swarm":scene=="Kannadi"?"kannadi":scene=="Optomotor"?"optomotor":"choice";
    }
}
