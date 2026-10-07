using UnityEngine;

/// <summary>Shared by standalone Swarm and a swarm embedded in a Choice terrain scene.</summary>
public static class SwarmConfiguration
{
    public static void Apply(LocustSpawner spawner, SwarmConfig config, ClosedLoop rig)
    {
        config.ValidateSwarm();
        spawner.loadConfigFromJsonFile = false;
        spawner.numberOfLocusts = config.numberOfLocusts ?? spawner.numberOfLocusts;
        spawner.spawnAreaSize = config.spawnAreaSize ?? spawner.spawnAreaSize;
        // Keep authored direction separate from the resolved world direction across repeats.
        spawner.authoredMu = config.mu ?? spawner.authoredMu ?? spawner.mu;
        var reference = config.useHeadingReference && rig != null ? rig.GetComponent<StimulusHeadingReference>()?.Result : null;
        float yaw = reference != null ? (float)reference.meanHeadingDegrees : 0;
        spawner.mu = Mathf.Repeat(spawner.authoredMu.Value + yaw, 360);
        spawner.kappa = config.kappa ?? spawner.kappa;
        spawner.locustSpeed = config.locustSpeed ?? spawner.locustSpeed;
        spawner.dimension = config.dimension ?? spawner.dimension;
        spawner.useSpawnVolume = config.spawnVolumeSize != null;
        if (config.spawnVolumeSize != null) spawner.spawnVolumeSize = config.spawnVolumeSize.ToVector3();
        spawner.muElevation = config.muElevation ?? spawner.muElevation;
        spawner.kappaElevation = config.kappaElevation ?? spawner.kappaElevation;
        spawner.boundaryHeight = config.boundaryHeight ?? spawner.boundaryHeight;
        spawner.wrapY = config.wrapY;
        spawner.agentVisual = config.agentVisual ?? spawner.agentVisual;
        if (config.bogongVisual != null) { config.bogongVisual.Validate(); spawner.bogongVisual = config.bogongVisual; }
        spawner.animateOnMove = config.animateOnMove;
        spawner.animationSpeedThreshold = config.animationSpeedThreshold;
        bool is3D = string.Equals(spawner.dimension, "3D", System.StringComparison.OrdinalIgnoreCase);
        if (!config.numberOfLocusts.HasValue && config.density.HasValue)
        {
            Vector3 size = spawner.useSpawnVolume ? spawner.spawnVolumeSize : Vector3.one * spawner.spawnAreaSize;
            float measure = is3D ? size.x * size.y * size.z : spawner.spawnAreaSize * spawner.spawnAreaSize;
            spawner.numberOfLocusts = Mathf.Max(0, Mathf.RoundToInt(config.density.Value * measure));
        }
        bool relative = config.spawnRelativeToAnimal || reference != null;
        Quaternion rotation = Quaternion.Euler(0, yaw, 0);
        Vector3 offset = rotation * (config.spawnCenter?.ToVector3() ?? Vector3.zero);
        spawner.useSpawnCenter = config.spawnCenter != null || relative;
        spawner.spawnCenter = relative && rig != null ? rig.transform.position + offset : offset;
        spawner.spawnRotation = rotation;
        if (spawner.boundaryManager != null)
        {
            var boundary = spawner.boundaryManager;
            boundary.boundaryLengthX = is3D ? config.boundaryLengthX : 0;
            boundary.boundaryLengthZ = is3D ? config.boundaryLengthZ : 0;
            boundary.wrapY = is3D && config.wrapY;
            boundary.boundaryHeight = boundary.wrapY ? (spawner.boundaryHeight > 0 ? spawner.boundaryHeight : spawner.spawnVolumeSize.y) : 0;
            boundary.referenceRotation = rotation;
            // Historical scenes keep their prefab boundary unless relative placement is requested.
            boundary.useCenterOverride = relative;
            boundary.centerOverride = spawner.spawnCenter;
            boundary.followTarget = config.followAnimalPosition && rig != null ? rig.transform : null;
            boundary.followOffset = relative ? offset : spawner.spawnCenter - (rig != null ? rig.transform.position : Vector3.zero);
        }
        spawner.Rebuild();
    }
}
