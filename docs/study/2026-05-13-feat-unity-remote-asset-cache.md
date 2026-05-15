# Remote Unity Asset Delivery / Cache Plan

Branch: `feat/unity/s14p31f206-180-remote-asset-cache`

## Goal

Unity 앱에 모든 꾸미기 에셋을 미리 포함하지 않고, S3에 올라간 에셋을 필요 시 다운로드해서 사용한다.

- 노트북 에셋 스토어: 서버의 `previewImageUrl`로 에셋 이미지를 보여준다.
- 구매 후 메인 꾸미기: 구매한 에셋이 인벤토리에 나타난다.
- 꾸미기 선택 UI: 서버 에셋의 preview 이미지를 보여준다.
- 배치 시: 실제 에셋 파일을 S3 URL에서 받아 로컬 캐시에 저장하고 GameObject로 로드한다.
- 다른 사람 공간 방문: 내 앱에 없는 에셋이면 방문 중 다운로드 후 캐시해서 다음 방문부터 빠르게 로드한다.

## Existing Backend State

MR !180 merged:

- `Asset.theme`, `Asset.placement`가 `compatibleTargets` JSON LOB에서 별도 컬럼으로 분리됨.
- 일반 사용자용 `AssetResponse`에 `theme`, `placement`, `collectionCode`, `fileSize`, `fileHash`, `contentType`, `version`이 이미 포함됨.
- 관리자 API는 이미 S3 presigned upload를 지원함.
  - `POST /v1/admin/assets/{assetId}/bundle/upload-url`
  - `PATCH /v1/admin/assets/{assetId}/bundle`
  - `POST /v1/admin/assets/{assetId}/preview-image/upload-url`
  - `PATCH /v1/admin/assets/{assetId}/preview-image`
  - `POST /v1/admin/assets/{assetId}/preview-bundle/upload-url`
  - `PATCH /v1/admin/assets/{assetId}/preview-bundle`
- 사용자 API도 다운로드 manifest 골격이 있음.
  - `GET /v1/assets/{assetId}/download-manifest`
  - 현재는 소유한 에셋만 다운로드 가능.

## Backend Gaps

1. `GET /v1/assets/me/inventory` 응답이 현재 `inventoryId`, `assetId`만 내려준다.
   - Unity 꾸미기 인벤토리는 이름, preview image, placement, theme, file metadata가 필요하다.
   - 1차 수정: `items[].asset`에 `AssetResponse`를 같이 내려서 N+1 상세 조회 없이 UI를 구성하게 한다.

2. 다른 사람 공간 방문용 다운로드 권한이 아직 분리되어 있지 않다.
   - 현재 manifest는 `내가 소유한 assetId`만 허용한다.
   - 방문자는 그 에셋을 소유하지 않아도 공개 공간에 배치된 에셋은 렌더링해야 한다.
   - 1차 구현: `GET /v1/spaces/{spaceId}/asset-manifests`를 추가했다.
   - 현재 범위는 `spaces.current*InventoryId`로 적용된 카테고리/테마 에셋이다.
   - 다음 단계는 layoutPayload 안에 저장될 `inv:{inventoryId}` / `asset:{assetId}` 배치 오브젝트까지 manifest 대상에 포함하는 것이다.

## Existing Unity State

- 노트북 에셋 스토어는 `GET /v1/assets`를 이미 호출한다.
- `NotebookAssetItem`은 현재 최소 필드만 가진다.
  - 부족: `collectionCode`, `theme`, `placement`, `fileSize`, `fileHash`, `contentType`, `version`, `inventoryId`.
- 꾸미기 인벤토리는 `EditableDecorationCatalog` 로컬 prefab catalog 기반이다.
  - 저장 키는 이미 `asset:{id}` / `inv:{id}` prefix를 미래 서버 인벤토리용으로 허용한다.
  - 즉 저장 구조는 원격 에셋 도입을 어느 정도 예상하고 있다.
- `IThemeAssetLoader` 주석에도 향후 `RemoteThemeAssetLoader` 도입 계획이 이미 적혀 있다.
- 패키지에 `com.unity.cloud.gltfast`와 AssetBundle 모듈이 있다.
  - GLB라면 glTFast 경로 사용 가능.
  - Unity AssetBundle이라면 `AssetBundle.LoadFromFileAsync` / `UnityWebRequestAssetBundle` 경로 사용 가능.

