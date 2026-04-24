#include <metal_stdlib>
#include "Common.h"
using namespace metal;

struct ShadowUniforms {
    float4x4 lightViewProjectionMatrix;
    float4x4 modelMatrix;
};

vertex float4 shadow_vertex(VertexIn in [[stage_in]],
                            constant ShadowUniforms &uniforms [[buffer(1)]]) {
    float4 worldPos = uniforms.modelMatrix * float4(in.position, 1.0);
    return uniforms.lightViewProjectionMatrix * worldPos;
}
