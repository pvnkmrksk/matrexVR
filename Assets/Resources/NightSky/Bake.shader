Shader "NightSky/Bake"
{
    Properties { _MainTex ("Celestial map", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off
        // Bake the library's observer rotation into a reusable horizontal panorama.
        Pass
        {
            ZTest Always
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment bake
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4x4 _WorldToEquatorial;
            float _Exposure;
            float _FaintDetailCutoff;
            float _MaskBelowHorizon;
            float4 bake(v2f_img i) : SV_Target
            {
                float lon = (.5 - i.uv.x) * (2 * UNITY_PI);
                float lat = (i.uv.y - .5) * UNITY_PI;
                float3 world = float3(cos(lat)*cos(lon), sin(lat), cos(lat)*sin(lon));
                if (_MaskBelowHorizon > .5 && world.y < 0) return float4(0,0,0,1);
                float3 eq = normalize(mul((float3x3)_WorldToEquatorial, world));
                // NASA: RA=0 at centre, increasing leftward; north celestial pole at top.
                float2 uv = float2(.5 - atan2(eq.y,eq.x)/(2*UNITY_PI), .5 + asin(clamp(eq.z,-1,1))/UNITY_PI);
                // The EXR is linear. Bake explicit sRGB bytes, independent of project colour space.
                float3 source = tex2D(_MainTex, uv).rgb;
                if (_FaintDetailCutoff > 0)
                    source *= smoothstep(_FaintDetailCutoff, 2 * _FaintDetailCutoff, max(source.r, max(source.g, source.b)));
                float3 rgb = LinearToGammaSpace(saturate(source * _Exposure));
                return float4(rgb,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
