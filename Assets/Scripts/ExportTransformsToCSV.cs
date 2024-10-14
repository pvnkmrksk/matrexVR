using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ExportTransformsToCSV : MonoBehaviour
{
    void Start()
    {
        // File path where you want to save the CSV
        string filePath = Application.dataPath + "/GameObjectsTransformData.csv";

        // List to store each line of CSV
        List<string> csvLines = new List<string>();

        // Header for the CSV file
        csvLines.Add("GameObject Name, Position (x, y, z), Rotation (x, y, z), Scale (x, y, z), Parent");

        // Get all root game objects in the scene
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject rootObject in rootObjects)
        {
            // Call recursive function to process each GameObject and its children
            ExportGameObject(rootObject.transform, ref csvLines);
        }

        // Write all lines to the CSV file
        File.WriteAllLines(filePath, csvLines.ToArray());

        Debug.Log("Transforms exported to " + filePath);
    }

    // Recursively process each GameObject and its children
    void ExportGameObject(Transform objTransform, ref List<string> csvLines)
    {
        // Get the GameObject name
        string name = objTransform.gameObject.name;

        // Get the Transform properties
        Vector3 position = objTransform.position;
        Vector3 rotation = objTransform.eulerAngles;
        Vector3 scale = objTransform.localScale;

        // Check if the object has a parent
        string parentName = objTransform.parent != null ? objTransform.parent.gameObject.name : "None";

        // Create a CSV line for this GameObject
        string csvLine = $"{name}, {position.x}, {position.y}, {position.z}, {rotation.x}, {rotation.y}, {rotation.z}, {scale.x}, {scale.y}, {scale.z}, {parentName}";
        csvLines.Add(csvLine);

        // Recursively call this method for each child
        foreach (Transform child in objTransform)
        {
            ExportGameObject(child, ref csvLines);
        }
    }
}
