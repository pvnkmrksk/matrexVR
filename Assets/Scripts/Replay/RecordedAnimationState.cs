using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>Additive, inspectable animation evidence. Poses remain authoritative for movement.</summary>
public sealed class RecordedAnimationState
{
    public int version = 1;
    public AnimatorPose[] animators;
    public LegacyPose[] legacy;
    public ProceduralPose procedural;
    // A sampled controller state cannot reproduce an in-progress transition blend.
    // Archive the realized local pose for those frames (including newly spawned bind poses).
    public LocalPose[] realizedPose;
    public BlendPose[] blendShapes;
    [Serializable] public sealed class LocalPose
    {
        public string path;
        public float[] position, rotation, scale;
    }
    [Serializable] public sealed class BlendPose
    {
        public string path;
        public float[] weights;
    }
    [Serializable] public sealed class AnimatorPose
    {
        public string path;
        public float speed;
        public LayerPose[] layers;
        public ParameterPose[] parameters;
    }
    [Serializable] public sealed class LayerPose
    {
        public int state;
        public float time, weight;
        public bool transitioning;
    }
    [Serializable] public sealed class ParameterPose
    {
        public int hash, type;
        public float value;
    }
    [Serializable] public sealed class ProceduralPose
    {
        public float time, phase;
    }
    [Serializable] public sealed class LegacyPose
    {
        public string path;
        public ClipPose[] clips;
    }
    [Serializable] public sealed class ClipPose
    {
        public string name;
        public float time, speed, weight;
        public int layer, wrap, blend;
        public bool enabled;
    }
}

