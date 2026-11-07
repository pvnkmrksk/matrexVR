using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Immutable skinning samples for the stock one-state walking controller.</summary>
[PreferBinarySerialization]
public sealed class StockLocustClipCache : ScriptableObject
{
    internal const string ResourcePath = "NativeLocust/StockWalkCycle";
    public const int FormatVersion = 1;
    public const int SkinMatrixStride = 48;

    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct SkinMatrix
    {
        public Vector4 x, y, z;

        public SkinMatrix(Matrix4x4 matrix)
        {
            x = matrix.GetRow(0);
            y = matrix.GetRow(1);
            z = matrix.GetRow(2);
        }

        public Vector3 TransformPoint(Vector3 point)
        {
            var p = new Vector4(point.x, point.y, point.z, 1);
            return new Vector3(Vector4.Dot(x, p), Vector4.Dot(y, p), Vector4.Dot(z, p));
        }
    }

    [SerializeField] private int formatVersion;
    [SerializeField] private string identity;
    [SerializeField] private Mesh stockMesh;
    [SerializeField] private RuntimeAnimatorController stockController;
    [SerializeField] private Avatar stockAvatar;
    [SerializeField] private bool stockApplyRootMotion;
    [SerializeField] private int sampleCount, boneCount, stateHash;
    [SerializeField] private float length, boundingRadius;
    [SerializeField] private SkinMatrix[] matrices;

    private static StockLocustClipCache active;
    private static bool attempted;
    private static GraphicsBuffer buffer;

    public int SampleCount => sampleCount;
    public int BoneCount => boneCount;
    public int StateHash => stateHash;
    public float Length => length;
    public float BoundingRadius => boundingRadius;
    public string Identity => identity;
    public GraphicsBuffer SkinningBuffer => this == active ? buffer : null;

    public int SampleIndex(double phase)
    {
        double fraction = phase - Math.Floor(phase);
        return (int)Math.Floor(fraction * sampleCount + .5) & (sampleCount - 1);
    }

    public float SamplePhase(int index) => (float)index / sampleCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Application.quitting -= Reset;
        buffer?.Dispose();
        buffer = null;
        active = null;
        attempted = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WarmCache()
    {
        if (attempted || !SystemInfo.supportsInstancing ||
            SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan ||
            SystemInfo.graphicsShaderLevel < 45) return;
        attempted = true;
        active = Resources.Load<StockLocustClipCache>(ResourcePath);
        if (active == null || !active.IsValid()) return;
        try
        {
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, active.matrices.Length, SkinMatrixStride);
            buffer.SetData(active.matrices);
            Application.quitting += Reset;
        }
        catch (Exception error)
        {
            Reset();
            attempted = true;
            Debug.LogWarning("Stock walk-cycle cache could not be uploaded: " + error.Message);
        }
    }

    internal static bool TryGet(SkinnedMeshRenderer skin, Animator animator, out StockLocustClipCache clip)
    {
        clip = null;
        if (!attempted) WarmCache();
        if (buffer == null || active == null || skin == null || animator == null ||
            skin.sharedMesh != active.stockMesh || animator.runtimeAnimatorController != active.stockController ||
            animator.avatar != active.stockAvatar || animator.applyRootMotion != active.stockApplyRootMotion)
            return false;
        clip = active;
        return true;
    }

    private bool IsValid() => formatVersion == FormatVersion && !string.IsNullOrEmpty(identity) &&
        stockMesh != null && stockController != null && sampleCount >= 2 && (sampleCount & (sampleCount - 1)) == 0 && boneCount > 0 &&
        stateHash != 0 && length > 0 && boundingRadius > 0 && matrices != null &&
        matrices.LongLength == (long)sampleCount * boneCount;

#if UNITY_EDITOR
    public void Initialize(string sourceIdentity, Mesh mesh, Animator animator, int count, int bones,
        int hash, float duration, float radius, SkinMatrix[] baked)
    {
        formatVersion = FormatVersion;
        identity = sourceIdentity;
        stockMesh = mesh;
        stockController = animator.runtimeAnimatorController;
        stockAvatar = animator.avatar;
        stockApplyRootMotion = animator.applyRootMotion;
        sampleCount = count;
        boneCount = bones;
        stateHash = hash;
        length = duration;
        boundingRadius = radius;
        matrices = baked;
        if (!IsValid()) throw new InvalidOperationException("The stock locust walk-cycle cache is incomplete.");
    }

    public SkinMatrix MatrixAt(int sample, int bone) => matrices[sample * boneCount + bone];
#endif
}
