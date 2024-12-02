using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ScaleWithDistance : MonoBehaviour
{
    public Transform playerCamera; // Will be assigned dynamically based on the object's tag
    public float visualAngleDegrees = 15f; // Desired visual angle in degrees

    void Start()
    {
        // Get the tag of this GameObject
        string objectTag = gameObject.tag;

        // Check if the tag starts with "ChoiceVR"
        if (objectTag.StartsWith("ChoiceVR"))
        {
            // Extract the number from the tag
            string vrNumber = objectTag.Substring("ChoiceVR".Length);

            // Construct the VR object name (e.g., "VR1", "VR2")
            string vrObjectName = "VR" + vrNumber;

            // Find the VR object by name
            GameObject vrObject = GameObject.Find(vrObjectName);

            if (vrObject != null)
            {
                playerCamera = vrObject.transform;
            }
            else
            {
                Debug.LogError("No VR object named '" + vrObjectName + "' found in the scene.");
            }
        }
        else
        {
            // If the tag doesn't start with "ChoiceVR", fallback to default behavior
            // Automatically find the camera with the "ScaleReference" tag
            GameObject cameraObject = GameObject.FindGameObjectWithTag("ScaleReference");
            if (cameraObject != null)
            {
                playerCamera = cameraObject.transform;
            }
            else
            {
                Debug.LogError("No camera with the 'ScaleReference' tag found in the scene.");
            }
        }
    }

    void Update()
    {
        if (playerCamera == null)
        {
            // Early exit if playerCamera is not assigned
            return;
        }

        // Calculate distance between object and camera
        float distance = Vector3.Distance(transform.position, playerCamera.position);

        // Calculate the scale needed to maintain the desired visual angle
        float scale = 2 * distance * Mathf.Tan(Mathf.Deg2Rad * visualAngleDegrees / 2f);

        // Apply the scale uniformly on all axes
        transform.localScale = new Vector3(scale, scale * 7, scale);
    }
}
