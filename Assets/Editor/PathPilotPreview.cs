#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated preview scene: editing appearance never starts the experiment or tracker.</summary>
public sealed class PathPilotPreview : EditorWindow
{
    public const string AppearanceFile = "Experiments/PathAnglePilot/appearance.json";
    public static readonly int[] Angles = {40,55,70,85,100,120,150,180,210};
    private PathGroundAppearance appearance;
    private int selected = 3;
    private bool noBars;
    private Scene scene;
    private GameObject root;
    private Camera camera;
    private Material barMaterial;
    private RenderTexture overhead, antView;
    private string error;
    private Vector2 scroll;

    [MenuItem("Ant experiments/Adjust path-angle pilot appearance")]
    public static void Open() => GetWindow<PathPilotPreview>("Path-angle pilot");
    private void OnEnable()
    {
        minSize = new Vector2(660,680);
        try { appearance = JsonConvert.DeserializeObject<PathGroundAppearance>(File.ReadAllText(ExperimentConfigFiles.Resolve(AppearanceFile))); }
        catch { appearance = new PathGroundAppearance(); }
        Rebuild();
    }
    private void OnDisable() => Clear();
    private void Clear()
    {
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        if (barMaterial != null) DestroyImmediate(barMaterial);
        foreach (var target in new[] { overhead, antView }) if (target != null) { target.Release(); DestroyImmediate(target); }
        root = null; camera = null;
    }
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Path-angle pilot · appearance preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Provisional appearance. Select an angle, tune the ground, then Save appearance for all pilot conditions. This preview does not start a session.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        selected = GUILayout.SelectionGrid(selected, Array.ConvertAll(Angles,a=>a+"°"), 9);
        noBars = EditorGUILayout.Toggle("85° no-bars condition", noBars);
        appearance.pathWidthCm = EditorGUILayout.Slider("Path width (cm)", appearance.pathWidthCm, .5f, 12);
        appearance.pathDarkness = EditorGUILayout.Slider("Path darkness", appearance.pathDarkness, 0, 1);
        appearance.groundBrightness = EditorGUILayout.Slider("Ground brightness", appearance.groundBrightness, .05f, 1);
        appearance.textureScaleCm = EditorGUILayout.Slider("Grass scale (cm)", appearance.textureScaleCm, 1, 30);
        appearance.extentCm = EditorGUILayout.FloatField("Ground extent (cm)", appearance.extentCm);
        appearance.groundHeightCm = EditorGUILayout.FloatField("Ground height (cm)", appearance.groundHeightCm);
        appearance.texturePath = EditorGUILayout.TextField("Optional texture path", appearance.texturePath);
        appearance.preparedGroundImage = EditorGUILayout.Toggle("Image includes painted paths", appearance.preparedGroundImage);
        if (EditorGUI.EndChangeCheck()) Rebuild();
        EditorGUILayout.HelpBox("Texture paths are relative to StreamingAssets (or absolute). A grass image repeats at Grass scale; a prepared ground/path image covers the entire ground square. Its centre maps to the starting position, top to +z. Per-condition prepared images can be set in design.json.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save appearance"))
            {
                try {
                    Rebuild();
                    if (!string.IsNullOrEmpty(error)) throw new ArgumentException(error);
                    appearance.Validate();
                    File.WriteAllText(ExperimentConfigFiles.Resolve(AppearanceFile),JsonConvert.SerializeObject(appearance,Formatting.Indented)+"\n");
                    AssetDatabase.Refresh(); error=null; ShowNotification(new GUIContent("Saved for the next pilot session"));
                } catch (Exception e) {error=e.Message;}
            }
            if (GUILayout.Button("Restore initial appearance")) {appearance=new PathGroundAppearance();Rebuild();}
        }
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error,MessageType.Error);
        float angle = noBars ? 85 : Angles[selected];
        EditorGUILayout.LabelField($"{angle:0}° opening · fork z = {PathGround.ForkZ(angle):F2} cm · goals ±20° at 60 cm");
        EditorGUILayout.LabelField("Overhead (+z up)                                     Ant view (start pose)",EditorStyles.boldLabel);
        Rect row = GUILayoutUtility.GetRect(640,320,GUILayout.ExpandWidth(true));
        float width = Mathf.Min(row.width/2-4,360);
        if (overhead != null) GUI.DrawTexture(new Rect(row.x,row.y,width,width),overhead,ScaleMode.ScaleToFit);
        if (antView != null) GUI.DrawTexture(new Rect(row.x+row.width/2,row.y,width,width),antView,ScaleMode.ScaleToFit);
        EditorGUILayout.EndScrollView();
    }
    private void Rebuild()
    {
        Clear(); error=null;
        try
        {
            appearance.Validate();
            scene=EditorSceneManager.NewPreviewScene();
            root=new GameObject("Pilot preview"); SceneManager.MoveGameObjectToScene(root,scene);
            float angle=noBars?85:Angles[selected];
            PathGround.Create(root.transform,0,new PathGroundConfig{openingAngle=angle},appearance,false);
            barMaterial = new Material(Shader.Find("Unlit/Color"));barMaterial.color=Color.black;
            if (!noBars) foreach (int side in new[] {-1,1})
            {
                var bar=GameObject.CreatePrimitive(PrimitiveType.Cylinder);bar.transform.SetParent(root.transform);
                bar.transform.position=new Vector3(side*PathGround.RightGoal.x,0,PathGround.RightGoal.y);
                float diameter=2*60*Mathf.Tan(5*Mathf.Deg2Rad);
                bar.transform.localScale=new Vector3(diameter,diameter*7,diameter);
                bar.GetComponent<Renderer>().sharedMaterial=barMaterial;
                DestroyImmediate(bar.GetComponent<Collider>());
            }
            var host=new GameObject("Preview camera");host.transform.SetParent(root.transform);camera=host.AddComponent<Camera>();
            camera.enabled=false;camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.8f,.8f);
            camera.nearClipPlane=.01f;camera.farClipPlane=1000;camera.aspect=1;
            overhead=new RenderTexture(640,640,24);antView=new RenderTexture(640,640,24);
            // Above the tall bars so both endpoints and the reflex fork are visible.
            camera.transform.SetPositionAndRotation(new Vector3(0,250,30),Quaternion.Euler(90,0,0));
            camera.orthographic=true;camera.orthographicSize=45;camera.targetTexture=overhead;camera.Render();
            camera.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            camera.orthographic=false;camera.fieldOfView=90;camera.targetTexture=antView;camera.Render();
            camera.targetTexture=null;
        } catch(Exception e) {error=e.Message;}
        Repaint();
    }
    public static void ExportImages(string output)
    {
        Directory.CreateDirectory(output);
        var preview=CreateInstance<PathPilotPreview>();
        try {
            foreach(int index in new[] {0,1,2,3,4,5,6,7,8,9}) {
                preview.selected=index==9?3:index;preview.noBars=index==9;preview.Rebuild();
                if(preview.error!=null)throw new Exception(preview.error);
                string name=index==9?"085-no-bars":Angles[index].ToString("000");
                Save(preview.overhead,Path.Combine(output,name+"-overhead.png"));
                Save(preview.antView,Path.Combine(output,name+"-ant.png"));
            }
        } finally {DestroyImmediate(preview);}
    }
    private static void Save(RenderTexture target,string path)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;
        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
        DestroyImmediate(pixels);RenderTexture.active=previous;
    }
}
#endif
