# Unity 룸 레이아웃 저장 복원 정리

- **브랜치**: `feat/fe/error2`
- **범위**: `e6902654..HEAD` (본문 커밋 2개)
- **기간**: 2026-05-20 ~ 2026-05-20
- **티켓**: 없음

## TL;DR

Unity 룸 편집에서 새 테마나 에셋을 적용한 뒤 재진입하면 이전 테마/에셋이 겹쳐 보이는 문제를 저장/복원 경로에서 정리했다. 로그인 상태에서는 서버 layout을 권위 데이터로 보고 legacy local JSON 복원을 건너뛰게 했다. 저장 시에는 현재 화면에 실제로 남은 `PlacedDecorations` 하위 배치만 저장하도록 제한해 rollback hold root의 이전 오브젝트가 payload에 섞이지 않게 했다. 추가로 로컬 개발자가 백엔드, DB, Unity, 웹, 런처를 실행할 수 있는 로컬 포팅 메뉴얼을 문서화했다.

## 무엇을 했나

### 1. Unity 룸 레이아웃 저장/복원 충돌 방지

- 사용자 관점 효과: 마지막으로 적용하고 저장한 테마/에셋만 재진입 후에도 보이며, 이전 테마 오브젝트가 겹쳐 복원되는 현상을 막는다.
- 시스템 관점 효과: 로그인 상태의 복원 기준을 서버 layout으로 통일하고, 저장 payload에 현재 배치 루트 밖의 숨김 오브젝트가 들어가지 않도록 했다.
- 주요 파일: `frontend/Assets/App/Bootstrap/RoomEditModeController.cs`

### 2. 로컬 포팅 메뉴얼 작성

- 사용자 관점 효과: 새 개발자가 백엔드, PostgreSQL, Redis, Unity, 웹 모듈, 런처를 로컬에서 실행하는 순서를 한 문서로 따라갈 수 있다.
- 시스템 관점 효과: 로컬 실행에 필요한 포트, 환경변수, seed, Unity API 설정, 검증 체크리스트를 한 곳에 모았다.
- 주요 파일: `docs/porting-manual.md`

## 왜 했나

테마나 에셋 적용 직후 화면은 정상인데, 나갔다 들어오면 이전 테마나 에셋이 겹쳐 보인다는 증상이 있었다. 원인은 크게 두 가지였다.

첫째, 로그인 상태에서도 legacy local JSON 복원과 서버 layout 복원이 함께 살아 있었다. 이러면 서버가 최신 상태를 내려주기 전후로 로컬에 남아 있던 이전 배치가 다시 붙을 수 있다.

둘째, 저장 시점에 씬 전체의 `DecorationItemMarker`를 스캔하고 있었다. 테마 적용 중 PATCH 실패 rollback을 위해 이전 오브젝트를 hidden hold root에 보관하는데, 씬 전체 스캔은 이 숨긴 오브젝트까지 저장 대상으로 볼 수 있다. 결과적으로 서버 layout 자체에 이전 테마 오브젝트가 섞이고, 다음 진입 때 새 테마 위로 다시 복원될 수 있었다.

## 어떻게 구현했나

### 로그인 상태에서는 서버 layout만 권위 데이터로 사용

`APIManager.Instance.HasAccessToken`이 있으면 서버 layout 동기화가 가능한 상태로 보고 local JSON 복원을 생략한다. 적용 지점은 씬 설치 직후 초기 복원과 편집 모드 진입 fallback 두 곳이다.

```csharp
private static bool ShouldUseServerDecorationLayout()
{
    var apiManager = APIManager.Instance;
    return apiManager != null && apiManager.HasAccessToken;
}
```

핵심 포인트:

- 비로그인/오프라인 개발 환경은 기존 local JSON 복원을 유지한다.
- 로그인 상태에서는 `ApplyExternalDecorationLayoutPayload(..., clearExisting: true)` 경로가 최종 복원 책임을 갖는다.
- fallback 복원에서도 같은 가드를 적용해, 초기 load가 실패했을 때 예전 local JSON이 뒤늦게 끼어들지 않게 했다.

