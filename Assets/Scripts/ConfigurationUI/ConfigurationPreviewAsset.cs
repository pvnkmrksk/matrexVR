using System;
using UnityEngine;

namespace ConfigurationUI
{
    [Serializable]
    public sealed class PreviewAnimationBinding
    {
        public string root;
        public AnimationClip clip;
    }

    /// <summary>A per-asset visual alias. It references no experiment behaviours or scene controllers.</summary>
    public sealed class ConfigurationPreviewAsset : ScriptableObject
    {
        public string fingerprint;
        public GameObject visual;
        public Material material;
        public PreviewAnimationBinding[] animations;
    }
}
