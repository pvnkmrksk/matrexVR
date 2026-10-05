using Newtonsoft.Json.Linq;
using UnityEngine;

public class LocustSpawner : MonoBehaviour
{
    public GameObject locustPrefab;

    [Tooltip("The number of locusts to spawn")]
    public int numberOfLocusts = 50;

    [Tooltip("The size of the area in which the locusts will be spawned")]
    public float spawnAreaSize = 200;

    //add tooltip
    [Tooltip("The mu value for the Van Mises distribution in degrees (0 to 360)")]
    [Range(0.0f, 360.0f)]
    public float mu = 0.0f; // mu value for Van Mises in degrees (0 to 360)

    [Tooltip(
        "The kappa value for the Van Mises distribution (0 to infinity). 0 + eps is uniform distribution, infinity is a point distribution."
    )]
    public float kappa = 10000f; // kappa value for Van Mises

    [Tooltip("The speed of the locusts")]
    public float locustSpeed = 3.0f; // Default speed for locusts
    public string layerName = "LocustLayer"; // Default value
    public BoundaryManager boundaryManager;

    [Tooltip("2D preserves the historical X/Z swarm. 3D samples a volume and wraps Y when enabled.")]
    public string dimension = "2D";
    public bool useSpawnCenter;
    public Vector3 spawnCenter;
    public bool useSpawnVolume;
    public Vector3 spawnVolumeSize = new Vector3(200f, 200f, 200f);
    public float muElevation;
    public float kappaElevation = 10000f;
    public float boundaryHeight;
    public bool wrapY;
    [Tooltip("ScenePrefab uses the existing prefab. Bogong creates an unlit circular patch at runtime.")]
    public string agentVisual = "ScenePrefab";
    public BogongVisualConfig bogongVisual = new BogongVisualConfig();

    public bool animateOnMove;
    public float animationSpeedThreshold = 0.5f;
    public bool loadConfigFromJsonFile = true; // If true, load config from json file, else use default values

    private readonly System.Collections.Generic.List<GameObject> spawned = new System.Collections.Generic.List<GameObject>();
    private bool initialized;

    void Start()
    {
        if (initialized) return;
        // if load config from json file bool is true, load config from json file
        // else use default values
        if (loadConfigFromJsonFile)
        {
            LoadConfig();
        }
        Rebuild();
    }

    void LoadConfig()
    {
        // Load the JSON file from the Resources folder
        TextAsset jsonFile = Resources.Load<TextAsset>("LocustConfig");

        // Parse the JSON string
        if (jsonFile == null) { Debug.LogWarning("LocustConfig was not found; using scene parameters."); return; }
        JObject config = JObject.Parse(jsonFile.text);

        // Update class variables
        // locustPrefab = Resources.Load<GameObject>((string)config["locustPrefab"]);
        numberOfLocusts = (int)config["numberOfLocusts"];
        spawnAreaSize = (float)config["spawnAreaSize"];
        mu = (float)config["mu"];
        kappa = (float)config["kappa"];
        locustSpeed = (float)config["locustSpeed"];
        // layerName = (string)config["layerName"];
    }

    public void PrepareObservation()
    {
        Cleanup();
        initialized = true; // Start must not spawn during assessment.
    }

    public void Rebuild()
    {
        Cleanup();
        initialized = true;
        if (locustPrefab == null && !UsesBogongVisual()) { Debug.LogError("LocustSpawner requires a locust prefab unless agentVisual is Bogong."); return; }
        SpawnLocusts();
    }
    private void Cleanup()
    {
        foreach (GameObject locust in spawned)
            if (locust != null) { locust.SetActive(false); Destroy(locust); }
        spawned.Clear();
    }
    private void OnDestroy() => Cleanup();

