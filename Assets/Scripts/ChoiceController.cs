using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

public class ChoiceController : MonoBehaviour, ISceneController
{
    public GameObject[] prefabs;
    private Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>();

    public Material[] materials;
    private Dictionary<string, Material> materialDict = new Dictionary<string, Material>();
    string[] tags = new string[] { "SimulatedLocustsVR1", "SimulatedLocustsVR2" };

    private void Awake()
    {
        // Initialize prefab dictionary
        foreach (var prefab in prefabs)
        {
            prefabDict[prefab.name] = prefab;
        }

        // Initialize material dictionary
        foreach (var material in materials)
        {
            materialDict[material.name] = material;
        }
    }

    public void InitializeScene(Dictionary<string, object> parameters)
    {
        Debugger.Log("InitializeScene called.");

        // Path to scene configuration JSON
        string configFile = parameters["configFile"].ToString();

        // Load and parse JSON
        string jsonPath = Path.Combine(Application.streamingAssetsPath, configFile);
        string jsonString = File.ReadAllText(jsonPath);
        SceneConfig config = JsonConvert.DeserializeObject<SceneConfig>(jsonString);
        //instantiate objects

        foreach (var obj in config.objects)
        {
            if (prefabDict.TryGetValue(obj.type, out GameObject prefab))
            {
                for (int i = 0; i < tags.Length; i++)
                {
                    Vector3 position = CalculatePosition(
                        obj.position.radius,
                        obj.position.angle,
                        obj.position.height
                    );

                    // Check if this is a band object
                    if (obj.type.ToLower().Contains("band"))
                    {
                        // For band objects, only instantiate through InstantiateBand method
                        if (obj.flip)
                        {
                            InstantiateBand(obj, i + 1);
                        }
                    }
                    else
                    {
                        // For regular objects, instantiate normally
                        GameObject instance = Instantiate(prefab, position, Quaternion.identity);

                        // Set the tag
                        instance.tag = tags[i];

                        // **Assign the layer based on the tag**
                        string layerName = "SimulatedLocustsVR" + (i + 1); // "SimulatedLocustsVR1", "SimulatedLocustsVR2", etc.
                        int layer = LayerMask.NameToLayer(layerName);
                        if (layer == -1)
                        {
                            Debug.LogError(
                                "Layer '"
                                    + layerName
                                    + "' not found. Please add it in the Tags and Layers manager."
                            );
                        }
                        else
                        {
                            SetLayerRecursively(instance, layer);
                        }

                        // Set scale
                        if (obj.flip)
                        {
                            instance.transform.localScale = new Vector3(
                                obj.scale.x * -1,
                                obj.scale.y,
                                obj.scale.z
                            );
                        }
                        else
                        {
                            instance.transform.localScale = new Vector3(
                                obj.scale.x,
                                obj.scale.y,
                                obj.scale.z
                            );
                        }

                        // Set speed
                        if (obj.speed != 0)
                        {
                            instance.GetComponent<LocustMover>().speed = obj.speed;
                        }

                        // Set rotation
                        if (obj.mu != 0)
                        {
                            instance.transform.localRotation = Quaternion.Euler(0, obj.mu, 0);
                        }

                        // Apply material if specified
                        if (
                            !string.IsNullOrEmpty(obj.material)
                            && materialDict.TryGetValue(obj.material, out Material material)
                        )
                        {
                            instance.GetComponent<Renderer>().material = material;
                        }

                        // **Apply the visual angle setting if the ScaleWithDistance component is present**
                        ScaleWithDistance scaleScript = instance.GetComponent<ScaleWithDistance>();
                        if (scaleScript != null)
                        {
                            scaleScript.visualAngleDegrees = obj.visualAngleDegrees;
                        }
                        ColorDrift colorDrift = instance.GetComponent<ColorDrift>();
                        if (colorDrift != null)
                        {
                            // If using typed fields in `SceneObject`:
                            colorDrift.meanBlueA = obj.meanBlueA;
                            colorDrift.meanBlueB = obj.meanBlueB;
                            colorDrift.switchInterval = obj.switchInterval;
                        }
                    }
                }
            }
        }

        ClosedLoop[] closedLoopComponents = FindObjectsOfType<ClosedLoop>();
        Debugger.Log("Number of ClosedLoop scripts found: " + closedLoopComponents.Length, 4);

        // if randomInitialRotation is true, then set the rotation to a random value for y axis and the rest to initialRotation value
        Quaternion initialRandRotation = Quaternion.Euler(config.initialRotation.x, Random.Range(0, 360), config.initialRotation.z);
        // Quaternion initialRandRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        foreach (ClosedLoop cl in closedLoopComponents)
        {
            Debugger.Log(
                "Setting values for ClosedLoop script..." + config.closedLoopOrientation,
                4
            );
            cl.SetClosedLoopOrientation(config.closedLoopOrientation);
            cl.SetClosedLoopPosition(config.closedLoopPosition);

            // Set the initial position and rotation in one go, convert the rotation to a quaternion
            //if randomInitialRotation is true, then set the rotation to a random value
            Quaternion initialRotation;
            if (config.randomInitialRotation)
            {
                //random angle
                initialRotation = initialRandRotation;
                Debug.Log("Initial rotation: " + initialRotation.eulerAngles);
            }
            else
            {
                initialRotation = Quaternion.Euler(config.initialRotation);
            }
            cl.SetPositionAndRotation(config.initialPosition, initialRotation);
        }
        // Read and set the background color of cameras
        if (config.backgroundColor != null)
        {
            Color bgColor = new Color(
                config.backgroundColor.r,
                config.backgroundColor.g,
                config.backgroundColor.b,
                config.backgroundColor.a
            );
            Camera[] cameras = GameObject
                .FindGameObjectsWithTag("MainCamera")
                .Select(obj => obj.GetComponent<Camera>())
                .ToArray();

            foreach (Camera cam in cameras)
            {
                if (cam != null)
                {
                    cam.backgroundColor = bgColor;
                }
            }
        }
        // TODO: Set sky and grass textures
        // Start the coroutine from here
        StartCoroutine(DelayedOnLoaded(0.05f));

        if (!string.IsNullOrEmpty(config.skyboxPath))
        {
            SetSkybox(config.skyboxPath);
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void InstantiateRegularObject(SceneObject obj)
    {
        if (prefabDict.TryGetValue(obj.type, out GameObject prefab))
        {
            Vector3 position = CalculatePosition(obj.position.radius, obj.position.angle);
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);

            Debug.Log("Instance position: " + instance.transform.position);
            // Set scale, Optionally flip the object if flip is true, set flip my scale * -1 in x axis

            if (obj.flip)
            {
                instance.transform.localScale = new Vector3(
                    obj.scale.x * -1,
                    obj.scale.y,
                    obj.scale.z
                );
            }
            else
            {
                instance.transform.localScale = new Vector3(obj.scale.x, obj.scale.y, obj.scale.z);
            }

            if (obj.speed != 0)
            {
                instance.GetComponent<LocustMover>().speed = obj.speed;
            }

            if (obj.mu != 0)
            {
                instance.transform.localRotation = Quaternion.Euler(0, obj.mu, 0);
            }

            //todo. add individual datalogger to each instance.

            // Optionally apply material
            if (
                !string.IsNullOrEmpty(obj.material)
                && materialDict.TryGetValue(obj.material, out Material material)
            )
            {
                instance.GetComponent<Renderer>().material = material;
            }
        }
    }

    private void InstantiateBand(SceneObject obj, int vrIndex)
    {
        Debug.Log($"InstantiateBand called for VR{vrIndex} with numberOfInstances: {obj.numberOfInstances}");
        
        if (prefabDict.TryGetValue(obj.type, out GameObject bandPrefab))
        {
            Vector3 position = CalculatePosition(obj.position.radius, obj.position.angle);
            GameObject bandInstance = Instantiate(bandPrefab, position, Quaternion.identity);

            // Set a proper name for the band instance
            bandInstance.name = $"{bandPrefab.name}_{vrIndex}";
            Debug.Log($"Created band instance: {bandInstance.name}");

            // Set layer - use consistent naming with the main loop
            string layerName = $"SimulatedLocustsVR{vrIndex}";
            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex == -1)
            {
                Debug.LogError(
                    $"Layer {layerName} does not exist. Please create it in the Unity Layer settings."
                );
                return;
            }
            bandInstance.layer = layerIndex;

            BandSpawner spawner = bandInstance.GetComponent<BandSpawner>();
            if (spawner != null)
            {
                Debug.Log($"Configuring BandSpawner for VR{vrIndex}");
                
                // Disable the BandSpawner temporarily to prevent automatic spawning with default values
                spawner.enabled = false;
                
                // Configure the spawner with JSON values
                spawner.vrIndex = vrIndex;
                spawner.numberOfInstances = obj.numberOfInstances;
                spawner.spawnLengthX = obj.spawnLengthX;
                spawner.spawnLengthZ = obj.spawnLengthZ;
                spawner.gridType = obj.gridType;
                spawner.mu = obj.mu;
                spawner.kappa = obj.kappa;
                spawner.speed = obj.speed;
                spawner.visibleOffDuration = obj.visibleOffDuration;
                spawner.visibleOnDuration = obj.visibleOnDuration;
                spawner.boundaryLengthX = obj.boundaryLengthX;
                spawner.boundaryLengthZ = obj.boundaryLengthZ;
                spawner.rotationAngle = obj.rotationAngle;

                // Set the new locking properties
                spawner.lockBoundaryWithAnimalPosition = obj.lockBoundaryWithAnimalPosition;
                spawner.lockAgentWithAnimalPosition = obj.lockAgentWithAnimalPosition;
                spawner.prioritizeNumbers = obj.prioritizeNumbers;

                // Set custom parent transform if either locking is enabled
                if (obj.lockBoundaryWithAnimalPosition || obj.lockAgentWithAnimalPosition)
                {
                    GameObject vrObject = GameObject.Find($"VR{vrIndex}");
                    if (vrObject != null)
                    {
                        spawner.customParentTransform = vrObject.transform;
                    }
                    else
                    {
                        Debug.LogWarning($"VR{vrIndex} object not found in the scene.");
                    }
                }
                if (obj.gridType == SpawnGridType.Hexagonal)
                {
                    spawner.hexRadius = obj.hexRadius;
                }
                else if (obj.gridType == SpawnGridType.Manhattan)
                {
                    spawner.sectionLengthZ = obj.sectionLengthZ;
                    spawner.sectionLengthX = obj.sectionLengthX;
                }

                Debug.Log($"About to spawn {spawner.numberOfInstances} instances for VR{vrIndex}");
                
                // Manually call the spawning methods in the correct order WITHOUT re-enabling the component
                spawner.SendMessage("SetupInitialTransform", SendMessageOptions.DontRequireReceiver);
                spawner.SendMessage("GenerateSpawnPositions", SendMessageOptions.DontRequireReceiver);
                spawner.SendMessage("SpawnInstances", SendMessageOptions.DontRequireReceiver);
                
                // Mark as manually initialized to prevent Start() from running
                spawner.SendMessage("SetManuallyInitialized", true, SendMessageOptions.DontRequireReceiver);
                
                // Now re-enable the spawner for updates, but prevent Start() from running
                spawner.enabled = true;
                
                Debug.Log($"Finished spawning for VR{vrIndex}");
            }

            // Set BandLogger properties
            BandLogger logger = bandInstance.GetComponent<BandLogger>();
            if (logger != null)
            {
                logger.targetLayerMask = LayerMask.GetMask(layerName);
                logger.enabled = true; // Ensure logger is enabled
            }

            // Set scale
            bandInstance.transform.localScale = new Vector3(obj.scale.x, obj.scale.y, obj.scale.z);

            // Apply flip if needed
            if (obj.flip)
            {
                bandInstance.transform.localScale = new Vector3(
                    bandInstance.transform.localScale.x * -1,
                    bandInstance.transform.localScale.y,
                    bandInstance.transform.localScale.z
                );
            }

            // Apply rotation
            bandInstance.transform.localRotation = Quaternion.Euler(0, obj.mu, 0);

            // TODO: Add individual datalogger to the band instance if needed
        }
        else
        {
            Debug.LogError($"Band prefab '{obj.type}' is not assigned in the ChoiceController.");
        }
    }