/// <summary>Plain data owned by one rented population frame. No Unity objects or source capture buffers are queued.</summary>
public sealed class RecordedAnimationSnapshot : AsyncSwarmWriter.IPackedAnimationSnapshot
{
    private RecordedAnimationState state;
    private RecordedAnimationState.AnimatorPose[] animators;
    private RecordedAnimationState.LegacyPose[] legacy;
    private RecordedAnimationState.LocalPose[] realized;
    private RecordedAnimationState.BlendPose[] blends;
    private RecordedAnimationState.ProceduralPose procedural;
    private bool hasState;
    private bool hasStockAnimator;
    private AnimatorSample stockAnimator;
    private struct AnimatorSample
    {
        public int State;
        public float Time, Speed, Weight;
        public bool Transitioning;
    }
    [ThreadStatic] private static JsonSerializer workerSerializer;
    [ThreadStatic] private static CsvJsonWriter workerCsv;
    [ThreadStatic] private static char[] workerNumber;
    [ThreadStatic] private static StringWriter workerJson;
    // New or annotated schema members automatically retain the standard serializer path.
    private static readonly bool steadySchema=PlainFields(typeof(RecordedAnimationState),"version,animators,legacy,procedural,realizedPose,blendShapes")&&
        PlainFields(typeof(RecordedAnimationState.AnimatorPose),"path,speed,layers,parameters")&&
        PlainFields(typeof(RecordedAnimationState.LayerPose),"state,time,weight,transitioning")&&
        PlainFields(typeof(RecordedAnimationState.ProceduralPose),"time,phase");
    private static bool PlainFields(Type type,string names)
    {
        if(type.BaseType!=typeof(object)||type.GetProperties(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public).Length!=0)return false;
        foreach(var attribute in type.GetCustomAttributes(false))if(attribute is JsonContainerAttribute||attribute is JsonConverterAttribute)return false;
        var fields=type.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public);
        var expected=names.Split(',');if(fields.Length!=expected.Length)return false;
        Array.Sort(fields,(a,b)=>a.MetadataToken.CompareTo(b.MetadataToken));
        for(int i=0;i<fields.Length;i++)if(fields[i].Name!=expected[i]||fields[i].GetCustomAttributes(false).Length!=0)return false;
        return true;
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    private bool TryGetSteadyAnimator(out AnimatorSample sample)
    {
        sample = stockAnimator;
        if (hasStockAnimator) return true;
        // The usual original locust: one root Animator/layer, no parameters or transition pose.
        // Everything outside this shape uses the full extensible JSON serializer below.
        if(!steadySchema||state.version!=1||state.animators==null||state.animators.Length!=1||
            state.legacy==null||state.legacy.Length!=0||state.procedural!=null||state.realizedPose!=null||state.blendShapes!=null)return false;
        var animator=state.animators[0];
        if(animator==null||animator.path!=""||animator.layers==null||animator.layers.Length!=1||animator.parameters==null||animator.parameters.Length!=0)return false;
        var layer=animator.layers[0];
        if(layer==null||!Finite(animator.speed)||!Finite(layer.time)||!Finite(layer.weight))return false;
        sample = new AnimatorSample { State = layer.state, Time = layer.time, Speed = animator.speed,
            Weight = layer.weight, Transitioning = layer.transitioning };
        return true;
    }
    private bool WriteSteadyAnimator(TextWriter output)
    {
        if(!TryGetSteadyAnimator(out var sample))return false;
        if(workerNumber==null)workerNumber=new char[64];
        output.Write("{\"version\":1,\"animators\":[{\"path\":\"\",\"speed\":");
        Float(output,sample.Speed);
        output.Write(",\"layers\":[{\"state\":");
        if(!sample.State.TryFormat(workerNumber,out int length,default,CultureInfo.InvariantCulture))throw new InvalidDataException("Animator state exceeds number buffer.");
        output.Write(workerNumber,0,length);output.Write(",\"time\":");Float(output,sample.Time);
        output.Write(",\"weight\":");Float(output,sample.Weight);
        output.Write(sample.Transitioning?",\"transitioning\":true}],\"parameters\":[]}],\"legacy\":[]}":",\"transitioning\":false}],\"parameters\":[]}],\"legacy\":[]}");
        return true;
    }
    private static void Float(TextWriter output,float value)
    {
        if(!value.TryFormat(workerNumber,out int length,"R",CultureInfo.InvariantCulture))throw new InvalidDataException("Animator float exceeds number buffer.");
        output.Write(workerNumber,0,length);
        // Json.NET writes integral floating-point values with a decimal suffix.
        bool decimalOrExponent=false;
        for(int i=0;i<length;i++)if(workerNumber[i]=='.'||workerNumber[i]=='E'||workerNumber[i]=='e'){decimalOrExponent=true;break;}
        if(!decimalOrExponent)output.Write(".0");
    }
    private static void Size<T>(ref T[] values,int count)
    {if(values==null||values.Length!=count)values=count==0?Array.Empty<T>():new T[count];}
    private static float[] Copy(float[] source,float[] destination)
    {if(source==null)return null;Size(ref destination,source.Length);Array.Copy(source,destination,source.Length);return destination;}
    internal bool TrySetStockAnimator(int stateHash, float normalizedTime, float speed)
    {
        if (!steadySchema || !Finite(normalizedTime) || !Finite(speed)) return false;
        stockAnimator = new AnimatorSample { State = stateHash, Time = normalizedTime, Speed = speed, Weight = 1 };
        hasState = hasStockAnimator = true;
        return true;
    }

