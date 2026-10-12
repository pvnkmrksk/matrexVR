#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Regenerate JSON Schema from the same contracts that the experiment loaders consume.</summary>
public sealed class ConfigurationSchemaBuilder : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => Generate();
    private static readonly DefaultContractResolver Resolver = new DefaultContractResolver();
    [MenuItem("Tools/Experiment setup/Open setup screen (Play)")]
    public static void Preview()
    {
        if(EditorApplication.isPlaying) return;
        if(!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Generate();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/ControlScene.unity");
        SessionState.SetBool("ConfigWorkbench.Preview",true);
        EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    private static void ClearPreviewOnStop()
    { EditorApplication.playModeStateChanged+=s=>{ if(s==PlayModeStateChange.EnteredEditMode) SessionState.SetBool("ConfigWorkbench.Preview",false); }; }
    [MenuItem("Tools/Experiment setup/Regenerate JSON schemas")]
    public static void Generate()
    {
        var schemas = new JObject {
            ["system"] = new JObject { ["type"]="object", ["additionalProperties"]=true, ["properties"]=new JObject {
                ["targetDisplay"]=Schema(typeof(int)), ["targetFrameRate"]=Schema(typeof(int)), ["vSyncCount"]=Schema(typeof(int)),
                ["configs"]=Schema(typeof(SystemConfig[])), ["telemetry"]=Schema(typeof(TelemetryConfig)),
                ["statusOverlay"]=Schema(typeof(StatusOverlayConfig)), ["overheadCamera"]=Schema(typeof(OverheadCameraConfig)) } },
            ["sequence"]=Schema(typeof(SequenceConfig)), ["choice"]=Schema(typeof(SceneConfig)),
            ["swarm"]=Schema(typeof(KannadiConfig)), ["kannadi"]=Schema(typeof(KannadiConfig)),
            ["optomotor"]=Schema(typeof(OptomotorConfig)),
            ["dynamic-choice"]=Schema(typeof(DynamicSequenceController).GetNestedType("DesignFile",BindingFlags.NonPublic))
        };
        foreach (var property in schemas.Properties().ToList())
        {
            var schema=(JObject)property.Value;
            schema["$schema"]="https://json-schema.org/draft/2020-12/schema";
            schema["$id"]="urn:matrex:config:"+property.Name;
            schema["title"]=property.Name;
            string template=property.Name=="system"?"system_config":property.Name;
            string path=Path.Combine(Application.streamingAssetsPath,"Templates",template+".template.json");
            if(File.Exists(path)) schema["default"]=JObject.Parse(File.ReadAllText(path));
            if(property.Name=="choice" || property.Name=="swarm" || property.Name=="kannadi" || property.Name=="optomotor")
            {
                schema["properties"]["autoTrimSettings"]=Schema(typeof(AutoTrimConfig));
                var phase=(JObject)schema.DeepClone(); phase.Remove("$id"); phase.Remove("$schema"); phase.Remove("default");
                var props=(JObject)phase["properties"];
                props["enabled"]=new JObject { ["type"]="boolean",["default"]=true };
                props["durationSeconds"]=new JObject { ["type"]="number",["exclusiveMinimum"]=0,["default"]=10 };
                props["autoTrimSettings"]=Schema(typeof(AutoTrimConfig));
                props["headingReference"]=Schema(typeof(HeadingReferenceConfig));
                if(property.Name=="swarm") props["swarm"]=Schema(typeof(SwarmConfig));
                schema["properties"]["preStimulus"]=phase.DeepClone();
                schema["properties"]["preStimulus"]["properties"]["enabled"]["default"]=false;
                schema["properties"]["stimulus"]=phase.DeepClone();
                schema["properties"]["postStimulus"]=phase.DeepClone();
                schema["properties"]["postStimulus"]["properties"]["enabled"]["default"]=false;
            }
        }
        var system=schemas["system"];
        system["required"]=new JArray("configs"); system["properties"]["configs"]["minItems"]=1;
        system["properties"]["configs"]["type"]="array";
        system["properties"]["targetDisplay"]["minimum"]=0;
        system["properties"]["targetDisplay"]["default"]=1;
        system["properties"]["targetFrameRate"]["default"]=120;
        system["properties"]["vSyncCount"]["default"]=0;
        system["properties"]["vSyncCount"]["minimum"]=0; system["properties"]["vSyncCount"]["maximum"]=4;
        system["properties"]["targetFrameRate"]["x-ui"]=new JObject { ["enabledWhen"]=Rule("vSyncCount",0),["help"]="Positive FPS, or -1 for uncapped. vSync overrides this value." };
        var rig=system["properties"]["configs"]["items"];
        rig["required"]=new JArray("vrId");
        rig["properties"]["sphereDiameter"]["x-ui"]=new JObject { ["visibleWhen"]=Rule("closedLoopMode","FicTrac",0),["help"]="Walking ball diameter in centimetres. Hidden in wing-angle flight mode." };
        rig["properties"]["targetDisplay"]["x-ui"]=new JObject { ["readOnly"]=true,["help"]="The top-level targetDisplay overrides this legacy field." };
        schemas["sequence"]["required"]=new JArray("sequences");
        schemas["sequence"]["properties"]["sequences"]["type"]="array";
        schemas["swarm"]["properties"]["swarm"]=Schema(typeof(SwarmConfig));
        var item=schemas["sequence"]["properties"]["sequences"]["items"];
        item["required"]=new JArray("sceneName","duration","parameters");
        item["properties"]["duration"]["exclusiveMinimum"]=0;
        item["properties"]["autoTrimSettings"]["x-ui"]=new JObject { ["enabledWhen"]=Rule("autoTrim",true) };
        item["properties"]["parameters"]=new JObject { ["type"]="object", ["additionalProperties"]=true, ["properties"]=new JObject {
            ["configFile"]=new JObject { ["type"]="string",["default"]="Templates/experiment.template.json",["x-ui"]=new JObject { ["visibleWhen"]=new JObject { ["ancestorProperty"]="sceneName",["notEquals"]="Choice_desync" } } },
            ["design"]=new JObject { ["type"]="string",["default"]="Templates/dynamic-choice.template.json",["x-ui"]=new JObject { ["visibleWhen"]=new JObject { ["ancestorProperty"]="sceneName",["equals"]="Choice_desync" } } }
        } };
        string root=Path.Combine(Application.dataPath,"Resources/ConfigurationUI"); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,"schemas.json"),schemas.ToString(Formatting.Indented)+"\n");
        AssetDatabase.Refresh();
    }
    private static JObject Rule(string name,params object[] values) => new JObject { ["property"]=name,["oneOf"]=JArray.FromObject(values) };
    private static JObject Schema(Type original)
    {
        Type type=Nullable.GetUnderlyingType(original)??original;
        bool nullable=Nullable.GetUnderlyingType(original)!=null || !original.IsValueType;
        var result=new JObject();
        if(type==typeof(object)) return result;
        if(type==typeof(string)) result["type"]="string";
        else if(type==typeof(bool)) result["type"]="boolean";
        else if(type.IsEnum) { result["type"]=new JArray("string","integer"); result["enum"]=new JArray(Enum.GetNames(type).Cast<object>().Concat(Enum.GetValues(type).Cast<object>().Select(v=>(object)Convert.ToInt32(v)))); }
        else if(type==typeof(int) || type==typeof(long) || type==typeof(uint) || type==typeof(short))
        {
            result["type"]="integer";
            if(type==typeof(int)) { result["minimum"]=int.MinValue; result["maximum"]=int.MaxValue; }
            if(type==typeof(short)) { result["minimum"]=short.MinValue; result["maximum"]=short.MaxValue; }
            if(type==typeof(uint)) { result["minimum"]=0; result["maximum"]=uint.MaxValue; }
        }
        else if(type==typeof(float) || type==typeof(double) || type==typeof(decimal))
        {
            result["type"]="number";
            if(type==typeof(float)) { result["minimum"]=-float.MaxValue; result["maximum"]=float.MaxValue; }
        }
        else if(typeof(IDictionary).IsAssignableFrom(type)) { result["type"]="object"; result["additionalProperties"]=true; }
        else if(type.IsArray || (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type)))
        { result["type"]="array"; result["items"]=Schema(type.IsArray?type.GetElementType():type.GetGenericArguments()[0]); }
        else
        {
            result["type"]="object"; result["additionalProperties"]=true;
            var props=new JObject(); result["properties"]=props;
            if(type==typeof(Vector3)) { foreach(string axis in new[]{"x","y","z"}) props[axis]=new JObject { ["type"]="number",["default"]=0 }; }
            else
            {
                object instance=null;
                try { instance=Activator.CreateInstance(type,true); } catch { }
                var contract=(JsonObjectContract)Resolver.ResolveContract(type);
                foreach(var property in contract.Properties.Where(p=>!p.Ignored && p.Writable))
                {
                    JObject child=Schema(property.PropertyType); props[property.PropertyName]=child;
                    try
                    {
                        object value=instance==null?null:property.ValueProvider.GetValue(instance);
                        if(value==null || value is string || value.GetType().IsPrimitive || value is Enum)
                            child["default"]=value==null?JValue.CreateNull():value is Enum?new JValue(value.ToString()):JToken.FromObject(value);
                    } catch { }
                    if(property.Converter is LocustGainConverter)
                        child["type"]=new JArray("number","boolean","null");
                    Annotate(child,property.PropertyName,type);
                }
            }
        }
        if(nullable && result["type"]!=null)
        { var types=result["type"] is JArray arr?arr:new JArray(result["type"]); if(!types.Any(v=>v.ToString()=="null")) types.Add("null"); result["type"]=types; }
        if(nullable && result["enum"] is JArray values && !values.Any(v=>v.Type==JTokenType.Null)) values.Add(JValue.CreateNull());
        return result;
    }
    private static void Annotate(JObject child,string name,Type owner)
    {
        var ui=new JObject();
        if(name=="autopilotSpeed") ui["enabledWhen"]=Rule("autopilotEnabled",true);
        if(owner==typeof(SceneConfig) && new[]{"windSpeed","windDirection","aglHeight","groundLayerMask"}.Contains(name))
            ui["visibleWhen"]=new JObject { ["contextProperty"]="flight",["equals"]=true };
        if(name=="animationSpeedThreshold") ui["enabledWhen"]=Rule("animateOnMove",true);
        if(name=="initialRotation") ui["enabledWhen"]=Rule("randomInitialRotation",false);
        if(name=="playback") ui["enabledWhen"]=new JObject { ["property"]="trajectoryFile",["nonEmpty"]=true };
        if(new[]{"muElevation","kappaElevation","boundaryHeight","wrapY"}.Contains(name)) ui["visibleWhen"]=Rule("dimension","3D");
        if(name=="bogongVisual") ui["visibleWhen"]=Rule("agentVisual","Bogong");
        if(owner==typeof(BogongVisualConfig))
        {
            if(name=="size") ui["visibleWhen"]=Rule("sizeMode","World");
            if(name=="angularSizeDegrees") ui["visibleWhen"]=Rule("sizeMode","Angular");
            if(new[]{"bodyLengthMetres","bodyDiameterMetres","wingSpanMetres","flapFrequencyHz","wingOpacity","subpixelVisibility"}.Contains(name)) ui["visibleWhen"]=Rule("shape","Moth");
        }
        if(owner==typeof(SwarmPlaybackConfig) && new[]{"despawnDistanceMetres","terminalVelocityWindowSeconds","continuationSpeedMetresPerSecond","preRollSeconds"}.Contains(name)) ui["enabledWhen"]=Rule("continueAfterTrack",true);
        if(owner==typeof(AutoTrimConfig) && name.StartsWith("flight") && name!="flightCheckEnabled") ui["enabledWhen"]=Rule("flightCheckEnabled",true);
        if(owner==typeof(DynamicSequenceController.Trigger))
        {
            if(name=="seconds") ui["visibleWhen"]=Rule("type","time");
            if(new[]{"areaTag","vrId","shape","size","boxSize","radius","height","triggerOnExit","timeoutSeconds"}.Contains(name)) ui["visibleWhen"]=Rule("type","area");
        }
        if(name.EndsWith("Seconds") || name=="duration") ui["unit"]="seconds";
        if(name.EndsWith("Degrees") || name=="mu" || name=="muElevation" || name=="windDirection") ui["unit"]="degrees";
        if(name.EndsWith("Metres")) ui["unit"]="metres";
        if(name=="autopilotSpeed" || name=="locustSpeed" || name=="translateSpeed") ui["unit"]="scene units / s";
        if(name=="latitude") { child["minimum"]=-90; child["maximum"]=90; }
        if(name=="longitude") { child["minimum"]=-180; child["maximum"]=180; }
        if(name=="port" || name=="zmqPort") { child["minimum"]=1; child["maximum"]=65535; }
        if(owner==typeof(ColorConfig) || name=="contrast" || name=="dutyCycle") { child["minimum"]=0; child["maximum"]=1; }
        Dictionary<string,string[]> enums=new Dictionary<string,string[]> {
            ["dimension"]=new[]{"2D","3D"},["agentVisual"]=new[]{"ScenePrefab","Bogong"},["sizeMode"]=new[]{"World","Angular"},
            ["centerStatistic"]=new[]{"mean","median"},["rotationAxis"]=new[]{"Yaw","Pitch","Roll"} };
        if(enums.TryGetValue(name,out var options)) child["enum"]=new JArray(options);
        if(owner==typeof(BogongVisualConfig) && name=="shape") child["enum"]=new JArray("Dot","Moth");
        if(owner==typeof(DynamicSequenceController.Trigger) && name=="type") child["enum"]=new JArray("time","area");
        if(ui.HasValues) child["x-ui"]=ui;
    }
}
#endif
