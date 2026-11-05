Shader "AntExperiments/PathGround"
{
    Properties { _GroundTexture("Ground texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _GroundTexture;
            float _Extent, _ForkZ, _PathWidth, _PathDarkness, _Brightness, _TileCm, _UseTexture, _PreparedImage;
            float4 _Goal;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 p : TEXCOORD1; };
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.p=v.vertex.xz; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) { float2 i=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y); }
            float segment(float2 p, float2 a, float2 b) { float2 d=b-a; float t=saturate(dot(p-a,d)/max(dot(d,d),0.000001)); return length(p-a-t*d); }
            fixed4 frag(v2f i) : SV_Target
            {
                if (_PreparedImage > .5) return tex2D(_GroundTexture,i.uv);
                float2 uv=i.p/_TileCm;
                // Deterministic monochrome, fine elongated flecks with coarser grass-like variation.
                float grass=.55+.30*noise(uv*5)+.15*noise(uv*float2(75,14));
                float3 baseColor = _UseTexture > .5 ? tex2D(_GroundTexture,uv).rgb : grass.xxx;
                baseColor *= _Brightness;
                float2 fork=float2(0,_ForkZ);
                float distanceToPath=min(segment(i.p,0,fork),min(segment(i.p,fork,_Goal.xy),segment(i.p,fork,float2(-_Goal.x,_Goal.y))));
                float aa=max(fwidth(distanceToPath),.015);
                float path=1-smoothstep(_PathWidth*.5-aa,_PathWidth*.5+aa,distanceToPath);
                return float4(baseColor*(1-path*_PathDarkness),1);
            }
            ENDCG
        }
    }
}
