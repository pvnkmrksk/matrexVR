using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using InSceneSequence;

public class ChoiceController : MonoBehaviour, IInSceneSequencer
{
    public GameObject[] prefabs;
    private Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>();

    public Material[] materials;
    private Dictionary<string, Material> materialDict = new Dictionary<string, Material>();
    string[] tags = new string[] { "ChoiceVR1", "ChoiceVR2", "ChoiceVR3", "ChoiceVR4" };
    private Transform spawnedObjectsRoot;
    private Material defaultSkyboxMaterial;
    private Material runtimeSkyboxMaterial;
    private Texture2D runtimeSkyboxTexture;

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

        defaultSkyboxMaterial = RenderSettings.skybox;
        spawnedObjectsRoot = new GameObject("ChoiceSpawnedObjects").transform;
        spawnedObjectsRoot.position = Vector3.zero;
        spawnedObjectsRoot.rotation = Quaternion.identity;
        spawnedObjectsRoot.localScale = Vector3.one;
    }

    public void InitializeScene(Dictionary<string, object> parameters)
    {
        ApplySceneParameters(parameters);
    }

    public void AdvanceStep(Dictionary<string, object> parameters)
    {
        ApplySceneParameters(parameters);
    }

    private void ApplySceneParameters(Dictionary<string, object> parameters)
    {
        Debugger.Log("InitializeScene called.");

        if (parameters == null || !parameters.ContainsKey("configFile"))
        {
            Debug.LogError("ChoiceController requires a configFile parameter.");
            return;
        }

        SceneConfig config = LoadSceneConfig(parameters["configFile"].ToString());
        if (config == null)
        {
            return;
        }

        CleanupSpawnedObjects();
        ApplyConfig(config);
    }

    private SceneConfig LoadSceneConfig(string configFile)
    {
        string jsonPath = Path.Combine(Application.streamingAssetsPath, configFile);

        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"Config file not found at: {jsonPath}");
            return null;
        }

        string jsonString = File.ReadAllText(jsonPath);
        return JsonConvert.DeserializeObject<SceneConfig>(jsonString);
    }

    private void ApplyConfig(SceneConfig config)
    {
        if (config == null || config.objects == null)
        {
            Debug.LogWarning("Choice scene config is empty.");
            return;
        }

        foreach (var obj in config.objects)
        {
            if (string.IsNullOrEmpty(obj.type))
            {
                continue; // Skip objects with no type specified
            }

            bool isBandObject = obj.type.ToLower().Contains("band");

            if (isBandObject)
            {
                for (int i = 0; i < tags.Length; i++)
                {
                    InstantiateBand(obj, i + 1);
                }
            }
            else if (prefabDict.TryGetValue(obj.type, out GameObject prefab))
            {
                GameObject instance = Instantiate(prefab, spawnedObjectsRoot);
                ApplyResolvedTransform(instance.transform, obj);
                ConfigureRegularObjectInstance(instance, obj);
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
        else
        {
            ClearRuntimeSkybox();
        }
    }

    private void CleanupSpawnedObjects()
    {
        if (spawnedObjectsRoot == null)
        {
            return;
        }

        for (int i = spawnedObjectsRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(spawnedObjectsRoot.GetChild(i).gameObject);
        }

        ClearRuntimeSkybox();
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void ConfigureRegularObjectInstance(GameObject instance, SceneObject obj)
    {
        Debug.Log("Instance position: " + instance.transform.position);

        if (obj.speed != 0)
        {
            instance.GetComponent<LocustMover>().speed = obj.speed;
        }

        if (
            !string.IsNullOrEmpty(obj.material)
            && materialDict.TryGetValue(obj.material, out Material material)
        )
        {
            instance.GetComponent<Renderer>().material = material;
        }

        ScaleWithDistance scaleScript = instance.GetComponent<ScaleWithDistance>();
        if (scaleScript != null)
        {
            scaleScript.visualAngleDegrees = obj.visualAngleDegrees;
        }

        ColorDrift colorDrift = instance.GetComponent<ColorDrift>();
        if (colorDrift != null)
        {
            colorDrift.meanBlueA = obj.meanBlueA;
            colorDrift.meanBlueB = obj.meanBlueB;
            colorDrift.switchInterval = obj.switchInterval;
        }
    }

    private void InstantiateBand(SceneObject obj, int vrIndex)
    {
        if (prefabDict.TryGetValue(obj.type, out GameObject bandPrefab))
        {
            GameObject bandInstance = Instantiate(bandPrefab, spawnedObjectsRoot);
            ApplyResolvedTransform(bandInstance.transform, obj);

            // Set a proper name for the band instance
            bandInstance.name = $"{bandPrefab.name}_{vrIndex}";

            // Set layer
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
            }

            // Set BandLogger properties
            BandLogger logger = bandInstance.GetComponent<BandLogger>();
            if (logger != null)
            {
                logger.targetLayerMask = LayerMask.GetMask(layerName);
                logger.enabled = true; // Ensure logger is enabled
            }

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
        // Use double-precision trig so mirrored angles stay symmetric when cast back to float.
        double angleRadians = angle * Mathf.Deg2Rad;
        float x = (float)(radius * System.Math.Sin(angleRadians));
        float z = (float)(radius * System.Math.Cos(angleRadians));
        return new Vector3(x, height, z); // Assuming y is always 0
    }

    private Vector3 ResolvePosition(Position position)
    {
        if (position == null)
        {
            return Vector3.zero;
        }

        if (position.HasExplicitCoordinates())
        {
            return new Vector3(position.x.Value, position.y.Value, position.z.Value);
        }

        return CalculatePosition(position.radius, position.angle, position.height);
    }

    private Vector3 ResolveScale(SceneObject obj)
    {
        if (obj?.scale == null)
        {
            return Vector3.one;
        }

        float x = obj.flip ? -obj.scale.x : obj.scale.x;
        return new Vector3(x, obj.scale.y, obj.scale.z);
    }

    private Quaternion ResolveRotation(SceneObject obj)
    {
        if (obj?.rotation != null && obj.rotation.HasExplicitRotation())
        {
            return Quaternion.Euler(obj.rotation.x.Value, obj.rotation.y.Value, obj.rotation.z.Value);
        }

        if (obj != null && obj.mu != 0)
        {
            return Quaternion.Euler(0, obj.mu, 0);
        }

        return Quaternion.identity;
    }

    private void ApplyResolvedTransform(Transform target, SceneObject obj)
    {
        if (target == null)
        {
            return;
        }

        // Apply the final transform values directly so the spawned object's own Transform matches JSON.
        target.position = ResolvePosition(obj.position);
        target.rotation = ResolveRotation(obj);
        target.localScale = ResolveScale(obj);
    }

    private void SetSkybox(string skyboxPath)
    {
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

                // Create texture with explicit settings for panoramic skyboxes
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
                    ClearRuntimeSkybox();

                    // Create a new material using the skybox shader
                    Shader skyboxShader = Shader.Find("Skybox/Panoramic");
                    if (skyboxShader == null)
                    {
                        Debug.LogError("Could not find Skybox/Panoramic shader!");
                        return;
                    }

                    Material skyboxMaterial = new Material(skyboxShader);
                    skyboxMaterial.mainTexture = skyboxTexture;
                    runtimeSkyboxMaterial = skyboxMaterial;
                    runtimeSkyboxTexture = skyboxTexture;

                    // Apply the skybox material to the scene
                    RenderSettings.skybox = skyboxMaterial;

                    // Force refresh the skybox
                    DynamicGI.UpdateEnvironment();

                    Debug.Log($"Skybox loaded successfully: {skyboxPath}");
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

    private void ClearRuntimeSkybox()
    {
        if (RenderSettings.skybox == runtimeSkyboxMaterial)
        {
            RenderSettings.skybox = defaultSkyboxMaterial;
        }

        if (runtimeSkyboxMaterial != null)
        {
            Destroy(runtimeSkyboxMaterial);
            runtimeSkyboxMaterial = null;
        }

        if (runtimeSkyboxTexture != null)
        {
            Destroy(runtimeSkyboxTexture);
            runtimeSkyboxTexture = null;
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
    public RotationConfig rotation;
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
    public float? x;
    public float? y;
    public float? z;

    public bool HasExplicitCoordinates()
    {
        return x.HasValue && y.HasValue && z.HasValue;
    }
}

[System.Serializable]
public class ScaleConfig
{
    public float x;
    public float y;
    public float z;
}

[System.Serializable]
public class RotationConfig
{
    public float? x;
    public float? y;
    public float? z;

    public bool HasExplicitRotation()
    {
        return x.HasValue && y.HasValue && z.HasValue;
    }
}

[System.Serializable]
public class ColorConfig
{
    public float r;
    public float g;
    public float b;
    public float a;
}
