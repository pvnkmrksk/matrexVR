using System;
using CosineKitty;
using UnityEngine;

public static class NightSkyOrientation
{
    /// <summary>Unity +X east, +Y up, +Z north to NASA's J2000 equatorial XYZ.</summary>
    public static Matrix4x4 WorldToEquatorial(NightSkyConfig config, DateTimeOffset utc)
    {
        var time = new AstroTime(utc.UtcDateTime);
        RotationMatrix rotation = Astronomy.Rotation_HOR_EQJ(time, new Observer(config.latitude, config.longitude, 0));
        Quaternion heading = Quaternion.Euler(0, -config.northYawDegrees, 0);
        Matrix4x4 matrix = Matrix4x4.identity;
        for (int axis = 0; axis < 3; axis++)
        {
            Vector3 world = heading * (axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward);
            // Astronomy Engine HOR is north, west, zenith (right handed).
            AstroVector eq = Astronomy.RotateVector(rotation, new AstroVector(world.z, -world.x, world.y, time));
            matrix.SetColumn(axis, new Vector4((float)eq.x, (float)eq.y, (float)eq.z, 0));
        }
        return matrix;
    }
}