    void SpawnLocusts()
    {
        int locustLayer = LayerMask.NameToLayer(layerName); // Get the layer by name
        if (locustLayer == -1) // Layer not found
        {
            Debugger.Log(
                "Layer "
                    + layerName
                    + " not found. Make sure it's created in Unity. Defaulting to object's layer.",
                2
            );
            locustLayer = gameObject.layer; // Default to the game object's layer
        }
        for (int i = 0; i < numberOfLocusts; i++)
        {
            Vector3 center = useSpawnCenter
                ? spawnCenter
                : new Vector3(transform.position.x, Is3D() ? transform.position.y : -0.25f, transform.position.z);
            Vector3 size = useSpawnVolume ? spawnVolumeSize : new Vector3(spawnAreaSize, Is3D() ? spawnAreaSize : 0f, spawnAreaSize);
            Vector3 spawnPosition = new Vector3(
                Random.Range(center.x - size.x / 2f, center.x + size.x / 2f),
                Is3D() ? Random.Range(center.y - size.y / 2f, center.y + size.y / 2f) : -0.25f,
                Random.Range(center.z - size.z / 2f, center.z + size.z / 2f));

            GameObject locust = UsesBogongVisual()
                ? CreateBogongAgent(spawnPosition, i)
                : Instantiate(locustPrefab, spawnPosition, Quaternion.identity); // Spawned independent of the game object
            spawned.Add(locust);
            locust.layer = locustLayer; // Set the layer of the spawned locust
            SetLayerRecursively(locust.transform, locustLayer); // Set layer for all children

            locust.transform.localRotation = GenerateSwarmRotation(); // Set the local rotation
            if (locust.GetComponent<LocustMover>() != null) locust.GetComponent<LocustMover>().speed = locustSpeed; // Set the speed of the locust
            locust.name = layerName + "_Locust_" + i; // Set the name
            locust.tag = "SimulatedLocust"; // Set the tag for the locust
            float wrap = boundaryManager != null ? boundaryManager.boundarySize : 0;
            AnimateOnMove.ConfigureHierarchy(locust, locust.transform, animateOnMove, animationSpeedThreshold, new Vector2(wrap, wrap));
            // Get the Animator component
            Animator locustAnimator = locust.GetComponent<Animator>();

            if (locustAnimator)
            {
                // Assuming the animation state you want to desynchronize is the default one at layer 0
                AnimatorStateInfo state = locustAnimator.GetCurrentAnimatorStateInfo(0);
                locustAnimator.Play(state.fullPathHash, -1, Random.value); // Random normalized time between 0 and 1
            }

            // Set the boundary manager for the locust
            LocustMover locustMover = locust.GetComponent<LocustMover>();
            if (locustMover)
            {
                locustMover.boundaryManager = boundaryManager;
            }
        }
    }

    private bool Is3D() => string.Equals(dimension, "3D", System.StringComparison.OrdinalIgnoreCase);
    private bool UsesBogongVisual() => string.Equals(agentVisual, "Bogong", System.StringComparison.OrdinalIgnoreCase);

    private GameObject CreateBogongAgent(Vector3 position, int index)
    {
        GameObject agent = GameObject.CreatePrimitive(PrimitiveType.Quad);
        // A visual stimulus must not participate in collisions or ground-height raycasts.
        Collider collider = agent.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        agent.AddComponent<LocustMover>();
        agent.transform.position = position;
        agent.name = layerName + "_Bogong_" + index;
        Bogong bogong = agent.AddComponent<Bogong>();
        bogong.Configure(bogongVisual ?? new BogongVisualConfig());
        return agent;
    }

    private void SetLayerRecursively(Transform parent, int layer)
    {
        parent.gameObject.layer = layer;
        foreach (Transform child in parent)
        {
            SetLayerRecursively(child, layer);
        }
    }

    Quaternion GenerateVanMisesRotation(float mu, float kappa)
    {
        // if kappa is 0 or less than 0 , add a small epsilon to prevent div by 0 error

        if (kappa <= 0)
        {
            kappa = 0.0001f;
        }
        //if kappa >= 10000, fixed angle mu is returned
        else if (kappa>=10000)
        {

            return Quaternion.Euler(0,mu,0);

        }


        float angle = VanMisesDistribution.Generate(Mathf.Deg2Rad * mu, kappa); // Generate angles by converting from deg to radians for the function to work
        // Debugger.Log("Generated Angle (in degrees): " + angle * Mathf.Rad2Deg);
        return Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0); // Convert radians to degrees for the Quaternion rotation
    }

    private Quaternion GenerateSwarmRotation()
    {
        Quaternion yaw = GenerateVanMisesRotation(mu, kappa);
        if (!Is3D()) return yaw;
        float elevation = muElevation;
        if (kappaElevation <= 0) elevation = Random.Range(-90f, 90f);
        else if (kappaElevation < 10000f)
            elevation = VanMisesDistribution.Generate(Mathf.Deg2Rad * muElevation, kappaElevation) * Mathf.Rad2Deg;
        return Quaternion.Euler(elevation, yaw.eulerAngles.y, 0f);
    }
}

public static class VanMisesDistribution
{
    public static float Generate(float mu, float kappa)
    {
        float s = 0.5f / kappa;
        float r = s + Mathf.Sqrt(1 + s * s);

        while (true)
        {
            float u1 = Random.value;
            float z = Mathf.Cos(Mathf.PI * u1);
            float f = (1 + r * z) / (r + z);
            float c = kappa * (r - f);

            float u2 = Random.value;

            if (u2 < c)
            {
                float u3 = Random.value;
                float sign = (u3 > 0.5f) ? 1.0f : -1.0f;
                return mu + sign * Mathf.Acos(f);
            }
            else if (u2 <= c + Mathf.Exp(-kappa))
            {
                float u3 = Random.value;
                return mu + Mathf.PI * (2 * u3 - 1);
            }
        }
    }
}
