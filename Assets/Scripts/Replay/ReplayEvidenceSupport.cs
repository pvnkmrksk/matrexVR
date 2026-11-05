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
        // Query the native component registry, rather than walking every bone
        // of every animated population twice on every rendered frame. This is
        // still a fresh query: newly created unsupported effects cannot evade it.
        foreach(var wind in Object.FindObjectsByType<WindZone>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(wind.gameObject.scene==scene&&wind.gameObject.activeInHierarchy&&(wind.windMain!=0||wind.windTurbulence!=0||wind.windPulseMagnitude!=0)&&!HasRecordedOwner(wind))
                throw new InvalidDataException("Active native wind requires a recorded seek-state owner (IReplayStateParticipant): "+wind.name+". Choose the non-windy tree asset or baked model animation.");
        foreach(var particles in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(particles.gameObject.scene==scene&&particles.gameObject.activeInHierarchy&&!HasRecordedOwner(particles))
                throw new InvalidDataException("Particle systems require a recorded seek-state owner (IReplayStateParticipant): "+particles.name);
    }
}
