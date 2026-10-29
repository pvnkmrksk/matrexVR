#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class SeedSequenceValidation
{
    public static void Run()
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        if (!File.Exists(Path.Combine(project, "KANNADI_VALIDATION_COPY"))) throw new InvalidOperationException("Use a disposable validation project.");
        string output = Path.Combine(project, "OverhaulValidation"); Directory.CreateDirectory(output);
        int checks = 0;
        void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
        try
        {
            var steps = new List<SequenceStep> {
                new SequenceStep("Kannadi", 1, null) { group = "pair", title = "Entraining" },
                new SequenceStep("Kannadi", 1, null) { group = "pair", title = "Response" },
                new SequenceStep("Choice", 1, null),
                new SequenceStep("Choice", 1, null) { group = "triplet" },
                new SequenceStep("Choice", 1, null) { group = "triplet" },
                new SequenceStep("Choice", 1, null) { group = "triplet" }
            };
            for (int seed = 0; seed < 128; seed++)
            {
                var order = MainController.BuildExecutionOrder(steps, new System.Random(seed));
                Check(order.Count == 6 && order.Distinct().Count() == 6, "Shuffle retains every row");
                Check(order.IndexOf(1) == order.IndexOf(0) + 1, "Pair order and adjacency");
                Check(order.IndexOf(4) == order.IndexOf(3) + 1 && order.IndexOf(5) == order.IndexOf(3) + 2, "Triplet order and adjacency");
                Check(order.SequenceEqual(MainController.BuildExecutionOrder(steps, new System.Random(seed))), "Same seed yields identical group order");
            }
            steps[5].group = "pair";
            bool invalid = false; try { MainController.BuildExecutionOrder(steps, new System.Random(1)); } catch (ArgumentException) { invalid = true; }
            Check(invalid, "Disjoint group fails visibly");
            var grouped = new SequenceConfig { sequences = new[] { new SequenceItem { group = "pair" }, new SequenceItem { group = "pair" } }, loopStartIndex = 1 };
            invalid = false; try { grouped.ValidateLoop(); } catch (ArgumentException) { invalid = true; }
            Check(invalid, "Loop start cannot split a pair");
            grouped.loopStartIndex = 0; grouped.ValidateLoop(); Check(true, "Complete pair is a valid loop unit");
            int NamedSequence(string directory)
            {
                Directory.CreateDirectory(directory);
                ExperimentRandom.Begin("Fixed", 345, directory, "My paradigm", "nested/sequence.json");
                int first = ExperimentRandom.NextSeed("objects");
                using (ExperimentRandom.Scope("initial-pose"))
                {
                    var values = Enumerable.Range(0, 20).Select(_ => UnityEngine.Random.value).ToArray();
                    File.WriteAllText(Path.Combine(directory, "samples.json"), new JArray(values).ToString());
                }
                Check((int)JObject.Parse(File.ReadAllText(Path.Combine(directory, "reproducibility.json")))["resolvedSeed"] == 345, "Resolved fixed seed saved");
                return first;
            }
            string a = Path.Combine(output, Guid.NewGuid().ToString("N")), b = Path.Combine(output, Guid.NewGuid().ToString("N"));
            Check(NamedSequence(a) == NamedSequence(b), "Named stream seed repeats across runs");
            Check(File.ReadAllText(Path.Combine(a, "samples.json")) == File.ReadAllText(Path.Combine(b, "samples.json")), "Unity initialization random values repeat");
            string c = Path.Combine(output, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(c);
            ExperimentRandom.Begin("Random", 1, c, null, "sequence.json");
            Check((int)JObject.Parse(File.ReadAllText(Path.Combine(c, "reproducibility.json")))["resolvedSeed"] == ExperimentRandom.ResolvedSeed, "Random seed is saved before use");
            invalid = false; try { ExperimentRandom.Begin("Fixed", 2, c, null, "sequence.json"); } catch (IOException) { invalid = true; }
            Check(invalid, "Existing run seed is never overwritten");
            string streams = Path.Combine(c, "random_streams.jsonl");
            ExperimentRandom.NextSeed("before-end"); string saved = File.ReadAllText(streams);
            ExperimentRandom.End(); ExperimentRandom.NextSeed("replay-only");
            Check(!ExperimentRandom.Active && File.ReadAllText(streams) == saved, "Replay random sampling after End cannot append to acquisition evidence");
            var fixture = new GameObject("Seed stream fixture"); var left = new GameObject("left"); var right = new GameObject("right");
            left.transform.SetParent(fixture.transform); right.transform.SetParent(fixture.transform);
            string d = Path.Combine(output, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
            ExperimentRandom.Begin("Fixed", 42, d, null, "sequence.json"); using (ExperimentRandom.Scope("phase")) { }
            int leftA = ExperimentRandom.ForComponent(left.transform, "noise").Next(), rightA = ExperimentRandom.ForComponent(right.transform, "noise").Next();
            using (ExperimentRandom.ScopeFor(left.transform, "late-start")) { }
            Check(ExperimentRandom.Context == "phase", "Delayed object initialization retains the owning phase context");
            string e = Path.Combine(output, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(e);
            ExperimentRandom.Begin("Fixed", 42, e, null, "sequence.json"); using (ExperimentRandom.Scope("phase")) { }
            int rightB = ExperimentRandom.ForComponent(right.transform, "noise").Next(), leftB = ExperimentRandom.ForComponent(left.transform, "noise").Next();
            Check(leftA == leftB && rightA == rightB && leftA != rightA, "Component streams are distinct and independent of initialization order");
            ExperimentRandom.End(); UnityEngine.Object.DestroyImmediate(fixture);
            File.WriteAllText(Path.Combine(output, "seed-sequence-results.json"), new JObject { ["passed"] = true, ["checks"] = checks, ["unity"] = Application.unityVersion }.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "seed-sequence-results.json"), new JObject { ["passed"] = false, ["checks"] = checks, ["error"] = error.ToString() }.ToString());
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
#endif
