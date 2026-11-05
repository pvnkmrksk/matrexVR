// Standalone contract tests use the production JSON classes without starting Unity.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
}

public class ColorConfig { public float r, g, b, a; }
// These are Unity scene/rendering collaborators, outside the JSON gain/phase contracts tested here.
public class SceneObject { }
public class SunConfig { public void Validate() { } }
public class ModelAppearanceConfig { public void Validate() { } }
public static class RuntimeRigRegistry { public const int MaximumRigCount = 16; }
public class HeadingReferenceConfig { public void Validate() { } public void WithinDuration(double duration,out string warning) { warning=null; } }
public class NightSkyConfig { public bool enabled; public void Validate() { } }
public static class ExperimentConfigFiles { public static string ReadAllText(string path) => File.ReadAllText(path); }
public static class ExperimentPhases
{
    public const string PayloadKey = "__test_phase_payload";
    public static bool IsPhase(Dictionary<string, object> parameters) => parameters != null && parameters.ContainsKey(PayloadKey);
    public static JObject Read(Dictionary<string, object> parameters) => (JObject)parameters[PayloadKey];
}
public static class RunConfigArchive
{
    public static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