    internal void CopyFrom(RecordedAnimationState source)
    {
        hasStockAnimator=false;
        hasState=source!=null;
        if(!hasState)return;
        if(state==null)state=new RecordedAnimationState();
        state.version=source.version;
        if(source.animators==null)state.animators=null;
        else
        {
            Size(ref animators,source.animators.Length);state.animators=animators;
            for(int i=0;i<animators.Length;i++)
            {
                var input=source.animators[i];if(input==null){animators[i]=null;continue;}
                var output=animators[i]??(animators[i]=new RecordedAnimationState.AnimatorPose());
                output.path=input.path;output.speed=input.speed;
                if(input.layers==null)output.layers=null;
                else
                {
                    Size(ref output.layers,input.layers.Length);
                    for(int j=0;j<output.layers.Length;j++)
                    {
                        var value=input.layers[j];if(value==null){output.layers[j]=null;continue;}
                        var copy=output.layers[j]??(output.layers[j]=new RecordedAnimationState.LayerPose());
                        copy.state=value.state;copy.time=value.time;copy.weight=value.weight;copy.transitioning=value.transitioning;
                    }
                }
                if(input.parameters==null)output.parameters=null;
                else
                {
                    Size(ref output.parameters,input.parameters.Length);
                    for(int j=0;j<output.parameters.Length;j++)
                    {
                        var value=input.parameters[j];if(value==null){output.parameters[j]=null;continue;}
                        var copy=output.parameters[j]??(output.parameters[j]=new RecordedAnimationState.ParameterPose());
                        copy.hash=value.hash;copy.type=value.type;copy.value=value.value;
                    }
                }
            }
        }
        if(source.legacy==null)state.legacy=null;
        else
        {
            Size(ref legacy,source.legacy.Length);state.legacy=legacy;
            for(int i=0;i<legacy.Length;i++)
            {
                var input=source.legacy[i];if(input==null){legacy[i]=null;continue;}
                var output=legacy[i]??(legacy[i]=new RecordedAnimationState.LegacyPose());output.path=input.path;
                if(input.clips==null)output.clips=null;
                else
                {
                    Size(ref output.clips,input.clips.Length);
                    for(int j=0;j<output.clips.Length;j++)
                    {
                        var value=input.clips[j];if(value==null){output.clips[j]=null;continue;}
                        var copy=output.clips[j]??(output.clips[j]=new RecordedAnimationState.ClipPose());
                        copy.name=value.name;copy.time=value.time;copy.speed=value.speed;copy.weight=value.weight;
                        copy.layer=value.layer;copy.wrap=value.wrap;copy.blend=value.blend;copy.enabled=value.enabled;
                    }
                }
            }
        }
        if(source.realizedPose==null)state.realizedPose=null;
        else
        {
            Size(ref realized,source.realizedPose.Length);state.realizedPose=realized;
            for(int i=0;i<realized.Length;i++)
            {
                var input=source.realizedPose[i];if(input==null){realized[i]=null;continue;}
                var output=realized[i]??(realized[i]=new RecordedAnimationState.LocalPose());output.path=input.path;
                output.position=Copy(input.position,output.position);output.rotation=Copy(input.rotation,output.rotation);output.scale=Copy(input.scale,output.scale);
            }
        }
        if(source.blendShapes==null)state.blendShapes=null;
        else
        {
            Size(ref blends,source.blendShapes.Length);state.blendShapes=blends;
            for(int i=0;i<blends.Length;i++)
            {
                var input=source.blendShapes[i];if(input==null){blends[i]=null;continue;}
                var output=blends[i]??(blends[i]=new RecordedAnimationState.BlendPose());output.path=input.path;output.weights=Copy(input.weights,output.weights);
            }
        }
        if(source.procedural==null)state.procedural=null;
        else
        {
            if(procedural==null)procedural=new RecordedAnimationState.ProceduralPose();
            procedural.time=source.procedural.time;procedural.phase=source.procedural.phase;state.procedural=procedural;
        }
    }
    public void WritePacked(BinaryWriter output)
    {
        if (!hasState)
        {
            output.Write((byte)0);
            return;
        }
        if (TryGetSteadyAnimator(out var sample))
        {
            output.Write((byte)1);
            output.Write(sample.Speed);
            output.Write(sample.State);
            output.Write(sample.Time);
            output.Write(sample.Weight);
            output.Write(sample.Transitioning);
            return;
        }
        if (steadySchema && state.version == 1 && state.animators?.Length == 0 && state.legacy?.Length == 0 &&
            state.realizedPose == null && state.blendShapes == null && state.procedural != null &&
            Finite(state.procedural.time) && Finite(state.procedural.phase))
        {
            output.Write((byte)2);
            output.Write(state.procedural.time);
            output.Write(state.procedural.phase);
            return;
        }

        // Rich Animator states preserve every field through the same serializer as CSV.
        if (workerJson == null) workerJson = new StringWriter(new StringBuilder(512), CultureInfo.InvariantCulture);
        workerJson.GetStringBuilder().Clear();
        Serialize(workerJson);
        output.Write((byte)3);
        output.Write(workerJson.ToString());
    }

    private void Serialize(TextWriter output)
    {
        if (workerSerializer == null) workerSerializer = JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        using (var writer = new JsonTextWriter(output) { CloseOutput = false }) workerSerializer.Serialize(writer, state);
    }

