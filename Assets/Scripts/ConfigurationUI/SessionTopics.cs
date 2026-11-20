using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConfigurationUI
{
    // Definitions and draft values travel with the existing metadata backup, including custom fields.
    internal static class SessionTopics
    {
        internal sealed class Topic
        {
            public string Key, Label, Scope, Kind, Unit;
            public bool Enabled;
            public string[] Choices;
        }
        internal static readonly string[] Conditions = {
            "No visible abnormalities", "Missing left foreleg", "Missing right foreleg",
            "Missing left middle leg", "Missing right middle leg", "Missing left hind leg",
            "Missing right hind leg", "Soft thorax", "Spreading wings"
        };
        private static Topic T(string key,string label,string scope,string kind="Text",bool enabled=false,string unit=null,string[] choices=null) =>
            new Topic {Key=key,Label=label,Scope=scope,Kind=kind,Enabled=enabled,Unit=unit,Choices=choices};
        internal static List<Topic> Definitions(AnimalData data)
        {
            var all = new List<Topic> {
                T("AnimalID","Animal ID","Animal",enabled:true),
                T("Sex","Sex","Animal","Choice",true,choices:new[]{"Unspecified","Female","Male"}),
                T("AgeDays","Age (days)","Animal","Number",true),
                T("LifeStage","Life stage / instar","Animal"),
                T("SinceMolt","Age since last molt","Animal","Number",unit:"hours"),
                T("Treatment","Treatment / group","Animal"),
                T("Condition","General condition","Animal","Condition"),
                T("LastFedAt","Last fed at","Animal","Timestamp"),
                T("LastWateredAt","Last watered at","Animal","Timestamp"),
                T("BodyMass","Body mass","Animal","Number",unit:"mg"),
                T("PreparationNotes","Preparation notes","Animal"),
                T("Comments","Comments","Animal",enabled:true),
                T("ExperimentName","Experiment name","Recording",enabled:true),
                T("ExperimenterName","Experimenter","Recording",enabled:true),
                T("SessionID","Session / trial ID","Recording"),
                T("Objective","Experimental objective","Recording"),
                T("Temperature","Temperature","Recording","Number",unit:"°C"),
                T("Humidity","Relative humidity","Recording","Number",unit:"%"),
                T("Pressure","Atmospheric pressure","Recording","Number",unit:"hPa"),
                T("Comments","Session notes","Recording",enabled:true)
            };
            var setup = data.Extensions != null && data.Extensions.TryGetValue("FieldSetup",out var token) ? token as JArray : null;
            if(setup != null)
            {
                foreach(var obj in setup.OfType<JObject>())
                {
                    var entry=obj.ToObject<Topic>();
                    if(entry==null || string.IsNullOrEmpty(entry.Key) || (entry.Scope!="Animal" && entry.Scope!="Recording")) continue;
                    var existing=all.FirstOrDefault(t=>t.Key==entry.Key && t.Scope==entry.Scope);
                    if(existing!=null) existing.Enabled=entry.Enabled;
                    else if(!all.Any(t=>t.Key==entry.Key && t.Scope==entry.Scope)) all.Add(entry);
                }
            }
            return all;
        }
        internal static void StoreDefinitions(AnimalData data,List<Topic> topics)
        {
            if(data.Extensions==null)data.Extensions=new Dictionary<string,JToken>();
            data.Extensions["FieldSetup"]=JArray.FromObject(topics);
        }
        internal static JToken Get(object target,Topic topic)
        {
            var prop=target.GetType().GetProperty(topic.Key);
            if(prop!=null && prop.PropertyType==typeof(string)) return prop.GetValue(target)==null ? JValue.CreateNull() : new JValue((string)prop.GetValue(target));
            var ext=target is AnimalData d?d.Extensions:((AnimalMetadata)target).Extensions;
            return ext!=null && ext.TryGetValue("Topics",out var token) && token is JObject values ? NormalizeTimestamp(values[topic.Key],topic.Kind) : null;
        }
        internal static void Set(object target,Topic topic,JToken value)
        {
            var prop=target.GetType().GetProperty(topic.Key);
            if(prop!=null && prop.PropertyType==typeof(string)){prop.SetValue(target,value?.Type==JTokenType.Null?null:value?.ToString());return;}
            IDictionary<string,JToken> ext;
            if(target is AnimalData d){if(d.Extensions==null)d.Extensions=new Dictionary<string,JToken>();ext=d.Extensions;}
            else {var a=(AnimalMetadata)target;if(a.Extensions==null)a.Extensions=new Dictionary<string,JToken>();ext=a.Extensions;}
            if(!ext.TryGetValue("Topics",out var token) || !(token is JObject)){token=new JObject();ext["Topics"]=token;}
            ((JObject)token)[topic.Key]=value??JValue.CreateNull();
        }
        private static JToken NormalizeTimestamp(JToken value,string kind)
        {
            if(kind!="Timestamp" || value?.Type!=JTokenType.Date)return value;
            object raw=((JValue)value).Value;
            var stamp=raw is DateTimeOffset offset?offset:new DateTimeOffset((DateTime)raw);
            return new JValue(stamp.ToString("o",CultureInfo.InvariantCulture));
        }
        internal static bool TryTimestamp(string text,out DateTimeOffset date) => DateTimeOffset.TryParseExact(text,"o",CultureInfo.InvariantCulture,DateTimeStyles.None,out date);
        internal static string Snapshot(AnimalData draft,DateTimeOffset at)
        {
            var root=JObject.FromObject(draft);
            var definitions=Definitions(draft);
            Filter(root,"Recording",definitions,at);
            foreach(var animal in (root["Animals"] as JArray ?? new JArray()).OfType<JObject>())Filter(animal,"Animal",definitions,at);
            root["Flies"]=root["Animals"]?.DeepClone();
            if(!definitions.Any(t=>t.Scope=="Animal" && t.Key=="AnimalID" && t.Enabled))
            {root.Remove("UsedAnimalIDs");root.Remove("UsedFlyIDs");}
            root["MetadataCapturedAt"]=at.ToString("o",CultureInfo.InvariantCulture);
            root["FieldSetup"]=JArray.FromObject(definitions.Where(t=>t.Enabled));
            return root.ToString(Formatting.Indented);
        }
        private static void Filter(JObject obj,string scope,List<Topic> definitions,DateTimeOffset at)
        {
            var topics=obj["Topics"] as JObject;
            obj.Remove("SinceWateredHours");
            foreach(var t in definitions.Where(t=>t.Scope==scope))
            {
                if(!t.Enabled)
                {
                    obj.Remove(t.Key);topics?.Remove(t.Key);
                    if(t.Key=="AnimalID") obj.Remove("FlyID");
                    continue;
                }
                if(t.Kind=="Timestamp")
                {
                    string key=t.Key=="LastFedAt"?"StarvedSinceHours":"SinceWateredHours";
                    obj.Remove(key);
                    if(TryTimestamp(NormalizeTimestamp(topics?[t.Key],t.Kind)?.ToString(),out var date))
                    {
                        if(date>at)throw new InvalidOperationException(t.Label+" cannot be in the future.");
                        obj[key]=Math.Round((at-date).TotalHours,6);
                    }
                }
            }
            // Old manually entered starvation durations stay in the draft, never masquerade as a calculated duration.
            if(!definitions.Any(t=>t.Scope==scope && t.Key=="LastFedAt" && t.Enabled))obj.Remove("StarvedSinceHours");
        }
    }
}
