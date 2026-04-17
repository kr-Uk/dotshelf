# Dotshelf 시안 프로토타입 — macOS / RealityKit

## 프로젝트 개요
Dotshelf는 3D 책장 기반 독서 커뮤니티 서비스다. 이 시안은 **RealityKit + SwiftUI**를 사용하여 고품질 PBR 렌더링과 인터랙션을 빠르게 구현하는 방향의 proof of concept이다.

팀원들에게 "Apple의 최신 3D 프레임워크로 이 수준의 비주얼과 인터랙션이 가능하다"를 보여주고, 향후 visionOS 확장 가능성도 함께 입증하는 것이 목적이다.

---

## 왜 RealityKit인가
- Apple이 SceneKit 대신 **현재 밀고 있는 프레임워크** (SceneKit은 유지보수 모드)
- Metal 위에 구축된 내장 PBR 렌더러 — 직접 셰이더를 짜지 않아도 photo-realistic 수준
- **RealityView** (macOS 14+): SwiftUI에 3D 콘텐츠를 네이티브로 임베딩
- **visionOS 네이티브 지원**: Dotshelf의 "추후 VR 지원" 목표와 직결. RealityKit으로 만들면 visionOS 이식이 거의 공짜
- 물리 엔진, 제스처, 애니메이션 내장 → 인터랙션 Phase까지 오늘 안에 도달 가능
- 오디오, 공간 컴퓨팅 등 Apple 생태계 기능과 자연스러운 통합

---

## 기술 스택
- **언어**: Swift
- **UI**: SwiftUI (App lifecycle) + RealityView
- **3D**: RealityKit (ModelEntity, PhysicallyBasedMaterial)
- **최소 지원**: macOS 14.0+
- **프로젝트**: `DotshelfRealityKit`

---

## 프로젝트 구조
```
DotshelfRealityKit/
├── DotshelfRealityKitApp.swift         # @main, WindowGroup
├── ContentView.swift                    # SwiftUI + RealityView 임베딩
├── Scene/
│   ├── BookshelfScene.swift            # Entity 계층 구성, 조명, 환경
│   ├── CameraController.swift          # 줌인/줌아웃 (PerspectiveCamera 제어)
│   └── EnvironmentSetup.swift          # IBL, 환경맵, 배경
├── Entities/
│   ├── BookEntity.swift                # 프로시저럴 책 Entity 생성
│   ├── BookshelfEntity.swift           # 프로시저럴 책장 Entity 생성
│   └── SlotSystem.swift                # 슬롯 그리드 관리
├── Materials/
│   └── MaterialFactory.swift           # PhysicallyBasedMaterial 팩토리
├── Interaction/
│   ├── BookGestureHandler.swift        # 탭, 드래그 제스처 처리
│   └── BookAnimation.swift             # 뽑기/꽂기 애니메이션
├── Resources/
│   └── Textures/                       # PBR 텍스처 파일
└── Info.plist
```

---

## 구현 태스크 (Phase 순서)

### Phase 1: 앱 셸
1. SwiftUI App + WindowGroup
2. ContentView에 `RealityView` 임베딩
   ```swift
   import RealityKit

   struct ContentView: View {
       var body: some View {
           RealityView { content in
               // 여기에 Entity 추가
           }
           .frame(minWidth: 800, minHeight: 600)
       }
   }
   ```
3. 빈 RealityView 확인 — 기본 배경이 렌더링됨
4. **빌드 & 실행 확인**

### Phase 2: 프로시저럴 책장
1. `BookshelfEntity.swift` — MeshResource로 책장 생성
   ```swift
   // 각 파트를 MeshResource.generateBox()로 생성
   let shelfBoard = MeshResource.generateBox(
       width: 1.8, height: 0.025, depth: 0.35
   )
   let sidePanel = MeshResource.generateBox(
       width: 0.025, height: 2.0, depth: 0.35
   )
   ```
   - 파라미터: columns(2), rows(3), width(1.8), height(2.0), depth(0.35)
   - 좌/우 기둥 + 상/하/중간 선반 + 뒷판
   - 각 파트를 ModelEntity로 만들어 부모 Entity 아래 addChild
2. `MaterialFactory.swift` — 나무 재질
   ```swift
   var woodMaterial = PhysicallyBasedMaterial()
   woodMaterial.baseColor = .init(tint: .init(hex: "#8B6914"))
   woodMaterial.roughness = .init(floatLiteral: 0.7)
   woodMaterial.metallic = .init(floatLiteral: 0.0)
   // 텍스처 있으면:
   // woodMaterial.baseColor = .init(texture: .init(try! .load(named: "wood_diffuse")))
   // woodMaterial.normal = .init(texture: .init(try! .load(named: "wood_normal")))
   ```
3. RealityView의 content에 책장 Entity 추가
4. **빌드 & 실행 확인** — 책장이 보이면 성공

