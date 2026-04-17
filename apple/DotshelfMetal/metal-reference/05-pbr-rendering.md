# PBR Rendering (Cook-Torrance BRDF)

## 반사율 방정식 (Reflectance Equation)

```
Lo = Σ (kD * albedo/π + DFG/(4·NdotV·NdotL)) * Li * NdotL
```

## 세 가지 핵심 함수

### D: GGX Normal Distribution Function
미세면이 halfway 벡터에 정렬된 비율.
```metal
float distributionGGX(float3 N, float3 H, float roughness) {
    float a = roughness * roughness;  // Disney remapping
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float denom = NdotH * NdotH * (a2 - 1.0) + 1.0;
    return a2 / (M_PI_F * denom * denom + 0.0001);
}
```

### G: Smith's Geometry Function
미세면 간 차폐/그림자.
```metal
float geometrySchlickGGX(float NdotV, float roughness) {
    float k = (roughness + 1.0) * (roughness + 1.0) / 8.0;  // direct light용
    return NdotV / (NdotV * (1.0 - k) + k);
}
```

### F: Fresnel-Schlick
시야각에 따른 반사율 변화.
```metal
float3 fresnelSchlick(float cosTheta, float3 F0) {
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}
```

## 흔한 실수

1. **roughness 최솟값**: roughness=0이면 NDF가 무한대. `roughness = max(roughness, 0.04)` 필수.
2. **에너지 보존**: `kD = (1 - kS) * (1 - metallic)`. 금속은 diffuse 없음.
3. **F0**: 비금속 = 0.04, 금속 = albedo. `F0 = mix(0.04, albedo, metallic)`.
4. **감마/리니어**: 텍스처는 sRGB → `pow(tex, 2.2)`. 최종 출력 → `pow(color, 1/2.2)`.
5. **톤매핑**: PBR은 HDR 출력. Reinhard: `color / (color + 1.0)`.
