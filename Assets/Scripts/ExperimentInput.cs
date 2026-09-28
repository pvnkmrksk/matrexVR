using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shared operator actions. Each rig reads the same frame; no per-rig device pairing.</summary>
public static class ExperimentInput
{
    private static InputActionAsset actions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        if (actions != null) { actions.Disable(); UnityEngine.Object.Destroy(actions); }
        actions = null;
        SceneManager.sceneLoaded -= SceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // Keep mouse wheel units consistent across desktop platforms.
        InputSystem.settings.scrollDeltaBehavior = InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
        EnsureActions();
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private static void EnsureActions()
    {
        if (actions != null) return;
        actions = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("ExperimentControls"));
        actions.Enable();
    }

    private static InputAction Action(string name)
    {
        EnsureActions();
        return actions.FindAction(name, true);
    }

    // Experiment hotkeys must not move animals or stop trials while entering metadata.
    public static bool IsEditingText
    {
        get
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var input = selected.GetComponent<InputField>();
            var tmp = selected.GetComponent<TMPro.TMP_InputField>();
            return (input != null && input.isFocused) || (tmp != null && tmp.isFocused);
        }
    }

    public static bool Pressed(string name) => !IsEditingText && Action(name).WasPressedThisFrame();
    public static bool Released(string name) => !IsEditingText && Action(name).WasReleasedThisFrame();
    public static bool Held(string name) => !IsEditingText && Action(name).IsPressed();
    public static float Axis(string name) => IsEditingText ? 0 : Action(name).ReadValue<float>();
    public static Vector2 Move => IsEditingText ? Vector2.zero : Action("Move").ReadValue<Vector2>();
    public static Vector3 MousePosition => Action("Pointer").ReadValue<Vector2>();
    // Unity 6 normalizes a wheel tick to 1; legacy zoom tuning expects 0.1 per tick.
    // Fractional trackpad deltas stay proportional. Do not apply the old Windows /120 scale.
    public static float Scroll => Action("Scroll").ReadValue<Vector2>().y * 0.1f;

    public static bool Pressed(KeyCode configured, KeyCode original, string action)
    {
        if (configured == original) return Pressed(action);
        if (IsEditingText || UnityEngine.InputSystem.Keyboard.current == null) return false;
        string name = configured.ToString().Replace("Arrow", "");
        if (name.StartsWith("Alpha")) name = "Digit" + name.Substring(5);
        if (name.StartsWith("Keypad")) name = "Numpad" + name.Substring(6);
        if (name == "Return") name = "Enter";
        return Enum.TryParse(name, true, out Key key) && key != Key.None &&
            UnityEngine.InputSystem.Keyboard.current[key].wasPressedThisFrame;
    }

    private static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var eventSystem in root.GetComponentsInChildren<EventSystem>(true)) UpgradeUI(eventSystem);
    }

    public static void UpgradeUI(EventSystem eventSystem)
    {
        foreach (var legacy in eventSystem.GetComponents<StandaloneInputModule>())
        {
            legacy.enabled = false;
            UnityEngine.Object.Destroy(legacy);
        }
        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }
}
