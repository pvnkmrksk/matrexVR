using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Native serial effects need an explicit owner whose seek state is actually journaled.</summary>
public static class ReplayEvidenceSupport
{
    private static bool HasRecordedOwner(Component effect)
    {
        var owner=effect.GetComponentInParent<RecordedWorldStateTarget>(true);
        return owner!=null&&owner.GetComponentsInChildren<MonoBehaviour>(true).Any(script=>script is IReplayStateParticipant);
    }
    public static void RequireSupportedScene(Scene scene)
    {
        foreach(var root in scene.GetRootGameObjects())
        {
            foreach(var wind in root.GetComponentsInChildren<WindZone>(true))
                if(wind.gameObject.activeInHierarchy&&(wind.windMain!=0||wind.windTurbulence!=0||wind.windPulseMagnitude!=0)&&!HasRecordedOwner(wind))
                    throw new InvalidDataException("Active native wind requires a recorded seek-state owner (IReplayStateParticipant): "+wind.name+". Choose the non-windy tree asset or baked model animation.");
            foreach(var particles in root.GetComponentsInChildren<ParticleSystem>(true))
                if(particles.gameObject.activeInHierarchy&&!HasRecordedOwner(particles))
                    throw new InvalidDataException("Particle systems require a recorded seek-state owner (IReplayStateParticipant): "+particles.name);
        }
    }
}
