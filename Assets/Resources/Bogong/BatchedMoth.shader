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
        // Keep composited target alpha correct for render-texture/UI previews.
        Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
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
                // A rudimentary 0.5mm blur edge for a 1cm body, retaining physical area.
                // It is the same coupled wing pair, not an extra silhouette or opacity floor.
                float blurWidth = _BodyDiameter * .05 * factor;
                wingArea += _WingSpan * factor * blurWidth * abs(forward.z) * scale * scale * _WingOpacity;
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
                float wingAlpha = _WingOpacity;
                if (v.part.x > .5 && v.part.x < 1.5)
                {
                    float normalZ = v.part.y < 0 ? abs(leftNormal.z) : abs(rightNormal.z);
                    float3 span = mul((float3x3)UNITY_MATRIX_V,
                        rotate(v.rotation,float3(v.part.y*cos(wingAngle),sin(wingAngle),0)));
                    if (normalZ < .05 && length(span.xy) > .1)
                    {
                        // Support a thin projected edge on the raster while reducing its
                        // opacity by its actual coverage. Front/back flapping no longer vanishes.
                        float width = max(blurWidth*abs(forward.z),_BodyLength*.6*factor*normalZ);
                        float widthPixels = width * scale;
                        float2 across = normalize(float2(-span.y,span.x));
                        local.z = 0;
                        view = center + mul((float3x3)UNITY_MATRIX_V,rotate(v.rotation,local));
                        view.xy += across * (v.local.z/.6) * max(1,widthPixels) / max(.0001,scale);
                        wingAlpha *= saturate(widthPixels);
                    }
                }
                if (v.part.x > 1.5)
                    view = center + float3(v.local.xy / max(.0001,scale) + wingShift.xy * wingArea / max(.000001,area+wingArea), 0);
                o.position = mul(UNITY_MATRIX_P, float4(view,1));
                o.proxy = v.local.xy;
                // A bilinear point footprint partitions area across 1-4 pixels without
                // spreading a subpixel moth's contrast across a fixed 3x3 blur.
                o.info = float4(v.part.x, area+wingArea, v.part.x > .5 && v.part.x < 1.5 ? wingAlpha : .8 + .2 * abs(v.local.y), extent);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                clip(i.visible);
                if (i.info.x > 1.5)
                {
                    float2 weight = saturate(1 - abs(i.proxy));
                    float coverage = saturate(weight.x * weight.y * i.info.y);
                    return float4(_Color.rgb, coverage);
                }
                if (i.info.x > .5) return float4(_Color.rgb, i.info.z);
                return float4(_Color.rgb * i.info.z, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
