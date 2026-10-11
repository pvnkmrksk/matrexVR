Shader "Bogong/BatchedMoth"
{
    Properties
    {
        _Color ("Body albedo", Color) = (.12,.12,.12,1)
        _BodyLength ("Body length", Float) = 2.54
        _BodyDiameter ("Body diameter", Float) = 1
        _WingSpan ("Wing span", Float) = 4
        _FlapFrequency ("Wing frequency Hz", Float) = 30
        _WingOpacity ("Wing blur opacity", Range(0,1)) = .25
        _Subpixel ("Subpixel contrast", Float) = 1
        _Angular ("Angular size", Float) = 0
        _TangentHalfAngle ("Angular radius tangent", Float) = .01
        _FlickerFrequency ("Flicker Hz", Float) = 0
        _FlickerDuty ("Flicker duty", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _PanelPixels;
            float _BodyLength, _BodyDiameter, _WingSpan, _FlapFrequency, _WingOpacity, _Subpixel;
            float _Angular, _TangentHalfAngle, _FlickerFrequency, _FlickerDuty;
            float _ManualAnimation, _AnimationTime;
            struct appdata { float4 vertex:POSITION; float4 rotation:TANGENT; float2 part:TEXCOORD0; float2 phase:TEXCOORD1; float3 local:TEXCOORD2; };
            struct v2f { float4 position:SV_POSITION; float4 info:TEXCOORD0; float2 proxy:TEXCOORD1; float visible:TEXCOORD2; };
            float3 rotate(float4 q, float3 p) { return p + 2 * cross(q.xyz, cross(q.xyz,p) + q.w*p); }
            v2f vert(appdata v)
            {
                v2f o;
                float3 center = mul(UNITY_MATRIX_V, float4(v.vertex.xyz,1)).xyz;
                float factor = _Angular > .5 ? 2 * length(center) * _TangentHalfAngle / _BodyLength : 1;
                float3 radii = float3(_BodyDiameter,_BodyDiameter,_BodyLength) * .5 * factor;
                float3 forward = mul((float3x3)UNITY_MATRIX_V, rotate(v.rotation,float3(0,0,1)));
                // GPU projection may invert Y for a render texture; pixel scale is a magnitude.
                float scale = unity_OrthoParams.w > .5 ? .5 * _PanelPixels.y * abs(UNITY_MATRIX_P[1][1])
                    : .5 * _PanelPixels.y * abs(UNITY_MATRIX_P[1][1]) / max(.0001,-center.z);
                float major = sqrt(lerp(radii.x*radii.x, radii.z*radii.z, saturate(1-forward.z*forward.z)));
                float extent = major * scale;
                float area = UNITY_PI * major * radii.x * scale * scale;
                float clock = _ManualAnimation > .5 ? _AnimationTime : _Time.y;
                float flap = sin(UNITY_TWO_PI * (clock * _FlapFrequency + v.phase.x));
                float wingAngle = flap * 1.05;
                float3 leftNormal = mul((float3x3)UNITY_MATRIX_V, rotate(v.rotation,float3(sin(wingAngle),cos(wingAngle),0)));
                float3 rightNormal = mul((float3x3)UNITY_MATRIX_V, rotate(v.rotation,float3(-sin(wingAngle),cos(wingAngle),0)));
                float wingArea = _WingSpan * _BodyLength * .6 * factor * factor *
                    .5 * (abs(leftNormal.z)+abs(rightNormal.z)) * scale * scale * _WingOpacity;
                // Keep faint flap motion below one pixel as well as in the near-field geometry.
                float3 wingShift = mul((float3x3)UNITY_MATRIX_V,
                    rotate(v.rotation,float3(0,_WingSpan*.25*factor*sin(wingAngle),0)));
                bool proxy = _Subpixel > .5 && extent < 1.5;
                o.visible = v.phase.y > .5 && (v.part.x > 1.5 ? proxy : !proxy) ? 1 : -1;
                if (_FlickerFrequency > 0 && frac((_Time.y + v.phase.x) * _FlickerFrequency) >= _FlickerDuty) o.visible = -1;
                float3 local = v.local * radii;
                if (v.part.x > .5 && v.part.x < 1.5)
                {
                    local.x = v.local.x * _WingSpan * .5 * factor * cos(wingAngle);
                    local.y = abs(v.local.x) * _WingSpan * .5 * factor * sin(wingAngle);
                    local.z = v.local.z * _BodyLength * factor;
                }
                float3 view = center + mul((float3x3)UNITY_MATRIX_V, rotate(v.rotation,local));
                if (v.part.x > 1.5)
                    view = center + float3(v.local.xy * 1.5 / max(.0001,scale) + wingShift.xy * _WingOpacity, 0);
                o.position = mul(UNITY_MATRIX_P, float4(view,1));
                o.proxy = v.local.xy * 1.5;
                // A Gaussian integrates to one pixel; its peak scales with true projected area.
                o.info = float4(v.part.x, saturate((area+wingArea) / UNITY_PI), .8 + .2 * abs(v.local.y), extent);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                clip(i.visible);
                if (i.info.x > 1.5)
                {
                    float coverage = exp(-dot(i.proxy,i.proxy)) * i.info.y;
                    return float4(_Color.rgb, coverage);
                }
                if (i.info.x > .5) return float4(_Color.rgb, _WingOpacity);
                return float4(_Color.rgb * i.info.z, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
