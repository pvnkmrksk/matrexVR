using UnityEngine;

public class OptomotorDebugHelper : MonoBehaviour
{
    [Header("Target References")]
    [SerializeField] private string drumObjectName = "OptomotorDrum";
    [SerializeField] private KeyCode forceUpdateKey = KeyCode.F1;

    [Header("Manual Rotation Settings")]
    [SerializeField][Range(0f, 50f)] private float testSpeed = 20f;
    [SerializeField] private bool testClockwise = true;
    [SerializeField] private string testAxis = "Yaw";

    [Header("Manual Grating Settings")]
    [SerializeField][Range(0f, 10f)] private float testFrequency = 4f;
    [SerializeField][Range(0f, 1f)] private float testContrast = 0.5f;
    [SerializeField] private Color testColor1 = Color.black;
    [SerializeField] private Color testColor2 = Color.white;

    // Component references
    private GameObject drumObject;
    private DrumRotator drumRotator;
    private SinusoidalGrating sinusoidalGrating;

    void Start()
    {
        Debug.Log("OptomotorDebugHelper started. Press F1 to force parameter updates.");
    }

    void Update()
    {
        // Check for the force update key
        if (Input.GetKeyDown(forceUpdateKey))
        {
            ForceParameterUpdates();
        }
    }

    public void ForceParameterUpdates()
    {
        // Find the drum object if not already found
        if (drumObject == null)
        {
            drumObject = GameObject.Find(drumObjectName);

            if (drumObject == null)
            {
                Debug.LogError($"Cannot find drum object with name '{drumObjectName}'");
                return;
            }

            Debug.Log($"Found drum object: {drumObject.name}");

            // Get components
            drumRotator = drumObject.GetComponent<DrumRotator>();
            sinusoidalGrating = drumObject.GetComponent<SinusoidalGrating>();

            if (drumRotator == null)
                Debug.LogError("DrumRotator component not found on drum object");

            if (sinusoidalGrating == null)
                Debug.LogError("SinusoidalGrating component not found on drum object");
        }

        // Apply test settings
        if (drumRotator != null)
        {
            Debug.Log($"Applying manual rotation settings: Speed={testSpeed}, Clockwise={testClockwise}, Axis={testAxis}");
            drumRotator.SetRotationParameters(testSpeed, testClockwise, testAxis);
        }

        if (sinusoidalGrating != null)
        {
            Debug.Log($"Applying manual grating settings: Frequency={testFrequency}, Contrast={testContrast}");
            sinusoidalGrating.SetGratingParameters(testFrequency, testContrast, testColor1, testColor2);
        }
    }

    void OnGUI()
    {
        // Create a simple GUI to display status and controls
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));

        GUILayout.Label("Optomotor Debug Helper", GUI.skin.box);

        if (drumObject == null)
        {
            GUILayout.Label($"Drum '{drumObjectName}' not found", GUI.skin.box);
        }
        else
        {
            GUILayout.Label($"Drum: {drumObject.name}", GUI.skin.box);

            if (drumRotator != null)
                GUILayout.Label($"Rotation: Speed={testSpeed}, Clockwise={testClockwise}", GUI.skin.box);
            else
                GUILayout.Label("No DrumRotator component found", GUI.skin.box);

            if (sinusoidalGrating != null)
                GUILayout.Label($"Grating: Freq={testFrequency}, Contrast={testContrast}", GUI.skin.box);
            else
                GUILayout.Label("No SinusoidalGrating component found", GUI.skin.box);

            if (GUILayout.Button("Force Parameter Updates"))
            {
                ForceParameterUpdates();
            }
        }

        GUILayout.Label($"Press {forceUpdateKey} to force parameter updates", GUI.skin.box);

        GUILayout.EndArea();
    }
}