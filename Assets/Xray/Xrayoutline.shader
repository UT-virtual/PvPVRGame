Shader "Custom/Xrayoutline"
{
    Properties
    {
        _Color ("Outline Color",Color) = (0,1,1,1)
        _OutlineWidth ("Outline Width (World Units)",Range(0.001,0.1)) = 0.025
        [HideInInspector] _Cull ("Cull", Float) = 1
        [HideInInspector] _ZTest ("Depth Test", Float) = 5
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 6
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull [_Cull]
            ColorMask [_ColorMask]
            Stencil
            {
                Ref 2
                ReadMask 2
                WriteMask [_StencilWriteMask]
                Comp [_StencilComp]
                Pass Replace
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.positionHCS = TransformWorldToHClip(p.positionWS + normalWS * _OutlineWidth);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                return _Color;
            }
            ENDHLSL
        }
    }
}
