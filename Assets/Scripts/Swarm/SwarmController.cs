using System.Collections.Generic;
using System.Linq;
using InSceneSequence;
using UnityEngine;

public class SwarmController : MonoBehaviour, IInSceneSequencer
{
    public void InitializeScene(Dictionary<string, object> parameters)
    {
        KannadiConfig config = KannadiConfig.LoadSwarm(parameters);
        NightSkyController.Apply(gameObject, config.nightSky, config.skyboxPath, config.uniformSkyColor);
        Keyboard.ApplyAutopilotConfig(config.autopilotEnabled, config.autopilotSpeed);
        foreach (LocustSpawner spawner in FindObjectsOfType<LocustSpawner>())
        {
            if (!spawner.isActiveAndEnabled || spawner.gameObject.scene != gameObject.scene) continue;
            ClosedLoop rig = spawner.GetComponent<ClosedLoop>();
            VRConfig vr = config.vrConfigs?.FirstOrDefault(v => v.vrIndex == Kannadi.ParseVRIndex(spawner.name));
            if (rig != null)
            {
                rig.SetLocustGains(vr?.closedLoopPosition ?? config.closedLoopPosition ?? 1,
                                  vr?.closedLoopOrientation ?? config.closedLoopOrientation ?? 1);
                Vector3 position = (vr?.initialPosition ?? config.initialPosition)?.ToVector3() ?? rig.transform.position;
                Vector3 rotation = (vr?.initialRotation ?? config.initialRotation)?.ToVector3() ?? rig.transform.eulerAngles;
                if (config.randomInitialRotation && config.resetRotationOnStart) rotation.y = config.SampleInitialYaw(Random.value);
                if (vr != null || config.initialPosition != null || config.initialRotation != null || config.randomInitialRotation)
                    rig.ApplyStartPose(position,
                        Quaternion.Euler(rotation), config.resetPositionOnStart, config.resetRotationOnStart);
                if (!ExperimentPhases.IsPhase(parameters))
                    StimulusHeadingReference.For(rig).Begin(config.headingReference,
                        reference => { if (reference != null && config.SwarmEnabled && config.useHeadingReference) SwarmConfiguration.Apply(spawner, config, rig); });
            }
            else if (config.headingReference?.enabled == true)
                throw new System.InvalidOperationException("Heading reference requires a ClosedLoop rig: " + spawner.name);
            if (config.SwarmEnabled) SwarmConfiguration.Apply(spawner, config, rig);
            else spawner.PrepareObservation();
        }
        SimpleOverheadCamera.EnsureInScene();
    }
    public void AdvanceStep(Dictionary<string, object> parameters) => InitializeScene(parameters);
    private void Start() => SimpleOverheadCamera.EnsureInScene();
}