    public void WriteCsvCell(TextWriter output)
    {
        output.Write(',');if(!hasState)return;
        if(workerCsv==null)workerCsv=new CsvJsonWriter();
        output.Write('"');workerCsv.Output=output;
        try
        {
            if(!WriteSteadyAnimator(workerCsv)) Serialize(workerCsv);
        }
        finally{workerCsv.Output=null;}
        output.Write('"');
    }
    /// <summary>JSON escapes control characters; a quoted CSV field only doubles quotation marks.</summary>
    private sealed class CsvJsonWriter:TextWriter
    {
        internal TextWriter Output;
        public override Encoding Encoding=>Encoding.UTF8;
        public override void Write(char value){Output.Write(value);if(value=='"')Output.Write('"');}
        public override void Write(string value)
        {
            if(value==null)return;int start=0;
            for(int i=0;i<value.Length;i++)if(value[i]=='"')
            {Output.Write(value.AsSpan(start,i-start));Output.Write("\"\"");start=i+1;}
            Output.Write(value.AsSpan(start));
        }
        public override void Write(char[] value,int index,int count)
        {
            int start=index,end=index+count;
            for(int i=index;i<end;i++)if(value[i]=='"')
            {Output.Write(value,start,i-start);Output.Write("\"\"");start=i+1;}
            Output.Write(value,start,end-start);
        }
        // Per-value disposal must not flush the population file per animal.
        public override void Flush(){}
    }
}

