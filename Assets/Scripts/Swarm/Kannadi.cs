using System;
using System.Collections.Generic;
using System.Linq;
using InSceneSequence;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Kannadi (ಕನ್ನಡಿ, mirror): tracked animals replicated across a hexagonal grid.</summary>
[DefaultExecutionOrder(100)]
public class Kannadi : MonoBehaviour, IInSceneSequencer
{
    public GameObject tilePrefab;
    public int numberOfRings = 3;
    [FormerlySerializedAs("spacing")] public float hexRadius = 10f;
    public GameObject[] Clones { get; private set; } = new GameObject[0];
    public int VRIndex => ParseVRIndex(gameObject.name);
    private Vector3[] offsets = new Vector3[0];
    private Quaternion prefabRotation;
    private KannadiConfig config = new KannadiConfig();
    private int? watchIndex;
    private bool initialized;
    protected virtual float RowSpacing => hexRadius * 2f;

    protected virtual void Start()
    {
        // sceneLoaded initializes before Start; direct scene play still gets a usable grid.
        if (!initialized) InitializeScene(new Dictionary<string, object>());
    }

    public static int ParseVRIndex(string name)
    {
        for (int i = 1; i <= 4; i++)
            if (name.StartsWith("VR" + i) && (name.Length == 3 || !char.IsDigit(name[3]))) return i;
        return 0;
    }

    public void InitializeScene(Dictionary<string, object> parameters)
    {
        try
        {
            KannadiConfig next = KannadiConfig.Load(parameters);
            Kannadi[] rigs = FindObjectsOfType<Kannadi>().Where(r => r.isActiveAndEnabled && r.gameObject.scene == gameObject.scene).ToArray();
            // Validate all prefab selections before replacing any existing grid.
            var selected = rigs.Select(r => r.ResolvePrefab(next.kannadiTilePrefab)).ToArray();
            for (int i = 0; i < rigs.Length; i++) rigs[i].Apply(next, selected[i]);
            SimpleOverheadCamera.EnsureInScene();
        }
        catch (Exception error)
        {
            Debug.LogError("Cannot initialize Kannadi: " + error.Message);
        }
    }
    public void AdvanceStep(Dictionary<string, object> parameters) => InitializeScene(parameters);

