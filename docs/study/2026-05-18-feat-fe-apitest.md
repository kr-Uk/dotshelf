# Unity API 연결 검증과 설정 화면 BE 연동

- **브랜치**: `feat/fe/apitest`
- **범위**: `origin/dev..HEAD` (커밋 1개)
- **기간**: 2026-05-18
- **관련 커밋**: `c277c5c5`

## TL;DR

Unity에서 BE API를 실제로 호출하는 흐름을 점검하면서 `assetId`와 `inventoryId`를 혼동하던 테마/꾸미기 적용 경로를 바로잡았다.
설정 화면은 프로필/알림 일부만 서버와 연결되어 있었고, 결제/신고/차단 목록은 데모 데이터가 남아 있어 실제 API 응답 기반 렌더링으로 교체했다.
BE에서는 구형 테마 PATCH API도 보유한 `THEME` 인벤토리인지 검증하게 하여 신규 `/theme/{inventoryId}/apply` API와 계약을 맞췄다.
MockMvc 통합 테스트와 Unity 컴파일 검증으로 Unity 클라이언트가 `GET /v1/assets/me/inventory`의 `items[].inventoryId`를 사용해야 하는 계약을 고정했다.

## 무엇을 했나

### 1. Unity 테마/꾸미기 적용 ID 계약 수정

- 사용자 관점 효과: 테마나 꾸미기 항목을 눌렀을 때 서버가 보유 자산을 잘못 판단해 적용 실패하는 문제를 줄였다.
- 시스템 관점 효과: Unity 카탈로그의 `assetId`와 BE 소유권 테이블의 `UserAssetInventory.id`를 명확히 분리했다.
- 주요 파일: `frontend/Assets/App/Bootstrap/RoomEditModeController.cs`, `frontend/Assets/App/Bootstrap/ApiSmokeTestRunner.cs`

`RoomEditModeController`는 인벤토리 응답을 받으면 두 방향 매핑을 만든다.

```csharp
ownedAssetIdByInventoryId[item.InventoryId] = item.AssetId;
if (!ownedInventoryIdByAssetId.ContainsKey(item.AssetId))
{
    ownedInventoryIdByAssetId[item.AssetId] = item.InventoryId;
}
```

프론트에서 서버로 보낼 때는 `assetId -> inventoryId`로 변환하고, 서버에서 내려온 현재 적용 상태를 UI에 복원할 때는 `inventoryId -> assetId`로 변환한다. 인벤토리 로딩이 늦게 도착하면 적용 복원을 보류했다가 다시 실행하는 pending 플래그도 추가했다.

### 2. BE 구형 테마 PATCH API 검증 추가

- 사용자 관점 효과: 잘못된 인벤토리 ID 또는 다른 타입 자산을 테마로 저장하지 않는다.
- 시스템 관점 효과: `PATCH /v1/spaces/me/theme`와 `POST /v1/spaces/me/theme/{inventoryId}/apply`가 같은 소유권/타입 규칙을 공유한다.
- 주요 파일: `backend/src/main/java/com/dotshelf/domain/space/service/SpaceService.java`

핵심은 기존 API도 `inventoryId`가 있으면 반드시 `THEME` 타입 보유 인벤토리인지 확인하는 것이다.

```java
Long inventoryId = request.inventoryId();
if (inventoryId != null) {
    verifyOwnedInventoryWithType(inventoryId, currentUserId, "THEME");
}
space.applyTheme(inventoryId);
```

### 3. 설정 화면의 데모 데이터 제거와 API 연결

- 사용자 관점 효과: 설정 화면의 공개 범위, 결제, 구매내역, 신고내역, 차단 목록이 실제 서버 상태를 보여준다.
- 시스템 관점 효과: 설정 화면도 `NotebookApiClient`의 기존 API 메서드를 재사용하고, 401/403에서는 `SessionRefresh` 후 1회 재시도한다.
- 주요 파일: `frontend/Assets/App/Bootstrap/SettingsScreenController.cs`

연결한 API:

- `GET /v1/spaces/me`: 공개 범위 초기 동기화
- `PATCH /v1/spaces/me/visibility`: 공개 범위 변경
- `GET /v1/payments/methods`: 결제수단 목록
- `GET /v1/payments/wallet`: 보유 페이지
- `GET /v1/payments/purchases`: 구매내역
- `GET /v1/reports/me`: 내 신고내역
- `GET /v1/users/me/blocks`: 내가 차단한 사용자 목록
- `DELETE /v1/users/{targetUserId}/block`: 차단 해제

특히 공개 범위는 서버 값 적용 때 PATCH가 다시 나가지 않도록 `suppressVisibilityRequest`를 사용하고, PATCH 실패 시 마지막 확정값으로 UI를 되돌린다.

### 4. API 계약 테스트 추가

- 사용자 관점 효과: Unity가 인벤토리 ID를 써야 하는 계약이 테스트로 남아 회귀를 막는다.
- 시스템 관점 효과: `assetId`를 path/body에 넣으면 실패하고 `inventoryId`를 넣어야 성공하는 흐름을 HTTP 레벨에서 검증한다.
- 주요 파일: `backend/src/test/java/com/dotshelf/integration/AssetIntegrationTest.java`, `backend/src/test/java/com/dotshelf/domain/space/service/SpaceServiceTest.java`

통합 테스트는 일부러 `assetId != inventoryId`가 되도록 dummy inventory를 먼저 만든 뒤 다음을 검증한다.

- `/v1/assets/me/inventory` 응답에 assetId와 inventoryId가 함께 온다.
- theme apply path에 assetId를 넣으면 4xx가 난다.
- 구형 theme PATCH body에 assetId를 넣어도 4xx가 난다.
- inventoryId를 넣으면 정상 적용된다.

