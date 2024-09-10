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

    public bool loadConfigFromJsonFile = true; // If true, load config from json file, else use default values

    void Start()
    {
        // if load config from json file bool is true, load config from json file
        // else use default values
        if (loadConfigFromJsonFile)
        {
            LoadConfig();
        }
        SpawnLocusts();
    }

    void LoadConfig()
    {
        // Load the JSON file from the Resources folder
        TextAsset jsonFile = Resources.Load<TextAsset>("LocustConfig");

        // Parse the JSON string
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
            Vector3 spawnPosition = new Vector3(
                Random.Range(
                    transform.position.x - spawnAreaSize / 2,
                    transform.position.x + spawnAreaSize / 2
                ),
                -0.25f,
                Random.Range(
                    transform.position.z - spawnAreaSize / 2,
                    transform.position.z + spawnAreaSize / 2
                )
                //transform.position.x+initial_receding_distance*sin(mu),-0.25f,transform.position.z+initial_receding_distance*cos(mu)
            );

            GameObject locust = Instantiate(locustPrefab, spawnPosition, Quaternion.identity); // Spawned independent of the game object
            locust.layer = locustLayer; // Set the layer of the spawned locust
            SetLayerRecursively(locust.transform, locustLayer); // Set layer for all children

            locust.transform.localRotation = GenerateVanMisesRotation(mu, kappa); // Set the local rotation
            locust.GetComponent<LocustMover>().speed = locustSpeed; // Set the speed of the locust
            locust.name = layerName + "_Locust_" + i; // Set the name
            locust.tag = "SimulatedLocust"; // Set the tag for the locust
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
