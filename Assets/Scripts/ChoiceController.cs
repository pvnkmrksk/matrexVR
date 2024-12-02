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
    string[] tags = new string[] { "ChoiceVR1", "ChoiceVR2", "ChoiceVR3", "ChoiceVR4" };
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
    
    //instantiate objects

    foreach (var obj in config.objects)
    {
        if (prefabDict.TryGetValue(obj.type, out GameObject prefab))
        {
            for (int i = 0; i < tags.Length; i++)
            {
                Vector3 position = CalculatePosition(obj.position.radius, obj.position.angle, obj.position.height);
                GameObject instance = Instantiate(prefab, position, Quaternion.identity);

                // Set the tag
                instance.tag = tags[i];
                
                // **Assign the layer based on the tag**
                string layerName = "ChoiceVR" + (i + 1); // "Choice1", "Choice2", etc.
                int layer = LayerMask.NameToLayer(layerName);
                if (layer == -1)
                {
                    Debug.LogError("Layer '" + layerName + "' not found. Please add it in the Tags and Layers manager.");
                }
                else
                {
                    SetLayerRecursively(instance, layer);
                }

                // Rest of your instance initialization code
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
                if (!string.IsNullOrEmpty(obj.material) && materialDict.TryGetValue(obj.material, out Material material))
                {
                    instance.GetComponent<Renderer>().material = material;
                }
            }
        }
    }

        ClosedLoop[] closedLoopComponents = FindObjectsOfType<ClosedLoop>();
        Debugger.Log("Number of ClosedLoop scripts found: " + closedLoopComponents.Length, 4);


        Quaternion initialRandRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
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
    }

    //helper method
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
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

    private Vector3 CalculatePosition(float radius, float angle, float height)
    {
        //todo.add initial position for the object postions
        float x = radius * Mathf.Sin(angle * Mathf.Deg2Rad);
        float z = radius * Mathf.Cos(angle * Mathf.Deg2Rad);
        return new Vector3(x, height, z); // Assuming y is always 0
    }

    // Update SceneConfig and other classes as needed to reflect JSON changes
}

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
    // Include other properties as before
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
