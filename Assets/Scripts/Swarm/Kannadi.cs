using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;

/// <summary>
/// Generates a kannadi (mirror) of hexagonal grid of tiles around the center cloning all of them.
/// 
/// KANNADI (ಕನ್ನಡಿ in Kannada script, transliteration: kannadi):
/// - Meaning: "mirror" in Kannada (Dravidian language of Karnataka, India)
/// - Etymology: The instrument that lets you see your own eye i.e. Mirror
/// - Pronunciation: [kɑːnːɑːɖi] - roughly "KAHN-nah-dee" with stress on the first syllable
/// - What it does in VR: Reflects and mirrors the insects in the virtual environment, 
///   creating a vishwaroopa (manifestation of infinite forms) where the insect sees itself
///   and its environment duplicated in a hexagonal grid pattern.
/// 
/// The name is chosen because this class creates a mirrored/reflected environment where
/// the insect sees multiple copies of itself and the scene arranged in hexagonal symmetry,
/// much like looking into a mirror or seeing infinite reflections.
/// </summary>
public class Kannadi : MonoBehaviour, ISceneController
{
    public GameObject tilePrefab; // Prefab for the tile. Locust prefab (fallback if not in JSON)
    public int numberOfRings = 3; // Number of rings outward from the center
    public float hexRadius = 10f; // hexRadius between tiles in the hexagonal grid in cm

    private GameObject[] clones; // Array to store clones
    public GameObject[] Clones { get { return clones; } } // Public accessor for cleanup
    private Vector3[] initialWorldPositions; // Array to store initial world positions
    private Quaternion[] initialLocalRotations; // Array to store initial local rotations
    private Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>(); // Dictionary for prefab lookup from JSON
    private int vrIndex = 0; // VR index extracted from GameObject name (1-4)
    private int? watchIndex = null; // Watch index from VRConfig (1-4, null if not set)

    void Start()
    {
        // Don't generate grid here - wait for InitializeScene() to be called with proper config
        // This prevents duplicate swarms (one from Start, one from InitializeScene)
    }
    
