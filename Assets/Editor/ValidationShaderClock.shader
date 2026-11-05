Shader "Hidden/MATREX/ValidationClock" {
SubShader { Pass { ZTest Always ZWrite Off Cull Off
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct input {float4 vertex:POSITION;};
float4 vert(input v):SV_POSITION{return UnityObjectToClipPos(v.vertex);}
float4 frag():SV_Target{return float4(_Time.y/10,unity_DeltaTime.x,(_SinTime.w+1)/2,1);}
ENDCG
} } }
