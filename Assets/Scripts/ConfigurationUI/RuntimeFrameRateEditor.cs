using System;
using UnityEngine;

/// <summary>The live cap is an application display setting; editing never stops acquisition.</summary>
public sealed class RuntimeFrameRateEditor : MonoBehaviour
{
    private static RuntimeFrameRateEditor instance;
    public static bool IsOpen => instance!=null;
    private string value,error;
    private Texture2D background;
    private GUIStyle box,label,field,button;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void Reset()=>instance=null;
    public static void Open()
    {
        if(instance!=null)return;
        var host=new GameObject("Frame rate setting");instance=host.AddComponent<RuntimeFrameRateEditor>();
        instance.value=(MainController.Instance?.TargetFrameRate??Application.targetFrameRate).ToString();
    }
    private void LateUpdate()
    {
        var keyboard=UnityEngine.InputSystem.Keyboard.current;
        if(keyboard?.escapeKey.wasReleasedThisFrame==true)Destroy(gameObject);
    }
    private void OnGUI()
    {
        float scale=Mathf.Min(DesktopDisplay.UiScale,Screen.width/370f,Screen.height/214f);
        if(box==null)
        {
            background=new Texture2D(1,1);background.SetPixel(0,0,new Color(.07f,.09f,.11f,.99f));background.Apply();
            box=new GUIStyle(GUI.skin.box);box.normal.background=background;
            label=new GUIStyle(GUI.skin.label){wordWrap=true};field=new GUIStyle(GUI.skin.textField);button=new GUIStyle(GUI.skin.button);
        }
        label.fontSize=field.fontSize=button.fontSize=Mathf.RoundToInt(14*scale);
        var desired=new Rect((Screen.width-350*scale)/2,(Screen.height-194*scale)/2,350*scale,194*scale);
        OperatorOverlayLayout.Reserve("frame-rate-editor",0,desired,out Rect placed,allowOverlapWhenNoSpace:true);
        var area=new Rect(placed.x,Screen.height-placed.yMax,placed.width,placed.height);
        GUI.Box(area,GUIContent.none,box);
        float x=area.x+16*scale,y=area.y+12*scale,w=area.width-32*scale;
        GUI.Label(new Rect(x,y,w,25*scale),"Frame-rate cap",label);y+=32*scale;
        int sync=MainController.Instance?.VSyncCount??QualitySettings.vSyncCount;
        bool oldEnabled=GUI.enabled;GUI.enabled=sync==0;
        GUI.SetNextControlName("live-frame-rate-value");
        value=GUI.TextField(new Rect(x,y,100*scale,29*scale),value,12,field);
        GUI.enabled=oldEnabled;
        GUI.Label(new Rect(x+112*scale,y,w-112*scale,32*scale),"FPS · -1 = uncapped",label);y+=38*scale;
        GUI.Label(new Rect(x,y,w,36*scale),sync>0?"VSync is active. It controls presentation timing; change VSync in system settings.":"Applies immediately to this session. Your saved experiment configuration is retained.",label);y+=43*scale;
        if(!string.IsNullOrEmpty(error))GUI.Label(new Rect(x,area.y+160*scale,w,30*scale),error,label);
        GUI.enabled=oldEnabled&&sync==0;
        if(GUI.Button(new Rect(x,y,100*scale,29*scale),"Apply",button))
        {
            try
            {
                if(!int.TryParse(value,out int target)||(target!=-1&&target<=0))throw new ArgumentException("Use positive FPS or -1.");
                var main=MainController.Instance;
                if(main!=null)main.SetRuntimeFrameRate(target);else Application.targetFrameRate=target;
                Destroy(gameObject);
            }
            catch(Exception failure){error=failure.Message;}
        }
        GUI.enabled=oldEnabled;
        if(GUI.Button(new Rect(area.xMax-116*scale,y,100*scale,29*scale),"Close",button))Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if(instance==this)instance=null;
        OperatorOverlayLayout.Release("frame-rate-editor");
        if(background!=null)Destroy(background);
    }
}
