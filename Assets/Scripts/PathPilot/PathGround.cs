using System;
using UnityEngine;

[Serializable]
public sealed class PathGroundConfig
{
    public float openingAngle = 85;
    // Optional condition-specific, already painted image, mapped to the entire ground square.
    public string preparedTexturePath;
    public void Validate()
    {
        if (float.IsNaN(openingAngle) || openingAngle < 40 || openingAngle > 210)
            throw new ArgumentException("Path opening angle must be between 40 and 210 degrees.");
    }
}

[Serializable]
public sealed class PathGroundAppearance
{
    public float pathWidthCm = 4;
    public float pathDarkness = .65f;
    public float groundBrightness = .55f;
    public float textureScaleCm = 8;
    public float extentCm = 400;
    public float groundHeightCm = -1;
    public string texturePath = "";
    public bool preparedGroundImage;
    public void Validate()
    {
        foreach (float value in new[] {pathWidthCm, pathDarkness, groundBrightness, textureScaleCm, extentCm, groundHeightCm})
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Ground values must be finite.");
        if (pathWidthCm <= 0 || pathDarkness < 0 || pathDarkness > 1 || groundBrightness < 0 || groundBrightness > 1 ||
            textureScaleCm <= 0 || extentCm < 150 || groundHeightCm >= 0)
            throw new ArgumentException("Invalid path width, darkness, brightness, texture scale, extent or ground height.");
        if (preparedGroundImage && string.IsNullOrWhiteSpace(texturePath))
            throw new ArgumentException("A prepared ground image requires a texture path.");
    }
}

/// <summary>A flat visual surface. No colliders, choice triggers or movement constraints.</summary>
public sealed class PathGround : MonoBehaviour
{
    public static readonly Vector2 RightGoal = new Vector2(60 * Mathf.Sin(20 * Mathf.Deg2Rad), 60 * Mathf.Cos(20 * Mathf.Deg2Rad));
    public static float ForkZ(float angle) => RightGoal.y - RightGoal.x / Mathf.Tan(angle * Mathf.Deg2Rad / 2);
    private Mesh mesh;
    private Material material;
    private Texture2D texture;

    public static PathGround Create(Transform parent, int layer, PathGroundConfig config, PathGroundAppearance appearance, bool archive = true)
    {
        config.Validate(); appearance.Validate();
        var host = new GameObject("PathGround_" + config.openingAngle + "deg");
        host.transform.SetParent(parent, false);
        host.layer = layer;
        var ground = host.AddComponent<PathGround>();
        ground.Build(config, appearance, archive);
        return ground;
    }
    private void Build(PathGroundConfig config, PathGroundAppearance appearance, bool archive)
    {
        float h = appearance.extentCm / 2, y = appearance.groundHeightCm;
        mesh = new Mesh { name = "Flat painted path ground" };
        mesh.vertices = new[] { new Vector3(-h,y,-h), new Vector3(-h,y,h), new Vector3(h,y,h), new Vector3(h,y,-h) };
        mesh.uv = new[] {Vector2.zero, Vector2.up, Vector2.one, Vector2.right};
        mesh.triangles = new[] {0,1,2,0,2,3}; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var shader = Resources.Load<Shader>("PathPilot/PathGround");
        if (shader == null) throw new InvalidOperationException("Path ground shader is missing.");
        material = new Material(shader);
        material.SetFloat("_Extent", appearance.extentCm);
        material.SetFloat("_ForkZ", ForkZ(config.openingAngle));
        material.SetVector("_Goal", new Vector4(RightGoal.x, RightGoal.y,0,0));
        material.SetFloat("_PathWidth", appearance.pathWidthCm);
        material.SetFloat("_PathDarkness", appearance.pathDarkness);
        material.SetFloat("_Brightness", appearance.groundBrightness);
        material.SetFloat("_TileCm", appearance.textureScaleCm);
        string reference = string.IsNullOrEmpty(config.preparedTexturePath) ? appearance.texturePath : config.preparedTexturePath;
        bool prepared = !string.IsNullOrEmpty(config.preparedTexturePath) || appearance.preparedGroundImage;
        if (!string.IsNullOrWhiteSpace(reference))
        {
            byte[] bytes = archive ? ExperimentConfigFiles.ReadAllBytes(reference,"pilot-ground-texture") : System.IO.File.ReadAllBytes(ExperimentConfigFiles.Resolve(reference));
            texture = new Texture2D(2,2,TextureFormat.RGBA32,true);
            if (!texture.LoadImage(bytes)) throw new ArgumentException("Could not decode ground texture: " + reference);
            texture.wrapMode = prepared ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            material.SetTexture("_GroundTexture", texture);
            material.SetFloat("_UseTexture", 1);
        }
        material.SetFloat("_PreparedImage", prepared ? 1 : 0);
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
    private void OnDestroy()
    {
        Release(mesh); Release(material); Release(texture);
    }
    private static void Release(UnityEngine.Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset);
    }
}
