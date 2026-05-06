// Window mask quad 전용 — stencil buffer 에 ref=1 을 찍기만 하고 색은 안 그림.
// 짝꿍 shader: WallStencilReader.shader (산뜻 테마 wall material 에 적용)
// → reader 가 stencil ref==1 픽셀에서 wall 을 discard → 그 자리에 skybox/외부가 보임.
//
// 사용처: WindowPrefab > WallMaskQuad (창문 hollow 영역과 정확히 같은 크기/위치 quad)
// Render Queue Geometry-1 — wall 보다 먼저 그려져야 stencil 값이 깔림.
Shader "Hidden/Dotshelf/WallStencilWriter"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry-1"
        }

        Pass
        {
            Name "StencilWrite"
            // wall 보다 앞에 있어야 mask 가 동작. ZWrite Off — depth 영향 안 줌.
            ZWrite Off
            // color 안 그림 — 보이지 않는 mask
            ColorMask 0
            Cull Back

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
