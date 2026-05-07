// 산뜻 테마 wall material 전용 — stencil ref==1 픽셀에서 wall 을 discard.
// 짝꿍 shader: WallStencilWriter.shader (창문 prefab 의 WallMaskQuad 가 적용)
//
// URP Lit 의 핵심 properties (_BaseMap, _BaseColor, _Smoothness 등) 호환되어
// 기존 .mat 파일 그대로 m_Shader 만 교체하면 동작. 다만 lighting 모델은
// URP Lit 의 PBR 이 아니라 baseColor + ambient 합성 — isometric 룸에서는
// wall lighting 변화가 작아 시각적 차이 거의 없음.
//
// stencil 적용 영역:
// - 산뜻 테마 적용 시: BookshelfGenerator 가 만드는 4 wall renderer 의 sharedMaterial
//   이 GoodVibes_Wall.mat 으로 swap → 자동으로 이 shader 사용 → mask quad 영역 discard
// - 다른 테마: wall material 이 다른 shader 라 stencil read 안 함, 정상 그려짐
//   (다른 테마는 windowSpec 도 비어있어 mask quad 자체가 spawn 안 됨, double-safe)
Shader "Dotshelf/WallStencilReader"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // mask 가 ref=1 찍은 픽셀에서는 NotEqual 실패 → 이 픽셀 그리지 않음
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _AmbientStrength;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = baseTex.rgb * _BaseColor.rgb;

                // URP 의 ambient SH probe (RenderSettings.ambientIntensity 등 반영) + main
                // directional light. URP Lit 의 톤과 비슷하게 산뜻하게. 이전 단순 ambient
                // multiply 만 쓰던 버전보다 wall 이 어둡게 보이지 않음.
                float3 normalWS = normalize(i.normalWS);
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS) * _AmbientStrength;
                half3 direct = mainLight.color * NdotL;
                half3 lit = albedo * (ambient + direct);
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
