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
            ["rugosity"]=Schema(typeof(RugosityConfig)),
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
                props["durationSeconds"]=new JObject { ["type"]="number",["exclusiveMinimum"]=0,["default"]=10,["x-ui"]=new JObject { ["unit"]="seconds",["slider"]=new JObject { ["min"]=1,["max"]=3600,["step"]=1 } } };
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
        // Top-level monitoring objects are optional, but have real runtime defaults.
        // Expose those to the form without inserting them into the user's JSON.
        system["properties"]["telemetry"]["default"]=JToken.FromObject(new TelemetryConfig());
        system["properties"]["statusOverlay"]["default"]=JToken.FromObject(new StatusOverlayConfig());
        system["properties"]["overheadCamera"]["default"]=JToken.FromObject(new OverheadCameraConfig());
        system["properties"]["targetDisplay"]["minimum"]=0;
        system["properties"]["targetDisplay"]["default"]=1;
        system["properties"]["targetFrameRate"]["default"]=120;
        system["properties"]["vSyncCount"]["default"]=0;
        system["properties"]["vSyncCount"]["minimum"]=0; system["properties"]["vSyncCount"]["maximum"]=4;
        system["properties"]["targetFrameRate"]["x-ui"]=new JObject { ["enabledWhen"]=Rule("vSyncCount",0),["help"]="Positive FPS, or -1 for uncapped. vSync overrides this value." };
        system["properties"]["targetFrameRate"]["x-ui"]["slider"]=new JObject { ["min"]=30,["max"]=240,["step"]=1 };
        var rig=system["properties"]["configs"]["items"];
        rig["required"]=new JArray("vrId");
        rig["properties"]["sphereDiameter"]["x-ui"]=new JObject { ["visibleWhen"]=Rule("closedLoopMode","FicTrac","Hejjetrac","HejjéTrac","hejjetrak",0,3),["help"]="Walking ball diameter in centimetres. Hidden in wing-angle flight mode." };
        rig["properties"]["targetDisplay"]["x-ui"]=new JObject { ["readOnly"]=true,["help"]="The top-level targetDisplay overrides this legacy field." };
        schemas["sequence"]["required"]=new JArray("sequences");
        schemas["sequence"]["properties"]["sequences"]["type"]="array";
        schemas["swarm"]["properties"]["swarm"]=Schema(typeof(SwarmConfig));
        var item=schemas["sequence"]["properties"]["sequences"]["items"];
        item["required"]=new JArray("sceneName","duration","parameters");
        item["properties"]["duration"]["exclusiveMinimum"]=0;
        item["properties"]["autoTrimSettings"]["x-ui"]=new JObject { ["enabledWhen"]=Rule("autoTrim",true) };
        item["properties"]["parameters"]=new JObject { ["type"]="object", ["additionalProperties"]=true, ["properties"]=new JObject {
            ["rugosity"]=Schema(typeof(RugosityConfig)),
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
        else if(type.IsEnum) { result["type"]=new JArray("string","integer"); result["enum"]=new JArray(Enum.GetNames(type).Cast<object>().Concat(Enum.GetValues(type).Cast<object>().Select(v=>(object)Convert.ToInt32(v)))); if(type==typeof(ClosedLoopMode)) { ((JArray)result["enum"]).Add("HejjéTrac");((JArray)result["enum"]).Add("hejjetrak"); } }
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
                        else if(value is Vector3 vector)
                            child["default"]=new JObject { ["x"]=vector.x,["y"]=vector.y,["z"]=vector.z };
                        else if(!property.PropertyType.IsArray && !typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                            child["default"]=JToken.FromObject(value);
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
        var attribute=owner.GetField(name)?.GetCustomAttribute<TooltipAttribute>();
        if(attribute!=null) ui["help"]=attribute.tooltip;
        if(name=="speed" && owner==typeof(OptomotorStimulus)) { ui["unit"]="degrees / s"; ui["help"]="Angular drum speed in degrees per second. Use nonnegative speed and the clockwise switch to choose direction."; }
        if(name=="speed" && owner==typeof(SwarmPlaybackConfig)) { ui["unit"]="× time"; ui["help"]="Playback time multiplier. 1 reproduces recorded timing; 2 runs twice as fast. This is not a speed in metres per second."; }
        if(name=="loop" && owner==typeof(OptomotorConfig)) ui["help"]="Repeat the configured optomotor stimuli while this scene runs. The sequence block's duration still ends the scene.";
        if(name=="loop" && owner==typeof(SwarmPlaybackConfig)) ui["help"]="Repeat the recorded swarm timeline. Continued agents keep moving according to the continuation settings; this does not change the outer scene duration.";
        if(name=="autopilotSpeed") ui["enabledWhen"]=Rule("autopilotEnabled",true);
        if(owner==typeof(SceneConfig) && new[]{"windSpeed","windDirection","aglHeight","groundLayerMask"}.Contains(name))
            ui["visibleWhen"]=new JObject { ["contextProperty"]="flight",["equals"]=true };
        if(name=="animationSpeedThreshold") ui["enabledWhen"]=Rule("animateOnMove",true);
        if(name=="initialPosition" && owner.GetField("resetPositionOnStart")!=null) ui["enabledWhen"]=Rule("resetPositionOnStart",true);
        if(name=="initialRotation" && owner.GetField("randomInitialRotation")!=null)
            ui["enabledWhen"]=owner.GetField("resetRotationOnStart")!=null?new JObject { ["all"]=new JArray(Rule("randomInitialRotation",false),Rule("resetRotationOnStart",true)) }:Rule("randomInitialRotation",false);
        if(name=="playback") ui["enabledWhen"]=new JObject { ["property"]="trajectoryFile",["nonEmpty"]=true };
        if(name=="speciesName") ui["help"]="Common or scientific name of the real animal controlling this rig. Saved with its AnimalID in run metadata.";
        if(owner==typeof(TelemetryConfig) && name=="contextIntervalSeconds")
        {
            child["minimum"]=.25;child["maximum"]=60;child["default"]=2;
            ui["unit"]="seconds";ui["help"]="Compact session context heartbeat for late-joining dashboards. Topic matrex.telemetry.v2.context uses schema 1/kind matrex.context; normal frame snapshots retain schema 2.";
        }
        if(owner==typeof(TrackerLaunchConfig)) ui["help"]=name=="executablePath"?"Absolute installed native executable. On macOS choose Contents/MacOS inside the app. Nothing launches automatically.":name=="configFile"?"Existing tracker calibration file. Relative paths resolve inside the MATREX config folder; MATREX never rewrites this file.":name=="device"?"Optional Rekkefly device override. HejjéTrac source selection stays in its calibration config.":"Optional headless Rekkefly launch. HejjéTrac uses its native desktop window.";
        if(owner==typeof(VRConfig) && (name=="vrIndex" || name=="watchIndex")) child["minimum"]=1;
        if(new[]{"agentSpeed","locoSpeed","agentSpeedStdDev","locustSpeedStdDev","volumeHalfExtentMetres","preserveBoundaryOvershoot"}.Contains(name))
            ui["visibleWhen"]=Rule("trajectoryFile","");
        if(new[]{"agentCount","agentSpeed","locoSpeed"}.Contains(name)) child["minimum"]=0;
        if(name=="agentCount") ui["help"]="Number of simulated agents. Takes precedence over legacy numberOfLocusts; omitted preserves the existing scene/config count.";
        if(name=="agentSpeed") ui["help"]="Generated agent speed in scene units/s. Takes precedence over locustSpeed and locoSpeed. Recorded trajectory timing is controlled by playback.speed.";
        if(name=="locoSpeed") ui["help"]="Legacy agent-speed spelling. Used only when agentSpeed and locustSpeed are omitted.";
        if(name=="locustSpeedStdDev" || name=="agentSpeedStdDev")
        {
            child["minimum"]=0;
            ui["help"]="Generated speed standard deviation, not variance. Sampled once per agent; zero retains constant speed.";
        }
        if(name=="volumeHalfExtentMetres")
        {
            child["exclusiveMinimum"]=0;
            ui["help"]="Physical half extent of the generated cube. 2 gives +/-2 m, a 4 m cube; overrides spawn/boundary sizes. Null preserves legacy sizes.";
        }
        if(name=="preserveBoundaryOvershoot") ui["help"]="Keep excess travel when wrapping the generated population; legacy edge snapping is used when false.";
        if(name=="headingReferenceId")
        {
            ui["enabledWhen"]=Rule("useHeadingReference",true);
            ui["help"]="Named per-animal mean saved by headingReference.saveAs in an earlier block of this cycle. Empty uses the current experiment's result. Missing named references stop the run.";
        }
        if(owner==typeof(HeadingReferenceConfig) && name=="measureDuringPresentation")
            ui["help"]="Retained legacy setting. Observation now fits within the configured scene duration; it never extends a scene. False still permits spawners to reapply the completed reference.";
        if(owner==typeof(HeadingReferenceConfig) && name=="saveAs")
            ui["help"]="Save this mean separately for each VR, for later swarm.headingReferenceId. Cleared at restart and each sequence loop.";
        if(name=="directionDistribution") ui["help"]="Legacy retains historical scatter and kappa>=10000 point motion. VonMises uses a normalized sampler; yaw/elevation kappa=100 each gives approximately 99% 3D directional order.";
        if(name=="loopStartIndex")
        {
            child["minimum"]=0;
            ui["help"]="Zero-based row to restart after the final row: 0 repeats everything; 1 runs row 0 once, then repeats rows 1 onward. Requires fixed order when nonzero.";
        }
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
        if (name == "rugosity") ui["help"] = "Independent visible, collision, height and normal surfaces. IDs refer to RugositySurface components in the scene; omitted values preserve scene tuning.";
        if (owner == typeof(RugosityConfig))
        {
            if (name.EndsWith("Cm")) ui["unit"] = "cm";
            if (name == "absoluteHeight") ui["unit"] = "world Y (scene units)";
            if (name == "centimetersPerUnit") { child["exclusiveMinimum"] = 0; ui["help"] = "1 for centimeter scenes; 100 for meter terrain scenes."; }
            if (name == "normalSamples") { child["minimum"] = 1; child["maximum"] = 32; }
            if (new[] { "clearanceCm", "normalRadiusCm", "tiltResponse" }.Contains(name)) child["minimum"] = 0;
        }
        if(name.EndsWith("Seconds") || name=="duration") ui["unit"]="seconds";
        if(name.EndsWith("Degrees") || name=="mu" || name=="muElevation" || name=="windDirection") ui["unit"]="degrees";
        if(name.EndsWith("Metres")) ui["unit"]="metres";
        if(new[]{"autopilotSpeed","locustSpeed","locustSpeedStdDev","agentSpeed","agentSpeedStdDev","locoSpeed","translateSpeed"}.Contains(name)) ui["unit"]="scene units / s";
        if(name=="latitude") { child["minimum"]=-90; child["maximum"]=90; }
        if(name=="longitude") { child["minimum"]=-180; child["maximum"]=180; }
        if(owner==typeof(NightSkyConfig) && name=="imageWidth") child["enum"]=new JArray(1024,2048,4096);
        if(name=="port" || name=="zmqPort") { child["minimum"]=1; child["maximum"]=65535; }
        if(owner==typeof(ColorConfig) || name=="contrast" || name=="dutyCycle") { child["minimum"]=0; child["maximum"]=1; }
        Dictionary<string,string[]> enums=new Dictionary<string,string[]> {
            ["dimension"]=new[]{"2D","3D"},["directionDistribution"]=new[]{"Legacy","VonMises"},["agentVisual"]=new[]{"ScenePrefab","Bogong"},["sizeMode"]=new[]{"World","Angular"},
            ["centerStatistic"]=new[]{"mean","median"},["rotationAxis"]=new[]{"Yaw","Pitch","Roll"} };
        if(enums.TryGetValue(name,out var options)) child["enum"]=new JArray(options);
        if(owner==typeof(BogongVisualConfig) && name=="shape") child["enum"]=new JArray("Dot","Moth");
        if(owner==typeof(DynamicSequenceController.Trigger) && name=="type") child["enum"]=new JArray("time","area");
        if(ui.HasValues) child["x-ui"]=ui;
        var ranges=new Dictionary<string,double[]> {
            ["duration"]=new[]{1d,3600,1},["durationSeconds"]=new[]{1d,3600,1},["gain"]=new[]{-10d,10,.1},
            ["autopilotSpeed"]=new[]{0d,30,.1},["locustSpeed"]=new[]{0d,30,.1},["agentSpeed"]=new[]{0d,30,.1},["locoSpeed"]=new[]{0d,30,.1},["windSpeed"]=new[]{0d,20,.1},["windDirection"]=new[]{0d,360,1},["aglHeight"]=new[]{0d,1000,1},
            ["locustSpeedStdDev"]=new[]{0d,10,.01},["agentSpeedStdDev"]=new[]{0d,10,.01},["volumeHalfExtentMetres"]=new[]{.1d,10,.1},
            ["closedLoopPosition"]=new[]{0d,5,.1},["closedLoopOrientation"]=new[]{0d,5,.1},
            ["numberOfLocusts"]=new[]{0d,1000,1},["agentCount"]=new[]{0d,1000,1},["numberOfRings"]=new[]{0d,10,1},["mu"]=new[]{-180d,180,1},["muElevation"]=new[]{-90d,90,1},["kappa"]=new[]{0d,1000,1},["kappaElevation"]=new[]{0d,1000,1},
            ["angularSizeDegrees"]=new[]{.1d,10,.1},["size"]=new[]{.01d,5,.01},["contrast"]=new[]{0d,1,.01},["dutyCycle"]=new[]{0d,1,.01},
            ["frequency"]=new[]{0d,40,.1},["speed"]=new[]{0d,100,.1},["exposure"]=new[]{.1d,5,.1},["latitude"]=new[]{-90d,90,.01},["longitude"]=new[]{-180d,180,.01},
            ["northYawDegrees"]=new[]{0d,360,1},["windowSeconds"]=new[]{1d,180,1},["flightWindowSeconds"]=new[]{.2d,30,.1},["aggressiveness"]=new[]{.001d,1,.01},
            ["maxOffsetRadians"]=new[]{.01d,2,.01},["rateHz"]=new[]{1d,120,1},["targetFrameRate"]=new[]{30d,240,1},["translateSpeed"]=new[]{0d,100,1},["rotateSpeed"]=new[]{0d,300,1},
            ["wingOpacity"]=new[]{0d,1,.01},["faintDetailCutoff"]=new[]{0d,1,.01},["flickerDutyCycle"]=new[]{0d,1,.01},["flapFrequencyHz"]=new[]{0d,100,1},["animationSpeedThreshold"]=new[]{0d,5,.1}
        };
        double[] range=null;
        if(owner==typeof(ColorConfig)) range=new[]{0d,1,.01}; else ranges.TryGetValue(name,out range);
        if(owner==typeof(HeadingReferenceConfig) && name=="windowSeconds") range=new[]{1d,600,1};
        if(range!=null)
        {
            if(child["x-ui"]==null) child["x-ui"]=new JObject();
            child["x-ui"]["slider"]=new JObject { ["min"]=range[0],["max"]=range[1],["step"]=range[2] };
        }
    }
}
#endif