### Phase 3: 프로시저럴 책
1. `BookEntity.swift` — 책 메시 생성
   ```swift
   static func create(
       thickness: Float = 0.03,
       height: Float = 0.25,
       width: Float = 0.17,
       color: NSColor = .systemRed
   ) -> ModelEntity {
       let mesh = MeshResource.generateBox(
           width: thickness, height: height, depth: width,
           cornerRadius: 0.002
       )
       var material = PhysicallyBasedMaterial()
       material.baseColor = .init(tint: .init(color))
       material.roughness = .init(floatLiteral: 0.3)
       material.metallic = .init(floatLiteral: 0.0)

       let entity = ModelEntity(mesh: mesh, materials: [material])
       // 충돌 컴포넌트 추가 (인터랙션용)
       entity.collision = CollisionComponent(
           shapes: [.generateBox(width: thickness, height: height, depth: width)]
       )
       // 입력 타겟 (제스처 수신)
       entity.components.set(InputTargetComponent())
       return entity
   }
   ```
2. `SlotSystem.swift` — 슬롯 관리 + 책 6~10권 배치
   - 각 슬롯의 월드 좌표 계산
   - 책을 세워서(Y축 up) 배치, 책등이 앞을 향하도록
3. 다양한 크기/색상의 책 생성
   - 빨강, 파랑, 초록, 갈색, 남색, 흰색, 검정, 노랑 등
   - 각각 약간 다른 thickness(0.02~0.05), height(0.22~0.28)
4. **빌드 & 실행 확인** — 책장에 다양한 색의 책이 꽂혀있으면 성공

### Phase 4: 조명 + 환경 (분위기의 핵심)
1. **방향 라이트** (DirectionalLight)
   ```swift
   let sunLight = DirectionalLight()
   sunLight.light.color = .init(hex: "#FFF5E6")
   sunLight.light.intensity = 3000   // lux 단위
   sunLight.light.isRealWorldProxy = false
   sunLight.shadow = DirectionalLightComponent.Shadow(
       maximumDistance: 10, depthBias: 0.5
   )
   sunLight.look(at: [0,0,0], from: [-2, 3, 2], relativeTo: nil)
   ```
2. **포인트 라이트 2개**
   ```swift
   let lamp = PointLight()
   lamp.light.color = .init(hex: "#FFE4B5")
   lamp.light.intensity = 500  // lumens
   lamp.light.attenuationRadius = 5.0
   lamp.position = [0.8, 2.2, 0.5]
   ```
3. **환경 조명 (IBL)** — RealityKit의 강점
   ```swift
   // HDRI 환경맵으로 간접광 + 반사
   let envResource = try! EnvironmentResource.load(named: "indoor_hdri")
   // RealityView의 content에서:
   content.environment.lighting.resource = envResource
   content.environment.lighting.intensityExponent = 1.0
   ```
   - HDRI가 없으면 기본 환경 조명도 충분히 좋음
   - 환경맵 하나로 간접광, 반사, 분위기가 한번에 해결됨
4. 배경색 설정
   ```swift
   content.environment.background = .color(.init(hex: "#F5F0E8"))
   ```
5. **빌드 & 실행 확인** — 따뜻하고 아늑한 조명, 그림자, 반사가 보이면 성공

### Phase 5: 카메라 줌
1. `CameraController.swift`
   ```swift
   let camera = PerspectiveCamera()
   camera.camera.fieldOfViewInDegrees = 45
   camera.position = [0, 1.0, 3.0]
   camera.look(at: [0, 1.0, 0], from: [0, 1.0, 3.0], relativeTo: nil)
   ```
2. 줌인/줌아웃: `MagnificationGesture` 또는 `scrollWheel`
   - 카메라 z축 이동: 0.5(클로즈업) ~ 3.0(전체 뷰)
   - 부드러운 보간 (withAnimation 또는 Entity.move)
3. **(선택) DOF 힌트**: RealityKit에서 직접 DOF 제어는 제한적이나,
   포스트 프로세싱으로 SwiftUI 레이어에서 블러 오버레이 가능
4. **빌드 & 실행 확인** — 스크롤로 줌, 부드러운 이동 확인

### Phase 6: 책 인터랙션 (RealityKit 제스처 활용)
1. RealityView에 제스처 추가:
   ```swift
   RealityView { content in
       // ... entity setup
   }
   .gesture(
       TapGesture()
           .targetedToAnyEntity()
           .onEnded { value in
               handleBookTap(entity: value.entity)
           }
   )
   .gesture(
       DragGesture()
           .targetedToAnyEntity()
           .onChanged { value in
               handleBookDrag(value: value)
           }
           .onEnded { value in
               handleBookDrop(value: value)
           }
   )
   ```
