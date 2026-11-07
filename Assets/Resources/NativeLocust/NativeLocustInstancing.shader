Shader "MATREX/Native Locust Instanced"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1
        _UVSec ("Secondary UV", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            ZWrite On
            Blend One Zero

            CGPROGRAM
            #pragma target 4.5
            #pragma vertex LocustVertex
            #pragma fragment LocustFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_local_fragment _ MATREX_LOCUST_BLACK
            #pragma instancing_options forcemaxcount:256

            // Reuse the Built-in Standard Forward vertex/fragment lighting.
            #define UNITY_SETUP_BRDF_INPUT MetallicSetup
            #include "UnityStandardCoreForward.cginc"

            struct SkinMatrix
            {
                float4 x, y, z;
            };
            // Live matrices are world-space and draw with identity transforms.
            // Shared clip matrices are root-local and draw with each agent's transform.
            StructuredBuffer<SkinMatrix> _LocustBones;
            UNITY_INSTANCING_BUFFER_START(LocustDraw)
                UNITY_DEFINE_INSTANCED_PROP(float, _LocustBoneOffset)
            UNITY_INSTANCING_BUFFER_END(LocustDraw)

            struct SkinVertex
            {
                float4 vertex : POSITION;
                half3 normal : NORMAL;
                half4 tangent : TANGENT;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 bone : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            #if UNITY_STANDARD_SIMPLE
            VertexOutputBaseSimple LocustVertex(SkinVertex source)
            #else
            VertexOutputForwardBase LocustVertex(SkinVertex source)
            #endif
            {
                UNITY_SETUP_INSTANCE_ID(source);
                uint index = (uint)(UNITY_ACCESS_INSTANCED_PROP(LocustDraw, _LocustBoneOffset) + source.bone.x);
                SkinMatrix skin = _LocustBones[index];
                VertexInput vertex;
                UNITY_INITIALIZE_OUTPUT(VertexInput, vertex);
                UNITY_TRANSFER_INSTANCE_ID(source, vertex);
                vertex.vertex = float4(dot(skin.x, source.vertex), dot(skin.y, source.vertex), dot(skin.z, source.vertex), 1);
                vertex.normal = normalize(float3(dot(skin.x.xyz, source.normal), dot(skin.y.xyz, source.normal), dot(skin.z.xyz, source.normal)));
                vertex.uv0 = source.uv0;
                vertex.uv1 = source.uv1;
                #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                vertex.uv2 = source.uv2;
                #endif
                #ifdef _TANGENT_TO_WORLD
                vertex.tangent = float4(normalize(float3(dot(skin.x.xyz, source.tangent.xyz), dot(skin.y.xyz, source.tangent.xyz), dot(skin.z.xyz, source.tangent.xyz))), source.tangent.w);
                #endif
                return vertBase(vertex);
            }

            #if UNITY_STANDARD_SIMPLE
            half4 LocustFragment(VertexOutputBaseSimple input) : SV_Target
            #else
            half4 LocustFragment(VertexOutputForwardBase input) : SV_Target
            #endif
            {
                #if defined(MATREX_LOCUST_BLACK)
                return half4(0, 0, 0, 1);
                #else
                return fragBase(input);
                #endif
            }
            ENDCG
        }
    }
    FallBack Off
}
