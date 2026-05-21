# Dotshelf 시안 프로토타입 — macOS / Metal 직접 구현

## 프로젝트 개요
Dotshelf는 3D 책장 기반 독서 커뮤니티 서비스다. 이 시안은 **Metal API를 직접 사용하여 커스텀 렌더링 파이프라인을 구축**하는 방향의 proof of concept이다.

팀원들에게 "우리가 렌더링 파이프라인을 완전히 소유하고 제어할 수 있다"는 기술적 가능성을 보여주는 것이 목적이다.

---

## 기술 스택
- **언어**: Swift + Metal Shading Language (MSL)
- **UI**: SwiftUI (App lifecycle) + MTKView (NSViewRepresentable)
- **그래픽 API**: Metal (직접)
- **셰이더**: MSL로 PBR BRDF 직접 구현
- **최소 지원**: macOS 14.0+
- **프로젝트**: `DotshelfMetal`

---

## 프로젝트 구조
```
DotshelfMetal/
├── DotshelfMetalApp.swift              # @main, WindowGroup
├── ContentView.swift                    # SwiftUI + MTKView 임베딩
├── Renderer/
│   ├── Renderer.swift                  # MTKViewDelegate, 렌더 루프
│   ├── RenderPipeline.swift            # MTLRenderPipelineState 생성/관리
│   ├── DepthStencil.swift              # 깊이/스텐실 상태
│   └── RenderPass.swift                # 렌더 패스 디스크립터 관리
├── Shaders/
│   ├── Common.metal                    # 공유 구조체 (Vertex, Uniforms, Light)
│   ├── PBR.metal                       # Cook-Torrance BRDF 함수들
│   ├── VertexShader.metal              # 버텍스 변환
│   └── FragmentShader.metal            # PBR 라이팅 + 출력
├── Geometry/
│   ├── MeshGenerator.swift             # 프로시저럴 메시 생성 (버텍스/인덱스 버퍼)
│   ├── BookMesh.swift                  # 책 지오메트리 생성
│   ├── BookshelfMesh.swift             # 책장 지오메트리 생성
│   └── Vertex.swift                    # 버텍스 구조체 (position, normal, uv, tangent)
├── Scene/
│   ├── SceneGraph.swift                # 씬 노드 트리, 트랜스폼 계층
│   ├── Camera.swift                    # 카메라 (projection + view 행렬, 줌)
│   ├── Light.swift                     # 라이트 데이터 (방향, 포인트)
│   └── SlotSystem.swift                # 책장 슬롯 관리
├── Math/
│   ├── Matrix4x4+Extensions.swift      # 행렬 연산 (perspective, lookAt, translate, rotate)
│   └── Vector+Extensions.swift         # 벡터 유틸리티
├── Input/
│   ├── InputHandler.swift              # 마우스/트랙패드 이벤트 처리
│   └── Raycaster.swift                 # 스크린 좌표 → 월드 레이 변환, AABB 교차
├── Resources/
│   └── Textures/                       # PBR 텍스처 파일
└── Info.plist
```

---

## 셰이더 공유 구조체 (Common.metal 핵심)

```metal
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
    float4x4 normalMatrix;    // transpose(inverse(modelMatrix))
    float3   cameraPosition;
};

struct MaterialUniforms {
    float3 albedo;        // base color (non-texture fallback)
    float  metallic;
    float  roughness;
    int    hasAlbedoMap;
    int    hasNormalMap;
    int    hasRoughnessMap;
};

struct PointLight {
    float3 position;
    float3 color;
    float  intensity;
    float  attenuationRadius;
};

struct DirectionalLight {
    float3 direction;
    float3 color;
    float  intensity;
};

struct LightData {
    DirectionalLight directional;
    PointLight       pointLights[4];
    int              pointLightCount;
    float3           ambientColor;
    float            ambientIntensity;
};
```

---

## PBR 셰이더 핵심 (PBR.metal)

```metal
// GGX/Trowbridge-Reitz Normal Distribution Function
float distributionGGX(float3 N, float3 H, float roughness) {
    float a  = roughness * roughness;
    float a2 = a * a;
    float NdotH  = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = M_PI_F * denom * denom;
    return a2 / max(denom, 0.0001);
}

// Schlick-GGX Geometry Function
float geometrySchlickGGX(float NdotV, float roughness) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    return NdotV / (NdotV * (1.0 - k) + k);
}

float geometrySmith(float3 N, float3 V, float3 L, float roughness) {
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    return geometrySchlickGGX(NdotV, roughness) * geometrySchlickGGX(NdotL, roughness);
}

// Fresnel-Schlick
float3 fresnelSchlick(float cosTheta, float3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}
```