2. **뽑기**: 탭 시 책 z축 +0.15m 이동
   ```swift
   func handleBookTap(entity: Entity) {
       var transform = entity.transform
       transform.translation.z += 0.15
       entity.move(to: transform, relativeTo: entity.parent, duration: 0.3)
   }
   ```
3. **드래그**: DragGesture의 translation을 Entity 위치에 반영
4. **스냅**: 드래그 종료 시 가장 가까운 빈 슬롯으로 move(to:duration:)
5. 유효하지 않은 위치면 원래 슬롯으로 복귀 애니메이션
6. **빌드 & 실행 확인** — 책을 탭→뽑기→드래그→다른 위치에 꽂기

### Phase 7: 폴리싱 (여유 시간)
1. 책등에 텍스트 표현 (ModelEntity에 텍스트 메시 또는 텍스처)
2. 뽑을 때 주변 책에 미세한 반응 (살짝 기울어짐)
3. 드래그 중 빈 슬롯 하이라이트 (반투명 가이드 Entity)
4. 줌인 시 디테일 추가 (책 표지 텍스처 등)

---

## 현실적 시간 예측 (오늘 하루)

| Phase | 예상 시간 | 누적 | 비고 |
|-------|----------|------|------|
| 1. 앱 셸 | 15~20분 | 20분 | RealityView 간단 |
| 2. 책장 | 30~45분 | 1h | generateBox 조합 |
| 3. 책 | 30~45분 | 1.5h | 슬롯 배치 포함 |
| 4. 조명 | 30~45분 | 2h | **IBL 적용 시 비주얼 점프** |
| 5. 카메라 | 20~30분 | 2.5h | 비교적 간단 |
| 6. 인터랙션 | 45~60분 | 3.5h | 제스처 내장이라 빠름 |
| 7. 폴리싱 | 30~60분 | 4.5h | 여유 있으면 |

**Phase 6(인터랙션)까지 충분히 가능**. Metal 대비 시간이 절반 이하.

---

## RealityKit의 한계 (알아둘 것)
- 커스텀 셰이더 파이프라인 직접 제어 불가 (Metal처럼 BRDF를 직접 짜는 게 아님)
- SSAO, SSR, 톤 매핑 등의 파라미터 세밀 조정이 제한적
- Apple 플랫폼 전용 (Android, Windows에는 별도 렌더러 필요)
- DOF, 블룸 등 포스트 프로세싱 직접 제어가 제한적

이 한계들은 Dotshelf 시안 수준에서는 문제가 되지 않으며, 프로덕션에서도 Apple 플랫폼 한정으로는 충분하다.

---

## 에셋 전략 (Metal 버전과 동일)

### PBR 텍스처 (CC0, 선택사항)
- 나무 (책장): ambientcg.com → Wood049 / Wood054 (1K) — Diffuse, Normal, Roughness
- 텍스처 없이 색상값만으로도 PBR 효과 있음

### HDRI 환경맵 (강력 권장)
- Poly Haven (polyhaven.com) → "interior" 검색 → 무료 HDRI 다운로드
- 예: "brown_photostudio" 또는 "studio_small" 계열
- 1K 해상도면 시안에 충분
- **환경맵 하나가 조명 퀄리티를 극적으로 올려줌**
- RealityKit에서 .hdr 파일을 EnvironmentResource로 바로 로드 가능

---

## 스코프 아님
- ❌ 멀티플랫폼 (macOS만)
- ❌ 네트워크/API
- ❌ AR 기능 (ARKit 연동 안 함, 순수 3D 뷰만)
- ❌ visionOS 빌드 (아키텍처만 호환되게 — 실제 빌드는 추후)
- ❌ 복잡한 UI
- ❌ glTF 로더 (프로시저럴 생성)

---

## Claude Code 사용 전략

RealityKit 버전은 작업량이 상대적으로 적으므로, **단일 세션 + 서브에이전트(subagent)** 방식이 효율적이다.

### 단일 세션 순차 진행 (권장)
```
메인 세션: Phase 1 → 2 → 3 → 4 → 5 → 6 → 7
각 Phase 끝에 빌드 확인 후 다음 진행
```

### 서브에이전트 활용 (선택)
Phase 2~3 진행 중 서브에이전트로 병렬 작업 가능:
- **메인**: BookshelfEntity + BookEntity (Phase 2~3)
- **서브에이전트**: MaterialFactory + EnvironmentSetup (Phase 4 준비)
  - HDRI 파일 찾기, PhysicallyBasedMaterial 프리셋 준비
  - 메인이 Phase 3 끝나면 바로 통합

### 주의사항
- RealityView는 macOS 14+에서만 사용 가능. iOS/visionOS 코드와 혼동하지 말 것
- `Entity.move(to:relativeTo:duration:)` 이 RealityKit의 핵심 애니메이션 API
- CollisionComponent + InputTargetComponent가 있어야 제스처가 동작
- `#if os(macOS)` 분기가 필요할 수 있음 (NSColor vs UIColor 등)
