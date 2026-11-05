Shader "Hidden/MATREX/ValidationShadowClock" {
SubShader { Pass { ZTest Always ZWrite Off Cull Off
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
float4 frag(v2f_img input):SV_Target{return float4(_Time.y/10,unity_DeltaTime.x,(_SinTime.w+1)/2,1);}
ENDCG
} } }
