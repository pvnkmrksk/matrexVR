using System;
using System.IO;
using UnityEngine;

/// <summary>Pure GPU star-map projection, shared by stimulus recording and idle setup thumbnails. No file writes or sky ownership.</summary>
public sealed class NightSkyImageRenderer : IDisposable
{
    private Material material;
    public Matrix4x4 WorldToEquatorial { get; private set; }
    public Texture2D Render(NightSkyConfig config,DateTimeOffset sample,int width=0)
    {
        config.Validate();
        var source=Resources.Load<Texture2D>("NightSky/starmap_2020_4k");
        if(source==null)throw new FileNotFoundException("Bundled NASA night-sky map is missing.");
        int size=width==0?config.imageWidth:width;
        if(size<2||size%2!=0||size>SystemInfo.maxTextureSize)throw new ArgumentException("Night sky image width is outside this device's texture limit.");
        WorldToEquatorial=NightSkyOrientation.WorldToEquatorial(config,sample);
        if(material==null)
        {
            var shader=Resources.Load<Shader>("NightSky/Bake");
            if(shader==null||!shader.isSupported)throw new InvalidOperationException("NightSky/Bake shader is unavailable on this device.");
            material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        }
        material.SetMatrix("_WorldToEquatorial",WorldToEquatorial);material.SetFloat("_Exposure",config.exposure);
        material.SetFloat("_FaintDetailCutoff",config.faintDetailCutoff);material.SetFloat("_MaskBelowHorizon",config.maskBelowHorizon?1:0);
        var target=RenderTexture.GetTemporary(size,size/2,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var previous=RenderTexture.active;bool srgb=GL.sRGBWrite;Texture2D image=null;
        try
        {
            GL.sRGBWrite=false;Graphics.Blit(source,target,material,0);RenderTexture.active=target;
            image=new Texture2D(size,size/2,TextureFormat.RGBA32,false,false);image.ReadPixels(new Rect(0,0,size,size/2),0,0);image.Apply(false,false);
            image.wrapModeU=TextureWrapMode.Repeat;image.wrapModeV=TextureWrapMode.Clamp;image.filterMode=FilterMode.Bilinear;
            var result=image;image=null;return result;
        }
        finally{RenderTexture.active=previous;GL.sRGBWrite=srgb;RenderTexture.ReleaseTemporary(target);if(image!=null)Release(image);}
    }
    private static void Release(UnityEngine.Object target)
    {if(target==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(target);else UnityEngine.Object.DestroyImmediate(target);}
    public void Dispose(){Release(material);material=null;}
}
