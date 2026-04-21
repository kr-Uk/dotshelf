# Unity Bookshelf Porting Guide

## 1. 현재 RealityKit 프로젝트를 Unity로 옮길 때의 전체 설계 요약
이 포팅안은 RealityKit 쪽의 핵심 구조를 Unity 런타임 계층으로 그대로 나누는 방식이다. 중심은 `BookshelfState`가 4x4 섹션 데이터를 소유하고, `BookshelfGenerator`가 절차적으로 책장과 기본 책을 만들며, `BookInteractionController`가 클릭, 보기, 회전, 드래그, 드롭, 방향변경을 처리하는 구조다. 각 책은 `BookEntity`가 담당하고, 레이아웃 규칙은 orientation별 footprint 계산으로 분리한다.

핵심은 "책의 회전"과 "책이 섹션에서 차지하는 폭"을 분리하는 것이다. Unity에서도 `BookOrientation` enum과 `BookFootprintUtility`를 분리해 `spine`, `front`, `angled45`에 따라 레이아웃 폭을 계산한다. 카메라는 `CameraController`가 `overview`, `frontal`, `whiteboard` 세 상태를 관리하며 `frontal` 상태에서만 줌을 허용한다.

런타임 흐름:

1. `BookshelfGenerator`가 절차적으로 방, 책장, 벽, 화이트보드, 섹션 데이터를 만든다.
2. `BookshelfState`가 각 섹션의 `leftBound`, `rightBound`, `shelfTopY`, `sectionHeight`, `books`를 관리한다.
3. 각 섹션에 기본 책 3권씩 생성하고 첫 섹션에는 스캔책 프리팹을 우선 배치한다.
4. `BookInteractionController`가 raycast 기반 선택과 드래그를 처리한다.
5. 방향 변경 시 footprint를 다시 계산하고, 부족한 책은 가장 가까운 다른 섹션으로 밀어낸다.
6. 시각 연출은 URP Lit PBR, Reflection Probe, Skybox/HDRI, warm light 세팅으로 맞춘다.

## 2. RealityKit -> Unity 대응 표
| RealityKit 개념 | Unity 대응 | 포팅 메모 |
|---|---|---|
| `Entity` | `GameObject` | 계층 구조는 Transform parent-child로 유지 |
| `ModelEntity` | `GameObject + MeshFilter + MeshRenderer` 또는 Primitive | 절차 책/책장 보드는 Cube primitive로 대체 |
| `Component` | `MonoBehaviour` / built-in Component | 상태성 있는 로직은 MonoBehaviour로 분리 |
| `Transform` | `Transform` | 둘 다 position/rotation/scale 보유 |
| `AnchorEntity` | 씬 루트 오브젝트 / parent root | `RoomRoot`, `BookshelfRoot`로 분리 추천 |
| Gesture input | Input System + Physics Raycast | Tap/drag를 pointer down/move/up로 대응 |
| `CollisionComponent` | `Collider` | 책은 `BoxCollider`, 스캔책은 `MeshCollider` 또는 복합 collider |
| `PhysicsBodyComponent` | `Rigidbody` | 이 프로젝트는 kinematic 위주 권장 |
| Physically based material | URP `Lit` Shader Material | Base Map / Normal / Smoothness로 대응 |
| Animation / `move(to:)` | Coroutine + Lerp/Slerp | 본 초안은 coroutine 기반 |
| Swift 상태 관리 | `BookshelfState` + controller references | UI 상태는 enum과 MonoBehaviour 필드로 관리 |
| USDZ asset | `FBX` / `GLB` / `glTF` | Unity 기본 파이프라인은 FBX가 가장 안정적 |

좌표계 차이:

- RealityKit, Unity 모두 Y-up이지만 asset import 축이 자주 어긋난다.
- Unity는 일반적으로 `Transform.forward = +Z`를 기준으로 쓴다.
- 책장을 정면에서 볼 때 책 `spine/front` 회전은 Y축 회전으로 통일하는 것이 안전하다.
- USDZ 변환 시 모델이 90도 누워 있으면 import root 아래 보정 parent를 둔다.

## 3. 추천 폴더 구조
```text
Assets/
  Art/
    Models/
      Books/
        TestBook/
    Textures/
      Wood/
      HDRI/
  Materials/
    Shared/
  Prefabs/
    Books/
    Environment/
    Bookshelf/
  Scenes/
    MainBookshelf.unity
  Scripts/
    BookshelfPorting/
      Runtime/
        BookOrientation.cs
        BookFootprintUtility.cs
        ShelfSection.cs
        BookEntity.cs
        BookshelfState.cs
        BookshelfGenerator.cs
        BookInteractionController.cs
        CameraController.cs
        EnvironmentSetup.cs
        MaterialFactory.cs
  Docs/
    UnityBookshelfPortingGuide.md
```

