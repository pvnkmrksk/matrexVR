Shader "Bogong/BatchedDot"
{
    Properties
    {
        _Color ("Solid color", Color) = (1,1,1,1)
        _Radius ("World radius", Float) = 0.5
        _Angular ("Angular sizing", Float) = 1
        _TangentHalfAngle ("Angular radius tangent", Float) = 0.017455
        _FlickerFrequency ("Flicker frequency", Float) = 0
        _FlickerDuty ("Flicker duty cycle", Float) = 1
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
            float _Radius, _Angular, _TangentHalfAngle, _FlickerFrequency, _FlickerDuty;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 phase : TEXCOORD1; };
            struct v2f { float4 position : SV_POSITION; float2 circle : TEXCOORD0; float visible : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                float3 center = mul(UNITY_MATRIX_V, float4(v.vertex.xyz, 1)).xyz;
                float radius = _Angular > .5 ? length(center) * _TangentHalfAngle : _Radius;
                float3 normal = normalize(-center);
                if (unity_OrthoParams.w > .5) normal = float3(0,0,1);
                float3 right = cross(float3(0,1,0), normal);
                if (dot(right,right) < 1e-8) right = float3(1,0,0);
                right = normalize(right);
                float3 up = cross(normal, right);
                o.circle = (v.uv - .5) * 3;
                float3 view = center + radius * (right * o.circle.x + up * o.circle.y);
                o.position = mul(UNITY_MATRIX_P, float4(view,1));
                o.visible = _FlickerFrequency <= 0 || frac((_Time.y + v.phase.x) * _FlickerFrequency) < _FlickerDuty ? 1 : -1;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                clip(i.visible);
                clip(1 - dot(i.circle,i.circle));
                return float4(_Color.rgb, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
