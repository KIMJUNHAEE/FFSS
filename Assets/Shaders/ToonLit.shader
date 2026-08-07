// 필드 지형용 셀 셰이딩 Lit 셰이더 - 화면 후처리(ToonPostProcess, 폐기됨) 대신 실제 조명/그림자를
// 받아 N·L을 계단화한다. 프로퍼티 이름(_BaseMap/_BaseColor/_Cull)은 기존
// Universal Render Pipeline/Unlit과 동일하게 맞춰서 HexTileMapGenerator.CreateMaterial()의
// SetTextureIfPresent/SetColorIfPresent 호출이 그대로 통한다.
// ShadowCaster/DepthOnly/DepthNormals는 URP 표준 Lit 셰이더 것을 UsePass로 그대로 재사용 -
// 그림자 바이어스/펀추얼 판별 같은 걸 직접 다시 구현하지 않는다.
Shader "FFSS/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Tint", Color) = (0.55, 0.57, 0.72, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness ("Shadow Edge Softness", Range(0.001, 0.5)) = 0.05
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.5
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.85, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 0.4
        _Cull ("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _AmbientStrength;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
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
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // 계단(band) 하나짜리 셀 셰이딩: 면이 빛을 향하고(NdotL) 동시에 그림자맵에도
                // 가려지지 않은 경우에만 "밝음" 쪽으로 부드럽게(스무스스텝) 전환한다.
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float lightMask = ndotl * mainLight.shadowAttenuation;
                float band = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, lightMask);

                float3 litColor = albedo.rgb * mainLight.color;
                float3 shadowedColor = albedo.rgb * _ShadowColor.rgb;
                float3 diffuse = lerp(shadowedColor, litColor, band);

                float3 ambient = SampleSH(normalWS) * albedo.rgb * _AmbientStrength;

                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float rimDot = 1.0 - saturate(dot(normalWS, viewDirWS));
                float rim = pow(rimDot, _RimPower) * band * _RimIntensity;

                float3 color = diffuse + ambient + rim * _RimColor.rgb;
                return float4(color, albedo.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Unlit"
}
