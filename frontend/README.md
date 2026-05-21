# dotshelf-demo-unity

Unity 6 기반 3D 책장 방 MVP입니다. 사용자는 하나의 `MainScene` 안에서 책장을 보고, 책을 선택하거나 이동하고, 이후 검색/추가/독후감/방명록/피드 기능으로 확장하는 흐름을 목표로 합니다.

현재 저장소는 두 축이 함께 있습니다.

- `Assets/App`: phase 문서 기준으로 새로 정리 중인 애플리케이션 아키텍처
- `Assets/Scripts/BookshelfPorting/Runtime`: 기존 프로토타입을 Unity 씬에서 동작시키는 포팅 런타임

## Unity Version

- Unity Editor: `6000.3.13f1`
- Render Pipeline: Universal Render Pipeline `17.3.0`
- Main target scene: `Assets/Scenes/MainScene.unity`

## Getting Started

1. Unity Hub에서 이 프로젝트 폴더를 엽니다.
2. Unity 버전은 `6000.3.13f1`을 사용합니다.
3. 패키지 복원이 끝나고 스크립트 컴파일이 완료될 때까지 기다립니다.
4. `Assets/Scenes/MainScene.unity`를 엽니다.
5. 씬에 책장/방 오브젝트가 보이지 않으면 `Tools > Project Setup > Build MainScene Content`를 실행합니다.
6. `MainScene`에서 Play를 실행합니다.

초기 구조를 다시 생성해야 할 때는 `Tools > Project Setup > Run Phase 0-1 Setup`을 사용할 수 있습니다.

## Scenes

Build Settings에는 현재 다음 씬이 등록되어 있습니다.

| Scene | Purpose |
| --- | --- |
| `Assets/Scenes/MainScene.unity` | 현재 MVP 실행용 메인 씬 |
| `Assets/Scenes/BootstrapScene.unity` | AppRoot/bootstrap 흐름 검증용 씬 |

`MainScene`은 단일 메인 공간을 유지하고, 화면 전환은 씬 교체가 아니라 카메라 상태, UI overlay, feature controller 전환으로 처리하는 방향입니다.

## Project Structure

```text
Assets/
  App/
    Bootstrap/        AppRoot, config load, session init, scene entry
    Config/           AppApiConfig 등 환경 설정
    Core/             공통 타입과 유틸리티
    Domain/           순수 도메인 모델
    Application/      UseCase와 애플리케이션 서비스 경계
    Infrastructure/   API, 저장소, 외부 연동 구현
    Features/         기능별 Unity presentation/controller 코드
    Platform/         플랫폼별 분기
    UI/               공통 UI
    Editor/           phase setup, scene builder, verifier 도구
    Tests/            테스트 영역
  Scripts/
    BookshelfPorting/
      Runtime/        기존 3D 책장 프로토타입 포팅 런타임
  Scenes/             MainScene, BootstrapScene, SampleScene
  Settings/           URP renderer/profile 설정
  QA_Books/           책 모델/프리팹/텍스처 테스트 에셋
  Arts/               UI/아트 리소스
  Docs/               포팅 가이드
docs/                 phase별 작업 리포트와 진행 상태 기록
Packages/             Unity Package Manager manifest/lock
ProjectSettings/      Unity 프로젝트 설정
```

## Architecture Direction

새 기능은 `Assets/App` 아래에 추가합니다. 기존 포팅 런타임은 동작 검증과 시각적 MVP를 위해 유지하되, phase가 진행될수록 책임을 `Domain`, `Application`, `Infrastructure`, `Features`로 옮기는 방향입니다.

핵심 규칙:

- `Domain` 계층은 `UnityEngine`에 의존하지 않습니다.
- Feature 코드는 API나 저장소 구현체를 직접 호출하지 않고 `Application`/UseCase 경계를 거칩니다.
- API 연동은 `Infrastructure`에 둡니다.
- 책 드래그/배치는 물리 기반이 아니라 slot 기반으로 유지합니다.
- preview 상태는 Domain 배치 데이터를 확정 변경하지 않습니다.
- Mock API와 Real API는 base URL 차이만으로 전환 가능해야 합니다.

현재 `Assets/App`에는 다음 phase skeleton이 들어와 있습니다.

- `Bootstrap`: `AppRoot`, `ConfigLoader`, `EnvironmentResolver`, `SessionInitializer`, `SceneEntry`
- `LibraryRoom`: `RoomStateController`, `CameraStateController`
- `ShelfSwitch`: `ShelfManager`, `ShelfView`, `ShelfSectionView`, `ShelfLayoutCalculator`, `ShelfTurntableController`
- `BookViewer`: `BookState`, `BookView`, `BookInteractionManager`, `BookFocusController`
- `Debug`: `DebugHud`

