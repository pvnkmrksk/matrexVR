using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConfigurationUI
{
    /// <summary>Copies only transforms, meshes and renderer bindings. Never instantiates asset behaviours.</summary>
    public static class PreviewVisuals
    {
        public static GameObject Clone(GameObject source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            var map=new Dictionary<Transform,Transform>();
            Transform Copy(Transform from,Transform parent)
            {
                var to=new GameObject(from.name).transform;map.Add(from,to);to.SetParent(parent,false);
                to.localPosition=from.localPosition;to.localRotation=from.localRotation;to.localScale=from.localScale;
                to.gameObject.SetActive(from==source.transform||from.gameObject.activeSelf);
                foreach(Transform child in from)Copy(child,to);
                return to;
            }
            var root=Copy(source.transform,null).gameObject;
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                foreach(var pair in map)
                {
                    var from=pair.Key;var to=pair.Value.gameObject;
                    var mesh=from.GetComponent<MeshFilter>();
                    if(mesh!=null)to.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                    var renderer=from.GetComponent<MeshRenderer>();
                    if(renderer!=null)CopyRenderer(renderer,to.AddComponent<MeshRenderer>());
                    var skin=from.GetComponent<SkinnedMeshRenderer>();
                    if(skin!=null)
                    {
                        var clone=to.AddComponent<SkinnedMeshRenderer>();CopyRenderer(skin,clone);
                        clone.bones=skin.bones.Select(bone=>bone!=null&&map.TryGetValue(bone,out var value)?value:null).ToArray();
                        clone.rootBone=skin.rootBone!=null&&map.TryGetValue(skin.rootBone,out var rootBone)?rootBone:null;
                        clone.sharedMesh=skin.sharedMesh;clone.localBounds=skin.localBounds;clone.quality=skin.quality;
                        clone.updateWhenOffscreen=true;
                        for(int i=0;i<(skin.sharedMesh?.blendShapeCount??0);i++)clone.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i));
                    }
                }
                // Native LOD groups preserve the authored choice without running procedural asset scripts.
                foreach(var pair in map)
                {
                    var lod=pair.Key.GetComponent<LODGroup>();if(lod==null)continue;
                    var copy=pair.Value.gameObject.AddComponent<LODGroup>();copy.localReferencePoint=lod.localReferencePoint;copy.size=lod.size;
                    copy.SetLODs(lod.GetLODs().Select(level=>new LOD(level.screenRelativeTransitionHeight,level.renderers.Where(r=>r!=null&&map.ContainsKey(r.transform)).Select(r=>map[r.transform].GetComponent<Renderer>()).ToArray())).ToArray());
                    copy.fadeMode=LODFadeMode.None;
                }
                return root;
            }
            catch{Dispose(root);throw;}
        }
        private static void CopyRenderer(Renderer source,Renderer target)
        {
            target.sharedMaterials=source.sharedMaterials;target.enabled=source.enabled;
            target.shadowCastingMode=ShadowCastingMode.Off;target.receiveShadows=false;
            target.lightProbeUsage=LightProbeUsage.Off;target.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        public static PreviewAnimationBinding[] Animations(GameObject source)
        {
            var result=new List<PreviewAnimationBinding>();
            string Path(Transform node)
            {
                var names=new List<string>();while(node!=source.transform&&node!=null){names.Add(node.name);node=node.parent;}
                names.Reverse();return string.Join("/",names);
            }
            foreach(var animator in source.GetComponentsInChildren<Animator>(true))
                if(animator.runtimeAnimatorController!=null)
                    foreach(var clip in animator.runtimeAnimatorController.animationClips.Where(clip=>clip!=null).Distinct())result.Add(new PreviewAnimationBinding{root=Path(animator.transform),clip=clip});
            foreach(var animation in source.GetComponentsInChildren<Animation>(true))
            {
                var clips=new List<AnimationClip>();if(animation.clip!=null)clips.Add(animation.clip);
                foreach(AnimationState state in animation)if(state.clip!=null)clips.Add(state.clip);
                foreach(var clip in clips.Distinct())result.Add(new PreviewAnimationBinding{root=Path(animation.transform),clip=clip});
            }
            return result.ToArray();
        }
        public static void Sample(GameObject clone,PreviewAnimationBinding[] clips,string requested=null)
        {
            foreach(var group in (clips??Array.Empty<PreviewAnimationBinding>()).Where(binding=>binding?.clip!=null).GroupBy(binding=>binding.root??""))
            {
                var binding=group.FirstOrDefault(value=>value.clip.name==requested)??group.First();
                var target=string.IsNullOrEmpty(group.Key)?clone.transform:clone.transform.Find(group.Key);
                if(target!=null)binding.clip.SampleAnimation(target.gameObject,Mathf.Min(.4f,binding.clip.length*.35f));
            }
        }
        public static void Dispose(UnityEngine.Object target)
        {
            if(target==null)return;
            if(target is GameObject go)go.SetActive(false);
            if(Application.isPlaying)UnityEngine.Object.Destroy(target);else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