/// <summary>Cache hierarchy discovery per spawned individual rather than scanning it every frame.</summary>
public sealed class RecordedAnimationCapture
{
    private static readonly bool usePrimitiveStockSnapshot =
        Array.IndexOf(Environment.GetCommandLineArgs(), "--copy-swarm-animation-objects") < 0;
    private readonly GameObject root;
    private readonly StockLocustPlayback clipPlayback;
    private readonly int clipIndex;
    private Animator[] animators;
    private string[] paths;
    private BogongSwarmRenderer ownProcedural;
    private Animation[] legacy;
    private string[] legacyPaths;
    private readonly RecordedAnimationState evidence=new RecordedAnimationState();
    private RuntimeAnimatorController[] controllers;
    private int[] parameterCounts;
    private readonly StringBuilder json=new StringBuilder(256);
    private readonly StringWriter jsonText;
    private readonly JsonSerializer serializer=JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling=NullValueHandling.Ignore });
    private RecordedAnimationState.ProceduralPose proceduralPose;
    public RecordedAnimationCapture(GameObject source)
    {
        root = source;
        StockLocustPlayback.TryGet(source, out clipPlayback, out clipIndex);
        jsonText=new StringWriter(json,CultureInfo.InvariantCulture);
        animators = root.GetComponentsInChildren<Animator>(true);
        evidence.animators=new RecordedAnimationState.AnimatorPose[animators.Length];
        controllers=new RuntimeAnimatorController[animators.Length];parameterCounts=new int[animators.Length];
        paths = new string[animators.Length];
        for (int i = 0; i < animators.Length; i++) paths[i] = Path(root.transform, animators[i].transform);
        ownProcedural = root.GetComponentInChildren<BogongSwarmRenderer>(true);
        legacy = root.GetComponentsInChildren<Animation>(true); legacyPaths = new string[legacy.Length];
        evidence.legacy=new RecordedAnimationState.LegacyPose[legacy.Length];
        for (int i = 0; i < legacy.Length; i++) legacyPaths[i] = Path(root.transform, legacy[i].transform);
    }
    private static string Path(Transform root, Transform target)
    {
        var parts = new List<string>();
        while (target != null && target != root) { parts.Add(target.GetSiblingIndex().ToString()); target = target.parent; }
        parts.Reverse(); return string.Join("/", parts);
    }
    public string Capture(BogongSwarmRenderer batch = null, int index = 0)
    {
        var value=ReadEvidence(batch,index);if(value==null)return "";
        json.Clear();
        using(var writer=new JsonTextWriter(jsonText){CloseOutput=false})serializer.Serialize(writer,value);
        return json.ToString();
    }
    public void CaptureSnapshot(RecordedAnimationSnapshot destination,BogongSwarmRenderer batch=null,int index=0)
    {
        if(destination==null)throw new ArgumentNullException(nameof(destination));
        if (usePrimitiveStockSnapshot && clipPlayback != null && clipPlayback.UsesCache &&
            animators.Length == 1 && legacy.Length == 0 && root != null)
        {
            clipPlayback.ReadState(clipIndex, out float normalizedTime, out float speed);
            if (destination.TrySetStockAnimator(clipPlayback.StateHash, normalizedTime, speed)) return;
        }
        destination.CopyFrom(ReadEvidence(batch,index));
    }
    private RecordedAnimationState ReadEvidence(BogongSwarmRenderer batch,int index)
    {
        if (root == null) return null;
        var procedural = batch != null ? batch : ownProcedural;
        if (animators.Length == 0 && legacy.Length == 0 && procedural == null) return null;
        evidence.realizedPose=null;evidence.blendShapes=null;evidence.procedural=null;
        if (clipPlayback != null && clipPlayback.UsesCache)
        {
            var pose = evidence.animators[0];
            if (pose == null)
            {
                pose = new RecordedAnimationState.AnimatorPose {
                    path = "", layers = new[] { new RecordedAnimationState.LayerPose { state = clipPlayback.StateHash, weight = 1 } },
                    parameters = Array.Empty<RecordedAnimationState.ParameterPose>()
                };
                evidence.animators[0] = pose;
            }
            clipPlayback.ReadState(clipIndex, out pose.layers[0].time, out pose.speed);
            return evidence;
        }
        bool captureRealizedPose = false;
        for (int i = 0; i < animators.Length; i++)
        {
            var animator = animators[i];
            var controller=animator!=null?animator.runtimeAnimatorController:null;
            if (animator == null || controller == null) {evidence.animators[i]=null;continue;}
            var pose=evidence.animators[i];int layerCount=animator.layerCount;
            if(pose==null||controllers[i]!=controller||parameterCounts[i]!=animator.parameterCount||pose.layers.Length!=layerCount)
            {
                pose=new RecordedAnimationState.AnimatorPose {path=paths[i],layers=new RecordedAnimationState.LayerPose[layerCount]};
                for(int layer=0;layer<layerCount;layer++)pose.layers[layer]=new RecordedAnimationState.LayerPose();
                var parameters=new List<RecordedAnimationState.ParameterPose>();
                foreach(var parameter in animator.parameters)
                    if(parameter.type!=AnimatorControllerParameterType.Trigger)parameters.Add(new RecordedAnimationState.ParameterPose {hash=parameter.nameHash,type=(int)parameter.type});
                pose.parameters=parameters.ToArray();evidence.animators[i]=pose;
                controllers[i]=controller;parameterCounts[i]=animator.parameterCount;
            }
            pose.speed=animator.speed;
            for (int layer = 0; layer < layerCount; layer++)
            {
                var state = animator.GetCurrentAnimatorStateInfo(layer);
                bool transitioning=animator.IsInTransition(layer);captureRealizedPose |= state.fullPathHash == 0 || transitioning;
                var sample=pose.layers[layer];sample.state=state.fullPathHash;sample.time=state.normalizedTime;
                sample.weight=animator.GetLayerWeight(layer);sample.transitioning=transitioning;
            }
            foreach (var parameter in pose.parameters)
            {
                float value;
                switch ((AnimatorControllerParameterType)parameter.type)
                {
                    case AnimatorControllerParameterType.Float: value = animator.GetFloat(parameter.hash); break;
                    case AnimatorControllerParameterType.Int: value = animator.GetInteger(parameter.hash); break;
                    case AnimatorControllerParameterType.Bool: value = animator.GetBool(parameter.hash) ? 1 : 0; break;
                    default: continue; // Trigger consumption cannot be queried from the Animator API.
                }
                parameter.value=value;
            }
        }
        if (captureRealizedPose)
        {
            var local = new List<RecordedAnimationState.LocalPose>();
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                // World movement is already authoritative in the pose stream.
                if (node == root.transform) continue;
                var p=node.localPosition;var r=node.localRotation;var s=node.localScale;
                local.Add(new RecordedAnimationState.LocalPose { path=Path(root.transform,node),
                    position=new[]{p.x,p.y,p.z},rotation=new[]{r.x,r.y,r.z,r.w},scale=new[]{s.x,s.y,s.z} });
            }
            evidence.realizedPose=local.ToArray();
            var blends = new List<RecordedAnimationState.BlendPose>();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int count=renderer.sharedMesh!=null?renderer.sharedMesh.blendShapeCount:0;
                if(count==0)continue;
                var weights=new float[count];for(int shape=0;shape<count;shape++)weights[shape]=renderer.GetBlendShapeWeight(shape);
                blends.Add(new RecordedAnimationState.BlendPose {path=Path(root.transform,renderer.transform),weights=weights});
            }
            evidence.blendShapes=blends.ToArray();
        }
        for (int i = 0; i < legacy.Length; i++)
        {
            var clips = new List<RecordedAnimationState.ClipPose>();
            foreach (AnimationState clip in legacy[i]) clips.Add(new RecordedAnimationState.ClipPose {
                name = clip.name, time = clip.time, speed = clip.speed, weight = clip.weight, layer = clip.layer,
                wrap = (int)clip.wrapMode, blend = (int)clip.blendMode, enabled = clip.enabled });
            evidence.legacy[i] = new RecordedAnimationState.LegacyPose { path = legacyPaths[i], clips = clips.ToArray() };
        }
        if (procedural != null)
        {
            if(proceduralPose==null)proceduralPose=new RecordedAnimationState.ProceduralPose();
            proceduralPose.time=procedural.AnimationClock;proceduralPose.phase=procedural.AnimationPhase(batch!=null?index:0);
            evidence.procedural=proceduralPose;
        }
        return evidence;
    }
}

