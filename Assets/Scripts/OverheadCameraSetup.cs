using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Automatically sets up the overhead camera and UI when the scene loads.
/// Add this script to any GameObject in the scene to automatically create the overhead camera system.
/// </summary>
public class OverheadCameraSetup : MonoBehaviour
{
    [Header("Auto Setup")]
    [Tooltip("Automatically create camera and UI on Start")]
    public bool autoSetupOnStart = true;
    
    [Header("Camera Settings")]
    [Tooltip("Initial height above scene")]
    public float initialHeight = 100f;
    
    [Tooltip("Auto-calculate target from locusts")]
    public bool autoCalculateTarget = true;
    
    [Header("UI Settings")]
    [Tooltip("Create UI button")]
    public bool createUIButton = true;
    
    [Tooltip("Button size")]
    public Vector2 buttonSize = new Vector2(150, 40);
    
    [Tooltip("Button position offset from bottom-right corner")]
    public Vector2 buttonOffset = new Vector2(10, 10);
    
    private GameObject overheadCameraObject;
    private GameObject uiCanvasObject;
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupOverheadCamera();
            if (createUIButton)
            {
                SetupUI();
            }
        }
    }
    
    public void SetupOverheadCamera()
    {
        // Check if overhead camera already exists
        OverheadCameraController existing = FindObjectOfType<OverheadCameraController>();
        if (existing != null)
        {
            Debug.Log("OverheadCameraController already exists in scene.");
            overheadCameraObject = existing.gameObject;
            return;
        }
        
        // Create camera GameObject
        overheadCameraObject = new GameObject("Overhead Camera");
        
        // Add Camera component
        Camera cam = overheadCameraObject.AddComponent<Camera>();
        cam.depth = 10; // High depth to render on top
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.cullingMask = -1; // See everything
        
        // Add OverheadCameraController
        OverheadCameraController controller = overheadCameraObject.AddComponent<OverheadCameraController>();
        
        // Set initial position high above the scene
        overheadCameraObject.transform.position = new Vector3(0, initialHeight, 0);
        overheadCameraObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        
        // Configure controller
        controller.targetPosition = Vector3.zero;
        controller.distance = initialHeight;
        controller.initialVerticalAngle = 90f; // Looking straight down
        controller.initialHorizontalAngle = 0f; // Default horizontal angle
        
        // Auto-calculate target from locusts if enabled
        if (autoCalculateTarget)
        {
            // Wait a frame for locusts to spawn, then calculate
            StartCoroutine(CalculateTargetDelayed(controller));
        }
        
        Debug.Log("Overhead camera created successfully.");
    }
    
    System.Collections.IEnumerator CalculateTargetDelayed(OverheadCameraController controller)
    {
        // Wait for locusts to spawn
        yield return new WaitForSeconds(0.5f);
        controller.CalculateTargetFromLocusts();
    }
    
    public void SetupUI()
    {
        // Check if UI already exists
        OverheadCameraUI existingUI = FindObjectOfType<OverheadCameraUI>();
        if (existingUI != null)
        {
            Debug.Log("OverheadCameraUI already exists in scene.");
            return;
        }
        
        // Find or create Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            // Create Canvas
            uiCanvasObject = new GameObject("Overhead Camera UI Canvas");
            canvas = uiCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // High sorting order to appear on top
            
            // Add CanvasScaler for different screen sizes
            CanvasScaler scaler = uiCanvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            // Add GraphicRaycaster
            uiCanvasObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            uiCanvasObject = canvas.gameObject;
        }
        
        // Create button GameObject
        GameObject buttonObject = new GameObject("Reset Camera Button");
        buttonObject.transform.SetParent(uiCanvasObject.transform, false);
        
        // Add RectTransform
        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        
        // Position in bottom-right corner
        rectTransform.anchorMin = new Vector2(1, 0);
        rectTransform.anchorMax = new Vector2(1, 0);
        rectTransform.pivot = new Vector2(1, 0);
        rectTransform.anchoredPosition = -buttonOffset;
        rectTransform.sizeDelta = buttonSize;
        
        // Add Image component for button background
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Semi-transparent dark gray
        
        // Add Button component
        Button button = buttonObject.AddComponent<Button>();
        
        // Create button text
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform, false);
        
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        Text text = textObject.AddComponent<Text>();
        text.text = "Reset View";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        
        // Add OverheadCameraUI component
        OverheadCameraUI uiController = buttonObject.AddComponent<OverheadCameraUI>();
        uiController.cameraController = overheadCameraObject != null 
            ? overheadCameraObject.GetComponent<OverheadCameraController>() 
            : FindObjectOfType<OverheadCameraController>();
        uiController.resetButton = button;
        uiController.buttonText = text;
        
        // Setup button colors
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        colors.selectedColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);
        button.colors = colors;
        
        Debug.Log("Overhead camera UI created successfully.");
    }
}