## 왜 했나

Unity 카탈로그는 로컬 에셋 식별을 위해 `assetId`를 들고 있고, BE 적용 API는 사용자가 실제로 보유한 항목인지 검증하기 위해 `UserAssetInventory.id`를 받는다. 두 값이 우연히 같을 수는 있지만 서로 다른 개념이다. 이 구분이 흐려지면 로컬 테스트에서는 통과하다가 실제 DB sequence가 어긋난 사용자에게만 테마 적용 실패가 발생한다.

설정 화면도 비슷한 문제가 있었다. 화면 구조는 있었지만 일부 탭이 하드코딩된 데모 데이터를 보여주고 있어서, BE API가 만들어져 있어도 사용자가 실제 서버 상태를 확인할 수 없었다. 이번 변경은 "API가 존재한다"와 "Unity에서 실제로 쓴다" 사이의 빈틈을 줄이는 작업이다.

## 어떻게 구현했나

### inventoryId를 source of truth로 삼기

프론트는 서버 mutating API를 호출할 때 inventoryId를 보내고, 카탈로그 visual을 찾을 때만 assetId를 사용한다. 이를 위해 인벤토리 응답에서 양방향 맵을 만든다.

핵심 포인트:

- 서버 저장 필드: `currentThemeInventoryId`, `currentWallpaperInventoryId` 등은 inventoryId다.
- Unity catalog lookup: `RoomThemeCatalog.Resolve(...)`, `CustomizationEntry.AssetId`는 assetId 기준이다.
- 서버 응답 복원은 `inventoryId -> assetId` 변환 후 catalog를 찾는다.
- 사용자 선택 저장은 `assetId -> inventoryId` 변환 후 API를 호출한다.

### 설정 화면의 API 로딩 패턴 통일

설정 화면의 새 로딩 메서드들은 같은 구조를 따른다.

1. 이미 in-flight면 중복 요청을 막는다.
2. Dev mode면 API 호출을 건너뛰고 안내 문구만 보여준다.
3. APIManager/NotebookApiClient가 없으면 실패 메시지를 렌더링한다.
4. 401/403이면 `SessionRefresh.TryRefreshAndRetry`로 한 번만 재시도한다.
5. 성공/실패 모두 UI message label과 list root를 명확히 갱신한다.

이 패턴은 알림 설정 로딩/수정에서 이미 쓰던 흐름과 맞췄다.

### 테스트에서 false positive 제거

`assetId`와 `inventoryId`가 같은 값이면 잘못된 클라이언트도 통과할 수 있다. 그래서 통합 테스트는 dummy inventory를 먼저 저장해 sequence를 밀고, 실제 테마 asset의 id와 inventory id가 다르다는 것을 확인한다.

```java
inventoryRepository.saveAndFlush(TestFixtures.inventory(null, user.getId(), dummyOne.getId()));
inventoryRepository.saveAndFlush(TestFixtures.inventory(null, user.getId(), dummyTwo.getId()));
var inventory = inventoryRepository.saveAndFlush(TestFixtures.inventory(null, user.getId(), theme.getId()));
assertThat(inventory.getId()).isNotEqualTo(theme.getId());
```

## 배운 것

- **assetId와 inventoryId는 다른 bounded context의 ID다**: assetId는 상품/카탈로그 식별자이고 inventoryId는 사용자 소유 인스턴스 식별자다. 적용 API처럼 소유권이 중요한 곳에서는 inventoryId가 더 안전하다.
- **UI 복원과 서버 저장은 반대 방향 매핑이 필요하다**: 사용자가 누르는 것은 catalog entry지만 서버가 기억하는 것은 inventory row다. 양쪽 변환을 한 곳에서 관리하지 않으면 화면 current 표시와 실제 저장값이 어긋난다.
- **데모 UI는 API 완성 후 회귀 위험이 된다**: 화면이 그럴듯하면 연결된 것처럼 보이지만, 실제로는 정적 데이터일 수 있다. API audit에서는 "endpoint 존재"뿐 아니라 "화면이 그 endpoint를 호출하는지"를 봐야 한다.
- **테스트 데이터의 ID 충돌을 피해야 계약 테스트가 의미 있다**: auto-increment ID가 우연히 같으면 잘못된 구현도 테스트를 통과한다. sequence를 의도적으로 분리하는 fixture가 필요하다.

## 더 공부할 것

- [ ] Unity 설정 화면에서 반복되는 API loading/retry/render 패턴을 작은 helper로 분리할 수 있을까?
- [ ] `assetId -> inventoryId` 매핑이 없는 상태에서 사용자가 빠르게 클릭할 때 UX를 어떻게 더 명확히 보여줄까?
- [ ] 결제수단 등록/삭제 API가 추가되면 Settings payment tab의 command flow를 어떻게 분리할까?
- [ ] 신고내역/차단 목록에 cursor pagination이 필요해지는 시점과 UI 구조는 어떻게 잡을까?

## 검증

- `backend .\gradlew.bat build --console=plain` PASS
- `dotnet restore frontend/Assembly-CSharp.csproj; dotnet build frontend/Assembly-CSharp.csproj --no-restore` PASS
- Unity batchmode compile PASS (`Tundra build success`)
- `git diff --check` PASS

## 참고

- 관련 커밋: `c277c5c5 feat: connect Unity settings and asset APIs`
- 주요 API: `/v1/assets/me/inventory`, `/v1/spaces/me/theme`, `/v1/spaces/me/theme/{inventoryId}/apply`, `/v1/payments/*`, `/v1/reports/me`, `/v1/users/me/blocks`