/// <summary>Sample the original animation controller using the recording clock, including reverse seeks.</summary>
public sealed class ReplayAnimationDriver : MonoBehaviour
{
    private Animator[] animators;
    private int[][] initialStates;
    private float[][] initialLengths;
    private float[] initialSpeeds;
    private BogongSwarmRenderer[] procedural;
    private Animation[] legacy;
    private RecordedAnimationState.LegacyPose[] initialLegacy;
    private string previousEvidence;
    private RecordedAnimationState parsed;
    public bool Approximate { get; private set; }
    public bool TransitionApproximation { get; private set; }
    private void Initialize()
    {
        if (animators != null) return;
        animators = GetComponentsInChildren<Animator>(true);
        initialStates = new int[animators.Length][]; initialLengths = new float[animators.Length][];
        initialSpeeds = new float[animators.Length];
        for (int i = 0; i < animators.Length; i++)
        {
            var animator = animators[i];
            initialSpeeds[i] = animator.speed;
            animator.enabled = true; animator.applyRootMotion = false; animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.speed = 0; animator.Update(0);
            initialStates[i] = new int[animator.layerCount]; initialLengths[i] = new float[animator.layerCount];
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                var state = animator.GetCurrentAnimatorStateInfo(layer);
                initialStates[i][layer] = state.fullPathHash; initialLengths[i][layer] = Mathf.Max(.0001f, state.length);
            }
            animator.enabled = false;
        }
        procedural = GetComponentsInChildren<BogongSwarmRenderer>(true);
        legacy = GetComponentsInChildren<Animation>(true);
        if (legacy.Length > 0)
            initialLegacy = JsonConvert.DeserializeObject<RecordedAnimationState>(new RecordedAnimationCapture(gameObject).Capture())?.legacy;
        foreach (var animation in legacy) animation.enabled = false;
    }
    private Transform AtPath(string path)
    {
        Transform current = transform;
        if (!string.IsNullOrEmpty(path)) foreach (string part in path.Split('/'))
        {
            if (!int.TryParse(part, out int index) || index < 0 || index >= current.childCount) return null;
            current = current.GetChild(index);
        }
        return current;
    }
    public void Sample(string evidence, double seconds)
    {
        Initialize(); Approximate = string.IsNullOrWhiteSpace(evidence); TransitionApproximation = false;
        if (evidence != previousEvidence)
        {
            previousEvidence = evidence; parsed = null;
            if (!string.IsNullOrWhiteSpace(evidence))
                try { parsed = JsonConvert.DeserializeObject<RecordedAnimationState>(evidence); }
                catch (JsonException) { Approximate = true; }
        }
        if (parsed?.version == 1 && parsed.animators != null)
        {
            foreach (var pose in parsed.animators)
            {
                if (pose == null) continue;
                var animator = AtPath(pose.path)?.GetComponent<Animator>(); if (animator == null || animator.runtimeAnimatorController == null) { Approximate = true; continue; }
                animator.enabled = true;
                foreach (var parameter in pose.parameters ?? Array.Empty<RecordedAnimationState.ParameterPose>())
                    switch ((AnimatorControllerParameterType)parameter.type)
                    {
                        case AnimatorControllerParameterType.Float: animator.SetFloat(parameter.hash, parameter.value); break;
                        case AnimatorControllerParameterType.Int: animator.SetInteger(parameter.hash, (int)parameter.value); break;
                        case AnimatorControllerParameterType.Bool: animator.SetBool(parameter.hash, parameter.value != 0); break;
                    }
                for (int layer = 0; layer < Mathf.Min(animator.layerCount, pose.layers?.Length ?? 0); layer++)
                {
                    var state = pose.layers[layer]; if (state == null) continue;
                    animator.SetLayerWeight(layer, state.weight);
                    if (state.state != 0) animator.Play(state.state, layer, state.time);
                    TransitionApproximation |= state.transitioning;
                }
                animator.Update(0); animator.enabled = false;
            }
        }
        else
        {
            Approximate = true;
            for (int i = 0; i < animators.Length; i++)
            {
                var animator = animators[i]; if (animator.runtimeAnimatorController == null) continue;
                animator.enabled = true;
                for (int layer = 0; layer < initialStates[i].Length; layer++)
                    if (initialStates[i][layer] != 0) animator.Play(initialStates[i][layer], layer, (float)(seconds * initialSpeeds[i] / initialLengths[i][layer]));
                animator.Update(0); animator.enabled = false;
            }
        }
        if (parsed?.version == 1 && parsed.legacy != null)
            foreach (var pose in parsed.legacy)
            {
                var animation = AtPath(pose.path)?.GetComponent<Animation>();
                if (animation == null) { Approximate = true; continue; }
                animation.enabled = true;
                foreach (var state in pose.clips ?? Array.Empty<RecordedAnimationState.ClipPose>())
                {
                    var clip = animation[state.name]; if (clip == null) { Approximate = true; continue; }
                    clip.enabled = state.enabled; clip.time = state.time; clip.speed = 0;
                    clip.weight = state.weight; clip.layer = state.layer; clip.wrapMode = (WrapMode)state.wrap; clip.blendMode = (AnimationBlendMode)state.blend;
                }
                animation.Sample(); animation.enabled = false;
            }
        else foreach (var pose in initialLegacy ?? Array.Empty<RecordedAnimationState.LegacyPose>())
            {
                var animation = AtPath(pose.path)?.GetComponent<Animation>(); if (animation == null) continue;
                animation.enabled = true;
                foreach (var state in pose.clips)
                { var clip = animation[state.name]; if (clip == null) continue;
                  clip.enabled = state.enabled; clip.weight = state.weight; clip.time = state.time + (float)seconds * state.speed; clip.speed = 0; }
                animation.Sample(); animation.enabled = false;
            }
        foreach (var renderer in procedural)
        {
            if (parsed?.procedural != null) renderer.SetRecordedAnimation(parsed.procedural.time, parsed.procedural.phase);
            else renderer.SetRecordedAnimation((float)seconds, 0);
            renderer.RefreshCenters();
        }
        if(parsed?.realizedPose!=null)
        {
            foreach(var pose in parsed.realizedPose)
            {
                var node=AtPath(pose.path);
                if(node==null){Approximate=true;continue;}
                node.localPosition=new Vector3(pose.position[0],pose.position[1],pose.position[2]);
                node.localRotation=new Quaternion(pose.rotation[0],pose.rotation[1],pose.rotation[2],pose.rotation[3]);
                node.localScale=new Vector3(pose.scale[0],pose.scale[1],pose.scale[2]);
            }
            foreach(var pose in parsed.blendShapes??Array.Empty<RecordedAnimationState.BlendPose>())
            {
                var renderer=AtPath(pose.path)?.GetComponent<SkinnedMeshRenderer>();
                if(renderer==null||renderer.sharedMesh==null||renderer.sharedMesh.blendShapeCount!=pose.weights.Length){Approximate=true;continue;}
                for(int shape=0;shape<pose.weights.Length;shape++)renderer.SetBlendShapeWeight(shape,pose.weights[shape]);
            }
            TransitionApproximation=false;
        }
    }
}
