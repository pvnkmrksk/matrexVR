Shader "Bogong/Dot"
{
    Properties
    {
        _Color ("Solid color", Color) = (1,1,1,1)
        _Radius ("Radius", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "DisableBatching"="True" }
        Cull Off ZWrite On ZTest LEqual Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            float _Radius;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 circle : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                float3 center = UnityObjectToViewPos(float3(0,0,0));
                float3 normal = normalize(-center);
                if (unity_OrthoParams.w > .5) normal = float3(0,0,1);
                float3 right = cross(float3(0,1,0), normal);
                if (dot(right,right) < 1e-8) right = float3(1,0,0);
                right = normalize(right);
                float3 up = cross(normal, right);
                // Pad beyond the circle: polygon edges must not soften its silhouette.
                o.circle = (v.uv - .5) * 3;
                float3 view = center + _Radius * (right * o.circle.x + up * o.circle.y);
                o.position = mul(UNITY_MATRIX_P, float4(view,1));
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                clip(1 - dot(i.circle,i.circle));
                return float4(_Color.rgb, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