## Recommended Delivery Contract

Asset store list:

```http
GET /v1/assets?category=...&sort=popular&limit=...
```

- public metadata + `previewImageUrl`.
- 실제 본체 URL은 노출하지 않는다.

Owned inventory:

```http
GET /v1/assets/me/inventory
```

Expected response after 1차 수정:

```json
{
  "assetIds": [10],
  "items": [
    {
      "inventoryId": 88,
      "assetId": 10,
      "asset": {
        "id": 10,
        "code": "WOODEN_DESK_01",
        "name": "원목 책상",
        "assetType": "DESK",
        "collectionCode": "NATURAL",
        "theme": "NATURAL",
        "placement": "floor",
        "previewImageUrl": "https://...",
        "fileSize": 102400,
        "fileHash": "sha256:...",
        "contentType": "model/gltf-binary",
        "version": 3,
        "owned": true
      }
    }
  ]
}
```

Placement/download:

```http
GET /v1/assets/{assetId}/download-manifest
```

- 구매한 내 공간 꾸미기에서는 이 API를 사용한다.
- Unity는 `assetId + version + fileHash`로 캐시 키를 만든다.

Visitor mode:

```http
GET /v1/spaces/{spaceId}/asset-manifests
```

- 공개 공간에 실제 배치된 에셋만 반환해야 한다.
- 1차 구현 완료: 카테고리/테마 inventory manifest 반환.
- 다음 단계: layoutPayload parsing 또는 layout item 정규화 테이블 도입 후 자유 배치 오브젝트 manifest까지 반환.

## Unity Cache Strategy

Cache root:

```text
Application.persistentDataPath/DotShelfRemoteAssets/
```

Cache key:

```text
asset-{assetId}/v{version}/{normalizedHash}/
```

Validation:

- `fileHash`가 있으면 다운로드 후 hash 검증.
- `fileHash`가 없으면 `fileSize + version`으로 최소 검증.
- manifest JSON을 같이 저장해서 다음 실행 때 URL 요청 전 빠른 cache hit 가능.

Runtime load:

- `contentType == model/gltf-binary` 또는 `.glb`: glTFast로 로드.
- `contentType`이 AssetBundle 계열 또는 확장자 `.bundle`: AssetBundle 로드.
- 실패 시 UI에는 "다운로드 실패" 상태를 보여주고 로컬 catalog fallback은 유지한다.

## Implementation Order

1. Backend inventory response enrich
   - `InventoryItemResponse`에 `AssetResponse asset` 추가.
   - `AssetService.getInventory`에서 asset들을 batch 조회해서 매핑.
   - 테스트 갱신.

2. Unity API/Application model boundary
   - DTO에 `collectionCode`, `theme`, `placement`, `fileSize`, `fileHash`, `contentType`, `version`, `asset` 추가.
   - Application 모델에 같은 메타 추가.
   - `LoadAssetInventory`, `LoadAssetDownloadManifest` usecase 추가.

3. Unity remote cache service
   - manifest 기반 다운로드/검증/cache.
   - GLB/AssetBundle load abstraction.

4. Decoration inventory integration
   - 로컬 `EditableDecorationCatalog` + 서버 owned inventory를 함께 표시.
   - 서버 에셋 슬롯은 `previewImageUrl` 이미지를 사용.
   - 배치 시 `inv:{inventoryId}` 또는 `asset:{assetId}`로 저장.

5. Other space visitor integration
   - 방문 공간 layout에 포함된 원격 asset key를 해석.
   - 방문 전용 manifest API가 생기면 해당 API로 캐시 로드.
   - 편집/이동은 금지, 보기/책 확인만 허용.

6. Verification
   - Backend unit/controller tests.
   - Unity compile check.
   - 에셋 스토어 preview image 표시.
   - 구매 후 꾸미기 인벤토리 표시.
   - 최초 배치 시 다운로드, 재진입 시 cache hit.
   - 타인 공간 방문 시 missing asset cache download.

## Progress