    public void InitializeScene(Dictionary<string, object> parameters)
    {
        if (parameters == null)
        {
            Debugger.Log("Parameters is null.", 1);
            return;
        }


        foreach (var entry in parameters)
        {
            Debugger.Log(
                $"Parames Key: {entry.Key}, Value: {entry.Value}, Type: {entry.Value?.GetType()}"
            );
        }

        // Load config file if provided
        SceneConfig config = null;
        if (parameters.ContainsKey("configFile"))
        {
            string configFile = parameters["configFile"].ToString();
            string jsonPath = Path.Combine(Application.streamingAssetsPath, configFile);
            
            if (File.Exists(jsonPath))
            {
                string jsonString = File.ReadAllText(jsonPath);
                config = JsonConvert.DeserializeObject<SceneConfig>(jsonString);
                Debugger.Log($"Loaded config from {configFile}", 3);
                Debugger.Log($"Config has {config?.vrConfigs?.Length ?? 0} VR configs", 3);
                
                // Apply per-VR configurations
                if (config.vrConfigs != null && config.vrConfigs.Length > 0)
                {
                    Debugger.Log($"Applying VR configurations now...", 3);
                    ApplyVRConfigurations(config);
                }
                else
                {
                    Debugger.Log("No VR configs in the loaded config file", 2);
                }
            }
            else
            {
                Debugger.Log($"Config file not found: {jsonPath}", 2);
            }
        }

        // Apply swarm configuration from JSON if available
        if (config != null)
        {
            // Get prefab dictionary from ChoiceController if available
            ChoiceController choiceController = FindObjectOfType<ChoiceController>();
            if (choiceController != null)
            {
                // Use reflection or public method to get prefabDict - for now, we'll use a different approach
                // We'll get prefabs from the scene's ChoiceController prefabs array
                var prefabsField = typeof(ChoiceController).GetField("prefabs", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prefabsField != null)
                {
                    GameObject[] prefabs = (GameObject[])prefabsField.GetValue(choiceController);
                    foreach (var prefab in prefabs)
                    {
                        if (prefab != null)
                        {
                            prefabDict[prefab.name] = prefab;
                        }
                    }
                }
            }

            Kannadi[] Kannadis = FindObjectsOfType<Kannadi>();

            if (Kannadis == null || Kannadis.Length == 0)
            {
                Debugger.Log("No Kannadis found.", 2);
                return;
            }

            foreach (Kannadi kannadi in Kannadis)
            {
                // Extract vrIndex from GameObject name (e.g., "VR1 Kannadi" -> 1)
                string objName = kannadi.gameObject.name;
                kannadi.vrIndex = 0;
                if (objName.StartsWith("VR"))
                {
                    string indexStr = objName.Substring(2).Split(' ')[0];
                    if (int.TryParse(indexStr, out int parsedIndex))
                    {
                        kannadi.vrIndex = parsedIndex;
                    }
                }
                
                // Find matching VRConfig and store watchIndex
                kannadi.watchIndex = null;
                if (config.vrConfigs != null)
                {
                    foreach (var vrConfig in config.vrConfigs)
                    {
                        if (vrConfig.vrIndex == kannadi.vrIndex && vrConfig.watchIndex.HasValue)
                        {
                            kannadi.watchIndex = vrConfig.watchIndex.Value;
                            break;
                        }
                    }
                }
                
                // Read from config JSON (inherited from SceneConfig)
                // Apply value if explicitly set in config (allows 0 and negative values)
                if (config.numberOfRings.HasValue)
                {
                    kannadi.numberOfRings = config.numberOfRings.Value;
                    Debugger.Log($"Set numberOfRings from config: {kannadi.numberOfRings}", 3);
                }
                else
                {
                    Debugger.Log("numberOfRings not specified in config, using default.", 3);
                }

                if (config.hexRadius > 0)
                {
                    kannadi.hexRadius = config.hexRadius;
                    Debugger.Log($"Set hexRadius from config: {kannadi.hexRadius}", 3);
                }
                else
                {
                    Debugger.Log("hexRadius not specified in config, using default.", 3);
                }

                // Set tile prefab from config (default to SimulatedLocust if unstated)
                string prefabName = !string.IsNullOrEmpty(config.kannadiTilePrefab) 
                    ? config.kannadiTilePrefab 
                    : "SimulatedLocust";
                
                if (prefabDict.TryGetValue(prefabName, out GameObject prefab))
                {
                    kannadi.tilePrefab = prefab;
                    Debugger.Log($"Set kannadi tile prefab from config: {prefabName}", 3);
                }
                else if (kannadi.tilePrefab != null)
                {
                    Debugger.Log($"Prefab '{prefabName}' not found, using default tilePrefab.", 2);
                }
                else
                {
                    Debugger.Log($"Prefab '{prefabName}' not found and no default tilePrefab set.", 1);
                }

                // Clean up old clones if they exist
                if (kannadi.Clones != null)
                {
                    foreach (GameObject clone in kannadi.Clones)
                    {
                        if (clone != null)
                        {
                            Destroy(clone);
                        }
                    }
                }
                
                // Set boundary size from config (simple, no components needed)
                float boundarySize = config.boundaryLengthX > 0 ? config.boundaryLengthX : 200f;
                if (config.boundaryLengthZ > 0 && config.boundaryLengthZ != config.boundaryLengthX)
                {
                    boundarySize = Mathf.Max(config.boundaryLengthX, config.boundaryLengthZ);
                }
                kannadi.globalBoundarySize = boundarySize;
                kannadi.globalBoundaryBuffer = 0.1f;
                
                // Regenerate grid with new parameters
                kannadi.GenerateHexGrid();
                
                // Add KannadiLogger to track clone positions and orientations
                KannadiLogger logger = kannadi.GetComponent<KannadiLogger>();
                if (logger == null)
                {
                    logger = kannadi.gameObject.AddComponent<KannadiLogger>();
                }
                
                // Apply animation settings to all clones
                if (kannadi.Clones != null)
                {
                    foreach (GameObject clone in kannadi.Clones)
                    {
                        if (clone != null)
                        {
                            // Apply animate-on-move settings
                            if (config.animateOnMove)
                            {
                                AnimateOnMove animateOnMove = clone.GetComponent<AnimateOnMove>();
                                if (animateOnMove == null)
                                {
                                    animateOnMove = clone.AddComponent<AnimateOnMove>();
                                }
                                animateOnMove.SetNoiseThreshold(config.animationNoiseThreshold > 0 
                                    ? config.animationNoiseThreshold 
                                    : 0.1f);
                                animateOnMove.SetEnabled(true);
                            }
                            else
                            {
                                // Remove animate-on-move if disabled
                                AnimateOnMove animateOnMove = clone.GetComponent<AnimateOnMove>();
                                if (animateOnMove != null)
                                {
                                    Destroy(animateOnMove);
                                }
                                // Ensure animator runs normally
                                Animator animator = clone.GetComponent<Animator>();
                                if (animator != null)
                                {
                                    animator.speed = 1f;
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            Debugger.Log("No config loaded, using default swarm parameters.", 2);
        }
    }

    private void ApplyVRConfigurations(SceneConfig config)
    {
        if (config.vrConfigs == null || config.vrConfigs.Length == 0)
        {
            Debugger.Log("No VR-specific configurations provided. Using defaults.", 3);
            return;
        }

        Debugger.Log($"Found {config.vrConfigs.Length} VR configs to apply", 3);
        foreach (var vrConfig in config.vrConfigs)
        {
            Debugger.Log($"Applying config for VR{vrConfig.vrIndex}", 3);
            ApplyVRConfiguration(vrConfig);
        }
    }

    private void ApplyVRConfiguration(VRConfig vrConfig)
    {
        // Try to find VR object with multiple possible names
        GameObject vrObject = GameObject.Find($"VR{vrConfig.vrIndex}");
        if (vrObject == null)
        {
            vrObject = GameObject.Find($"VR{vrConfig.vrIndex} Kannadi");
        }
        if (vrObject == null)
        {
            Debugger.Log($"VR{vrConfig.vrIndex} object not found in the scene.", 2);
            return;
        }

        // Apply initial rotation if provided
        if (vrConfig.initialRotation != null)
        {
            Quaternion rotation = Quaternion.Euler(
                vrConfig.initialRotation.x,
                vrConfig.initialRotation.y,
                vrConfig.initialRotation.z
            );
            vrObject.transform.rotation = rotation;
            Debugger.Log($"Set VR{vrConfig.vrIndex} rotation to {rotation.eulerAngles}", 3);
        }

        // Apply initial position if provided
        if (vrConfig.initialPosition != null)
        {
            Vector3 position = new Vector3(
                vrConfig.initialPosition.x,
                vrConfig.initialPosition.y,
                vrConfig.initialPosition.z
            );
            vrObject.transform.position = position;
            Debugger.Log($"Set VR{vrConfig.vrIndex} position to {position}", 3);
        }

        // Sync ClosedLoop base pose to the new transform values
        ClosedLoop closedLoop = vrObject.GetComponent<ClosedLoop>();
        if (closedLoop != null)
        {
            Debugger.Log($"Setting base pose for VR{vrConfig.vrIndex}: pos={vrObject.transform.position}, rot={vrObject.transform.rotation.eulerAngles}", 3);
            closedLoop.SetBasePose(vrObject.transform.position, vrObject.transform.rotation);
        }

        // Apply watchIndex camera culling mask if provided
        if (vrConfig.watchIndex.HasValue)
        {
            SetVRCameraCullingMask(vrConfig.vrIndex, vrConfig.watchIndex.Value);
        }
    }


    void Update()
    {
        // Wrap parent position first (if parent moves, clones follow)
        WrapParentPosition();
        
        // Update clones (they will also be wrapped)
        UpdateClonesPositionAndRotation();
    }

    /// <summary>
    /// Generates the hexagonal grid of tiles.
    /// </summary>
    public void GenerateHexGrid()
    {
        // If rings is negative, skip instantiating any clones
        if (numberOfRings < 0)
        {
            clones = new GameObject[0];
            initialWorldPositions = new Vector3[0];
            initialLocalRotations = new Quaternion[0];
            Debug.Log($"numberOfRings is negative ({numberOfRings}), skipping clone instantiation.");
            return;
        }
        
        // Handle numberOfRings = 0 (just center tile)
        int rings = numberOfRings;
        
        // Check if we should skip the center tile (when watchIndex == vrIndex)
        bool skipCenterTile = (watchIndex.HasValue && watchIndex.Value == vrIndex && vrIndex > 0);
        
        int numberOfTiles = CalculateNumberOfTiles(rings);
        // Reduce count by 1 if we're skipping the center tile
        if (skipCenterTile && numberOfTiles > 0)
        {
            numberOfTiles--;
        }
        clones = new GameObject[numberOfTiles];
        initialWorldPositions = new Vector3[numberOfTiles];
        initialLocalRotations = new Quaternion[numberOfTiles];

        // Proper hexagonal close packing coordinates
        // Horizontal hexRadius: hexRadius * sqrt(3)
        // Vertical hexRadius: hexRadius * 1.5 (for close packing)
        float hexWidth = hexRadius * Mathf.Sqrt(3f);
        float hexHeight = hexRadius * 2f;

        int index = 0;
        
        // If rings = 0, just create center tile (unless we're skipping it)
        if (rings == 0)
        {
            if (skipCenterTile)
            {
                // Skip center tile when watchIndex == vrIndex
                Debug.Log($"Skipping center tile for VR{vrIndex} (watchIndex == vrIndex)");
                return;
            }
            
            // Instantiate at origin first
            GameObject clone = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity);
            
            // Try to get prefab's stored Y offset
            // At runtime, Instantiate places objects at (0,0,0), so we need to check the prefab's stored position
            // For SimulatedLocust, the stored Y is -0.25, but we'll read it from the instantiated object's initial state
            // If the prefab has a stored offset, it might be in localPosition after instantiation
            float prefabY = clone.transform.localPosition.y;
            
            // Debug: Log what we're getting
            Debug.Log($"Clone instantiated, localPosition.y = {prefabY}, position.y = {clone.transform.position.y}, parent Y = {transform.position.y}");
            
            // If we got 0 (which means prefab's stored position wasn't applied), 
            // use the known offset for SimulatedLocust (-0.25)
            if (Mathf.Approximately(prefabY, 0f))
            {
                prefabY = -0.25f; // Default offset for SimulatedLocust prefab
                Debug.Log($"Using default prefab Y offset: {prefabY}");
            }
            
            // Preserve prefab's Y offset: use the prefab's Y offset directly, not relative to parent's Y
            // This ensures the prefab's stored Y position (-0.25) is preserved
            Vector3 worldPosition = new Vector3(transform.position.x, prefabY, transform.position.z);
            clone.transform.position = worldPosition;
            
            // Disable movement components
            LocustMover mover = clone.GetComponent<LocustMover>();
            if (mover != null) mover.enabled = false;
            
            var allComponents = clone.GetComponents<MonoBehaviour>();
            foreach (var comp in allComponents)
            {
                if (comp != null && (comp.GetType().Name.Contains("Mover") || comp.GetType().Name.Contains("Movement")))
                {
                    comp.enabled = false;
                }
            }
            
            SetLayerAndTagRecursively(clone, gameObject.layer, gameObject.tag);
            // Store relative position (X and Z relative to parent, Y is prefab's Y)
            Vector3 hexPosition = new Vector3(0f, prefabY, 0f);
            initialWorldPositions[0] = hexPosition;
            initialLocalRotations[0] = clone.transform.rotation;
            clones[0] = clone;
            return;
        }
        
        // Generate hexagonal grid for rings > 0 (proper close packing)
        for (int q = -rings; q <= rings; q++)
        {
            int r1 = Mathf.Max(-rings, -q - rings);
            int r2 = Mathf.Min(rings, -q + rings);
            for (int r = r1; r <= r2; r++)
            {
                // Skip center tile (q=0, r=0) if watchIndex == vrIndex
                if (skipCenterTile && q == 0 && r == 0)
                {
                    continue;
                }
                
                // Instantiate at origin first
                GameObject clone = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity);
                
                // Try to get prefab's stored Y offset
                // At runtime, Instantiate places objects at (0,0,0), so we need to check the prefab's stored position
                float prefabY = clone.transform.localPosition.y;
                
                // If we got 0 (which means prefab's stored position wasn't applied), 
                // use the known offset for SimulatedLocust (-0.25)
                if (Mathf.Approximately(prefabY, 0f))
                {
                    prefabY = -0.25f; // Default offset for SimulatedLocust prefab
                }
                
                // Proper hexagonal close packing: x = hexRadius*sqrt(3)*(q + r/2), z = hexRadius*1.5*r
                // Preserve prefab's Y position (e.g., -0.25) instead of hardcoding 0f
                float hexX = hexWidth * (q + r / 2f);
                float hexZ = hexHeight * r;
                Vector3 worldPosition = new Vector3(
                    transform.position.x + hexX,
                    prefabY, // Use prefab's Y offset directly (e.g., -0.25), not relative to parent
                    transform.position.z + hexZ
                );
                
                // Set position preserving the prefab's Y
                clone.transform.position = worldPosition;
                
                // Disable movement components to prevent clones from walking away
                LocustMover mover = clone.GetComponent<LocustMover>();
                if (mover != null)
                {
                    mover.enabled = false;
                }
                
                // Disable any other movement components
                var allComponents = clone.GetComponents<MonoBehaviour>();
                foreach (var comp in allComponents)
                {
                    if (comp != null && (comp.GetType().Name.Contains("Mover") || comp.GetType().Name.Contains("Movement")))
                    {
                        comp.enabled = false;
                    }
                }
                
                SetLayerAndTagRecursively(clone, gameObject.layer, gameObject.tag); // Set layer and tag recursively
                // Store relative position (X and Z relative to parent, Y is prefab's Y)
                Vector3 hexPosition = new Vector3(hexX, prefabY, hexZ);
                initialWorldPositions[index] = hexPosition; // Store local position relative to the parent
                initialLocalRotations[index] = clone.transform.rotation; // Store initial rotation
                clones[index] = clone;
                index++;
            }
        }
    }

    /// <summary>
    /// Calculates the total number of tiles in the hexagonal grid.
    /// </summary>
    /// <param name="rings">The number of rings outward from the center. 0 = just center tile (1 tile), negative = 0 tiles.</param>
    /// <returns>The total number of tiles.</returns>
    int CalculateNumberOfTiles(int rings)
    {
        if (rings < 0)
            return 0; // Negative rings means no tiles
        if (rings == 0)
            return 1; // Just the center tile
        
        int tiles = 1; // Center tile
        for (int i = 1; i <= rings; i++)
        {
            tiles += 6 * i;
        }
        return tiles;
    }

    /// <summary>
    /// Sets the layer and tag of the specified GameObject and its children recursively.
    /// </summary>
    /// <param name="obj">The GameObject to set the layer and tag for.</param>
    /// <param name="layer">The layer to set.</param>
    /// <param name="tag">The tag to set.</param>
    void SetLayerAndTagRecursively(GameObject obj, int layer, string tag)
    {
        obj.layer = layer;
        obj.tag = tag;
        foreach (Transform child in obj.transform)
        {
            SetLayerAndTagRecursively(child.gameObject, layer, tag);
        }
    }

    private float globalBoundarySize = 200f;
    private float globalBoundaryBuffer = 0.1f;
    
    /// <summary>
    /// Simple boundary wrap function (inline, no components needed).
    /// </summary>
    void WrapPosition(ref Vector3 pos, Vector3 center, float halfSize, float buffer)
    {
        // Use >= and <= for immediate wrapping (no sticky edges)
        if (pos.x >= center.x + halfSize)
            pos.x = center.x - halfSize + buffer;
        else if (pos.x <= center.x - halfSize)
            pos.x = center.x + halfSize - buffer;
        
        if (pos.z >= center.z + halfSize)
            pos.z = center.z - halfSize + buffer;
        else if (pos.z <= center.z - halfSize)
            pos.z = center.z + halfSize - buffer;
    }
    
    /// <summary>
    /// Updates the position and rotation of the clones based on the parent's position and rotation.
    /// Uses simple inline boundary wrapping (same logic as LocustMover, but simplified).
    /// </summary>
    void UpdateClonesPositionAndRotation()
    {
        // Skip if no clones exist (e.g., when rings is negative)
        if (clones == null || clones.Length == 0)
            return;
        
        Vector3 center = Vector3.zero;
        float halfSize = globalBoundarySize / 2f;
        
        for (int i = 0; i < clones.Length; i++)
        {
            if (clones[i] != null)
            {
                // Calculate new position: X and Z relative to parent, Y is absolute (prefab's stored Y)
                Vector3 newPosition = new Vector3(
                    transform.position.x + initialWorldPositions[i].x,
                    initialWorldPositions[i].y,  // Use prefab's absolute Y (-0.25), not relative to parent
                    transform.position.z + initialWorldPositions[i].z
                );
                
                // Wrap immediately (no sticky edges, uses >= and <=)
                WrapPosition(ref newPosition, center, halfSize, globalBoundaryBuffer);
                
                // Set position (already wrapped, no glitches, relative positions preserved)
                clones[i].transform.position = newPosition;
                
                // Always update rotation
                clones[i].transform.rotation = transform.rotation * initialLocalRotations[i];
            }
        }
    }
    
    /// <summary>
    /// Wraps the parent (VR object) position.
    /// </summary>
    void WrapParentPosition()
    {
        Vector3 pos = transform.position;
        Vector3 center = Vector3.zero;
        float halfSize = globalBoundarySize / 2f;
        
        WrapPosition(ref pos, center, halfSize, globalBoundaryBuffer);
        transform.position = pos;
    }

    /// <summary>
    /// Sets the VR camera culling mask to only render the specified watchIndex layer.
    /// This allows a VR headset to render objects from a different VR's layer.
    /// For example, if vrIndex=1 and watchIndex=2, VR1 will render objects in layer SimulatedLocustsVR2.
    /// </summary>
    /// <param name="vrIndex">The VR index (1-4) whose cameras to configure</param>
    /// <param name="watchIndex">The layer index (1-4) to render. Objects in SimulatedLocustsVR{watchIndex} will be visible.</param>
    public void SetVRCameraCullingMask(int vrIndex, int watchIndex)
    {
        // Validate watchIndex range
        // if (watchIndex < 1 || watchIndex > 4)
        // {
        //     Debugger.Log($"WatchIndex {watchIndex} is out of range. Must be 1-4.", 1);
        //     return;
        // }

        // Find the VR object with multiple possible names
        GameObject vrObject = GameObject.Find($"VR{vrIndex} Kannadi");
        
        // if (vrObject == null)
        // {
        //     vrObject = GameObject.Find($"VR{vrIndex} Kannadi");
        // }
        // if (vrObject == null)
        // {
        //     Debugger.Log($"VR{vrIndex} object not found in the scene.", 2);
        //     return;
        // }

        // Get the layer name for the watchIndex
        string watchLayerName = $"SimulatedLocustsVR{watchIndex}";
        int watchLayerIndex = LayerMask.NameToLayer(watchLayerName);
        
        if (watchLayerIndex == -1)
        {
            Debugger.Log($"Layer {watchLayerName} does not exist. Please create it in the Unity Layer settings.", 1);
            return;
        }

        // Create a LayerMask that only includes the watchIndex layer
        LayerMask watchLayerMask = 1 << watchLayerIndex;

        // Find all cameras in the VR object and its children
        Camera[] cameras = vrObject.GetComponentsInChildren<Camera>(true);
        
        // if (cameras.Length == 0)
        // {
        //     Debugger.Log($"No cameras found in VR{vrIndex} object.", 2);
        //     return;
        // }

        // Set culling mask for all cameras to only render the watchIndex layer
        foreach (Camera cam in cameras)
        {
            cam.cullingMask = watchLayerMask;
            // Debugger.Log($"Set VR{vrIndex} camera '{cam.name}' culling mask to layer {watchLayerName} (index {watchLayerIndex})", 3);
        }

        // Debugger.Log($"Configured VR{vrIndex} to render layer {watchLayerName} (watchIndex={watchIndex})", 3);
    }
}
