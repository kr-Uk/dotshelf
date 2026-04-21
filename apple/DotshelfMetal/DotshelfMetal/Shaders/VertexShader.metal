#include <metal_stdlib>
#include "Common.h"
using namespace metal;

vertex VertexOut vertex_main(VertexIn in [[stage_in]],
                             constant Uniforms &uniforms [[buffer(1)]]) {
    VertexOut out;
    float4 worldPos = uniforms.modelMatrix * float4(in.position, 1.0);
    out.worldPosition = worldPos.xyz;
    out.clipPosition = uniforms.projectionMatrix * uniforms.viewMatrix * worldPos;
    out.worldNormal = (uniforms.normalMatrix * float4(in.normal, 0.0)).xyz;
    out.uv = in.uv;
    out.worldTangent = (uniforms.normalMatrix * float4(in.tangent, 0.0)).xyz;
    return out;
}
