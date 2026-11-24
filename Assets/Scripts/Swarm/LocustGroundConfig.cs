using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

[JsonConverter(typeof(StringEnumConverter))]
public enum LocustGroundTexture { SceneDefault, Sand, Grass, Custom }

[Serializable]
public sealed class LocustGroundConfig
{
    public const string Field = "groundAppearance";

    public LocustGroundTexture texture = LocustGroundTexture.SceneDefault;
    public string groundTexturePath;
    public float? tilingX;
    public float? tilingY;

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(LocustGroundTexture), texture))
            throw new ArgumentException("Ground texture must be SceneDefault, Sand, Grass or Custom.");
        if (texture == LocustGroundTexture.Custom && string.IsNullOrWhiteSpace(groundTexturePath))
            throw new ArgumentException("Choose a PNG or JPEG for the custom ground texture.");
        if (!ValidTiling(tilingX) || !ValidTiling(tilingY))
            throw new ArgumentException("Ground X and Y tiling must be positive and finite.");
    }

    private static bool ValidTiling(float? value) =>
        !value.HasValue || (value.Value > 0 && !float.IsInfinity(value.Value));
}
