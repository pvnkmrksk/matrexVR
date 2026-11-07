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
        float unitsPerMetre = config.worldUnitsPerMetre ?? (sceneUnitsPerMetre > 0 ? sceneUnitsPerMetre : SceneUnitsPerMetre(spawner.gameObject));
        config.ValidateSpawnBounds(unitsPerMetre);
        spawner.modelAppearance = config.modelAppearance;
        if (!string.IsNullOrWhiteSpace(config.modelPath)) spawner.locustPrefab = ExternalModelLibrary.GetPrefab(config.modelPath);
        if (config.HasAgentGroups &&
            (spawner.locustPrefab == null || Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog")?.Find("SimulatedLocust") != spawner.locustPrefab))
            throw new System.ArgumentException("agentGroups require the built-in SimulatedLocust model.");
        spawner.agentGroups = config.agentGroups;
        // Births at t=0 must use the settled flight height, before the first LateUpdate.
        if (rig != null && !string.IsNullOrWhiteSpace(config.trajectoryFile))
            rig.GetComponent<MigrationMotion>()?.Advance(0);
        spawner.worldUnitsPerMetre = unitsPerMetre;
        spawner.trajectoryFile = config.trajectoryFile;
        spawner.playbackSettings = config.playback ?? new SwarmPlaybackConfig();
        spawner.playbackTarget = rig != null ? rig.transform : null;
        spawner.playbackVolumeSpecified = config.spawnVolumeSize != null;
        spawner.loadConfigFromJsonFile = false;
        spawner.numberOfLocusts = config.EffectiveAgentCount ?? spawner.numberOfLocusts;
        spawner.spawnType = config.spawnType;
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
        spawner.dimension = config.dimension ?? spawner.dimension;
        spawner.muElevation = config.muElevation ?? spawner.muElevation;
        spawner.kappaElevation = config.kappaElevation ?? spawner.kappaElevation;
        spawner.periodicBoundary = config.periodicBoundary;
        spawner.wrapY = config.wrapY;
        bool generated = string.IsNullOrWhiteSpace(config.trajectoryFile);
        float? cubeSide = generated ? config.PhysicalArenaSize(unitsPerMetre) : null;
        float lengthX = config.boundaryLengthX ?? cubeSide ?? SwarmConfig.DefaultBoundaryLength;
        float lengthZ = config.boundaryLengthZ ?? cubeSide ?? SwarmConfig.DefaultBoundaryLength;
        float defaultSpawnSize = cubeSide ?? spawner.SceneSpawnAreaSize;
        if (generated && config.periodicBoundary)
            defaultSpawnSize = Mathf.Min(defaultSpawnSize, Mathf.Min(lengthX, lengthZ));
        bool is3D = spawner.Is3D();
        spawner.boundaryHeight = config.boundaryHeight ?? cubeSide ?? config.spawnVolumeSize?.y ?? config.spawnAreaSize ?? defaultSpawnSize;
        if (generated && config.periodicBoundary && is3D && config.wrapY)
            defaultSpawnSize = Mathf.Min(defaultSpawnSize, spawner.boundaryHeight);
        spawner.spawnAreaSize = config.spawnAreaSize ?? defaultSpawnSize;
        spawner.useSpawnVolume = config.spawnVolumeSize != null;
        spawner.spawnVolumeSize = config.spawnVolumeSize?.ToVector3() ?? Vector3.one * spawner.spawnAreaSize;
        config.ValidateAgentGroupBounds(spawner.spawnVolumeSize, lengthX, lengthZ,
            is3D && config.wrapY ? spawner.boundaryHeight : (float?)null);
        spawner.agentVisual = config.agentVisual ?? spawner.agentVisual;
        if (config.bogongVisual != null) { config.bogongVisual.Validate(); spawner.bogongVisual = config.bogongVisual; }
        spawner.animateOnMove = config.animateOnMove;
        spawner.animationSpeedThreshold = config.animationSpeedThreshold;
        if (!config.EffectiveAgentCount.HasValue && config.density.HasValue)
        {
            Vector3 size = spawner.spawnVolumeSize;
            float measure = size.x * size.z * (is3D ? size.y : 1);
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
            boundary.boundaryLengthX = lengthX;
            boundary.boundaryLengthZ = lengthZ;
            boundary.preserveOvershoot = true;
            boundary.wrapY = is3D && config.wrapY;
            boundary.boundaryHeight = boundary.wrapY ? spawner.boundaryHeight : 0;
            boundary.referenceRotation = rotation;
            boundary.useCenterOverride = true;
            boundary.centerOverride = spawner.SpawnOrigin;
            boundary.followTarget = config.followAnimalPosition && rig != null ? rig.transform : null;
            boundary.followOffset = boundary.centerOverride - (rig != null ? rig.transform.position : Vector3.zero);
        }
        spawner.Rebuild();
        float? near = config.nearClipMetres;
        if (near == null && System.String.Equals(config.bogongVisual?.shape, "Moth", System.StringComparison.OrdinalIgnoreCase)) near = .001f;
        if (rig != null && (near.HasValue || config.farClipMetres.HasValue))
            spawner.OverrideClipPlanes(rig.GetComponentsInChildren<Camera>(true), near * unitsPerMetre, config.farClipMetres * unitsPerMetre);
    }
}
