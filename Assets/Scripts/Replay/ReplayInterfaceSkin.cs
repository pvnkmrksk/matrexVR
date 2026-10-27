using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Replay-only IMGUI artwork and state colors. Owns no input, layout or playback state.</summary>
internal sealed class ReplayInterfaceSkin : IDisposable
{
    public GUISkin Skin { get; }
    public GUIStyle Title { get; }
    public GUIStyle Small { get; }
    public GUIStyle PrimaryButton { get; }
    private readonly List<Texture2D> textures=new List<Texture2D>();
    private readonly Font semibold;
    private float appliedScale=-1;
    private bool disposed;
    private static readonly Color Text=new Color32(36,52,68,255),Muted=new Color32(94,112,129,255);
    private static readonly Color Teal=new Color32(23,111,99,255),TealDark=new Color32(18,90,81,255);
    private static readonly Color Border=new Color32(191,205,216,255),Focus=new Color32(35,121,167,255);
    public ReplayInterfaceSkin(GUISkin source)
    {
        Skin=UnityEngine.Object.Instantiate(source);Skin.name="MATREX replay interface";
        Skin.font=Resources.Load<Font>("ConfigurationUI/Fonts/Inter-Regular")??source.font;
        semibold=Resources.Load<Font>("ConfigurationUI/Fonts/Inter-SemiBold")??Skin.font;
        Title=new GUIStyle(Skin.label){fontStyle=FontStyle.Bold};
        Small=new GUIStyle(Skin.label){wordWrap=true};
        PrimaryButton=new GUIStyle(Skin.button){font=semibold,fontStyle=FontStyle.Normal};
    }
    public void ApplyScale(float scale)
    {
        if(disposed||Mathf.Approximately(appliedScale,scale))return;
        appliedScale=scale;ClearTextures();
        int P(float value)=>Mathf.Max(1,Mathf.RoundToInt(value*scale));
        var panel=Rounded(P(48),P(48),P(7),Color.white,Border,P(1));
        var normal=Rounded(P(40),P(40),P(5),Color.white,Border,P(1));
        var hover=Rounded(P(40),P(40),P(5),new Color32(233,241,247,255),new Color32(88,133,164,255),P(1));
        var active=Rounded(P(40),P(40),P(5),new Color32(220,235,244,255),Focus,P(1));
        var focused=Rounded(P(40),P(40),P(5),new Color32(244,250,254,255),Focus,P(1));
        var selected=Rounded(P(40),P(40),P(5),new Color32(225,241,237,255),new Color32(94,159,144,255),P(1));
        var primary=Rounded(P(40),P(40),P(5),Teal,Teal,P(1));
        var primaryHover=Rounded(P(40),P(40),P(5),TealDark,TealDark,P(1));
        var primaryFocus=Rounded(P(40),P(40),P(5),Teal,Focus,P(2));
        States(Skin.box,panel,panel,panel,panel,Text);Skin.box.border=new RectOffset(P(8),P(8),P(8),P(8));
        Skin.box.padding=new RectOffset(P(12),P(12),P(10),P(10));
        States(Skin.label,null,null,null,null,Text);Skin.label.padding=new RectOffset();
        States(Title,null,null,null,null,Text);States(Small,null,null,null,null,Muted);
        States(Skin.button,normal,hover,active,focused,Text,selected,Teal);
        States(PrimaryButton,primary,primaryHover,primaryHover,primaryFocus,Color.white);
        States(Skin.textField,normal,hover,normal,focused,Text);
        States(Skin.textArea,normal,hover,normal,focused,Text);Skin.textArea.wordWrap=true;
        foreach(var style in new[]{Skin.button,PrimaryButton,Skin.textField,Skin.textArea})
        {
            style.border=new RectOffset(P(6),P(6),P(6),P(6));style.clipping=TextClipping.Clip;
            style.font=style==PrimaryButton?semibold:Skin.font;style.fontSize=P(14);
        }
        PrimaryButton.fixedHeight=30*scale;PrimaryButton.alignment=TextAnchor.MiddleCenter;
        PrimaryButton.padding=new RectOffset(P(8),P(8),P(5),P(5));
        var checkOff=Checkbox(P(32),false,Border,Color.white);
        var checkHover=Checkbox(P(32),false,Focus,new Color32(233,241,247,255));
        var checkOn=Checkbox(P(32),true,Teal,Teal);
        var checkOnHover=Checkbox(P(32),true,TealDark,TealDark);
        States(Skin.toggle,checkOff,checkHover,checkHover,checkHover,Text,checkOn,Teal);
        Skin.toggle.onHover.background=checkOnHover;Skin.toggle.onActive.background=checkOnHover;Skin.toggle.onFocused.background=checkOnHover;
        Skin.toggle.border=new RectOffset(P(21),0,0,0);Skin.toggle.padding=new RectOffset(P(25),0,0,0);
        var rail=Rail(P(32),P(18),new Color32(190,208,221,255),false);
        var verticalRail=Rail(P(18),P(32),new Color32(222,231,238,255),true);
        var thumb=Rounded(P(22),P(22),P(10),new Color32(40,127,136,255),new Color32(40,127,136,255),0);
        var thumbHover=Rounded(P(22),P(22),P(10),TealDark,TealDark,0);
        States(Skin.horizontalSlider,rail,rail,rail,rail,Text);Skin.horizontalSlider.border=new RectOffset(P(4),P(4),0,0);Skin.horizontalSlider.padding=new RectOffset();
        States(Skin.horizontalSliderThumb,thumb,thumbHover,thumbHover,thumbHover,Text);Skin.horizontalSliderThumb.border=new RectOffset(P(8),P(8),P(8),P(8));
        States(Skin.verticalScrollbar,verticalRail,verticalRail,verticalRail,verticalRail,Text);Skin.verticalScrollbar.border=new RectOffset(0,0,P(4),P(4));
        States(Skin.verticalScrollbarThumb,normal,hover,active,focused,Text);Skin.verticalScrollbarThumb.border=new RectOffset(P(5),P(5),P(5),P(5));
        Skin.verticalScrollbarUpButton.fixedHeight=0;Skin.verticalScrollbarDownButton.fixedHeight=0;
        Skin.settings.cursorColor=Text;Skin.settings.selectionColor=new Color(.20f,.51f,.70f,.28f);
    }
    private static void State(GUIStyleState state,Texture2D background,Color text)
    {
        state.background=background;
#if UNITY_EDITOR
        // Scaled backgrounds are an Editor-only API. Clear inherited artwork
        // there; the standalone skin uses the assigned background directly.
        state.scaledBackgrounds=Array.Empty<Texture2D>();
#endif
        state.textColor=text;
    }
    private static void States(GUIStyle style,Texture2D normal,Texture2D hover,Texture2D active,Texture2D focused,Color text,Texture2D selected=null,Color? selectedText=null)
    {
        State(style.normal,normal,text);State(style.hover,hover,text);State(style.active,active,text);State(style.focused,focused,text);
        State(style.onNormal,selected??normal,selectedText??text);State(style.onHover,selected??hover,selectedText??text);
        State(style.onActive,selected??active,selectedText??text);State(style.onFocused,selected??focused,selectedText??text);
    }
    private Texture2D Rounded(int width,int height,float radius,Color fill,Color border,float stroke)
    {
        var pixels=new Color32[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            float dx=Mathf.Abs(x+.5f-width*.5f)-(width*.5f-radius),dy=Mathf.Abs(y+.5f-height*.5f)-(height*.5f-radius);
            float distance=Mathf.Min(Mathf.Max(dx,dy),0)+Mathf.Sqrt(Mathf.Max(dx,0)*Mathf.Max(dx,0)+Mathf.Max(dy,0)*Mathf.Max(dy,0))-radius;
            Color color=Color.Lerp(fill,border,stroke>0?Mathf.Clamp01(distance+stroke+.5f):0);color.a*=Mathf.Clamp01(.5f-distance);pixels[y*width+x]=color;
        }
        return Texture(width,height,pixels);
    }
    private Texture2D Rail(int width,int height,Color color,bool vertical)
    {
        var pixels=new Color32[width*height];float scale=Mathf.Max(1,appliedScale),center=(vertical?width:height)*.5f;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {Color point=color;point.a*=Mathf.Clamp01(2*scale-Mathf.Abs((vertical?x:y)+.5f-center));pixels[y*width+x]=point;}
        return Texture(width,height,pixels);
    }
    private Texture2D Checkbox(int size,bool on,Color border,Color fill)
    {
        var pixels=new Color32[size*size];float unit=size/32f;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float px=(x+.5f)/unit,py=(y+.5f)/unit;
            if(px<2||px>18||py<8||py>24)continue;
            Color color=px<3||px>17||py<9||py>23?border:fill;
            if(on&&(SegmentDistance(px,py,5,16,9,12)<1.2f||SegmentDistance(px,py,9,12,15,20)<1.2f))color=Color.white;
            pixels[y*size+x]=color;
        }
        return Texture(size,size,pixels);
    }
    private static float SegmentDistance(float x,float y,float ax,float ay,float bx,float by)
    {
        float dx=bx-ax,dy=by-ay,t=Mathf.Clamp01(((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy));
        return Mathf.Sqrt((x-ax-t*dx)*(x-ax-t*dx)+(y-ay-t*dy)*(y-ay-t*dy));
    }
    private Texture2D Texture(int width,int height,Color32[] pixels)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="MATREX replay UI",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels32(pixels);texture.Apply(false,true);textures.Add(texture);return texture;
    }
    private void ClearTextures(){foreach(var texture in textures)if(texture!=null)UnityEngine.Object.Destroy(texture);textures.Clear();}
    public void Dispose()
    {if(disposed)return;disposed=true;ClearTextures();if(Skin!=null)UnityEngine.Object.Destroy(Skin);}
}
