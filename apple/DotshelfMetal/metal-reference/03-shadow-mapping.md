# Metal Shadow Mapping

## sample_compare 동작 원리

```metal
float result = shadowMap.sample_compare(sampler, uv, referenceDepth);
```

**인자 순서: compare_func(referenceDepth, sampledDepth)**

| compare_func | 반환 1.0 조건 | 의미 |
|-------------|-------------|------|
| less | referenceDepth < sampledDepth | 프래그먼트가 기록된 깊이보다 가까움 = **빛 받음** |
| greater_equal | referenceDepth >= sampledDepth | 프래그먼트가 기록된 깊이 이상 = **그림자** |

## 올바른 Shadow Map 구현

### Metal 기본 depth 범위: [0=near, 1=far]
- clearDepth = 1.0 (가장 먼 값으로 초기화)
- depthCompareFunction = .less (가까운 것이 이김)

### Shadow sampler: `compare_func::less` 사용
```metal
constexpr sampler shadowSampler(
    coord::normalized,
    filter::linear,         // hardware PCF (2x2 비교 결과 보간)
    address::clamp_to_edge,
    compare_func::less      // referenceDepth < storedDepth → 1.0 (lit)
);

// referenceDepth = 현재 프래그먼트의 라이트 공간 깊이
// storedDepth = 섀도우맵에 기록된 가장 가까운 표면의 깊이
// 프래그먼트가 가장 가까운 표면보다 뒤에 있으면 → 0.0 (shadow)
// 프래그먼트가 가장 가까운 표면이면 → 1.0 (lit)
float visibility = shadowMap.sample_compare(shadowSampler, uv, fragmentDepth - bias);
```

### PCF 3x3
```metal
float visibility = 0.0;
for (int x = -1; x <= 1; x++) {
    for (int y = -1; y <= 1; y++) {
        visibility += shadowMap.sample_compare(shadowSampler, uv + offset, depth - bias);
    }
}
visibility /= 9.0;
// visibility = 1.0이면 완전히 밝음, 0.0이면 완전히 그림자
```

### NDC → UV 변환
```metal
float3 proj = lightSpacePos.xyz / lightSpacePos.w;
float2 uv = proj.xy * 0.5 + 0.5;  // [-1,1] → [0,1]
uv.y = 1.0 - uv.y;                // Metal 텍스처 원점은 좌상단
float depth = proj.z;              // [0,1] (Metal convention)
```

## Shadow Pass 설정

```swift
// Depth-only 렌더 (프래그먼트 셰이더 없음)
pipelineDescriptor.fragmentFunction = nil
pipelineDescriptor.colorAttachments[0].pixelFormat = .invalid
pipelineDescriptor.depthAttachmentPixelFormat = .depth32Float

// 렌더 패스
rpd.depthAttachment.texture = shadowMapTexture
rpd.depthAttachment.loadAction = .clear
rpd.depthAttachment.storeAction = .store
rpd.depthAttachment.clearDepth = 1.0

// Shadow acne 방지: front face culling
shadowEncoder.setCullMode(.front)
```

## 라이트 공간 행렬

```swift
// 직교 투영 (방향광)
let lightProjection = float4x4.orthographic(
    left: -size, right: size,
    bottom: -size, top: size,
    near: 0.1, far: 20.0
)

// 라이트 뷰 행렬 - up 벡터가 시선과 평행하면 NaN 발생!
let lightView = float4x4.lookAt(
    eye: lightPos,
    center: sceneCenter,
    up: SIMD3<Float>(0, 1, 0)  // 라이트가 위에서 내려오면 (0,0,1) 사용
)
```