    // Coroutine to delay the execution of OnLoaded
    private IEnumerator DelayedOnLoaded(float delay)
    {
        yield return new WaitForSeconds(delay);
        OnLoaded();
    }

    private void OnLoaded()
    {
        ClosedLoop[] closedLoopComponents = FindObjectsOfType<ClosedLoop>();
        foreach (ClosedLoop cl in closedLoopComponents)
        {
            // cl.ResetPosition();
            // cl.ResetRotation();
            cl.ResetPositionAndRotation();
        }
    }

    private Vector3 CalculatePosition(float radius, float angle, float height = 0)
    {
        //todo.add initial position for the object postions
        float x = radius * Mathf.Sin(angle * Mathf.Deg2Rad);
        float z = radius * Mathf.Cos(angle * Mathf.Deg2Rad);
        return new Vector3(x, height, z); // Assuming y is always 0
    }

    private void SetSkybox(string skyboxPath)
    {
        // If skyboxPath is empty, retain existing skybox
        if (string.IsNullOrEmpty(skyboxPath))
        {
            return;
        }

        // Construct full path in StreamingAssets
        string fullPath = Path.Combine(Application.streamingAssetsPath, skyboxPath);

        if (File.Exists(fullPath))
        {
            try
            {
                // Load the image bytes
                byte[] imageBytes = File.ReadAllBytes(fullPath);

                // Create texture with explicit settings:
                // - RGBA32 format for full color support
                // - mipmapChain: false to prevent mipmap generation which can cause seams in panoramic skyboxes
                // - linear: true for proper color space handling
                Texture2D skyboxTexture = new Texture2D(
                    width: 2,
                    height: 2,
                    textureFormat: TextureFormat.RGBA32,
                    mipChain: false,
                    linear: true
                );

                // Load the image data into the texture
                if (skyboxTexture.LoadImage(imageBytes))
                {
                    // Create a new material using the skybox shader
                    Material skyboxMaterial = new Material(Shader.Find("Skybox/Panoramic"));
                    skyboxMaterial.mainTexture = skyboxTexture;

                    // Apply the skybox material to the scene
                    RenderSettings.skybox = skyboxMaterial;
                }
                else
                {
                    Debug.LogWarning($"Failed to load skybox texture from: {fullPath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading skybox: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Skybox file not found at: {fullPath}");
        }
    }

    // Update SceneConfig and other classes as needed to reflect JSON changes
}

// Update SceneConfig and other classes as needed to reflect JSON changes

[System.Serializable]
public class SceneConfig
{
    public SceneObject[] objects;
    public bool closedLoopOrientation;
    public bool closedLoopPosition;

    public Vector3 initialPosition;
    public Vector3 initialRotation;

    public bool randomInitialRotation;
    public ColorConfig backgroundColor;

    public string skyboxPath;
}

[System.Serializable]
public class SceneObject
{
    public string type;
    public Position position;
    public string material;
    public ScaleConfig scale;
    public bool flip;

    public float speed;

    public float mu;

    // New field for visual angle
    public float visualAngleDegrees;

    // Include other properties as before
    // Optional: add these for your dynamic cylinder
    public float meanBlueA;
    public float meanBlueB;
    public float switchInterval;

    // Band properties
    public int numberOfInstances;
    public float spawnLengthX;
    public float spawnLengthZ;
    public SpawnGridType gridType;
    public float kappa;
    public float visibleOffDuration;
    public float visibleOnDuration;
    public float boundaryLengthZ;
    public float boundaryLengthX;
    public bool lockBoundaryWithAnimalPosition; // Renamed from moveWithTransform
    public bool lockAgentWithAnimalPosition; // Renamed from agentsMoveWithParent
    public bool prioritizeNumbers;
    public float hexRadius;
    public float sectionLengthZ;
    public float sectionLengthX;
    public float rotationAngle;
}

[System.Serializable]
public class Position
{
    public float radius;
    public float angle;
    public float height;
}

[System.Serializable]
public class ScaleConfig
{
    public float x;
    public float y;
    public float z;
}

[System.Serializable]
public class ColorConfig
{
    public float r;
    public float g;
    public float b;
    public float a;
}
