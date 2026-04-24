#include <metal_stdlib>
#include "Common.h"
#include "PBR.h"
using namespace metal;

float calculateShadow(float4 lightSpacePos, depth2d<float> shadowMap) {
    float3 projCoords = lightSpacePos.xyz / lightSpacePos.w;
    float2 shadowUV = projCoords.xy * 0.5 + 0.5;
    shadowUV.y = 1.0 - shadowUV.y;

    if (shadowUV.x < 0.0 || shadowUV.x > 1.0 || shadowUV.y < 0.0 || shadowUV.y > 1.0)
        return 1.0;

    float currentDepth = projCoords.z;
    if (currentDepth > 1.0) return 1.0;

    constexpr sampler shadowSampler(coord::normalized, filter::linear, address::clamp_to_edge, compare_func::less);

    float visibility = 0.0;
    float2 texelSize = float2(1.0 / 2048.0);
    float bias = 0.005;
    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            float2 offset = float2(float(x), float(y)) * texelSize;
            visibility += shadowMap.sample_compare(shadowSampler, shadowUV + offset, currentDepth - bias);
        }
    }
    visibility /= 9.0;
    return visibility;
}

fragment float4 fragment_main(VertexOut in [[stage_in]],
                              constant MaterialUniforms &material [[buffer(0)]],
                              constant LightData &lights [[buffer(1)]],
                              constant Uniforms &uniforms [[buffer(2)]],
                              constant ShadowData &shadowData [[buffer(3)]],
                              depth2d<float> shadowMap [[texture(0)]],
                              texture2d<float> albedoMap [[texture(1)]],
                              texture2d<float> normalMap [[texture(2)]],
                              texture2d<float> roughnessMap [[texture(3)]]) {
    constexpr sampler texSampler(coord::normalized, filter::linear, address::repeat);

    float3 N = normalize(in.worldNormal);
    float3 V = normalize(float3(uniforms.cameraPosition) - in.worldPosition);
    float3 albedo = float3(material.albedo);
    float metallic = material.metallic;
    float roughness = max(material.roughness, 0.04);

    if (material.hasAlbedoMap) {
        float4 texColor = albedoMap.sample(texSampler, in.uv);
        albedo = pow(texColor.rgb, float3(2.2));
    }
    if (material.hasRoughnessMap) {
        roughness = max(roughnessMap.sample(texSampler, in.uv).r, 0.04);
    }
    if (material.hasNormalMap) {
        float3 T = normalize(in.worldTangent);
        float3 B = normalize(cross(N, T));
        float3x3 TBN = float3x3(T, B, N);
        float3 normalSample = normalMap.sample(texSampler, in.uv).rgb * 2.0 - 1.0;
        N = normalize(TBN * normalSample);
    }

    float3 F0 = mix(float3(0.04), albedo, metallic);
    float3 Lo = float3(0.0);

    // Shadow
    float4 lightSpacePos = shadowData.lightViewProjectionMatrix * float4(in.worldPosition, 1.0);
    float shadow = calculateShadow(lightSpacePos, shadowMap);

    // Directional light
    {
        float3 L = normalize(-float3(lights.directional.direction));
        float3 H = normalize(V + L);
        float NdotL = max(dot(N, L), 0.0);

        float NDF = distributionGGX(N, H, roughness);
        float G = geometrySmith(N, V, L, roughness);
        float3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

        float3 specular = (NDF * G * F) / (4.0 * max(dot(N, V), 0.0) * NdotL + 0.0001);
        float3 kD = (float3(1.0) - F) * (1.0 - metallic);

        Lo += (kD * albedo / M_PI_F + specular) * float3(lights.directional.color) * lights.directional.intensity * NdotL * shadow;
    }

    // Point lights
    for (int i = 0; i < lights.pointLightCount && i < 4; i++) {
        float3 lightPos = float3(lights.pointLights[i].position);
        float3 lightColor = float3(lights.pointLights[i].color);
        float intensity = lights.pointLights[i].intensity;
        float radius = lights.pointLights[i].attenuationRadius;

        float3 L = normalize(lightPos - in.worldPosition);
        float3 H = normalize(V + L);
        float dist = length(lightPos - in.worldPosition);
        float attenuation = saturate(1.0 - (dist / radius)) / (dist * dist + 0.01);
        float NdotL = max(dot(N, L), 0.0);

        float NDF = distributionGGX(N, H, roughness);
        float G = geometrySmith(N, V, L, roughness);
        float3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

        float3 specular = (NDF * G * F) / (4.0 * max(dot(N, V), 0.0) * NdotL + 0.0001);
        float3 kD = (float3(1.0) - F) * (1.0 - metallic);

        Lo += (kD * albedo / M_PI_F + specular) * lightColor * intensity * attenuation * NdotL;
    }

    float3 ambient = float3(lights.ambientColor) * lights.ambientIntensity * albedo;
    float3 color = ambient + Lo;

    color = color / (color + float3(1.0));
    color = pow(color, float3(1.0 / 2.2));

    return float4(color, material.alpha);
}