---

## 구현 태스크 (Phase 순서)

### Phase 1: Metal 렌더 루프 셋업
1. SwiftUI App + WindowGroup
2. `MTKView`를 `NSViewRepresentable`로 래핑하여 ContentView에 임베딩
3. `Renderer` 클래스: `MTKViewDelegate` 채택
   - `MTLDevice`, `MTLCommandQueue` 초기화
   - `mtkView(_:drawableSizeWillChange:)` — 뷰포트/투영 행렬 업데이트
   - `draw(in:)` — 매 프레임 렌더 루프
4. 렌더 파이프라인 생성:
   - `MTLRenderPipelineDescriptor` — 버텍스/프래그먼트 함수 연결
   - `MTLDepthStencilDescriptor` — 깊이 테스트 활성화
5. 화면을 단색(#F5F0E8)으로 클리어하는 것만 확인
6. **빌드 & 실행** — MTKView에 아이보리 배경이 보이면 성공

### Phase 2: 기본 지오메트리 렌더링
1. `Vertex.swift` — 버텍스 구조체 정의 (position, normal, uv, tangent)
2. `Matrix4x4+Extensions.swift` — 행렬 유틸리티
   - `perspective(fov:aspect:near:far:)` → float4x4
   - `lookAt(eye:center:up:)` → float4x4
   - `translation(_:)`, `rotation(angle:axis:)`, `scale(_:)` → float4x4
3. `VertexShader.metal` — MVP 변환
4. `FragmentShader.metal` — 일단 단색 출력 (PBR은 다음 Phase)
5. 하드코딩된 큐브 하나를 렌더링
6. **빌드 & 실행** — 화면에 3D 큐브가 보이면 성공

### Phase 3: 프로시저럴 메시
1. `BookMesh.swift` — 책 버텍스/인덱스 데이터 생성
   - 파라미터: thickness, height, width
   - 6면의 버텍스 + 노멀 + UV 생성
   - MTLBuffer로 변환
2. `BookshelfMesh.swift` — 책장 메시 생성
   - 기둥, 선반, 뒷판 각각의 박스 지오메트리
   - 파라미터: columns(2), rows(3), width(1.8), height(2.0), depth(0.35)
3. `SceneGraph.swift` — 노드 트리 구조
   - 각 노드: localTransform, worldTransform, children, mesh, material
   - 재귀적으로 worldTransform 계산
4. `SlotSystem.swift` — 슬롯 그리드 정의, 책 6~10권 배치
5. **빌드 & 실행** — 책장 + 책이 단색으로 보이면 성공

### Phase 4: PBR 라이팅 (핵심 Phase)
1. `PBR.metal` — Cook-Torrance BRDF 함수 구현 (위 코드 참고)
2. `FragmentShader.metal` 업데이트:
   - Uniforms, MaterialUniforms, LightData를 받아서
   - 각 라이트별로 BRDF 계산 → 누적
   - 최종 색상 = 직접광 + 앰비언트
3. `MaterialUniforms` 설정:
   - 책장: albedo #8B6914, roughness 0.7, metallic 0.0
   - 책: 각각 다른 albedo, roughness 0.3, metallic 0.0
4. 라이트 설정:
   - DirectionalLight: direction(-1,-1,-0.5) 정규화, color(1.0,0.96,0.9), intensity 2.0
   - PointLight 2개: 책장 위/옆, color(1.0,0.9,0.7), intensity 1.5
   - Ambient: color(0.16,0.14,0.12), intensity 0.5
5. **빌드 & 실행** — 조명에 의한 음영, 하이라이트가 보이면 성공. 이것이 시안의 핵심 비주얼.

### Phase 5: 그림자
1. 섀도우맵 렌더 패스 추가:
   - 방향 라이트 시점에서 씬을 깊이만 렌더링
   - 1024×1024 `MTLTexture` (depth only)
   - 라이트 공간 MVP 행렬 계산
2. FragmentShader에서 섀도우맵 샘플링:
   - 프래그먼트의 라이트 공간 좌표 계산
   - PCF 3×3 커널로 소프트 섀도우
3. **빌드 & 실행** — 책과 책장에 그림자가 보이면 성공

### Phase 6: 텍스처 적용 (있으면)
1. `TextureLoader.swift` — MTKTextureLoader로 PNG/JPG 로드
2. FragmentShader에서 `hasAlbedoMap` 체크:
   - 텍스처 있으면 샘플링, 없으면 albedo 단색 사용
3. 나무 텍스처 적용 (ambientCG Wood049 등)
4. 노멀맵 적용: TBN 행렬로 변환
5. **빌드 & 실행** — 나무 질감이 보이면 성공

### Phase 7: 카메라 줌
1. `Camera.swift` — projection + view 행렬 관리
   - 초기: eye(0, 1.0, 3.0), center(0, 1.0, 0), fov 45°
2. `InputHandler.swift` — 스크롤/핀치 제스처 감지
   - MagnificationGesture 또는 scrollWheel 이벤트
   - 줌 범위: z=0.5 ~ z=3.0
   - 부드러운 감속 (lerp)
3. **빌드 & 실행** — 줌인/줌아웃 동작하면 성공

### Phase 8: 인터랙션 (시간 허용 시)
1. `Raycaster.swift`
   - 스크린 좌표 → NDC → 역투영으로 월드 레이 생성
   - 각 책의 AABB와 교차 검사
2. 클릭 시 책 "뽑기": 해당 노드의 z축 위치를 0.15m 앞으로 애니메이션
3. 드래그 → 이동 → 놓기 → 슬롯 스냅
4. **빌드 & 실행** — 책 클릭/이동이 되면 성공

---

## 현실적 시간 예측 (오늘 하루)

| Phase | 예상 시간 | 누적 | 비고 |
|-------|----------|------|------|
| 1. Metal 셋업 | 30~45분 | 45분 | 보일러플레이트 많음 |
| 2. 기본 지오메트리 | 45~60분 | 1.5h | 행렬 연산 + 셰이더 기본 |
| 3. 프로시저럴 메시 | 60~90분 | 3h | 씬 그래프 + 메시 생성 |
| 4. PBR 라이팅 | 60~90분 | 4.5h | **가장 임팩트 큰 Phase** |
| 5. 그림자 | 45~60분 | 5.5h | 섀도우맵 패스 추가 |
| 6. 텍스처 | 30~45분 | 6h | 텍스처 있으면 큰 개선 |
| 7. 카메라 줌 | 30분 | 6.5h | 비교적 간단 |
| 8. 인터랙션 | 60~90분 | 8h | 여유 있으면 |

**Phase 5(그림자)까지가 현실적 목표**. 그림자가 들어가면 시안으로서의 임팩트가 크게 올라감.

---

## 에셋 전략

### PBR 텍스처 (CC0, 선택사항)
- 나무 (책장): ambientcg.com → Wood049 / Wood054 (1K) — Diffuse, Normal, Roughness
- 책 표지: 단색 albedo로 시작, 나중에 텍스처 교체
- **텍스처 없이도 PBR 셰이더의 효과는 보임** (메탈릭/러프니스에 의한 하이라이트 차이)

---

## 스코프 아님
- ❌ IBL (환경맵 기반 조명) — 큐브맵 로딩 + 프리필터링 필요, 오늘은 직접 조명만
- ❌ SSAO, Bloom, 톤 매핑 — 포스트 프로세싱 패스는 다음 단계
- ❌ 멀티플랫폼 (macOS만)
- ❌ 네트워크/API
- ❌ glTF 로더
- ❌ 물리 엔진

---

## Claude Code 사용 전략: Agent Teams 활용

Metal 직접 구현은 작업량이 크므로, **Claude Code Agent Teams**를 활용하여 병렬로 진행하면 효율적이다.

### Agent Teams 설정
```bash
# 환경변수 또는 settings.json에 추가
CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=true
```

### 팀 구성 (권장)
```
Lead Agent: 전체 조율, 앱 셸, 렌더 루프 (Phase 1~2)
├── Agent A (Shader): PBR.metal, VertexShader.metal, FragmentShader.metal
│   → Phase 4~5의 셰이더 코드 전담
├── Agent B (Geometry): MeshGenerator, BookMesh, BookshelfMesh, SceneGraph
│   → Phase 3의 프로시저럴 메시 + 씬 그래프 전담
└── Agent C (Input/Camera): Camera, InputHandler, Raycaster
    → Phase 7~8 전담
```

### 진행 방식
1. **Lead가 Phase 1~2 완료** (Metal 셋업 + 기본 렌더링 확인)
2. **Phase 3~5를 Agent A, B가 병렬 진행**:
   - Agent B: 프로시저럴 메시 생성 (Swift 코드, Metal 비의존)
   - Agent A: PBR 셰이더 작성 (MSL 코드)
   - Lead: 둘을 통합하여 빌드 확인
3. **Phase 6~8은 순차 또는 Agent C가 담당**

### 주의사항
- 각 Agent가 만드는 파일이 겹치지 않도록 구조를 분리해둠 (위 프로젝트 구조 참고)
- 셰이더의 구조체(`Common.metal`)는 Lead가 먼저 확정한 후 Agent들이 참조
- 빌드 확인은 반드시 Lead에서 통합 후 수행
