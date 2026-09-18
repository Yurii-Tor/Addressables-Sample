Shader "AddressablesSample/Target Surface"
{
    Properties
    {
        _BaseMap("Round image", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _BackgroundColor("Cube background", Color) = (0.78, 0.86, 0.96, 1)
        _EmissionColor("Feedback emission", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _BackgroundColor;
                half4 _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 image = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 surface = lerp(_BackgroundColor.rgb, image.rgb, image.a) * _BaseColor.rgb;
                half3 normalWS = normalize(input.normalWS);
                half3 lightDirection = normalize(half3(0.35h, 0.75h, -0.55h));
                half lightAmount = 0.72h + 0.28h * saturate(dot(normalWS, lightDirection));
                return half4(surface * lightAmount + _EmissionColor.rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
