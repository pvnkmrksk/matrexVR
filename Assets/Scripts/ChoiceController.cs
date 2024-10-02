using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

public class ChoiceController : MonoBehaviour, ISceneController
{
    // Assuming these prefabs are assigned in the Unity Editor
    public GameObject[] prefabs;
    private Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>();

    public Material[] materials; // Materials are assigned in the Unity Editor
    private Dictionary<string, Material> materialDict = new Dictionary<string, Material>();

    // New field for band prefab
    public GameObject bandPrefab;
    private int numberOfVR = 4;

    private void Awake()
    {
        // Initialize prefab dictionary
        foreach (var prefab in prefabs)
        {
            if (!prefabDict.ContainsKey(prefab.name))
            {
                prefabDict.Add(prefab.name, prefab);
                // Debug.Log("Added prefab: " + prefab.name);
            }
        }

        // Initialize material dictionary
        foreach (var material in materials)
        {
            if (!materialDict.ContainsKey(material.name))
            {
                materialDict.Add(material.name, material);
            }
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
                if (obj.type == "Band")
                {
                    InstantiateBand(obj, i + 1);
                }
                else
                {
                    InstantiateRegularObject(obj);
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
        if (bandPrefab != null)
        {
            Vector3 position = CalculatePosition(obj.position.radius, obj.position.angle);
            GameObject bandInstance = Instantiate(bandPrefab, position, Quaternion.identity);

            // Set layer
            string layerName = $"SimulatedLocustsVR{vrIndex}";
            bandInstance.layer = LayerMask.NameToLayer(layerName);

            BandSpawner spawner = bandInstance.GetComponent<BandSpawner>();
            if (spawner != null)
            {
                // Set BandSpawner properties
                spawner.numberOfInstances = obj.numberOfInstances;
                spawner.spawnWidth = obj.spawnWidth;
                spawner.spawnLength = obj.spawnLength;
                spawner.gridType = obj.gridType;
                spawner.mu = obj.mu;
                spawner.kappa = obj.kappa;
                spawner.speed = obj.speed;
                spawner.visibleOffDuration = obj.visibleOffDuration;
                spawner.visibleOnDuration = obj.visibleOnDuration;
                spawner.boundaryWidth = obj.boundaryWidth;
                spawner.boundaryLength = obj.boundaryLength;

                // Set custom parent transform
                spawner.moveWithCustomTransform = true;
                spawner.customParentTransform = GameObject.Find($"VR{vrIndex}")?.transform;
            }

            // Set BandLogger properties
            BandLogger logger = bandInstance.GetComponent<BandLogger>();
            if (logger != null)
            {
                logger.targetLayerMask = LayerMask.GetMask(layerName);
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
            Debug.LogError("Band prefab is not assigned in the ChoiceController.");
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

    // Update SceneConfig and other classes as needed to reflect JSON changes
}

[System.Serializable]
public class SceneConfig
{
    public SceneObject[] objects;
    public bool closedLoopOrientation;
    public bool closedLoopPosition;
    public ColorConfig backgroundColor;
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
    public float spawnWidth;
    public float spawnLength;
    public SpawnGridType gridType;
    public float kappa;
    public float visibleOffDuration;
    public float visibleOnDuration;
    public float boundaryWidth;
    public float boundaryLength;
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
