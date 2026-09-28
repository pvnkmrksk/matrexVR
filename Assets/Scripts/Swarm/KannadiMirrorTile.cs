using System.Collections;
using UnityEngine;

// A band prefab creates children in Start. Freeze their independent movement only after spawning.
public class KannadiMirrorTile : MonoBehaviour
{
    private Transform movementSource;
    private bool animateOnMove;
    private float animationThreshold;
    private Vector2 wrapSize;
    public void ConfigureAnimation(Transform source, bool gate, float threshold, Vector2 periodicSize)
    {
        movementSource = source; animateOnMove = gate; animationThreshold = threshold; wrapSize = periodicSize;
    }
    private IEnumerator Start()
    {
        yield return null;
        foreach (LocustMover mover in GetComponentsInChildren<LocustMover>(true)) mover.enabled = false;
        foreach (DirectionalMovement mover in GetComponentsInChildren<DirectionalMovement>(true)) mover.enabled = false;
        foreach (PeriodicBoundary boundary in GetComponentsInChildren<PeriodicBoundary>(true)) boundary.enabled = false;
        foreach (BandSpawner band in GetComponentsInChildren<BandSpawner>(true)) band.enabled = false;
        AnimateOnMove.ConfigureHierarchy(gameObject, movementSource, animateOnMove, animationThreshold, wrapSize);
    }
}
