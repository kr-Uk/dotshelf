# DeepSea 테마 에셋과 배치 적용

- **브랜치**: `feat/fe/deepsea`
- **범위**: `merge-base(origin/dev, HEAD)..HEAD` (커밋 1개)
- **기간**: 2026-05-12
- **티켓**: 없음

## TL;DR

Unity 꾸미기 시스템에 `DeepSea` 테마를 추가했다. 벽지/바닥/가구/소품/대시보드/모니터 에셋을 카탈로그와 테마 프리셋에 등록하고, 테마 선택 시 사용자가 조정한 배치가 그대로 재현되도록 했다. 대시보드 패널과 노트북 화면은 각각 보드/모니터 기준 좌표로 붙여 카메라 시점 변화에도 화면이 밀려 보이지 않게 했다.

## 무엇을 했나

### 1. DeepSea 테마 프리셋과 에셋 등록

- 사용자 관점 효과: 테마 목록에서 DeepSea를 선택하면 바다 테마 벽지, 바닥, 가구, 장식물이 한 번에 적용된다.
- 시스템 관점 효과: `RoomTheme_DeepSea.asset`, `RoomThemeCatalog.asset`, `EditableDecorationCatalog.asset`, 각 skin catalog에 DeepSea 항목을 연결했다.
- 주요 파일: `frontend/Assets/App/Composition/Configs/RoomThemes/RoomTheme_DeepSea.asset`, `frontend/Assets/Resources/RoomThemeCatalog.asset`, `frontend/Assets/Resources/EditableDecorationCatalog.asset`

### 2. DeepSea 모델/텍스처/머티리얼 추가

- 사용자 관점 효과: aquarium, shark, dolphin, coral, shelf, desk, board 등 실제 DeepSea 오브젝트를 테마에 사용할 수 있다.
- 시스템 관점 효과: Unity import metadata와 material을 함께 커밋해 다른 개발 환경에서도 동일한 에셋 참조가 유지된다.
- 주요 파일: `frontend/Assets/Arts/Models/deepsea_*.glb`, `frontend/Assets/Arts/Textures/Environment/**/deepsea_*.png`, `frontend/Assets/App/Composition/Materials/Themes/DeepSea/*`

### 3. 보드/모니터 화면 고정 로직 추가

- 사용자 관점 효과: 대시보드는 DeepSea board 위에 붙고, 모니터 화면은 DeepSea monitor 위에 고정되어 시점에 따라 위치가 어긋나지 않는다.
- 시스템 관점 효과: `NotebookStation` 위치 계산을 DeepSea에서는 `MeshSwap_DeskMonitor` local transform 기준으로 바꿨다. 기존 GoodVibes 등 다른 테마는 기존 좌표 해석을 유지한다.
- 주요 파일: `frontend/Assets/App/Bootstrap/RoomEditModeController.cs`, `frontend/Assets/Scripts/BookshelfPorting/Runtime/CameraController.cs`

## 왜 했나

DeepSea 테마는 단순히 벽지/바닥만 바꾸는 수준이 아니라, 기존 책장/책상/대시보드까지 바다 테마 모델로 바꾸고 여러 소품을 배치해야 한다. 특히 대시보드와 모니터 화면은 실제 사용자가 보는 UI이므로 3D 모델 위에 정확히 얹혀야 한다. 화면을 월드 좌표나 다른 기준 오브젝트 좌표에 놓으면 카메라 이동이나 테마 재적용 순서에 따라 어긋나 보일 수 있어, 화면이 붙어야 하는 실제 메쉬를 기준점으로 삼는 방식이 필요했다.

## 어떻게 구현했나

### DeepSea 전용 monitor-local 화면 배치

책상 테마 적용 시 모니터 메쉬를 먼저 생성하고, 반환된 transform을 `NotebookStation` 배치에 넘긴다. DeepSea처럼 `monitorPrefab`을 가진 테마는 `notebookStationPosition`을 모니터 local 좌표로 해석한다.

```csharp
var monitor = ApplyMonitorMeshSwap(roomRoot, entry);
ApplyNotebookStationPlacement(roomRoot, entry, monitor);

var worldPosition = resolvedMonitorAnchor.TransformPoint(entry.NotebookStationPosition);
var worldRotation = resolvedMonitorAnchor.rotation * Quaternion.Euler(entry.NotebookStationRotation);
notebookStation.SetPositionAndRotation(worldPosition, worldRotation);
```

핵심 포인트:
- 화면 위치를 `MeshSwap_DeskMonitor` 기준 local 값으로 저장한다.
- 기존 테마와 충돌하지 않게 `ThemeTag == "DeepSea"` 조건으로 제한한다.
- editor capture 유틸도 같은 기준으로 저장해, Play 중 조정한 위치를 재적용할 수 있게 했다.

### 대시보드 보드와 카메라 기준 동기화

DeepSea board가 기존 벽 위치와 달라질 수 있으므로, 대시보드 화면 패널과 카메라 포커스가 정적인 wall anchor를 보지 않게 했다. `GuestbookHitArea`를 기준으로 화면 패널 크기와 카메라 위치를 다시 계산한다.

```csharp
var guestbookHitArea = FindFirstObjectByType<GuestbookBoardHitArea>(FindObjectsInactive.Include);
cameraController.MoveToWhiteboardSurface(guestbookHitArea != null ? guestbookHitArea.transform : null);
```

핵심 포인트:
- 보드 모델이 바뀌어도 hit area 기준으로 대시보드 화면을 맞춘다.
- 카메라는 현재 보드 표면을 바라보도록 이동한다.
- hit area를 찾지 못하면 기존 whiteboard mode로 fallback한다.

## 배운 것

- **Unity 테마 프리셋은 에셋 참조와 transform 의미를 같이 관리해야 한다**: 같은 `Vector3` 값이어도 어떤 transform의 local 값인지가 다르면 재적용 결과가 달라진다.
- **화면 오브젝트는 실제 붙을 대상 기준으로 계산해야 한다**: 모니터 화면을 책상이나 월드 기준으로 두면 카메라 시점에 따라 어긋난 것처럼 보일 수 있다.
- **Editor capture 유틸과 런타임 적용 로직은 같은 좌표계를 써야 한다**: 저장은 A 기준, 적용은 B 기준이면 수동 보정값이 계속 깨진다.
- **Unity `.meta` 파일은 에셋의 일부다**: 모델/텍스처만 추가하면 GUID가 유지되지 않아 다른 환경에서 catalog 참조가 깨질 수 있다.

## 더 공부할 것

- [ ] Unity에서 world/local transform을 섞어 쓸 때 `SetParent(worldPositionStays)`가 배치 보존에 미치는 영향
- [ ] GLB import 후 pivot/origin이 제각각인 에셋을 테마 배치 시스템에서 표준화하는 방법
- [ ] 테마 에셋을 S3 asset bundle 또는 addressable 형태로 내려받을 때 catalog GUID/assetId를 어떻게 안정적으로 매핑할지
- [ ] 화면 패널처럼 UI가 3D surface 위에 붙는 경우 z-fighting과 depth sorting을 방지하는 기준 offset 설계

## 참고

- 관련 커밋: `94e7dca2`
- 검증: `git diff --cached --check`, `dotnet build frontend/Assembly-CSharp.csproj --no-restore`
