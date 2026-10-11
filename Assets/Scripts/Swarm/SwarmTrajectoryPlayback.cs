using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(500)]
public sealed class SwarmTrajectoryPlayback : MonoBehaviour
{
    private SwarmTrajectoryData data;
    private SwarmPlaybackConfig config;
    private IReadOnlyList<GameObject> agents;
    private Transform target;
    private BoundaryManager boundary;
    private Vector3 offset, fixedOrigin;
    private Quaternion rotation;
    private float positionScale;
    private Vector3[] anchors;
    private bool[] visible;
    private double start, previousTime = -1;
    private long previousCycle = -1;
    public double PlaybackTime { get; private set; }

    public void Configure(SwarmTrajectoryData source, SwarmPlaybackConfig settings, IReadOnlyList<GameObject> roots,
        Transform animal, BoundaryManager bounds, Vector3 centerOffset, Quaternion yaw, float worldUnitsPerMetre = 100f)
    {
        data = source; config = settings ?? new SwarmPlaybackConfig(); config.Validate(); agents = roots;
        target = animal; boundary = bounds; offset = centerOffset;
        fixedOrigin = animal != null ? animal.position : transform.position;
        rotation = yaw * Quaternion.Euler(0, config.yawOffsetDegrees, 0);
        positionScale = worldUnitsPerMetre / config.dataUnitsPerMetre;
        anchors = new Vector3[agents.Count]; visible = new bool[agents.Count];
        start = Time.timeAsDouble;
        Advance(0);
    }
    private void Update() => Advance(Time.timeAsDouble - start);
    public void Advance(double elapsed)
    {
        if (data == null) return;
        double raw = config.startTimeSeconds + Math.Max(0, elapsed) * config.speed;
        double time = config.loop ? raw % data.Duration : raw;
        long cycle = config.loop ? (long)Math.Floor(raw / data.Duration) : 0;
        bool looped = cycle != previousCycle || time < previousTime;
        previousCycle = cycle;
        if (looped) Array.Clear(visible, 0, visible.Length);
        PlaybackTime = time; previousTime = time;
        for (int i = 0; i < agents.Count; i++)
        {
            GameObject agent = agents[i];
            bool active = data.Tracks[i].TrySample(time, config.maxGapSeconds, out Vector3 p, out Vector3 velocity);
            if (active && !visible[i])
            {
                Vector3 origin = target != null ? target.position : fixedOrigin;
                anchors[i] = config.centerOnAnimal ? origin + offset - rotation * (data.Centroid * positionScale) : offset;
            }
            visible[i] = active;
            if (agent.activeSelf != active) agent.SetActive(active);
            if (!active) continue;
            Vector3 position = anchors[i] + rotation * (p * positionScale);
            if (config.periodicBoundary && boundary != null)
                position = Wrap(position, boundary);
            agent.transform.position = position;
            Vector3 forward = rotation * velocity;
            if (forward.sqrMagnitude > 1e-8f)
                agent.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
    public static Vector3 Wrap(Vector3 position, BoundaryManager boundary)
    {
        Vector3 local = Quaternion.Inverse(boundary.referenceRotation) * (position - boundary.Center);
        float x = boundary.boundaryLengthX > 0 ? boundary.boundaryLengthX : boundary.boundarySize;
        float z = boundary.boundaryLengthZ > 0 ? boundary.boundaryLengthZ : boundary.boundarySize;
        local.x = Mathf.Repeat(local.x + x / 2, x) - x / 2;
        local.z = Mathf.Repeat(local.z + z / 2, z) - z / 2;
        if (boundary.wrapY && boundary.boundaryHeight > 0)
            local.y = Mathf.Repeat(local.y + boundary.boundaryHeight / 2, boundary.boundaryHeight) - boundary.boundaryHeight / 2;
        return boundary.Center + boundary.referenceRotation * local;
    }
}
