using System;
using System.Collections.Generic;
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

/// <summary>Cache hierarchy discovery per spawned individual rather than scanning it every frame.</summary>
public sealed class RecordedAnimationCapture
{
    private readonly GameObject root;
    private Animator[] animators;
    private string[] paths;
    private BogongSwarmRenderer ownProcedural;
    private Animation[] legacy;
    private string[] legacyPaths;
    public RecordedAnimationCapture(GameObject source)
    {
        root = source;
        animators = root.GetComponentsInChildren<Animator>(true);
        paths = new string[animators.Length];
        for (int i = 0; i < animators.Length; i++) paths[i] = Path(root.transform, animators[i].transform);
        ownProcedural = root.GetComponentInChildren<BogongSwarmRenderer>(true);
        legacy = root.GetComponentsInChildren<Animation>(true); legacyPaths = new string[legacy.Length];
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
        if (root == null) return "";
        var procedural = batch != null ? batch : ownProcedural;
        if (animators.Length == 0 && legacy.Length == 0 && procedural == null) return "";
        var evidence = new RecordedAnimationState { animators = new RecordedAnimationState.AnimatorPose[animators.Length] };
        bool captureRealizedPose = false;
        for (int i = 0; i < animators.Length; i++)
        {
            var animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null) continue;
            var pose = new RecordedAnimationState.AnimatorPose { path = paths[i], speed = animator.speed,
                layers = new RecordedAnimationState.LayerPose[animator.layerCount] };
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                var state = animator.GetCurrentAnimatorStateInfo(layer);
                captureRealizedPose |= state.fullPathHash == 0 || animator.IsInTransition(layer);
                pose.layers[layer] = new RecordedAnimationState.LayerPose { state = state.fullPathHash,
                    time = state.normalizedTime, weight = animator.GetLayerWeight(layer), transitioning = animator.IsInTransition(layer) };
            }
            var parameters = new List<RecordedAnimationState.ParameterPose>();
            foreach (var parameter in animator.parameters)
            {
                float value;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float: value = animator.GetFloat(parameter.nameHash); break;
                    case AnimatorControllerParameterType.Int: value = animator.GetInteger(parameter.nameHash); break;
                    case AnimatorControllerParameterType.Bool: value = animator.GetBool(parameter.nameHash) ? 1 : 0; break;
                    default: continue; // Trigger consumption cannot be queried from the Animator API.
                }
                parameters.Add(new RecordedAnimationState.ParameterPose { hash = parameter.nameHash, type = (int)parameter.type, value = value });
            }
            pose.parameters = parameters.ToArray(); evidence.animators[i] = pose;
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
        evidence.legacy = new RecordedAnimationState.LegacyPose[legacy.Length];
        for (int i = 0; i < legacy.Length; i++)
        {
            var clips = new List<RecordedAnimationState.ClipPose>();
            foreach (AnimationState clip in legacy[i]) clips.Add(new RecordedAnimationState.ClipPose {
                name = clip.name, time = clip.time, speed = clip.speed, weight = clip.weight, layer = clip.layer,
                wrap = (int)clip.wrapMode, blend = (int)clip.blendMode, enabled = clip.enabled });
            evidence.legacy[i] = new RecordedAnimationState.LegacyPose { path = legacyPaths[i], clips = clips.ToArray() };
        }
        if (procedural != null) evidence.procedural = new RecordedAnimationState.ProceduralPose {
            time = procedural.AnimationClock, phase = procedural.AnimationPhase(batch != null ? index : 0) };
        return JsonConvert.SerializeObject(evidence, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
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
