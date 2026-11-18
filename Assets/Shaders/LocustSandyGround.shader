Shader "Locust/Sandy Textured Ground"
{
    Properties
    {
        _MainTex ("Ground detail", 2D) = "white" {}
        _GroundColor ("Sandy brown", Color) = (0.706, 0.604, 0.471, 1)
        _TextureStrength ("Texture contrast", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _GroundColor;
            half _TextureStrength;
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex); return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // Retain the authored spatial detail without its green chroma.
                half luminance = dot(tex2D(_MainTex, i.uv).rgb, half3(0.2126, 0.7152, 0.0722));
                half detail = lerp(1.0h, 2.0h * luminance, _TextureStrength);
                return fixed4(_GroundColor.rgb * detail, 1);
            }
            ENDCG
        }
    }
}
