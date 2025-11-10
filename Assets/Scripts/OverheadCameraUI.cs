using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI controller for the overhead camera reset button
/// </summary>
public class OverheadCameraUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the OverheadCameraController")]
    public OverheadCameraController cameraController;
    
    [Header("UI Settings")]
    [Tooltip("Button to reset camera view")]
    public Button resetButton;
    
    [Tooltip("Button text (optional)")]
    public Text buttonText;
    
    void Start()
    {
        // Find camera controller if not assigned
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<OverheadCameraController>();
        }
        
        // Setup button if assigned
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetButtonClicked);
        }
        
        // Set button text if assigned
        if (buttonText != null)
        {
            buttonText.text = "Reset View";
        }
    }
    
    void OnResetButtonClicked()
    {
        if (cameraController != null)
        {
            cameraController.ResetToSkyView();
        }
        else
        {
            Debug.LogWarning("OverheadCameraController not found!");
        }
    }
    
    void OnDestroy()
    {
        // Clean up button listener
        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(OnResetButtonClicked);
        }
    }
}

