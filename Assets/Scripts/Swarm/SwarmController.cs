using System.Collections.Generic;
using System.Linq;
using InSceneSequence;
using UnityEngine;

public class SwarmController : MonoBehaviour, IInSceneSequencer
{
    private bool configured;

    public void InitializeScene(Dictionary<string, object> parameters)
    {
        NightSkyController.Apply(gameObject, null, null);
        KannadiConfig movement = KannadiConfig.Load(parameters);
        NightSkyController.Apply(gameObject, movement.nightSky, movement.skyboxPath);
        Keyboard.ApplyAutopilotConfig(movement.autopilotEnabled, movement.autopilotSpeed);
        foreach (LocustSpawner spawner in FindObjectsOfType<LocustSpawner>())
        {
            if (!spawner.isActiveAndEnabled || spawner.gameObject.scene != gameObject.scene) continue;
            spawner.loadConfigFromJsonFile = false;
            spawner.numberOfLocusts = movement.numberOfLocusts ?? spawner.numberOfLocusts;
            spawner.spawnAreaSize = movement.spawnAreaSize ?? spawner.spawnAreaSize;
            var previousReference = spawner.GetComponent<StimulusHeadingReference>();
            float priorZero = previousReference != null && previousReference.Result != null ? (float)previousReference.Result.meanHeadingDegrees : 0f;
            spawner.mu = movement.mu ?? Mathf.Repeat(spawner.mu - priorZero, 360f);
            spawner.kappa = movement.kappa ?? spawner.kappa;
            spawner.locustSpeed = movement.locustSpeed ?? spawner.locustSpeed;
            spawner.dimension = movement.dimension ?? spawner.dimension;
            spawner.useSpawnCenter = movement.spawnCenter != null;
            spawner.spawnCenter = movement.spawnCenter != null ? movement.spawnCenter.ToVector3() : spawner.spawnCenter;
            spawner.useSpawnVolume = movement.spawnVolumeSize != null;
            spawner.spawnVolumeSize = movement.spawnVolumeSize != null ? movement.spawnVolumeSize.ToVector3() : spawner.spawnVolumeSize;
            spawner.muElevation = movement.muElevation ?? spawner.muElevation;
            spawner.kappaElevation = movement.kappaElevation ?? spawner.kappaElevation;
            spawner.boundaryHeight = movement.boundaryHeight ?? spawner.boundaryHeight;
            spawner.wrapY = movement.wrapY;
            spawner.agentVisual = movement.agentVisual ?? spawner.agentVisual;
            if (movement.bogongVisual != null) spawner.bogongVisual = movement.bogongVisual;
            if (!movement.numberOfLocusts.HasValue && movement.density.HasValue)
            {
                bool is3D = string.Equals(spawner.dimension, "3D", System.StringComparison.OrdinalIgnoreCase);
                Vector3 volume = spawner.useSpawnVolume ? spawner.spawnVolumeSize : new Vector3(spawner.spawnAreaSize, spawner.spawnAreaSize, spawner.spawnAreaSize);
                float measure = is3D ? volume.x * volume.y * volume.z : spawner.spawnAreaSize * spawner.spawnAreaSize;
                spawner.numberOfLocusts = Mathf.Max(0, Mathf.RoundToInt(movement.density.Value * measure));
            }
            if (spawner.boundaryManager != null)
            {
                bool is3D = string.Equals(spawner.dimension, "3D", System.StringComparison.OrdinalIgnoreCase);
                // The historical 2D swarm owns its boundary in the scene prefab;
                // only a 3D experiment opts into config-driven rectangular bounds.
                spawner.boundaryManager.boundaryLengthX = is3D ? movement.boundaryLengthX : 0;
                spawner.boundaryManager.boundaryLengthZ = is3D ? movement.boundaryLengthZ : 0;
                spawner.boundaryManager.wrapY = is3D && movement.wrapY;
                if (spawner.boundaryManager.wrapY)
                    spawner.boundaryManager.boundaryHeight = spawner.boundaryHeight > 0 ? spawner.boundaryHeight : spawner.spawnVolumeSize.y;
                else
                    spawner.boundaryManager.boundaryHeight = 0;
            }
            ClosedLoop closedLoop = spawner.GetComponent<ClosedLoop>();
            VRConfig vr = movement.vrConfigs?.FirstOrDefault(v => v.vrIndex == Kannadi.ParseVRIndex(spawner.name));
            if (closedLoop != null)
            {
                closedLoop.SetLocustGains(vr?.closedLoopPosition ?? movement.closedLoopPosition ?? 1,
                                          vr?.closedLoopOrientation ?? movement.closedLoopOrientation ?? 1);
                if (vr != null && !(configured && movement.headingReference?.enabled == true))
                    closedLoop.SetPositionAndRotation(vr.initialPosition?.ToVector3() ?? spawner.transform.position,
                        vr.initialRotation != null ? Quaternion.Euler(vr.initialRotation.ToVector3()) : spawner.transform.rotation);
            }
            spawner.animateOnMove = movement.animateOnMove;
            spawner.animationSpeedThreshold = movement.animationSpeedThreshold;
            if (closedLoop != null)
            {
                float relativeMu = spawner.mu;
                spawner.PrepareObservation();
                StimulusHeadingReference.For(closedLoop).Begin(movement.headingReference, reference => {
                    spawner.mu = reference != null ? reference.Resolve(relativeMu) : relativeMu;
                    spawner.Rebuild();
                });
            }
            else if (movement.headingReference?.enabled == true)
                throw new System.InvalidOperationException("Heading-relative swarm requires a ClosedLoop rig: " + spawner.name);
            else spawner.Rebuild();
        }
        configured = true;
        SimpleOverheadCamera.EnsureInScene();
    }
    public void AdvanceStep(Dictionary<string, object> parameters) => InitializeScene(parameters);
    private void Start() => SimpleOverheadCamera.EnsureInScene();
}
