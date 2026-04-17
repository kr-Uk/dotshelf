#ifndef COMMON_H
#define COMMON_H

#include <metal_stdlib>
using namespace metal;

struct VertexIn {
    float3 position  [[attribute(0)]];
    float3 normal    [[attribute(1)]];
    float2 uv        [[attribute(2)]];
    float3 tangent   [[attribute(3)]];
};

struct VertexOut {
    float4 clipPosition [[position]];
    float3 worldPosition;
    float3 worldNormal;
    float2 uv;
    float3 worldTangent;
};

struct Uniforms {
    float4x4 modelMatrix;
    float4x4 viewMatrix;
    float4x4 projectionMatrix;
    float4x4 normalMatrix;
    packed_float3 cameraPosition;
    float _pad0;
};

struct MaterialUniforms {
    packed_float3 albedo;
    float  metallic;
    float  roughness;
    int    hasAlbedoMap;
    int    hasNormalMap;
    int    hasRoughnessMap;
    float  alpha;
    float  _pad0;
    float  _pad1;
    float  _pad2;
};

struct PointLight {
    packed_float3 position;
    float _pad0;
    packed_float3 color;
    float  intensity;
    float  attenuationRadius;
    float  _pad1;
    float  _pad2;
    float  _pad3;
};

struct DirectionalLight {
    packed_float3 direction;
    float _pad0;
    packed_float3 color;
    float  intensity;
};

struct LightData {
    DirectionalLight directional;
    PointLight       pointLights[4];
    int              pointLightCount;
    float            _pad0;
    float            _pad1;
    float            _pad2;
    packed_float3    ambientColor;
    float            ambientIntensity;
};

struct ShadowData {
    float4x4 lightViewProjectionMatrix;
};

#endif