## 4. 추천 씬 구조
```text
MainBookshelf
  Systems
    BookshelfState
    BookshelfGenerator
    BookInteractionController
    CameraController
    EnvironmentSetup
    MaterialFactory
  Cameras
    Main Camera
    OverviewAnchor
    FrontalAnchor
    WhiteboardAnchor
    BookViewAnchor
  Environment
    RoomRoot
      Floor
      BackWall
      LeftWall
      RightWall
      Whiteboard
      ReflectionProbe
      Lights
  BookshelfRoot
    BookshelfGeometry
    BooksRoot
    GhostPreview
```

권장 월드 기준:

- 책장 중심: `(0, 0.8, 0.9)`
- 책장 정면 방향: `+Z`
- overview: `(1.9, 1.45, -2.2)` looking at `(0, 0.8, 0.95)`
- frontal: `(0, 0.95, -1.2)` looking at `(0, 0.85, 1.02)`
- whiteboard: `(-1.45, 1.05, -0.25)` looking at `(-1.95, 1.05, 0.15)`

## 5. 핵심 클래스 설계
### `BookshelfGenerator`
- 절차적 방/책장 생성
- 4x4 섹션 경계 계산
- 기본 책 3권씩 생성
- 첫 섹션에 스캔책 프리팹 우선 배치

### `ShelfSection`
- row/column 인덱스
- `leftBound`, `rightBound`, `shelfTopY`, `sectionHeight`
- `books` 목록

### `BookshelfState`
- 전체 섹션 목록 보관
- 섹션별 레이아웃 실행
- 섹션 overflow 처리
- orientation 변경 후 재배치
- 가까운 대체 섹션 탐색

### `BookEntity`
- 책의 크기/색/현재 orientation/원래 섹션/현재 섹션 보관
- footprint 계산
- visual 회전 적용
- view mode 복귀용 original transform 저장

### `BookInteractionController`
- raycast 선택
- 책 보기 상태 진입/복귀
- 보기 상태 회전 드래그
- 이동 드래그와 ghost preview
- orientation 변경 API

### `CameraController`
- overview/frontal/whiteboard 상태
- 부드러운 카메라 전환
- frontal 전용 zoom clamp

### `EnvironmentSetup`
- warm lighting, ambient, reflection probe 세팅
- skybox/HDRI assignment

### `MaterialFactory`
- URP Lit 재질 생성
- wood / wall / whiteboard / ghost material 구성

## 6. C# 스크립트 초안
실제 구현 가능한 초안은 아래 경로에 포함되어 있다.

- [BookOrientation.cs](/C:/SSAFY/sample/Assets/Scripts/BookshelfPorting/Runtime/BookOrientation.cs)
- [BookFootprintUtility.cs](/C:/SSAFY/sample/Assets/Scripts/BookshelfPorting/Runtime/BookFootprintUtility.cs)
- [ShelfSection.cs](/C:/SSAFY/sample/Assets/Scripts/BookshelfPorting/Runtime/ShelfSection.cs)
- [BookEntity.cs](/C:/SSAFY/sample/Assets/Scripts/BookshelfPorting/Runtime/BookEntity.cs)
- [BookshelfState.cs](/C:/SSAFY/sample/Assets/Scripts/BookshelfPorting/Runtime/BookshelfState.cs)
- [BookshelfGenerator.cs](/C:/SSAFY/sample/Assets/Scripts/BookshelfPorting/Runtime/BookshelfGenerator.cs)
- [BookInteractionController.cs](/C:/SSAFY/sample/Assets/Scripts/BookshelfPorting/Runtime/BookInteractionController.cs)
- [CameraController.cs](/C:/SSAFY/sample/Assets/Scripts/BookshelfPorting/Runtime/CameraController.cs)
- [EnvironmentSetup.cs](/C:/SSAFY/sample/Assets/Scripts/BookshelfPorting/Runtime/EnvironmentSetup.cs)
- [MaterialFactory.cs](/C:/SSAFY/sample/Assets/Scripts/BookshelfPorting/Runtime/MaterialFactory.cs)

## 7. Unity 에디터 세팅 방법
### 필수 컴포넌트 연결
1. 빈 오브젝트 `Systems` 생성.
2. `BookshelfState`, `BookshelfGenerator`, `BookInteractionController`, `CameraController`, `EnvironmentSetup`, `MaterialFactory`를 붙인다.
3. `BookshelfGenerator`의 `state`, `materialFactory` 필드를 연결한다.
4. `BookInteractionController`의 `state`, `cameraController`, `generator`, `materialFactory`를 연결한다.

### 카메라 앵커
1. `Cameras` 아래에 `OverviewAnchor`, `FrontalAnchor`, `WhiteboardAnchor`, `BookViewAnchor` 생성.
2. anchor 위치를 위 권장 좌표 기준으로 배치.
3. `CameraController`에 메인 카메라와 anchor 4개를 연결.