- [x] `dev` 최신 기준 브랜치 생성.
- [x] MR !180 확인.
- [x] 백엔드 에셋 API / S3 업로드 / manifest 구조 확인.
- [x] Unity 노트북 에셋 스토어 / 꾸미기 로컬 catalog 구조 확인.
- [x] Backend inventory response enrich.
- [x] 방문 공간용 `GET /v1/spaces/{spaceId}/asset-manifests` 1차 추가.
- [x] Unity DTO/Application model 확장.
- [x] Unity remote asset cache service 추가.
- [x] 다른 사람 공간 방문 시 manifest 기반 백그라운드 캐시 prefetch 연결.
- [ ] 꾸미기 인벤토리 원격 에셋 표시/배치 연결.
- [ ] 타인 공간 방문용 manifest 권한/API 설계 반영.
- [ ] 검증 및 코드리뷰.

## Current Implementation Notes

- Backend `GET /v1/assets/me/inventory` now includes `items[].asset` so Unity can build an owned asset inventory without per-item detail calls.
- Backend `GET /v1/spaces/{spaceId}/asset-manifests` returns manifests for owner-applied current category/theme inventory IDs.
- Unity `NotebookApiClient` now has:
  - `GetAssetDownloadManifest(assetId, ...)`
  - `GetSpaceAssetManifests(spaceId, ...)`
- Unity `RemoteAssetCacheService` downloads files under:

```text
Application.persistentDataPath/DotShelfRemoteAssets/asset-{assetId}/v{version}/{hash}/
```

- `BookshelfApiSyncController.EnterSpaceById` now calls space asset manifest prefetch for other-user spaces.
- Runtime instantiation from cached GLB/AssetBundle into `RoomEditModeController` is now connected in the 2026-05-14 update below.

## Resume Notes

다음에 이어서 할 때는 이 순서로 확인하면 된다.

1. `git status --short --branch`
2. `backend/src/main/java/com/dotshelf/domain/asset/dto/AssetDtos.java`
3. `backend/src/main/java/com/dotshelf/domain/asset/service/AssetService.java`
4. `frontend/Assets/App/Infrastructure/Api/Dtos/NotebookApiDtos.cs`
5. `frontend/Assets/App/Application/Notebook/NotebookRuntimeModels.cs`
6. `frontend/Assets/App/Bootstrap/RoomEditModeController.cs`

## 2026-05-14 Update

Completed in this branch:

- Backend now exposes `GET /v1/spaces/{spaceId}/layout` for the latest saved `layoutPayload`.
- Backend `GET /v1/spaces/{spaceId}/asset-manifests` now includes assets referenced by the latest layout payload as `asset:{assetId}` or legacy `inv:{inventoryId}` keys, after verifying the space owner owns them.
- Unity saves decoration layout snapshots to `/v1/spaces/me/layout` when the room edit save succeeds, while keeping the existing local JSON save path.
- Remote decoration inventory keys now prefer `asset:{assetId}` so visitor restoration can resolve by public space manifest.
- Unity registers visitor-space manifests before loading the visitor layout, so another user's remote assets can be restored without calling the owner-only `/v1/assets/{assetId}/download-manifest` endpoint.
- `RoomEditModeController` can restore external server layout payloads, instantiate cached GLB/GLTF via glTFast, and load AssetBundle files from the remote cache.
- Notebook object cards and the room edit inventory now use `previewImageUrl` when present.

Verified:

- Backend targeted tests passed:
  `./gradlew.bat test --tests "com.dotshelf.domain.asset.service.AssetServiceTest" --tests "com.dotshelf.domain.asset.controller.AssetControllerTest" --tests "com.dotshelf.domain.space.service.SpaceServiceTest" --tests "com.dotshelf.domain.space.controller.SpaceControllerTest"`
- Unity EditMode tests passed:
  `Unity.exe -batchmode -projectPath frontend -runTests -testPlatform EditMode`

Remaining practical validation:

- Run the Unity app against dev API with a real purchased S3-backed asset.
- Purchase/own an asset from the notebook asset store, enter decorate mode, confirm the preview appears, place it, save, leave and re-enter.
- Visit the same space from another account and confirm the remote asset is downloaded into `Application.persistentDataPath/DotShelfRemoteAssets` and rendered from cache.
- Confirm S3 objects have correct `contentType`, `fileSize`, and `fileHash`; hash validation is strict when a `sha256:` hash is present.
