using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Simple overhead camera that automatically creates itself and renders in bottom-right corner.
/// Just add this script to any GameObject in the scene.
/// </summary>
public class SimpleOverheadCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("Height above scene")]
    public float cameraHeight = 150f;
    
    [Tooltip("Viewport size (0-1)")]
    public float viewportSize = 0.3f;
    
    [Tooltip("Viewport position from bottom-right corner")]
    public Vector2 viewportOffset = new Vector2(0.02f, 0.02f);
    
    [Header("Performance")]
    [Tooltip("Render texture resolution (lower = better performance)")]
    public int renderTextureResolution = 256;
    
    [Header("VR Highlighting")]
    [Tooltip("Size of VR markers")]
    public float vrMarkerSize = 5f;
    
    [Tooltip("Height above VR objects")]
    public float vrMarkerHeight = 2f;
    
    [Header("Enable/Disable")]
    [Tooltip("Enable overhead camera on start")]
    public bool enableOnStart = true;
    
    private Camera overheadCam;
    private GameObject cameraObject;
    private GameObject uiCanvas;
    private Button resetButton;
    private Button toggleButton;
    private Text toggleButtonText;
    private RenderTexture renderTexture;
    private RawImage displayImage;
    private List<GameObject> vrMarkers = new List<GameObject>();
    private List<GameObject> locustArrows = new List<GameObject>();
    
    void Awake()
    {
        // Set VSync to 60 FPS
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
        
        // Create black background camera first (renders before everything)
        CreateBlackBackgroundCamera();
        
        if (enableOnStart)
        {
            CreateCamera();
            CreateUI();
        }
    }
    
    void CreateBlackBackgroundCamera()
    {
        // Check if already exists
        if (GameObject.Find("Black Background Camera") != null)
        {
            return;
        }
        
        // Create a camera that renders black first, before all other cameras
        GameObject bgCameraObj = new GameObject("Black Background Camera");
        Camera bgCamera = bgCameraObj.AddComponent<Camera>();
        
        // Configure to render first and clear to black
        bgCamera.depth = -100; // Render before everything
        bgCamera.clearFlags = CameraClearFlags.SolidColor;
        bgCamera.backgroundColor = Color.black;
        bgCamera.cullingMask = 0; // Don't render anything, just clear
        bgCamera.rect = new Rect(0, 0, 1, 1); // Full screen
        
        Debug.Log("Black background camera created.");
    }
    
    void CreateCamera()
    {
        // Check if already exists
        Camera existing = GameObject.Find("Simple Overhead Camera")?.GetComponent<Camera>();
        if (existing != null)
        {
            overheadCam = existing;
            cameraObject = existing.gameObject;
            return;
        }
        
        // Create low-res render texture for performance
        renderTexture = new RenderTexture(renderTextureResolution, renderTextureResolution, 16);
        renderTexture.name = "OverheadCameraRT";
        renderTexture.antiAliasing = 1; // No AA for performance
        renderTexture.filterMode = FilterMode.Bilinear;
        
        // Create camera GameObject
        cameraObject = new GameObject("Simple Overhead Camera");
        overheadCam = cameraObject.AddComponent<Camera>();
        
        // Position high above scene, looking down
        cameraObject.transform.position = new Vector3(0, cameraHeight, 0);
        cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        
        // Create or get layer for overhead camera markers
        int markerLayer = LayerMask.NameToLayer("OverheadCameraMarkers");
        if (markerLayer == -1)
        {
            // Layer doesn't exist, use layer 8 (often unused)
            markerLayer = 8;
        }
        
        // Configure camera
        overheadCam.clearFlags = CameraClearFlags.SolidColor; // Use solid color to prevent flicker
        overheadCam.backgroundColor = Color.black; // Black background to prevent flicker
        // See everything including VR contents and markers
        int cullingMask = -1; // See everything
        overheadCam.cullingMask = cullingMask;
        overheadCam.fieldOfView = 60f;
        overheadCam.nearClipPlane = 0.1f;
        overheadCam.farClipPlane = 2000f;
        overheadCam.depth = 100; // Render on top
        overheadCam.targetTexture = renderTexture; // Use render texture for low-res rendering
        
        // Add controller for mouse interaction
        OverheadCameraController controller = cameraObject.AddComponent<OverheadCameraController>();
        controller.targetPosition = Vector3.zero;
        controller.distance = cameraHeight;
        controller.initialVerticalAngle = 90f;
        controller.initialHorizontalAngle = 0f;
        
        Debug.Log($"Simple Overhead Camera created with {renderTextureResolution}x{renderTextureResolution} render texture.");
    }
    
    void CreateUI()
    {
        // Check if UI already exists
        if (GameObject.Find("Overhead Camera Reset Button") != null)
        {
            return;
        }
        
        // Find or create Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            uiCanvas = new GameObject("Overhead Camera Canvas");
            canvas = uiCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            
            CanvasScaler scaler = uiCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            uiCanvas.AddComponent<GraphicRaycaster>();
            
            // Ensure EventSystem exists for button clicks
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
        else
        {
            uiCanvas = canvas.gameObject;
        }
        
        // Create display image for render texture in bottom-right corner
        GameObject displayObj = new GameObject("Overhead Camera Display");
        displayObj.transform.SetParent(uiCanvas.transform, false);
        
        RectTransform displayRect = displayObj.AddComponent<RectTransform>();
        displayRect.anchorMin = new Vector2(1, 0);
        displayRect.anchorMax = new Vector2(1, 0);
        displayRect.pivot = new Vector2(1, 0);
        float displaySize = viewportSize * Screen.width;
        displayRect.anchoredPosition = new Vector2(-viewportOffset.x * Screen.width, viewportOffset.y * Screen.height);
        displayRect.sizeDelta = new Vector2(displaySize, displaySize);
        
        displayImage = displayObj.AddComponent<RawImage>();
        displayImage.texture = renderTexture;
        
        // Create button above the display (use normalized coordinates for better positioning)
        GameObject buttonObj = new GameObject("Overhead Camera Reset Button");
        buttonObj.transform.SetParent(uiCanvas.transform, false);
        
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        // Position relative to display - use percentage of screen for better scaling
        float buttonY = (viewportOffset.y + viewportSize) * Screen.height + 5;
        rect.anchoredPosition = new Vector2(-viewportOffset.x * Screen.width - 10, buttonY);
        rect.sizeDelta = new Vector2(120, 35);
        
        // Button image
        Image img = buttonObj.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        
        // Button component
        resetButton = buttonObj.AddComponent<Button>();
        resetButton.onClick.AddListener(ResetCamera);
        
        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        Text text = textObj.AddComponent<Text>();
        text.text = "Reset View";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 12;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        
        // Button colors
        ColorBlock colors = resetButton.colors;
        colors.normalColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        colors.highlightedColor = new Color(0.25f, 0.25f, 0.25f, 0.95f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        resetButton.colors = colors;
        
        // Create toggle button to enable/disable camera
        CreateToggleButton();
        
        // Add FPS indicator (moved to separate location)
        AddFPSIndicator();
        
        Debug.Log("Overhead Camera UI created.");
    }
    
    void CreateBlackBackground()
    {
        // Black background is handled by camera clear color, not needed here
        // This function kept for compatibility but does nothing
    }
    
    void CreateToggleButton()
    {
        // Create toggle button to enable/disable camera
        GameObject toggleObj = new GameObject("Toggle Camera Button");
        toggleObj.transform.SetParent(uiCanvas.transform, false);
        
        RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1, 0);
        toggleRect.anchorMax = new Vector2(1, 0);
        toggleRect.pivot = new Vector2(1, 0);
        float displaySize = viewportSize * Screen.width;
        float buttonY = (viewportOffset.y + viewportSize) * Screen.height + 45; // Above reset button
        toggleRect.anchoredPosition = new Vector2(-viewportOffset.x * Screen.width - 10, buttonY);
        toggleRect.sizeDelta = new Vector2(120, 35);
        
        Image toggleImg = toggleObj.AddComponent<Image>();
        toggleImg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        
        Button toggleButton = toggleObj.AddComponent<Button>();
        toggleButton.onClick.AddListener(ToggleCamera);
        
        GameObject toggleTextObj = new GameObject("Text");
        toggleTextObj.transform.SetParent(toggleObj.transform, false);
        
        RectTransform toggleTextRect = toggleTextObj.AddComponent<RectTransform>();
        toggleTextRect.anchorMin = Vector2.zero;
        toggleTextRect.anchorMax = Vector2.one;
        toggleTextRect.sizeDelta = Vector2.zero;
        toggleTextRect.anchoredPosition = Vector2.zero;
        
        Text toggleText = toggleTextObj.AddComponent<Text>();
        toggleText.text = "Hide Camera";
        toggleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        toggleText.fontSize = 12;
        toggleText.color = Color.white;
        toggleText.alignment = TextAnchor.MiddleCenter;
        
        // Store references
        this.toggleButton = toggleButton;
        this.toggleButtonText = toggleText;
        
        ColorBlock toggleColors = toggleButton.colors;
        toggleColors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        toggleColors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 0.95f);
        toggleColors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        toggleButton.colors = toggleColors;
    }
    
    void ToggleCamera()
    {
        if (cameraObject == null)
        {
            cameraObject = GameObject.Find("Simple Overhead Camera");
        }
        
        if (cameraObject != null)
        {
            bool isActive = cameraObject.activeSelf;
            cameraObject.SetActive(!isActive);
            
            // Also toggle display
            if (displayImage != null)
            {
                displayImage.gameObject.SetActive(!isActive);
            }
            
            // Update button text
            if (toggleButtonText != null)
            {
                toggleButtonText.text = isActive ? "Show Camera" : "Hide Camera";
            }
            
            Debug.Log($"Overhead camera {(isActive ? "disabled" : "enabled")}.");
        }
    }
    
    public void ResetCamera()
    {
        // Find camera if not cached
        if (cameraObject == null)
        {
            cameraObject = GameObject.Find("Simple Overhead Camera");
        }
        
        if (cameraObject == null)
        {
            Debug.LogError("Simple Overhead Camera not found!");
            return;
        }
        
        OverheadCameraController controller = cameraObject.GetComponent<OverheadCameraController>();
        if (controller != null)
        {
            controller.ResetToSkyView();
            Debug.Log("Camera reset to nadir view.");
        }
        else
        {
            Debug.LogError("OverheadCameraController component not found on camera!");
        }
    }
    
    void Start()
    {
        // Create VR markers after a short delay to ensure VR objects are spawned
        Invoke(nameof(CreateVRMarkers), 0.5f);
        // Create simple locust arrows after locusts spawn
        Invoke(nameof(CreateLocustArrows), 1f);
    }
    
    void Update()
    {
        // Auto-center on locusts after a delay
        if (Time.time > 1f && Time.time < 1.1f)
        {
            OverheadCameraController controller = cameraObject?.GetComponent<OverheadCameraController>();
            if (controller != null)
            {
                controller.CalculateTargetFromLocusts();
            }
        }
        
        // Update VR marker positions
        UpdateVRMarkers();
    }
    
    void CreateVRMarkers()
    {
        // Find VR objects
        Color[] vrColors = new Color[] {
            Color.red,      // VR1
            Color.green,    // VR2
            Color.blue,     // VR3
            Color.yellow    // VR4
        };
        
        for (int i = 1; i <= 4; i++)
        {
            // Try different naming patterns
            GameObject vrObj = GameObject.Find($"VR{i}") ?? 
                               GameObject.Find($"VR{i} Swarm") ?? 
                               GameObject.Find($"VR{i} Kannadi");
            
            if (vrObj != null)
            {
                CreateVRMarker(vrObj, i, vrColors[i - 1]);
            }
        }
        
        Debug.Log($"Created {vrMarkers.Count} VR markers.");
    }
    
    void CreateVRMarker(GameObject vrObject, int vrIndex, Color color)
    {
        // Get or create marker layer
        int markerLayer = LayerMask.NameToLayer("OverheadCameraMarkers");
        if (markerLayer == -1)
        {
            // Try to create the layer (this might fail if layer doesn't exist in project settings)
            // For now, use a layer that's less likely to be used by other cameras
            markerLayer = 8; // Layer 8 is often unused
        }
        
        // Create marker sphere
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = $"VR{vrIndex} Marker";
        marker.layer = markerLayer; // Set to marker layer so only overhead camera sees it
        marker.transform.SetParent(vrObject.transform);
        marker.transform.localPosition = new Vector3(0, vrMarkerHeight, 0);
        marker.transform.localScale = Vector3.one * vrMarkerSize;
        
        // Set color
        Renderer renderer = marker.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.5f);
        renderer.material = mat;
        
        // Make it always visible (no shadows, no culling)
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        // Add label text (using 3D text)
        GameObject labelObj = new GameObject("Label");
        labelObj.layer = markerLayer; // Also set label to marker layer
        labelObj.transform.SetParent(marker.transform);
        labelObj.transform.localPosition = new Vector3(0, vrMarkerSize * 0.7f, 0);
        labelObj.transform.localScale = Vector3.one * 0.5f;
        
        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = $"VR{vrIndex}";
        textMesh.fontSize = 50;
        textMesh.color = color;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        
        vrMarkers.Add(marker);
    }
    
    void UpdateVRMarkers()
    {
        // Update marker positions to stay above VR objects
        foreach (GameObject marker in vrMarkers)
        {
            if (marker != null && marker.transform.parent != null)
            {
                marker.transform.localPosition = new Vector3(0, vrMarkerHeight, 0);
            }
        }
    }
    
    void CreateLocustArrows()
    {
        // Find all locusts
        GameObject[] locusts = GameObject.FindGameObjectsWithTag("SimulatedLocust");
        
        int markerLayer = LayerMask.NameToLayer("OverheadCameraMarkers");
        if (markerLayer == -1) markerLayer = 8;
        
        foreach (GameObject locust in locusts)
        {
            // Create simple arrow pointing in direction of movement
            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            arrow.name = "Locust Arrow";
            arrow.layer = markerLayer; // Only visible to overhead camera
            arrow.transform.SetParent(locust.transform);
            arrow.transform.localPosition = new Vector3(0, 0.5f, 0);
            arrow.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            
            // Set color to light gray
            Renderer renderer = arrow.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", 0.3f);
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            
            // Point arrow in forward direction
            LocustMover mover = locust.GetComponent<LocustMover>();
            if (mover != null)
            {
                arrow.transform.rotation = locust.transform.rotation;
            }
            
            locustArrows.Add(arrow);
        }
        
        Debug.Log($"Created {locustArrows.Count} locust arrows.");
    }
    
    void AddFPSIndicator()
    {
        // Create FPS text label in top-right corner of screen (not on camera display)
        GameObject fpsObj = new GameObject("FPS Indicator");
        fpsObj.transform.SetParent(uiCanvas.transform, false);
        
        RectTransform fpsRect = fpsObj.AddComponent<RectTransform>();
        fpsRect.anchorMin = new Vector2(1, 1);
        fpsRect.anchorMax = new Vector2(1, 1);
        fpsRect.pivot = new Vector2(1, 1);
        fpsRect.anchoredPosition = new Vector2(-10, -10);
        fpsRect.sizeDelta = new Vector2(100, 30);
        
        Text fpsText = fpsObj.AddComponent<Text>();
        fpsText.text = "60 fps";
        fpsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        fpsText.fontSize = 16;
        fpsText.color = Color.white;
        fpsText.alignment = TextAnchor.UpperLeft;
        
        // Add background for better visibility
        GameObject bgObj = new GameObject("FPS Background");
        bgObj.transform.SetParent(fpsObj.transform, false);
        bgObj.transform.SetAsFirstSibling();
        
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = new Vector2(10, 5); // Slightly larger
        bgRect.anchoredPosition = Vector2.zero;
        
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);
        
        // Add FPS updater component
        FPSUpdater updater = fpsObj.AddComponent<FPSUpdater>();
        updater.fpsText = fpsText;
    }
    
    void OnDestroy()
    {
        // Clean up render texture
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
        
        // Clean up markers
        foreach (GameObject marker in vrMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }
        
        // Clean up arrows
        foreach (GameObject arrow in locustArrows)
        {
            if (arrow != null)
            {
                Destroy(arrow);
            }
        }
    }
}

// Simple FPS updater component
public class FPSUpdater : MonoBehaviour
{
    public Text fpsText;
    private float deltaTime = 0.0f;
    
    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        if (fpsText != null)
        {
            float fps = 1.0f / deltaTime;
            fpsText.text = string.Format("{0:0.} fps", fps);
        }
    }
}

