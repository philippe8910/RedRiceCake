// 教學箭頭專用的極簡 Unlit：可選擇穿牆顯示（ZTest Always），
// 再加一點點假打光讓低面數箭頭不會糊成一片。
Shader "Mooncake/Tutorial Overlay"
{
    Properties
    {
        _BaseColor("顏色", Color) = (1, 0.72, 0.2, 1)
        // 8 = Always（穿牆），4 = LEqual（正常遮擋）
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("深度測試", Float) = 8
        _TopLight("上方假打光", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "TutorialOverlay"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _TopLight;
                float  _ZTest;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 朝上的面亮一點，純粹是為了看得出立體感
                half up = saturate(dot(normalize(input.normalWS), half3(0, 1, 0)) * 0.5 + 0.5);
                half3 rgb = _BaseColor.rgb * (1.0 - _TopLight + _TopLight * up * 2.0);
                return half4(rgb, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback "Unlit/Color"
}
