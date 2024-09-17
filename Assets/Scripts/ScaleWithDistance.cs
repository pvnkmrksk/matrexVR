using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ScaleWithDistance : MonoBehaviour
{
    public Transform playerCamera; // Assign the player's camera in the inspector
    public float visualAngleDegrees = 15f; // Desired visual angle in degrees
        void Start()
        {
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
    void Update()
    {
        // Calculate distance between object and camera
        float distance = Vector3.Distance(transform.position, playerCamera.position);

        // Calculate the scale needed to maintain the desired visual angle
        float scale = 2 * distance * Mathf.Tan(Mathf.Deg2Rad * visualAngleDegrees / 2f);

        // Apply the scale uniformly on all axes
        transform.localScale = new Vector3(scale, scale*7, scale);
    }
}
