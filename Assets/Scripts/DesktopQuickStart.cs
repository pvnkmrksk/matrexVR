using System;
using System.IO;
using ConfigurationUI;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Ready-to-run desktop previews use their own files; apparatus and custom experiment files are never replaced.</summary>
public static class DesktopQuickStart
{
    private static JObject Marker(ConfigWorkspace workspace) => workspace?.System?.Root?["desktopQuickStart"] as JObject;
    public static bool IsQuickStart(ConfigWorkspace workspace)
    {
        if ((int?)Marker(workspace)?["version"] != 1) return false;
        string folder = Path.GetFullPath(Path.Combine(RuntimePaths.ConfigRoot, "QuickStart")) + Path.DirectorySeparatorChar;
        return workspace.System.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFullPath(workspace.System.Path) == Path.GetFullPath(Path.Combine(RuntimePaths.ConfigRoot, "system_config.json"));
    }
    public static string Mode(ConfigWorkspace workspace) => (string)Marker(workspace)?["mode"] ?? "flying";
    public static string Scene(ConfigWorkspace workspace) => (string)Marker(workspace)?["scene"] ?? "night-sky";
    public static bool IsDemo(ConfigWorkspace workspace) => (bool?)Marker(workspace)?["demo"] ?? false;

    public static bool InitialSelection(out string system, out string sequence)
    {
        system = sequence = null;
        if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "--config-dir") >= 0) return false;
        string root = RuntimePaths.ConfigRoot;
        string state = Path.Combine(root, "Desktop", "workspace.json");
        if (File.Exists(state))
        {
            try
            {
                var saved = JObject.Parse(File.ReadAllText(state));
                string a = (string)saved["systemFile"], b = (string)saved["sequenceFile"];
                if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && File.Exists(a) && File.Exists(b))
                { system = a; sequence = b; return true; }
            }
            catch (Exception error) { Debug.LogWarning("The saved workspace could not be opened: " + error.Message); }
        }
        // The old system/sequence files are still available in Advanced/Open. Never replace a calibrated apparatus.
        string folder = EnsureProfile(root, "flying", "night-sky", true);
        system = Path.Combine(folder, "system.json"); sequence = Path.Combine(folder, "sequence.json"); return true;
    }
    public static void RememberWorkspace(string system, string sequence)
    {
        if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "--config-dir") >= 0) return;
        try
        {
            string folder = Path.Combine(RuntimePaths.ConfigRoot, "Desktop"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "workspace.json");
            var state = new JObject { ["schemaVersion"] = 1, ["systemFile"] = Path.GetFullPath(system), ["sequenceFile"] = Path.GetFullPath(sequence) };
            File.WriteAllText(path + ".tmp", state.ToString() + "\n");
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak"); else File.Move(path + ".tmp", path);
        }
        catch (Exception error) { Debug.LogWarning("The experiment is ready, but the last-workspace preference could not be saved: " + error.Message); }
    }

    public static void ApplyToWorkspace(ConfigWorkspace workspace, string mode, string scene, bool demo)
    {
        if (workspace == null) throw new ArgumentNullException(nameof(workspace));
        string folder = EnsureProfile(RuntimePaths.ConfigRoot, mode, scene, demo);
        workspace.SelectPrimary("system", Path.Combine(folder, "system.json"));
        workspace.SelectPrimary("sequence", Path.Combine(folder, "sequence.json"));
        workspace.RefreshReferences();
    }
    public static void SeedFreshProfile(string root)
    {
        string folder = EnsureProfile(root, "flying", "night-sky", true);
        File.Copy(Path.Combine(folder, "system.json"), Path.Combine(root, "system_config.json"), true);
        File.Copy(Path.Combine(folder, "sequence.json"), Path.Combine(root, "sequenceConfig.json"), true);
    }
    public static string EnsureProfile(string root, string mode, string scene, bool demo)
    {
        if (mode != "flying" && mode != "walking") throw new ArgumentException("Choose flying or walking.");
        if (scene != "night-sky" && scene != "forest") throw new ArgumentException("Choose night-sky or forest.");
        string relative = "QuickStart/" + mode + "-" + scene + "-" + (demo ? "demo" : "live");
        string folder = Path.Combine(root, relative); Directory.CreateDirectory(folder);
        var native = Screen.currentResolution;
        var system = new JObject {
            ["desktopQuickStart"] = new JObject { ["version"] = 1, ["mode"] = mode, ["scene"] = scene, ["demo"] = demo },
            ["targetDisplay"] = 0, ["targetFrameRate"] = 120,
            ["telemetry"] = new JObject { ["enabled"] = true, ["bindAddress"] = "127.0.0.1", ["port"] = 9880, ["rateHz"] = 20, ["contextIntervalSeconds"] = 2, ["topic"] = "matrex.telemetry.v2" },
            ["statusOverlay"] = new JObject { ["visible"] = false, ["targetDisplay"] = 0 },
            ["overheadCamera"] = new JObject { ["enabled"] = false },
            ["configs"] = new JArray(new JObject {
                ["vrId"] = "VR1", ["speciesName"] = "", ["closedLoopMode"] = mode == "flying" ? "Rekkefly" : "Hejjetrac",
                ["desktopPreview"] = true, ["displayOrder"] = "F", ["targetDisplay"] = 0,
                ["ledPanelWidth"] = Math.Max(1, native.width), ["ledPanelHeight"] = Math.Max(1, native.height),
                ["startRow"] = 0, ["startCol"] = 0, ["horizontal"] = true,
                ["zmqAddress"] = "127.0.0.1", ["zmqPort"] = 9871, ["sphereDiameter"] = 2.6,
                ["manualControls"] = new JObject { ["enabled"] = true, ["translateSpeed"] = mode == "flying" ? 7 : 1.5, ["rotateSpeed"] = 50, ["maxTranslateSpeed"] = 30, ["maxRotateSpeed"] = 180, ["allowVerticalTranslation"] = mode == "flying" }
            })
        };
        var environment = new JObject {
            ["objects"] = new JArray(), ["initialPosition"] = new JObject { ["x"] = 0, ["y"] = 100, ["z"] = 0 },
            ["initialRotation"] = new JObject { ["x"] = 0, ["y"] = 0, ["z"] = 0 },
            ["closedLoopOrientation"] = true, ["closedLoopPosition"] = mode == "walking",
            ["walkingPositionGain"] = .01, ["walkingOrientationGain"] = 1,
            ["autopilotEnabled"] = mode == "flying", ["autopilotSpeed"] = 7,
            ["aglHeight"] = mode == "flying" && scene == "night-sky" ? 100 : 1.6,
            ["groundLayerMask"] = 1, ["randomInitialRotation"] = false,
            ["resetPositionOnStart"] = true, ["resetRotationOnStart"] = true,
            ["nightSky"] = new JObject { ["enabled"] = scene == "night-sky", ["latitude"] = -35.894409, ["longitude"] = 148.453948, ["date"] = "2026-10-07", ["localTime"] = "22:00:00", ["utcOffsetHours"] = 11, ["advanceWithRealTime"] = false, ["imageWidth"] = 4096, ["exposure"] = .9, ["faintDetailCutoff"] = .03 }
        };
        if (scene == "forest")
        {
            environment["uniformSkyColor"] = new JObject { ["r"] = .57, ["g"] = .72, ["b"] = .86, ["a"] = 1 };
            var trees = (JArray)environment["objects"];
            for (int row = 0; row < 7; row++)
                for (int column = -3; column <= 3; column++)
                {
                    if (column == 0) continue; // An open walking/flying corridor.
                    trees.Add(new JObject { ["type"] = "tree01", ["groundOnTerrain"] = true,
                        ["position"] = new JObject { ["x"] = column * 12 + (row % 2) * 3, ["y"] = 0, ["z"] = row * 24 + 8 },
                        ["scale"] = new JObject { ["x"] = 1, ["y"] = 1, ["z"] = 1 } });
                }
        }
        var sequence = new JObject { ["autoStart"] = false, ["loop"] = true, ["loopStartIndex"] = 0, ["randomise"] = false,
            ["sequences"] = new JArray(new JObject { ["sceneName"] = "Choice_Selwyn", ["duration"] = 600, ["gain"] = 35, ["autoTrim"] = false, ["reloadScene"] = false,
                ["parameters"] = new JObject { ["configFile"] = relative + "/" + scene + ".json" } }) };
        WriteMissing(Path.Combine(folder, "system.json"), system);
        WriteMissing(Path.Combine(folder, scene + ".json"), environment);
        WriteMissing(Path.Combine(folder, "sequence.json"), sequence);
        return folder;
    }
    private static void WriteMissing(string path, JObject value) { if (!File.Exists(path)) File.WriteAllText(path, value.ToString() + "\n"); }
    public static Vector3 GroundPosition(Vector3 position)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;
            Vector3 local = position - terrain.transform.position, size = terrain.terrainData.size;
            if (local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z)
            { position.y += terrain.SampleHeight(position) + terrain.transform.position.y; break; }
        }
        return position;
    }
}
