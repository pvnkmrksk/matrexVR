using System;
using UnityEngine;

/// <summary>Preferred operator-widget position in physical display pixels; stimulus pixels remain protected.</summary>
[Serializable]
public class OverlayPlacementConfig
{
    public bool visible = true;
    public int targetDisplay = 0;
    public bool automaticPlacement = true;
    public float x = 12, y = 12;

    public Rect Preferred(Rect automatic)
    {
        if (automaticPlacement || !Finite(x) || !Finite(y)) return automatic;
        return new Rect(x, y, automatic.width, automatic.height);
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
