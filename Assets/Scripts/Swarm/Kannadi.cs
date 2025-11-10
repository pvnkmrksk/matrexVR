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
    public float spacing = 10f; // Spacing between tiles in the hexagonal grid in cm

    private GameObject[] clones; // Array to store clones
    public GameObject[] Clones { get { return clones; } } // Public accessor for cleanup
    private Vector3[] initialWorldPositions; // Array to store initial world positions
    private Quaternion[] initialLocalRotations; // Array to store initial local rotations
    private Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>(); // Dictionary for prefab lookup from JSON

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
                // Read from config JSON (inherited from SceneConfig)
                if (config.numberOfRings > 0)
                {
                    kannadi.numberOfRings = config.numberOfRings;
                    Debugger.Log($"Set numberOfRings from config: {kannadi.numberOfRings}", 3);
                }
                else
                {
                    Debugger.Log("numberOfRings not specified in config, using default.", 3);
                }

                if (config.spacing > 0)
                {
                    kannadi.spacing = config.spacing;
                    Debugger.Log($"Set spacing from config: {kannadi.spacing}", 3);
                }
                else
                {
                    Debugger.Log("spacing not specified in config, using default.", 3);
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
                
                // Regenerate grid with new parameters
                kannadi.GenerateHexGrid();
                
                // Apply boundary and animation settings to all clones
                if (kannadi.Clones != null)
                {
                    foreach (GameObject clone in kannadi.Clones)
                    {
                        if (clone != null)
                        {
                            // Apply boundary settings
                            BoundaryManager boundaryManager = clone.GetComponent<BoundaryManager>();
                            if (boundaryManager == null)
                            {
                                boundaryManager = clone.AddComponent<BoundaryManager>();
                            }
                            if (config.boundarySize > 0)
                            {
                                boundaryManager.boundarySize = config.boundarySize;
                            }
                            if (config.boundaryBuffer > 0)
                            {
                                boundaryManager.boundaryBuffer = config.boundaryBuffer;
                            }
                            
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
    }


    void Update()
    {
        UpdateClonesPositionAndRotation();
    }

    /// <summary>
    /// Generates the hexagonal grid of tiles.
    /// </summary>
    public void GenerateHexGrid()
    {
        int numberOfTiles = CalculateNumberOfTiles(numberOfRings);
        clones = new GameObject[numberOfTiles];
        initialWorldPositions = new Vector3[numberOfTiles];
        initialLocalRotations = new Quaternion[numberOfTiles];

        float hexWidth = spacing * Mathf.Sqrt(3);
        float hexHeight = spacing * 2;

        int index = 0;
        for (int q = -numberOfRings; q <= numberOfRings; q++)
        {
            int r1 = Mathf.Max(-numberOfRings, -q - numberOfRings);
            int r2 = Mathf.Min(numberOfRings, -q + numberOfRings);
            for (int r = r1; r <= r2; r++)
            {
                Vector3 hexPosition = new Vector3(hexWidth * (q + r / 2f), 0f, hexHeight * r / 2f);

                Vector3 worldPosition = transform.position + hexPosition;
                GameObject clone = Instantiate(tilePrefab, worldPosition, Quaternion.identity); // No parent transform
                
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
    /// <param name="rings">The number of rings outward from the center.</param>
    /// <returns>The total number of tiles.</returns>
    int CalculateNumberOfTiles(int rings)
    {
        int tiles = 1;
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

    /// <summary>
    /// Updates the position and rotation of the clones based on the parent's position and rotation.
    /// </summary>
    void UpdateClonesPositionAndRotation()
    {
        for (int i = 0; i < clones.Length; i++)
        {
            if (clones[i] != null)
            {
                // Maintain the initial local position relative to the parent
                clones[i].transform.position = transform.position + initialWorldPositions[i];
                // Apply the parent's rotation to each clone individually
                clones[i].transform.rotation = transform.rotation * initialLocalRotations[i];
            }
        }
    }
}
