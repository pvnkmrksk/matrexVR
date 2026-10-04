Shader "NightSky/Panorama"
{
    Properties { _MainTex ("Panorama", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        // Same panorama convention as Unity Skybox/Panoramic: +X at u=.5, +Z at u=.25.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float2 uv = float2(.5 - atan2(d.z, d.x) / (2 * UNITY_PI), .5 + asin(clamp(d.y,-1,1)) / UNITY_PI);
                return float4(tex2D(_MainTex, uv).rgb, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
