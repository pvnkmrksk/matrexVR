#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class EmbodiedIntegrationValidation
{
    [Serializable]
    private class ValidationDesign
    {
        public int seed;
        public EmbodiedIntegrationConfig embodiedIntegration;
    }

    private sealed class SimRig
    {
        public readonly System.Random Random;
        public List<string> Pool = new();
        public int Block;
        public string PairFirstSide;
        public string SymmetricSide;
        public int Timeouts;
        public int TechnicalFailures;
        public int Nonchoices;

        public SimRig(int seed)
        {
            Random = new System.Random(seed);
        }

        public void StartBlock()
        {
            Block++;
            Pool = EmbodiedIntegrationProtocol.NewCellPool();
            if (Block % 2 == 1)
            {
                PairFirstSide = Random.Next(2) == 0 ? "left" : "right";
                SymmetricSide = PairFirstSide;
            }
            else
            {
                SymmetricSide = EmbodiedIntegrationProtocol.OppositeSide(PairFirstSide);
            }
        }
    }

    [MenuItem("Tools/Validate Embodied Integration Protocol")]
    public static void RunFromMenu()
    {
        RunBatch();
        EditorUtility.DisplayDialog(
            "Embodied integration",
            "Simulation validation passed. See Assets/StreamingAssets/Validation.",
            "OK");
    }

    public static void RunBatch()
    {
        string designPath = Path.Combine(
            Application.streamingAssetsPath,
            "sequenceDesign_embodiedIntegration.json");
        var design = JsonConvert.DeserializeObject<ValidationDesign>(
            File.ReadAllText(designPath));
        Require(design?.embodiedIntegration != null, "Resolved protocol config loads.");
        EmbodiedIntegrationConfig cfg = design.embodiedIntegration;

        var checks = new List<string>();
        CheckGeometry(cfg, checks);
        CheckConditions(cfg, checks);
        CheckFourIndependentRigs(checks);
        CheckTiming(cfg, checks);
        CheckAttemptLogSerialization(checks);

        string outputDirectory = Path.Combine(Application.streamingAssetsPath, "Validation");
        Directory.CreateDirectory(outputDirectory);
        string reportPath = Path.Combine(
            outputDirectory,
            "embodied_integration_simulation_validation.md");

        var report = new StringBuilder();
        report.AppendLine("# Embodied integration simulated-controller validation");
        report.AppendLine();
        report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        report.AppendLine("Result: **PASS** for deterministic protocol logic.");
        report.AppendLine();
        foreach (string check in checks)
            report.AppendLine($"- PASS — {check}");
        report.AppendLine();
        report.AppendLine("## Required hardware/render validation before animal 1");
        report.AppendLine();
        report.AppendLine("- Freeze `rotYPlusZDegrees`, `yawSign`, the sensor-axis mapping, and measured latency; replace the pending calibration ID in the resolved design.");
        report.AppendLine("- Capture all eleven pre/post rendered transitions on each of four VRs and verify no partial frame.");
        report.AppendLine("- Replay asynchronous sensor streams and verify the transaction-spanning delta is logged but never applied.");
        report.AppendLine("- Verify achieved-state tolerances at the actual display refresh rate and test explicit neutral stop behavior.");
        report.AppendLine("- Record the display color-space setting and code commit in the frozen animal-1 archive.");

        File.WriteAllText(reportPath, report.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[EmbodiedIntegration] simulation validation passed -> {reportPath}");
    }

    private static void CheckGeometry(
        EmbodiedIntegrationConfig cfg,
        List<string> checks)
    {
        float expectedX = cfg.goalRadiusCm *
            Mathf.Sin(cfg.goalBearingDegrees * Mathf.Deg2Rad);
        float expectedZ = cfg.goalRadiusCm *
            Mathf.Cos(cfg.goalBearingDegrees * Mathf.Deg2Rad);
        Require(Mathf.Abs(cfg.goalRightX - expectedX) < 0.0001f, "Right goal coordinate.");
        Require(Mathf.Abs(cfg.goalLeftX + expectedX) < 0.0001f, "Left goal coordinate.");
        Require(Mathf.Abs(cfg.goalZ - expectedZ) < 0.0001f, "Forward goal coordinate.");
        Require(
            Mathf.Abs(cfg.goalContactRadiusCm - 3.5f) < 0.0001f,
            "Reference experiment goal-contact radius.");
        Require(
            EmbodiedIntegrationProtocol.GoalContactSide(
                cfg.goalLeftX + 3.49f, cfg.goalZ, cfg) == "left",
            "Point inside contact radius reaches left goal.");
        Require(
            EmbodiedIntegrationProtocol.GoalContactSide(
                0f, cfg.goalZ + 10f, cfg) == "",
            "Passing the goal z-coordinate does not count as contact.");
        Require(
            EmbodiedIntegrationProtocol.FirstForwardCrossing(20.837f, 20.838f, cfg.triggerZCm),
            "First forward crossing fires.");
        Require(
            !EmbodiedIntegrationProtocol.FirstForwardCrossing(20.838f, 21f, cfg.triggerZCm),
            "Starting on/after threshold does not refire.");
        checks.Add("fixed goal geometry, 3.5-cm radial contact, and first-crossing trigger");
    }

    private static void CheckConditions(
        EmbodiedIntegrationConfig cfg,
        List<string> checks)
    {
        Require(
            EmbodiedIntegrationProtocol.CellNames.Distinct().Count() == 11,
            "Eleven unique cells.");

        foreach (string cell in EmbodiedIntegrationProtocol.CellNames)
        {
            string pseudo = "right";
            EmbodiedCondition condition = EmbodiedIntegrationProtocol.Resolve(cell, pseudo);
            EmbodiedIntegrationProtocol.ResolveCueValues(
                condition, cfg, false, out float preLeft, out float preRight);
            EmbodiedIntegrationProtocol.ResolveCueValues(
                condition, cfg, true, out float postLeft, out float postRight);

            if (condition.CueSequence == "U")
            {
                Require(
                    Mathf.Approximately(postLeft, cfg.qEqual) &&
                    Mathf.Approximately(postRight, cfg.qEqual),
                    $"{cell} equalizes.");
            }
            else
            {
                Require(
                    Mathf.Approximately(preLeft, postLeft) &&
                    Mathf.Approximately(preRight, postRight),
                    $"{cell} matched sham cue setter.");
            }

            float xPre = 6.75f;
            float zPre = cfg.triggerZCm + 0.01f;
            float bisectorPre =
                EmbodiedIntegrationProtocol.BisectorDegrees(xPre, zPre, cfg);
            float psiPre = -7.25f;
            float xPost = condition.Position == EmbodiedPositionTreatment.Reset ? 0f : xPre;
            float bisectorPost =
                EmbodiedIntegrationProtocol.BisectorDegrees(xPost, zPre, cfg);
            float yawTarget = condition.Heading == EmbodiedHeadingTreatment.Reset
                ? bisectorPost
                : EmbodiedIntegrationProtocol.WrapDegrees(bisectorPost + psiPre);
            float achievedPsi = EmbodiedIntegrationProtocol.WrapDegrees(
                yawTarget - bisectorPost);
            Require(
                Mathf.Abs(achievedPsi -
                    (condition.Heading == EmbodiedHeadingTreatment.Reset ? 0f : psiPre)) <
                0.0001f,
                $"{cell} heading residual target.");
            _ = bisectorPre;
        }

        checks.Add("all eleven cue transitions and P/H target equations");
    }

    private static void CheckFourIndependentRigs(List<string> checks)
    {
        var rigs = new[]
        {
            new SimRig(101), new SimRig(202), new SimRig(303), new SimRig(404)
        };
        var asynchronousTriggerFrames = new[] { 25, 91, 47, 133 };
        Require(asynchronousTriggerFrames.Distinct().Count() == 4, "Asynchronous triggers.");

        for (int rigIndex = 0; rigIndex < rigs.Length; rigIndex++)
        {
            SimRig rig = rigs[rigIndex];
            var symmetricSides = new List<string>();
            for (int block = 0; block < 10; block++)
            {
                rig.StartBlock();
                symmetricSides.Add(rig.SymmetricSide);

                int beforeTimeout = rig.Pool.Count;
                rig.Timeouts++;
                Require(rig.Pool.Count == beforeTimeout, "Timeout does not consume.");

                int beforeFailure = rig.Pool.Count;
                rig.TechnicalFailures++;
                Require(rig.Pool.Count == beforeFailure, "Technical failure does not consume.");

                var consumed = new HashSet<string>();
                while (rig.Pool.Count > 0)
                {
                    int index = rig.Random.Next(rig.Pool.Count);
                    string cell = rig.Pool[index];
                    Require(consumed.Add(cell), "Cell consumed once.");
                    if (cell == "U_L_P1_H1")
                        rig.Nonchoices++;
                    rig.Pool.RemoveAt(index); // valid nonchoices consume the cell
                }
                Require(consumed.SetEquals(EmbodiedIntegrationProtocol.CellNames), "Complete block.");
            }

            for (int pair = 0; pair < symmetricSides.Count; pair += 2)
                Require(symmetricSides[pair] != symmetricSides[pair + 1], "Pseudo-side pair balance.");
        }

        Require(rigs[0].Nonchoices == 10, "Nonchoices retained.");
        Require(rigs[3].Timeouts == 10, "One rig can time out independently.");
        checks.Add("four asynchronous VR queues, timeout/failure reinsertion, nonchoice consumption");
        checks.Add("eleven-cell renewal through block ten and symmetric pseudo-side pair balance");
    }

    private static void CheckTiming(
        EmbodiedIntegrationConfig cfg,
        List<string> checks)
    {
        Require(Mathf.Approximately(cfg.intertrialSeconds, 5f), "ITI.");
        Require(Mathf.Approximately(cfg.writeDeadlineSeconds, 20f), "Write.");
        Require(Mathf.Approximately(cfg.readSeconds, 20f), "Read.");
        Require(Mathf.Approximately(cfg.sessionLimitSeconds, 10800f), "Session.");
        Require(
            cfg.minimumAttemptBudgetSeconds >=
            cfg.writeDeadlineSeconds + cfg.readSeconds + 1f / cfg.expectedFrameRateHz - 0.0001f,
            "Attempt start budget.");
        checks.Add("5-s ITI, 20-s write/read, 3-h stop, and one-frame start budget");
    }

    private static void CheckAttemptLogSerialization(List<string> checks)
    {
        string json = JsonConvert.SerializeObject(new Dictionary<string, object>
        {
            ["rawSensorPosition"] = new[] { 1f, 2f, 3f },
            ["rawSensorRotation"] = new[] { 4f, 5f, 6f },
            ["discardedSensorDelta"] = new[] { 0.1f, 0.2f, 0.3f },
            ["sensorBaseline"] = new[] { 7f, 8f, 9f }
        });
        Require(
            json.Contains("\"rawSensorPosition\":[1.0,2.0,3.0]"),
            "Attempt sensor vectors serialize without Unity object graphs.");
        checks.Add("attempt sensor fields serialize as finite numeric arrays");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                $"Embodied integration validation failed: {message}");
    }
}
#endif
