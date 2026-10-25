using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ConfigurationUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class Regression
{
    private static readonly List<string> checks = new List<string>();
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); checks.Add(name); }
    private static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, name);
    }
    public static int Main(string[] args)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "matrex-contracts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            Metadata(); Swarms(); Descriptions(); Documents(scratch); Schemas(args[0]);
            Console.WriteLine(JsonConvert.SerializeObject(new { checks = checks.Count, passed = checks }, Formatting.Indented));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Directory.Delete(scratch, true); }
    }
    private static void Descriptions()
    {
        foreach(string optional in new[]{"nightSky","adaptiveDecision"})
        {
            Check(ExperimentBlocks.Describe(JObject.Parse("{\""+optional+"\":null}"))=="Scene defaults · open to inspect", "Explicit null optional config is safe in setup summary: "+optional);
            Check(ExperimentBlocks.Describe(JObject.Parse("{\"stimulus\":{\""+optional+"\":null}}"))=="Scene defaults · open to inspect", "Explicit null optional phase config is safe in setup summary: "+optional);
        }
        Check(ExperimentBlocks.Describe(JObject.Parse("{\"nightSky\":{\"enabled\":true}}"))=="Live sky", "Object-valued sky summary retains behavior");
    }
    private static void Metadata()
    {
        var legacy = JsonConvert.DeserializeObject<AnimalData>("{\"customSession\":\"retain\",\"Flies\":[{\"VR\":\"VR10\",\"FlyID\":\"animal β / 001\",\"customAnimal\":true}],\"UsedFlyIDs\":[\"older\"]}");
        Check(legacy.Animals.Single().AnimalID == "animal β / 001" && legacy.UsedAnimalIDs.Single() == "older", "Legacy metadata migrates without numeric ID coercion");
        legacy.Animals[0].SpeciesName = "Agrotis infusa";
        var saved = JObject.Parse(JsonConvert.SerializeObject(legacy));
        Check((string)saved["Animals"][0]["AnimalID"] == "animal β / 001" && (string)saved["Animals"][0]["SpeciesName"] == "Agrotis infusa", "Canonical animal ID and species serialize");
        Check(JToken.DeepEquals(saved["Animals"], saved["Flies"]) && (string)saved["Flies"][0]["FlyID"] == "animal β / 001", "Legacy readers receive equivalent transitional aliases");
        Check((string)saved["customSession"] == "retain" && (bool)saved["Animals"][0]["customAnimal"], "Session and animal extensions survive migration");
        foreach (bool canonicalFirst in new[] { true, false })
        {
            string canonical = "\"Animals\":[{\"AnimalID\":\"canonical\"}],\"UsedAnimalIDs\":[\"canonical\"]";
            string old = "\"Flies\":[{\"FlyID\":\"legacy\"}],\"UsedFlyIDs\":[\"legacy\"]";
            var both = JsonConvert.DeserializeObject<AnimalData>("{" + (canonicalFirst ? canonical + "," + old : old + "," + canonical) + "}");
            Check(both.Animals.Count == 1 && both.Animals[0].AnimalID == "canonical" && both.UsedAnimalIDs.SequenceEqual(new[] { "canonical" }), "Canonical collections win regardless of alias order: " + canonicalFirst);
            string ids = canonicalFirst ? "\"AnimalID\":\"canonical\",\"FlyID\":\"legacy\"" : "\"FlyID\":\"legacy\",\"AnimalID\":\"canonical\"";
            Check(JsonConvert.DeserializeObject<AnimalMetadata>("{" + ids + "}").AnimalID == "canonical", "Canonical ID wins regardless of alias order: " + canonicalFirst);
        }
        var empty = JsonConvert.DeserializeObject<AnimalData>("{\"Animals\":[],\"Flies\":[{\"FlyID\":\"legacy\"}],\"UsedAnimalIDs\":null,\"UsedFlyIDs\":[\"legacy\"]}");
        Check(empty.Animals.Count == 0 && empty.UsedAnimalIDs.Count == 0, "Explicit empty/null canonical metadata suppresses legacy values");
        var again = JsonConvert.DeserializeObject<AnimalData>(saved.ToString());
        Check(again.Animals.Count == 1 && again.UsedAnimalIDs.Count == 1, "Dual alias output reloads without duplicating animals or IDs");
    }
    private static void Swarms()
    {
        var old = JsonConvert.DeserializeObject<SwarmConfig>("{\"numberOfLocusts\":150,\"locustSpeed\":6.17,\"locustSpeedStdDev\":1.24}");
        old.ValidateSwarm();
        Check(old.EffectiveAgentCount == 150 && Math.Abs(old.EffectiveAgentSpeed.Value - 6.17f) < .0001 && Math.Abs(old.EffectiveAgentSpeedStdDev - 1.24f) < .0001, "Existing swarm JSON keeps its exact runtime meanings");
        var modern = JsonConvert.DeserializeObject<SwarmConfig>("{\"agentCount\":0,\"numberOfLocusts\":150,\"agentSpeed\":0,\"locustSpeed\":6,\"locoSpeed\":7,\"agentSpeedStdDev\":0,\"locustSpeedStdDev\":1}");
        modern.ValidateSwarm();
        Check(modern.EffectiveAgentCount == 0 && modern.EffectiveAgentSpeed == 0 && modern.EffectiveAgentSpeedStdDev == 0, "Explicit canonical zero overrides all legacy swarm aliases");
        Check(JsonConvert.DeserializeObject<SwarmConfig>("{\"locoSpeed\":3}").EffectiveAgentSpeed == 3, "Historical locoSpeed spelling remains accepted");
        Check(JsonConvert.DeserializeObject<SwarmConfig>("{\"locustSpeed\":2,\"locoSpeed\":3}").EffectiveAgentSpeed == 2, "Legacy locustSpeed takes precedence over locoSpeed");
        Check(new SwarmConfig().EffectiveAgentCount == null && new SwarmConfig().EffectiveAgentSpeed == null, "Omitted canonical fields retain authored defaults");
        var phase = JObject.Parse("{\"swarm\":{\"agentCount\":12,\"agentSpeed\":4,\"agentSpeedStdDev\":0.5}}");
        var nested = KannadiConfig.LoadSwarm(new Dictionary<string, object> { [ExperimentPhases.PayloadKey] = phase });
        Check(nested.EffectiveAgentCount == 12 && nested.EffectiveAgentSpeed == 4 && nested.EffectiveAgentSpeedStdDev == .5f, "Nested phased Swarm configurations carry canonical settings into the runtime loader");
        foreach (string input in new[] { "{\"agentCount\":-1}", "{\"agentSpeed\":-1}", "{\"agentSpeedStdDev\":-1}", "{\"locoSpeed\":-1}" })
            Reject(() => JsonConvert.DeserializeObject<SwarmConfig>(input).ValidateSwarm(), "Reject invalid canonical/legacy speed or population: " + input);
        var rigs = new KannadiConfig { vrConfigs = new[] { new VRConfig { vrIndex = 10, watchIndex = 6 } } };
        rigs.Validate(); Check(true, "Kannadi config supports positive rig indexes beyond four");
        rigs.vrConfigs = new[] { new VRConfig { vrIndex = 10 }, new VRConfig { vrIndex = 10 } };
        Reject(rigs.Validate, "Duplicate Kannadi rig indexes are rejected");
    }
    private static void Documents(string scratch)
    {
        string path = Path.Combine(scratch, "experiment.json");
        string original = "{\n  \"future\": {\"precise\": 1.2345678901234567890123456789, \"exponent\": 1e999, \"negativeZero\": -0}, \"enabled\":false, \"unset\":null\n}\n";
        byte[] bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(original)).ToArray();
        File.WriteAllBytes(path, bytes);
        var doc = new ConfigDocument(path, "swarm"); doc.Save();
        Check(File.ReadAllBytes(path).SequenceEqual(bytes), "No-op save preserves BOM, whitespace and exact bytes");
        try { doc.Edit(root => { root["partial"] = true; throw new InvalidOperationException("Interrupted UI callback"); }); } catch (InvalidOperationException) { }
        Check(doc.Root["partial"] == null && !doc.Dirty && !doc.CanUndo, "Failed UI transaction rolls back every mutation without creating undo history");
        doc.Edit(root => root["enabled"] = true); doc.Undo();
        try { doc.Edit(root => { root["partial"] = true; throw new InvalidOperationException(); }); } catch (InvalidOperationException) { }
        Check(doc.CanRedo && doc.Root["partial"] == null, "Failed edit also preserves existing redo history"); doc.Redo(); doc.Save();
        string saved = File.ReadAllText(path);
        Check(saved.Contains("1.2345678901234567890123456789") && saved.Contains("1e999") && saved.Contains("-0") && doc.Root["unset"].Type == JTokenType.Null, "Form save preserves future fields, arbitrary numeric lexemes, null and false");
        Check(File.ReadAllBytes(path + ".bak").SequenceEqual(bytes), "Saving retains the previous exact file as a recoverable backup");
        doc.Edit(root => root["draft"] = "retain me"); doc.RevertToSaved();
        Check(doc.Root["draft"] == null && !doc.Dirty, "Revert restores the saved JSON"); doc.Undo();
        Check((string)doc.Root["draft"] == "retain me", "Revert itself is undoable");
        File.WriteAllText(path, "{\"external\":true}"); bool conflict = false;
        try { doc.Save(); } catch (IOException) { conflict = true; }
        Check(conflict && File.ReadAllText(path) == "{\"external\":true}", "External conflicts never overwrite disk");
        doc.Reload(true); Check(doc.Root["external"] != null && !doc.Conflict, "Explicit disk reload resolves conflict");
        doc.Undo(); Check((string)doc.Root["draft"] == "retain me", "Disk reload keeps the prior draft available through Undo");
        File.WriteAllText(path, "{\"external\":true}"); doc.CheckExternalChange();
        Check(!doc.Conflict, "A conflict clears when disk again matches the accepted baseline");
        doc.ApplyJson("{\"agentSpeed\":3,\"unknown\":[null,false,-0,1e999]}"); doc.Undo();
        Check((string)doc.Root["draft"] == "retain me", "Raw JSON replacement shares the same undoable document");
    }
    private static void Schemas(string repo)
    {
        var schemas = JObject.Parse(File.ReadAllText(Path.Combine(repo, "Assets/Resources/ConfigurationUI/schemas.json")));
        Check(schemas["system"]["properties"]["configs"]["items"]["properties"]["speciesName"] != null, "Shipped system schema exposes speciesName");
        foreach (string schema in new[] { "swarm", "kannadi" })
            foreach (string key in new[] { "agentCount", "agentSpeed", "agentSpeedStdDev", "locoSpeed" })
                Check(schemas[schema]["properties"][key] != null, "Shipped schema exposes " + schema + "." + key);
    }
}
