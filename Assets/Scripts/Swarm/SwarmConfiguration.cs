using UnityEngine;

/// <summary>Shared by standalone Swarm and a swarm embedded in a Choice terrain scene.</summary>
public static class SwarmConfiguration
{
    public static float SceneUnitsPerMetre(GameObject owner)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
            if (terrain.gameObject.scene == owner.scene) return 1f;
        return 100f;
    }

    public static void Apply(LocustSpawner spawner, SwarmConfig config, ClosedLoop rig, float sceneUnitsPerMetre = 0f)
    {
        config.ValidateSwarm();
        // Births at t=0 must use the settled flight height, before the first LateUpdate.
        if (rig != null && !string.IsNullOrWhiteSpace(config.trajectoryFile))
            rig.GetComponent<MigrationMotion>()?.Advance(0);
        spawner.worldUnitsPerMetre = config.worldUnitsPerMetre ?? (sceneUnitsPerMetre > 0 ? sceneUnitsPerMetre : SceneUnitsPerMetre(spawner.gameObject));
        spawner.trajectoryFile = config.trajectoryFile;
        spawner.playbackSettings = config.playback ?? new SwarmPlaybackConfig();
        spawner.playbackTarget = rig != null ? rig.transform : null;
        spawner.playbackVolumeSpecified = config.spawnVolumeSize != null;
        spawner.loadConfigFromJsonFile = false;
        spawner.numberOfLocusts = config.EffectiveAgentCount ?? spawner.numberOfLocusts;
        spawner.spawnAreaSize = config.spawnAreaSize ?? spawner.spawnAreaSize;
        // Keep authored direction separate from the resolved world direction across repeats.
        spawner.authoredMu = config.mu ?? spawner.authoredMu ?? spawner.mu;
        var reference = config.useHeadingReference && rig != null ? rig.GetComponent<StimulusHeadingReference>()?.Result : null;
        if (!string.IsNullOrWhiteSpace(config.headingReferenceId))
        {
            if (rig == null || MainController.Instance == null)
                throw new System.InvalidOperationException("A named swarm heading requires a rig and a running sequence.");
            reference = MainController.Instance.ResolveHeadingReference(rig.gameObject, config.headingReferenceId);
        }
        float yaw = reference != null ? (float)reference.meanHeadingDegrees : 0;
        spawner.mu = Mathf.Repeat(spawner.authoredMu.Value + yaw, 360);
        spawner.kappa = config.kappa ?? spawner.kappa;
        spawner.directionDistribution = config.directionDistribution;
        spawner.locustSpeed = config.EffectiveAgentSpeed ?? spawner.locustSpeed;
        spawner.locustSpeedStdDev = config.EffectiveAgentSpeedStdDev;
        spawner.preserveBoundaryOvershoot = config.preserveBoundaryOvershoot;
        spawner.dimension = config.dimension ?? spawner.dimension;
        spawner.useSpawnVolume = config.spawnVolumeSize != null;
        if (config.spawnVolumeSize != null) spawner.spawnVolumeSize = config.spawnVolumeSize.ToVector3();
        spawner.muElevation = config.muElevation ?? spawner.muElevation;
        spawner.kappaElevation = config.kappaElevation ?? spawner.kappaElevation;
        spawner.boundaryHeight = config.boundaryHeight ?? spawner.boundaryHeight;
        spawner.wrapY = config.wrapY;
        bool generatedCube = string.IsNullOrWhiteSpace(config.trajectoryFile) && config.volumeHalfExtentMetres.HasValue;
        if (generatedCube)
        {
            float side = 2f * config.volumeHalfExtentMetres.Value * spawner.worldUnitsPerMetre;
            spawner.spawnAreaSize = side;
            spawner.useSpawnVolume = true;
            spawner.spawnVolumeSize = Vector3.one * side;
            spawner.boundaryHeight = side;
        }
        spawner.agentVisual = config.agentVisual ?? spawner.agentVisual;
        if (config.bogongVisual != null) { config.bogongVisual.Validate(); spawner.bogongVisual = config.bogongVisual; }
        spawner.animateOnMove = config.animateOnMove;
        spawner.animationSpeedThreshold = config.animationSpeedThreshold;
        bool is3D = string.Equals(spawner.dimension, "3D", System.StringComparison.OrdinalIgnoreCase);
        if (!config.EffectiveAgentCount.HasValue && config.density.HasValue)
        {
            Vector3 size = spawner.useSpawnVolume ? spawner.spawnVolumeSize : Vector3.one * spawner.spawnAreaSize;
            float measure = is3D ? size.x * size.y * size.z : spawner.spawnAreaSize * spawner.spawnAreaSize;
            spawner.numberOfLocusts = Mathf.Max(0, Mathf.RoundToInt(config.density.Value * measure));
        }
        bool relative = config.spawnRelativeToAnimal || reference != null;
        Quaternion rotation = Quaternion.Euler(0, yaw, 0);
        Vector3 offset = rotation * (config.spawnCenter?.ToVector3() ?? Vector3.zero);
        spawner.playbackOffset = offset;
        spawner.useSpawnCenter = config.spawnCenter != null || relative;
        spawner.spawnCenter = relative && rig != null ? rig.transform.position + offset : offset;
        spawner.spawnRotation = rotation;
        if (spawner.boundaryManager != null)
        {
            var boundary = spawner.boundaryManager;
            boundary.boundaryLengthX = generatedCube ? spawner.spawnVolumeSize.x : is3D ? config.boundaryLengthX : 0;
            boundary.boundaryLengthZ = generatedCube ? spawner.spawnVolumeSize.z : is3D ? config.boundaryLengthZ : 0;
            boundary.preserveOvershoot = config.preserveBoundaryOvershoot;
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
        if (rig != null && (config.nearClipMetres.HasValue || System.String.Equals(config.bogongVisual?.shape, "Moth", System.StringComparison.OrdinalIgnoreCase)))
            spawner.OverrideNearClip(rig.GetComponentsInChildren<Camera>(true), (config.nearClipMetres ?? .001f) * spawner.worldUnitsPerMetre);
    }
}
