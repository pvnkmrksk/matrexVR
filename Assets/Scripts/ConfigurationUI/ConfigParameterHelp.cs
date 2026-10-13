using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ConfigurationUI
{
    /// <summary>Operator descriptions shared by generated schemas and runtime forms.</summary>
    public static class ConfigParameterHelp
    {
        private static JObject catalog;
        public static string Description(string key,string[] path,JObject schema)
        {
            if(catalog==null) catalog=JObject.Parse(Resources.Load<TextAsset>("ConfigurationUI/field-help").text);
            string scoped=null;
            foreach(string parent in path.Reverse().Skip(1))
                if(catalog[parent+"."+key]!=null) { scoped=(string)catalog[parent+"."+key]; break; }
            string help=scoped??(string)schema["x-ui"]?["help"]??(string)catalog[key];
            if(key=="closedLoopOrientation" || key=="closedLoopPosition")
            {
                help=key=="closedLoopOrientation"?
                    "Use the animal’s measured orientation to control virtual yaw. Position is controlled separately by Closed loop position or autopilot.":
                    "Use the animal’s measured translation to move its virtual rig. Orientation remains independent. Translation uses scene units: centimetres for walking, metres on terrain.";
                help+=ConfigSchema.Types(schema).Contains("number")?
                    " True uses gain 1, false disables it, and a numeric value sets this control’s gain. An omitted/null gain retains the scene's setting.":
                    " ON enables this feedback and OFF disables it. The sequence block’s gain scales the closed loop response.";
            }
            if(string.IsNullOrWhiteSpace(help))
                help="Configuration parameter for this experiment. Edit its JSON value here; extensions are preserved when saving. Refer to the configuration reference for scene-specific behavior.";
            string unit=(string)schema["x-ui"]?["unit"];
            string defaults=schema["default"]==null?"Scene or prefab default":schema["default"].Type==JTokenType.Null?"Unset / scene default":schema["default"] is JContainer?"Defaults shown below":schema["default"].ToString();
            return help+"\n\nJSON: "+string.Join(".",path)+(string.IsNullOrEmpty(unit)?"":" · "+unit)+"\nOmitted default: "+defaults;
        }
    }
}