### 저장 payload를 현재 배치 루트로 제한

기존 저장은 씬 전체의 `DecorationItemMarker`를 찾았다. 이번 변경에서는 명시 저장 경로가 `CollectDecorationLayoutItems(scopeToPlacedItemsRoot: true)`를 사용하도록 바꿨다.

```csharp
var items = CollectDecorationLayoutItems(scopeToPlacedItemsRoot: true);
```

핵심 포인트:

- `PlacedDecorations` 밖의 rollback hold root, scene helper, hidden object는 저장하지 않는다.
- 현재 화면에 실제로 남아 있는 마지막 배치 상태만 서버 layout payload로 직렬화한다.
- 테마 reset layout 저장 경로도 이미 같은 scoped 수집을 쓰고 있어서, 명시 저장과 reset 저장 정책이 맞춰졌다.

### 서버 저장 완료 기준 강화

로그인 상태에서는 로컬 JSON 저장만 성공했다고 저장 성공으로 처리하지 않는다. 서버 layout 저장까지 성공해야 dirty를 정리하고 성공 토스트를 보여준다.

핵심 포인트:

- 서버 저장 실패 시 dirty 상태를 유지한다.
- 재진입 기준이 서버 layout이므로, 서버 저장 실패를 조용히 넘기지 않는다.
- 비로그인/오프라인 모드에서는 기존처럼 local JSON 저장으로 동작할 수 있다.

### 포팅 메뉴얼 구조화

포팅 문서는 단순 실행 명령 나열이 아니라 구성, 사전 설치, 백엔드/DB, Unity, 웹, 런처, 검증, 보안, 문제 해결 순서로 정리했다.

핵심 포인트:

- 민감값은 placeholder와 취급 규칙만 적고 실제 secret은 포함하지 않았다.
- Unity API URL, 소셜 로그인, seed 데이터, admin-web backend 모드처럼 로컬 연동에서 자주 막히는 부분을 별도 섹션으로 분리했다.
- 각 모듈별 최소 검증 체크리스트를 제공해 포팅 후 어디까지 확인해야 하는지 드러나게 했다.

## 배운 것

- **복원 source of truth는 하나로 좁혀야 한다**: 서버 layout과 local JSON을 동시에 복원하면 단순 race가 아니라 사용자 눈에는 데이터가 겹쳐 보이는 상태가 된다.
- **씬 전체 스캔은 편하지만 위험하다**: Unity에서는 inactive/hidden root도 같은 scene 안에 남아 있으면 `FindObjectsByType`에 잡힐 수 있다. 저장 로직은 의도한 root로 scope를 제한해야 한다.
- **저장 성공 기준은 재진입 기준과 같아야 한다**: 재진입 때 서버 layout을 읽는다면, 저장도 서버 layout 저장 완료를 성공 기준으로 삼아야 한다.
- **rollback 구조는 저장 구조와 분리해야 한다**: 실패 복구를 위한 hold root는 화면 상태가 아니므로 persistence 대상에서 제외되어야 한다.

## 더 공부할 것

- [ ] Unity `FindObjectsByType`의 inactive object 탐색 정책과 `HideFlags`별 차이
- [ ] optimistic UI와 서버 저장 실패 처리 UX를 어디까지 강하게 묶을지
- [ ] Unity scene object persistence에서 root scoping을 테스트로 검증하는 패턴
- [ ] 서버 layout version 충돌이 발생했을 때 client merge/overwrite 정책을 어떻게 둘지

## 참고

- 관련 커밋: `c14801be`, `039e2dc8`
- 관련 파일: `frontend/Assets/App/Bootstrap/RoomEditModeController.cs`
- 관련 문서: `docs/porting-manual.md`
