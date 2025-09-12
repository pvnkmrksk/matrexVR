using UnityEngine;
using UnityEngine.SceneManagement;

public class StatusUI : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private bool showStatusUI = true;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] private int fontSize = 14;
    [SerializeField] private int margin = 10;

    // Cached references
    private MainController mainController;
    private ClosedLoop closedLoop;
    
    // Cached values to avoid searching every frame
    private string cachedSceneName;
    private float cacheTimer = 0f;
    private const float CACHE_UPDATE_INTERVAL = 0.5f; // Update cache every 0.5 seconds

    // For calculating translational speed
    private Vector3 lastPosition;
    private float lastPositionTime;
    private bool hasLastPosition = false;

    // For calculating rotational speed
    private float lastYRotation;
    private float lastRotationTime;
    private bool hasLastRotation = false;

    // Status data
    private int trialNumber = 0;
    private int sequenceNumber = 0;
    private string sceneName = "";
    private float gain = 1.0f;
    private float dcOffset = 0.0f;
    private float translationSpeed = 0.0f; // Speed of movement (translation)
    private float rotationSpeed = 0.0f;    // Speed of rotation (deg/s)

    void Start()
    {
        // Find MainController (should persist across scenes)
        mainController = FindObjectOfType<MainController>();
        
        // Cache initial scene name
        cachedSceneName = SceneManager.GetActiveScene().name;
        
        // Initial update
        UpdateStatusData();
    }

    void Update()
    {
        // Only update UI if enabled
        if (!showStatusUI) return;

        // Update cache periodically to avoid expensive searches every frame
        cacheTimer += Time.unscaledDeltaTime;
        if (cacheTimer >= CACHE_UPDATE_INTERVAL)
        {
            UpdateStatusData();
            cacheTimer = 0f;
        }
    }

    void UpdateStatusData()
    {
        // Get data from MainController
        if (mainController != null)
        {
            trialNumber = mainController.currentTrial;
            sequenceNumber = mainController.currentStep;
        }

        // Get current scene name
        sceneName = SceneManager.GetActiveScene().name;

        // Find ClosedLoop component (may change between scenes)
        if (closedLoop == null)
        {
            closedLoop = FindObjectOfType<ClosedLoop>();
        }

        // Get data from ClosedLoop
        if (closedLoop != null)
        {
            gain = closedLoop.GetYawGain();
            dcOffset = closedLoop.GetYawDCOffset();
            
            // Calculate translational speed
            CalculateTranslationalSpeed();
        }

        // Get speed from scene-specific sources
        UpdateSpeedData();
    }

    void CalculateTranslationalSpeed()
    {
        if (closedLoop != null)
        {
            Vector3 currentPosition = closedLoop.transform.position;
            float currentTime = Time.time;

            if (hasLastPosition)
            {
                float deltaTime = currentTime - lastPositionTime;
                if (deltaTime > 0.01f) // Avoid division by very small numbers
                {
                    Vector3 deltaPosition = currentPosition - lastPosition;
                    translationSpeed = deltaPosition.magnitude / deltaTime; // units per second
                }
            }

            lastPosition = currentPosition;
            lastPositionTime = currentTime;
            hasLastPosition = true;
        }
        else
        {
            translationSpeed = 0.0f;
            hasLastPosition = false;
        }
    }

    void UpdateSpeedData()
    {
        // Try to get speed from different scene types
        translationSpeed = 0.0f;
        rotationSpeed = 0.0f;

        // Check for OptomotorSceneController (drum rotation speed)
        OptomotorSceneController optomotorController = FindObjectOfType<OptomotorSceneController>();
        if (optomotorController != null)
        {
            // Try to get drum speed if available
            Transform drumTransform = optomotorController.GetDrumTransform();
            if (drumTransform != null)
            {
                DrumRotator drumRotator = drumTransform.GetComponent<DrumRotator>();
                if (drumRotator != null)
                {
                    // Get the actual rotation speed
                    rotationSpeed = drumRotator.GetRotationSpeed();
                }
            }
        }

        // Could add other scene types here (SwarmController, etc.)
    }

    void OnGUI()
    {
        if (!showStatusUI) return;

        // Calculate UI dimensions
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        
        // Prepare status text
        string statusText = $"Trial: {trialNumber}\n" +
                           $"Sequence: {sequenceNumber}\n" +
                           $"Scene: {sceneName}\n" +
                           $"Gain: {gain:F2}\n" +
                           $"DC Offset: {dcOffset:F2}°\n" +
                           $"Translation: {translationSpeed:F2} u/s\n" +
                           $"Rotation: {rotationSpeed:F1}°/s";

        // Create GUI style
        GUIStyle backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = CreateColorTexture(backgroundColor);

        GUIStyle textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = fontSize;
        textStyle.normal.textColor = textColor;
        textStyle.alignment = TextAnchor.UpperLeft;
        textStyle.wordWrap = false;

        // Calculate text size
        Vector2 textSize = textStyle.CalcSize(new GUIContent(statusText));
        
        // Position in bottom-right corner
        float boxWidth = textSize.x + (margin * 2);
        float boxHeight = textSize.y + (margin * 2);
        float xPos = screenWidth - boxWidth - margin;
        float yPos = screenHeight - boxHeight - margin;

        Rect backgroundRect = new Rect(xPos, yPos, boxWidth, boxHeight);
        Rect textRect = new Rect(xPos + margin, yPos + margin, textSize.x, textSize.y);

        // Draw background
        GUI.Box(backgroundRect, "", backgroundStyle);
        
        // Draw text
        GUI.Label(textRect, statusText, textStyle);
    }

    // Helper method to create a colored texture for the background
    private Texture2D CreateColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    // Public method to toggle UI visibility
    public void ToggleStatusUI()
    {
        showStatusUI = !showStatusUI;
        Debug.Log($"Status UI: {(showStatusUI ? "ON" : "OFF")}");
    }

    // Public method to manually update rotational speed (can be called by scene controllers)
    public void SetRotationSpeed(float newRotationSpeed)
    {
        rotationSpeed = newRotationSpeed;
    }

    // Public method to manually update translational speed (can be called by scene controllers)  
    public void SetTranslationSpeed(float newTranslationSpeed)
    {
        translationSpeed = newTranslationSpeed;
    }

    // Legacy method for backward compatibility
    public void SetSpeed(float newSpeed)
    {
        rotationSpeed = newSpeed;
    }

    void OnDestroy()
    {
        // Clean up any created textures to avoid memory leaks
        // (Unity's GC should handle this, but it's good practice)
    }
} 