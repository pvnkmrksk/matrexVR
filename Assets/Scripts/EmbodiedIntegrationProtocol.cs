using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EmbodiedIntegrationConfig
{
    public bool enabled = false;
    public string protocolVersion = "embodied-integration-2026-07-30";
    public float goalRadiusCm = 60f;
    public float goalBearingDegrees = 20f;
    public float goalLeftX = -20.5212f;
    public float goalRightX = 20.5212f;
    public float goalZ = 56.3816f;
    public float goalHeight = 0f;
    public float goalScaleX = 7f;
    public float goalScaleY = 100f;
    public float goalScaleZ = 7f;
    public float goalVisualAngleDegrees = 10f;
    public float goalContactRadiusCm = 3.5f;
    public float backgroundGray = 0.8f;
    public float qWrite = 0.4f;
    public float qEqual = 0.2f;
    public float triggerZCm = 20.838f;
    public float intertrialSeconds = 5f;
    public float writeDeadlineSeconds = 20f;
    public float readSeconds = 20f;
    public float sessionLimitSeconds = 10800f;
    public float minimumAttemptBudgetSeconds = 40.0167f;
    public float rotYPlusZDegrees = 0f;
    public int yawSign = 1;
    public float positionToleranceCm = 0.05f;
    public float zToleranceCm = 0.05f;
    public float headingToleranceDegrees = 0.25f;
    public float expectedFrameRateHz = 60f;
    public float sphereDiameterCm = 2.6f;
    public float standardStartHeightCm = 0f;
    public int ledPanelWidth = 128;
    public int ledPanelHeight = 128;
    public string displayOrder = "RBLF";
    public string displayColorSpace = "project_setting";
    public string controllerCalibrationId = "REPLACE_WITH_FROZEN_BENCH_CALIBRATION";
    public string codeCommit = "REPLACE_WITH_FROZEN_CODE_COMMIT";
}

public enum EmbodiedPositionTreatment
{
    Preserve,
    Reset
}

public enum EmbodiedHeadingTreatment
{
    Preserve,
    Reset
}

public sealed class EmbodiedCondition
{
    public string Cell;
    public string CueSequence;
    public string AssignedSide;
    public EmbodiedPositionTreatment Position;
    public EmbodiedHeadingTreatment Heading;

    public bool IsSymmetric => CueSequence == "S";
    public bool EqualizeAtTrigger => CueSequence == "U";
}

/// <summary>
/// Pure protocol helpers shared by the live controller and editor validation.
/// Keeping assignment and geometry here makes them testable without sensors.
/// </summary>
public static class EmbodiedIntegrationProtocol
{
    public static readonly string[] CellNames =
    {
        "U_L_P1_H1", "U_R_P1_H1",
        "U_L_P1_H0", "U_R_P1_H0",
        "U_L_P0_H1", "U_R_P0_H1",
        "U_L_P0_H0", "U_R_P0_H0",
        "A_L_P0_H0", "A_R_P0_H0",
        "S_P0_H0"
    };

    public static List<string> NewCellPool()
    {
        return new List<string>(CellNames);
    }

    public static EmbodiedCondition Resolve(string cell, string symmetricPseudoSide)
    {
        if (string.IsNullOrEmpty(cell))
            throw new ArgumentException("A condition cell is required.", nameof(cell));

        string[] parts = cell.Split('_');
        string cue = parts[0];
        string side = cue == "S" ? symmetricPseudoSide : parts[1] == "L" ? "left" : "right";

        bool preservePosition = cell.Contains("_P1_");
        bool preserveHeading = cell.EndsWith("_H1", StringComparison.Ordinal);
        return new EmbodiedCondition
        {
            Cell = cell,
            CueSequence = cue,
            AssignedSide = side,
            Position = preservePosition
                ? EmbodiedPositionTreatment.Preserve
                : EmbodiedPositionTreatment.Reset,
            Heading = preserveHeading
                ? EmbodiedHeadingTreatment.Preserve
                : EmbodiedHeadingTreatment.Reset
        };
    }

    public static float WrapDegrees(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    public static float EngineToMathYaw(float engineRotY, EmbodiedIntegrationConfig config)
    {
        int sign = config.yawSign < 0 ? -1 : 1;
        return WrapDegrees(sign * (engineRotY - config.rotYPlusZDegrees));
    }

    public static float MathToEngineYaw(float mathYaw, EmbodiedIntegrationConfig config)
    {
        int sign = config.yawSign < 0 ? -1 : 1;
        return WrapDegrees(config.rotYPlusZDegrees + sign * mathYaw);
    }

    public static float BearingDegrees(float x, float z, float goalX, float goalZ)
    {
        return Mathf.Atan2(goalX - x, goalZ - z) * Mathf.Rad2Deg;
    }

    public static float BisectorDegrees(float x, float z, EmbodiedIntegrationConfig config)
    {
        float left = BearingDegrees(x, z, config.goalLeftX, config.goalZ) * Mathf.Deg2Rad;
        float right = BearingDegrees(x, z, config.goalRightX, config.goalZ) * Mathf.Deg2Rad;
        return Mathf.Atan2(
            Mathf.Sin(left) + Mathf.Sin(right),
            Mathf.Cos(left) + Mathf.Cos(right)
        ) * Mathf.Rad2Deg;
    }

    public static float ConflictAngleDegrees(float x, float z, EmbodiedIntegrationConfig config)
    {
        float left = BearingDegrees(x, z, config.goalLeftX, config.goalZ);
        float right = BearingDegrees(x, z, config.goalRightX, config.goalZ);
        return Mathf.Abs(WrapDegrees(right - left));
    }

    public static bool FirstForwardCrossing(float previousZ, float currentZ, float triggerZ)
    {
        return previousZ < triggerZ && currentZ >= triggerZ;
    }

    public static string GoalContactSide(
        float x,
        float z,
        EmbodiedIntegrationConfig config)
    {
        float radiusSquared =
            config.goalContactRadiusCm * config.goalContactRadiusCm;
        float leftDistance = new Vector2(
            x - config.goalLeftX,
            z - config.goalZ
        ).sqrMagnitude;
        float rightDistance = new Vector2(
            x - config.goalRightX,
            z - config.goalZ
        ).sqrMagnitude;

        bool left = leftDistance <= radiusSquared;
        bool right = rightDistance <= radiusSquared;
        if (left && right)
            return leftDistance <= rightDistance ? "left" : "right";
        if (left)
            return "left";
        if (right)
            return "right";
        return "";
    }

    public static void ResolveCueValues(
        EmbodiedCondition condition,
        EmbodiedIntegrationConfig config,
        bool postTrigger,
        out float left,
        out float right)
    {
        if (condition.IsSymmetric || (postTrigger && condition.EqualizeAtTrigger))
        {
            left = config.qEqual;
            right = config.qEqual;
            return;
        }

        bool blackLeft = condition.AssignedSide == "left";
        left = blackLeft ? 0f : config.qWrite;
        right = blackLeft ? config.qWrite : 0f;
    }

    public static string OppositeSide(string side)
    {
        return side == "left" ? "right" : "left";
    }
}