## Assembly Definitions

현재 정의된 주요 asmdef는 다음과 같습니다.

- `Assets/App/Core/App.Core.asmdef`
- `Assets/App/Domain/App.Domain.asmdef`
- `Assets/App/Application/App.Application.asmdef`
- `Assets/App/Infrastructure/App.Infrastructure.asmdef`
- `Assets/App/Features/App.Features.asmdef`

외부/에셋 쪽 asmdef:

- `Assets/Packages/ai.meshy/Editor/Script/MeshyAssembly.asmdef`

## Packages

`Packages/manifest.json` 기준 주요 패키지는 다음과 같습니다.

| Package | Version | Usage |
| --- | --- | --- |
| `com.unity.render-pipelines.universal` | `17.3.0` | URP 렌더링 |
| `com.unity.cinemachine` | `2.10.7` | 카메라 상태 전환 |
| `com.unity.inputsystem` | `1.19.0` | 입력 처리 |
| `com.unity.ugui` | `2.0.0` | 런타임 UI |
| `com.unity.textmeshpro` | `5.0.0` | 텍스트/UI 표시 |
| `com.unity.addressables` | `2.10.1` | 향후 에셋 로딩 경계 |
| `com.cysharp.unitask` | `2.5.10` | 비동기 흐름 |
| `com.unity.nuget.newtonsoft-json` | `3.2.2` | JSON 직렬화 |
| `com.unity.formats.fbx` | `5.1.5` | FBX 모델 에셋 |
| `com.unity.cloud.gltfast` | `6.18.0` | glTF/GLB 에셋 처리 |
| `com.unity.test-framework` | `1.6.0` | Unity 테스트 |
| `com.unity.ai.navigation` | `2.0.12` | Navigation 패키지 |

추가 에디터 플러그인:

- `Assets/Packages/ai.meshy`: Meshy Bridge `0.2.1`

아직 설치되지 않은 항목:

- DOTween은 현재 manifest에 없습니다. Tween 의존 기능을 구현하기 전 설치 경로와 라이선스/배포 방식을 먼저 확정해야 합니다.

## Current Implementation Status

완료 또는 skeleton 반영:

- Phase 00: `Assets/App` 기본 폴더와 asmdef 구조
- Phase 01: `MainScene`/`BootstrapScene`, scene setup editor tools, room/camera state skeleton
- Phase 02: 책장/섹션 Domain 모델과 Shelf feature skeleton
- Phase 03: 책 오브젝트 focus 상태와 BookViewer skeleton
- 기존 포팅 런타임 기반 `MainScene` 책장/방 콘텐츠 생성

진행 예정:

- Phase 04: 같은 책장 내 slot 기반 드래그
- Phase 05: gap preview
- Phase 06: 책장 간 드래그 이동
- Phase 07: Mock API/repository
- Phase 08: 도서 검색 및 추가
- Phase 09: 독후감/방명록/feed
- Phase 10: 안정화, 테스트, 정리

상세 진행 기록은 `docs/progress-audit.md`와 `docs/phase-*.md`를 확인합니다.

## Editor Tools

Unity 메뉴에서 사용할 수 있는 프로젝트 도구:

- `Tools > Project Setup > Run Phase 0-1 Setup`
  - phase 0~1 기준 폴더, asmdef, scene/build setting 초기 구조를 재생성합니다.
- `Tools > Project Setup > Build MainScene Content`
  - `MainScene`을 열고 기존 포팅 런타임 기반 방/책장 콘텐츠를 생성한 뒤 저장합니다.

## Development Notes

- Git에 올릴 때 `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `.vs/` 같은 Unity 생성물은 제외합니다.
- `Assets/App/Domain`에 Unity component, `MonoBehaviour`, `GameObject`, `Vector3` 같은 Unity 의존 타입을 넣지 않습니다.
- feature별 View는 serialized Unity 참조와 표시 갱신을 담당하고, Controller는 입력/상태 전환/UseCase 호출을 담당합니다.
- 기존 `BookshelfExperienceManager`, `BookInteractionController`, `NotebookScreenController`는 큰 포팅 런타임 클래스이므로 새 기능을 추가할 때는 가능한 한 `Assets/App` 구조로 분리합니다.

## Verification Checklist

변경 후 최소 확인 항목:

- Unity Console compile error 0개
- `MainScene` 단독 Play 가능
- `Tools > Project Setup > Build MainScene Content` 실행 후 책장/방 콘텐츠 표시
- `Assets/App/Domain`에 `UnityEngine` 참조 없음
- 새 feature가 `Application`/UseCase 경계를 우회해 `Infrastructure`를 직접 호출하지 않음