    private GameObject ResolvePrefab(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            if (tilePrefab != null) return tilePrefab;
            name = "SimulatedLocust";
        }
        KannadiPrefabCatalog catalog = Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog");
        GameObject prefab = catalog != null ? catalog.Find(name) : null;
        if (prefab == null && tilePrefab != null && tilePrefab.name == name) prefab = tilePrefab;
        if (prefab == null) throw new ArgumentException("Unknown kannadiTilePrefab '" + name + "'. Add it to the KannadiPrefabCatalog.");
        return prefab;
    }

    private void Apply(KannadiConfig next, GameObject prefab)
    {
        config = next;
        tilePrefab = prefab;
        numberOfRings = next.numberOfRings ?? numberOfRings;
        hexRadius = next.hexRadius ?? next.spacing ?? hexRadius;
        VRConfig vr = next.vrConfigs?.FirstOrDefault(v => v.vrIndex == VRIndex);
        watchIndex = vr?.watchIndex;
        Vector3 position = vr?.initialPosition != null ? vr.initialPosition.ToVector3() : transform.position;
        Quaternion rotation = vr?.initialRotation != null ? Quaternion.Euler(vr.initialRotation.ToVector3()) : transform.rotation;
        ClosedLoop closedLoop = GetComponent<ClosedLoop>();
        if (closedLoop != null)
        {
            closedLoop.SetPositionAndRotation(position, rotation);
            closedLoop.SetLocustGains(vr?.closedLoopPosition ?? next.closedLoopPosition ?? 1f,
                                      vr?.closedLoopOrientation ?? next.closedLoopOrientation ?? 1f);
        }
        else transform.SetPositionAndRotation(position, rotation);
        ConfigureCameras();
        GenerateHexGrid();
        initialized = true;
        if (GetComponent<KannadiLogger>() == null) gameObject.AddComponent<KannadiLogger>();
    }

    private void ConfigureCameras()
    {
        int swarmMask = LayerMask.GetMask("SimulatedLocustsVR1", "SimulatedLocustsVR2", "SimulatedLocustsVR3", "SimulatedLocustsVR4");
        int ownLayer = LayerMask.NameToLayer("SimulatedLocustsVR" + VRIndex);
        int visible = watchIndex.HasValue ? LayerMask.GetMask("SimulatedLocustsVR" + watchIndex.Value)
                                        : swarmMask & ~(ownLayer >= 0 ? 1 << ownLayer : 0);
        int markerMask = LayerMask.GetMask("OverheadCameraMarkers");
        foreach (Camera camera in GetComponentsInChildren<Camera>(true))
        {
            // Preserve the floor and shared world, replace only the animal layers.
            camera.cullingMask = (camera.cullingMask & ~swarmMask & ~markerMask) | visible;
            if (config.backgroundColor != null)
            {
                ColorConfig c = config.backgroundColor;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(c.r, c.g, c.b, c.a);
            }
        }
    }

    public void SetVRCameraCullingMask(int vrIndex, int index)
    {
        if (index < 1 || index > 4) throw new ArgumentOutOfRangeException(nameof(index));
        foreach (Kannadi rig in FindObjectsOfType<Kannadi>())
            if (rig.VRIndex == vrIndex) { rig.watchIndex = index; rig.ConfigureCameras(); }
    }

    public void GenerateHexGrid()
    {
        CleanupClones();
        if (numberOfRings < 0 || tilePrefab == null) return;
        int layer = LayerMask.NameToLayer("SimulatedLocustsVR" + VRIndex);
        if (layer < 0) throw new InvalidOperationException("Missing animal layer for " + gameObject.name);
        bool skipCenter = watchIndex == VRIndex;
        var copies = new List<GameObject>();
        var positions = new List<Vector3>();
        prefabRotation = tilePrefab.transform.rotation;
        for (int q = -numberOfRings; q <= numberOfRings; q++)
        {
            int first = Mathf.Max(-numberOfRings, -q - numberOfRings);
            int last = Mathf.Min(numberOfRings, -q + numberOfRings);
            for (int r = first; r <= last; r++)
            {
                if (skipCenter && q == 0 && r == 0) continue;
                Vector3 offset = new Vector3(hexRadius * Mathf.Sqrt(3f) * (q + r / 2f), tilePrefab.transform.position.y, RowSpacing * r);
                GameObject clone = Instantiate(tilePrefab, Vector3.zero, prefabRotation);
                clone.name = $"VR{VRIndex}_Kannadi_{copies.Count}";
                SetLayerRecursively(clone.transform, layer);
                clone.tag = "SimulatedLocust";
                // Mirrored individuals are driven only by the tracked rig.
                foreach (LocustMover mover in clone.GetComponentsInChildren<LocustMover>(true)) mover.enabled = false;
                foreach (DirectionalMovement mover in clone.GetComponentsInChildren<DirectionalMovement>(true)) mover.enabled = false;
                foreach (PeriodicBoundary boundary in clone.GetComponentsInChildren<PeriodicBoundary>(true)) boundary.enabled = false;
                foreach (BandSpawner band in clone.GetComponentsInChildren<BandSpawner>(true))
                {
                    band.speed = 0;
                    band.lockAgentWithAnimalPosition = true;
                    band.lockBoundaryWithAnimalPosition = true;
                    band.customParentTransform = null;
                    band.gameObject.layer = layer;
                }
                if (config.animateOnMove)
                    foreach (Animator animator in clone.GetComponentsInChildren<Animator>(true))
                    {
                        AnimateOnMove animation = animator.GetComponent<AnimateOnMove>() ?? animator.gameObject.AddComponent<AnimateOnMove>();
                        animation.SetNoiseThreshold(config.animationNoiseThreshold);
                    }
                clone.AddComponent<KannadiMirrorTile>();
                copies.Add(clone); positions.Add(offset);
            }
        }
        Clones = copies.ToArray(); offsets = positions.ToArray();
        UpdateClones();
    }

    private static void SetLayerRecursively(Transform item, int layer)
    {
        item.gameObject.layer = layer;
        foreach (Transform child in item) SetLayerRecursively(child, layer);
    }

    // Modulo preserves overshoot even after a displacement larger than the whole arena.
    public static float Wrap(float value, float length) => Mathf.Repeat(value + length / 2f, length) - length / 2f;
    private Vector3 WrapPosition(Vector3 value)
    {
        if (config.periodicBoundary)
        {
            value.x = Wrap(value.x, config.boundaryLengthX);
            value.z = Wrap(value.z, config.boundaryLengthZ);
        }
        return value;
    }
    protected virtual void LateUpdate()
    {
        if (!initialized) return;
        transform.position = WrapPosition(transform.position);
        UpdateClones();
    }
    private void UpdateClones()
    {
        for (int i = 0; i < Clones.Length; i++)
        {
            if (Clones[i] == null) continue;
            Vector3 position = new Vector3(transform.position.x + offsets[i].x, offsets[i].y, transform.position.z + offsets[i].z);
            Clones[i].transform.SetPositionAndRotation(WrapPosition(position), transform.rotation * prefabRotation);
        }
    }
    private void CleanupClones()
    {
        foreach (GameObject clone in Clones)
            if (clone != null) { clone.SetActive(false); Destroy(clone); }
        Clones = new GameObject[0]; offsets = new Vector3[0];
    }
    protected virtual void OnDestroy() => CleanupClones();
}
