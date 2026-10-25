using System.Collections;
using UnityEngine;

/// <summary>Clear a retained HUD display once; no camera continues rendering after the washout.</summary>
public sealed class DisplayFrameClear : MonoBehaviour
{
    private Camera clearCamera;
    private int startedFrame;
    public int PresentedFrames { get; private set; }
    public static int Requests { get; private set; }
    public Camera ClearCamera => clearCamera;
    public static DisplayFrameClear ClearOnce(int display)
    {
        Requests++;
        Cancel(display);
        var root=new GameObject("One frame HUD washout");DontDestroyOnLoad(root);
        var clear=root.AddComponent<DisplayFrameClear>();clear.Initialize(display);return clear;
    }
    public static void Cancel(int display)
    {
        foreach(var clear in FindObjectsByType<DisplayFrameClear>(FindObjectsSortMode.None))
            if(clear.clearCamera!=null && clear.clearCamera.targetDisplay==display) {clear.clearCamera.enabled=false;Destroy(clear.gameObject);}
    }
    private void Initialize(int display)
    {
        startedFrame=Time.frameCount;
        clearCamera=gameObject.AddComponent<Camera>();clearCamera.targetDisplay=display;clearCamera.cullingMask=0;
        clearCamera.clearFlags=CameraClearFlags.SolidColor;clearCamera.backgroundColor=Color.black;
        // Clear before stimulus cameras, so hiding UI never presents a black LED frame.
        clearCamera.depth=-10000;clearCamera.allowHDR=false;clearCamera.allowMSAA=false;
        StartCoroutine(ReleaseAfterFrame());
    }
    private void OnPostRender() {PresentedFrames++;clearCamera.enabled=false;}
    private IEnumerator ReleaseAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        if(clearCamera!=null) clearCamera.enabled=false;
        Destroy(gameObject);
    }
    // End-of-frame coroutines can be suspended by editor/batch focus; never keep clearing in that case.
    private void LateUpdate() {if(Time.frameCount>startedFrame+1) {if(clearCamera!=null) clearCamera.enabled=false;Destroy(gameObject);}}
}