### 책장 생성
1. play mode 시작 또는 `BookshelfGenerator.Generate()` context menu 실행.
2. `booksRoot`, `bookshelfRoot`, `roomRoot`가 비어 있으면 자동 생성된다.
3. `scannedBookPrefab`에는 변환된 `testbook` prefab을 연결한다.

### 입력
1. `Project Settings > Player > Active Input Handling`을 `Both` 또는 `Input System Package (New)`로 설정.
2. 현재 초안은 `Mouse.current` 기준이라 PC 테스트가 바로 가능하다.
3. 모바일 대응 시 `Pointer.current` 또는 `EnhancedTouch`로 같은 구조를 확장하면 된다.

## 8. 자산 변환 전략(특히 USDZ)
### `testbook.usdz`
권장 순위는 아래와 같다.

1. `FBX`
2. `GLB`
3. `glTF`

권장 워크플로우:

1. `testbook.usdz`를 Blender 또는 Reality Converter로 연다.
2. 축과 스케일을 확인한다.
3. Unity용으로 `FBX` export.
4. Unity import 후 root prefab을 만들고 회전 보정 parent를 둔다.
5. `BookEntity`를 붙여 layout dimension을 직접 지정한다.

USDZ가 바로 어려우면:

- 1차 구현에서는 같은 bounding box를 가진 placeholder prefab으로 연결
- 나중에 스캔책 prefab만 교체
- 핵심은 `BookEntity`가 "시각 모델"과 "레이아웃 footprint"를 분리해서 가지는 것이다

### Wood / HDRI 자산 세팅
보유 파일:

- `Wood049_1K-JPG_Color.jpg`
- `Wood049_1K-JPG_NormalGL.jpg`
- `Wood049_1K-JPG_Roughness.jpg`
- `brown_photostudio_04_4k.exr`

URP 세팅:

1. Color 텍스처: `sRGB On`
2. NormalGL: Texture Type `Normal map`
3. Roughness: `Default`, `sRGB Off`
4. URP Lit material에서:
   - Base Map = Color
   - Normal Map = NormalGL
   - Smoothness = `1 - roughness`
   - 간단 구현은 roughness 맵을 못 쓰면 상수 Smoothness 0.35~0.5로 시작
   - 정확도를 높이려면 roughness를 alpha smoothness로 패킹한 마스크 텍스처를 추가 제작

재질 추천:

- bookshelf wood: metallic 0, smoothness 0.42
- floor wood: metallic 0, smoothness 0.28
- wall: off-white lit, smoothness 0.08
- whiteboard frame: dark wood/metal, smoothness 0.32
- whiteboard surface: white lit, smoothness 0.62
- ghost preview: URP Lit transparent, alpha 0.25

### HDRI / Reflection Probe
1. `brown_photostudio_04_4k.exr`를 `Assets/Art/HDRI`로 복사
2. `Lighting` 창에서 `Skybox/Panoramic` material 생성
3. EXR 연결
4. 방 중앙에 box `Reflection Probe` 1개 배치, size는 방 전체를 커버
5. 필요하면 책장 앞 보조 probe 추가

## 9. 구현 Phase 분할
### Phase 1. 상태/레이아웃 포팅
- `BookshelfState`, `ShelfSection`, `BookEntity`, footprint 계산 완성
- orientation 변경과 overflow relocation 검증

### Phase 2. 절차 책장/방 생성
- 책장 보드, 방, 벽, 화이트보드, floor 생성
- 기본 책 3권씩 배치

### Phase 3. 인터랙션
- 클릭 보기, view rotation, close restore
- drag and drop, ghost preview, nearest valid section 계산

### Phase 4. 카메라
- overview/frontal/whiteboard transition
- frontal zoom clamp

### Phase 5. 룩디브
- wood PBR
- HDRI, reflection probe, warm lights
- 스캔책 모델 교체

### Phase 6. 안정화
- invalid drop fallback
- collider fine-tune
- clipping과 pivot 조정

## 10. 구현 중 주의할 점
- orientation은 회전값이 아니라 레이아웃 규칙이다. footprint와 visual rotation을 같이 갱신해야 한다.
- drag 중에는 원래 섹션에서 책을 먼저 제거하고 나머지 책을 즉시 재배치해야 공간 판단이 정확해진다.
- 스캔책은 실제 mesh 폭을 그대로 쓰지 말고 `layoutDimensions`를 따로 둬야 한다.
- Unity primitive cube는 기본 크기 1m라서 localScale로 실측 단위를 맞춘다.
- 책 `spine/front` 정의는 모델 원점 방향에 따라 반대로 보일 수 있으니 첫 import에서 기준축을 고정해야 한다.
- URP Lit은 roughness 맵을 직접 받지 않으므로 smoothness 파이프라인을 맞추는 전처리가 필요하다.
- 카메라 view mode 중 책을 카메라 앞 anchor로 이동시킬 때 parent를 바꾸지 않고 월드 transform 보간으로 처리해야 복귀가 단순하다.
