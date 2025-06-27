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

    private int numberOfVR = 2;

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


        for (int i = 0; i < numberOfVR; i++)
        {
            foreach (var obj in config.objects)
            {
                if (prefabDict.TryGetValue(obj.type, out GameObject prefab))
                {
                    if (obj.type.ToLower().Contains("band"))
                    {
                        InstantiateBand(obj, i + 1);
                    }
                    else
                    {
                        InstantiateRegularObject(obj);
                    }
                }
                else
                {
                    Debug.LogError($"Prefab '{obj.type}' not found in prefabs list.");
                }
            }
        }

        ClosedLoop[] closedLoopComponents = FindObjectsOfType<ClosedLoop>();
        Debugger.Log("Number of ClosedLoop scripts found: " + closedLoopComponents.Length, 4);

        foreach (ClosedLoop cl in closedLoopComponents)
        {
            Debugger.Log(
                "Setting values for ClosedLoop script..." + config.closedLoopOrientation,
                4
            );
            cl.SetClosedLoopOrientation(config.closedLoopOrientation);
            cl.SetClosedLoopPosition(config.closedLoopPosition);
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
            Debug.Log($"Skybox path found in config: {config.skyboxPath}");
            SetSkybox(config.skyboxPath);
        }
        else
        {
            Debug.Log("No skyboxPath found in config or skyboxPath is empty");
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
                instance.transform.localScale = new Vector3(
                    obj.scale.x,
                    obj.scale.y,
                    obj.scale.z
                );
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
                Debug.LogError($"Layer {layerName} does not exist. Please create it in the Unity Layer settings.");
                return;
            }
            bandInstance.layer = layerIndex;

            BandSpawner spawner = bandInstance.GetComponent<BandSpawner>();
            if (spawner != null)
            {
                spawner.vrIndex = vrIndex; // Set the VR index
                // Set BandSpawner properties
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

                // Set custom parent transform
                spawner.moveWithCustomTransform = obj.moveWithTransform;
                spawner.prioritizeNumbers = obj.prioritizeNumbers;
                if (obj.moveWithTransform)
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

    private Vector3 CalculatePosition(float radius, float angle)
    {
        //todo.add initial position for the object postions
        float x = radius * Mathf.Sin(angle * Mathf.Deg2Rad);
        float z = radius * Mathf.Cos(angle * Mathf.Deg2Rad);
        return new Vector3(x, 0, z); // Assuming y is always 0
    }

    private void SetSkybox(string skyboxPath)
    {
        Debug.Log($"SetSkybox called with path: {skyboxPath}");

        // If skyboxPath is empty, retain existing skybox
        if (string.IsNullOrEmpty(skyboxPath))
        {
            Debug.Log("SkyboxPath is empty, returning");
            return;
        }

        // Construct full path in StreamingAssets
        string fullPath = Path.Combine(Application.streamingAssetsPath, skyboxPath);
        Debug.Log($"Full skybox path: {fullPath}");

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
                    Debug.Log("Successfully loaded skybox texture");

                    // Create a new material using the skybox shader
                    Shader skyboxShader = Shader.Find("Skybox/Panoramic");
                    if (skyboxShader == null)
                    {
                        Debug.LogError("Could not find Skybox/Panoramic shader!");
                        return;
                    }

                    Material skyboxMaterial = new Material(skyboxShader);
                    skyboxMaterial.mainTexture = skyboxTexture;
                    Debug.Log($"Created skybox material with texture size: {skyboxTexture.width}x{skyboxTexture.height}");

                    // Apply the skybox material to the scene
                    RenderSettings.skybox = skyboxMaterial;
                    Debug.Log("Skybox material applied to RenderSettings");

                    // Set all cameras to use Skybox clear flags
                    SetCameraClearFlagsToSkybox();
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

    private void SetCameraClearFlagsToSkybox()
    {
        // Find all cameras in the scene, including those with MainCamera tag
        Camera[] allCameras = FindObjectsOfType<Camera>();

        foreach (Camera cam in allCameras)
        {
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                Debug.Log($"Set camera '{cam.name}' clear flags to Skybox");
            }
        }

        Debug.Log($"Updated {allCameras.Length} cameras to use Skybox clear flags");
    }

    // Update SceneConfig and other classes as needed to reflect JSON changes
}

[System.Serializable]
public class SceneConfig
{
    public SceneObject[] objects;
    public bool closedLoopOrientation;
    public bool closedLoopPosition;
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
    // New properties for bands
    public int numberOfInstances;
    public float spawnLengthX;
    public float spawnLengthZ;
    public SpawnGridType gridType;
    public float kappa;
    public float visibleOffDuration;
    public float visibleOnDuration;
    public float boundaryLengthZ;
    public float boundaryLengthX;
    public bool moveWithTransform;
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
