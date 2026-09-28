using System.Collections;
using UnityEngine;

// A band prefab creates children in Start. Freeze their independent movement only after spawning.
public class KannadiMirrorTile : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;
        foreach (LocustMover mover in GetComponentsInChildren<LocustMover>(true)) mover.enabled = false;
        foreach (DirectionalMovement mover in GetComponentsInChildren<DirectionalMovement>(true)) mover.enabled = false;
        foreach (PeriodicBoundary boundary in GetComponentsInChildren<PeriodicBoundary>(true)) boundary.enabled = false;
        foreach (BandSpawner band in GetComponentsInChildren<BandSpawner>(true)) band.enabled = false;
    }
}
