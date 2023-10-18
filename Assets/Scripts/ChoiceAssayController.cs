using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;

public class ChoiceAssayController : MonoBehaviour, ISceneController
{
    public GameObject cubePrefab;
    public GameObject spherePrefab;
    public GameObject cylinderPrefab;

    public Material redMaterial;
    public Material blueMaterial;
    public Material greenMaterial;
    public Material blackMaterial;
    public Material grey14Material;
    public Material grey50Material;

    public void InitializeScene(Dictionary<string, object> parameters)
    {
        Debug.Log("InitializeScene called.");

        // Path to scene configuration JSON
        string configFile = parameters["configFile"].ToString();

        // Load and parse JSON
        string jsonPath = Path.Combine(Application.streamingAssetsPath, configFile);
        string jsonString = File.ReadAllText(jsonPath);
        SceneConfig config = JsonConvert.DeserializeObject<SceneConfig>(jsonString);

        // Instantiate objects
        foreach (var obj in config.objects)
        {
            GameObject prefab = null;

            // Match prefab based on type
            switch (obj.type)
            {
                case "cube":
                    prefab = cubePrefab;
                    break;
                case "sphere":
                    prefab = spherePrefab;
                    break;
                case "cylinder":
                    prefab = cylinderPrefab;
                    break;
            }

            if (prefab != null)
            {
                // Compute position based on radius and angle
                Vector3 position = new Vector3(obj.position.radius * Mathf.Cos(obj.position.angle * Mathf.Deg2Rad), 0, 
                                               obj.position.radius * Mathf.Sin(obj.position.angle * Mathf.Deg2Rad));

                // Instantiate and initialize object
                GameObject instance = Instantiate(prefab, position, Quaternion.identity);
                
                // Set scale
                if (obj.scale != null)
                {
                    instance.transform.localScale = new Vector3(obj.scale.x, obj.scale.y, obj.scale.z);
                }

                // Set material
                Material materialToSet = null;
                switch (obj.material)
                {
                    case "Black_000000":
                        materialToSet = blackMaterial;
                        break;
                    case "Grey_141414":
                        materialToSet = grey14Material;
                        break;
                    case "Grey_505050":
                        materialToSet = grey50Material;
                        break;
                    case "red":
                        materialToSet = redMaterial;
                        break;
                    case "blue":
                        materialToSet = blueMaterial;
                        break;
                    case "green":
                        materialToSet = greenMaterial;
                        break;
                }
                if (materialToSet != null)
                {
                    instance.GetComponent<Renderer>().material = materialToSet;
                }
            }
        }
        
        // TODO: Set sky and grass textures
    }
}

[System.Serializable]
public class SceneConfig
{
    public SceneObject[] objects;
}

[System.Serializable]
public class SceneObject
{
    public string type;
    public Position position;
    public string material;
    public ScaleConfig scale;
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