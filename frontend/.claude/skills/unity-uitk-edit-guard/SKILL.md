---
name: unity-uitk-edit-guard
description: Use this skill whenever the user is editing or about to edit any UXML or USS file under frontend/Assets/, especially in Assets/App/Features/**/Presentation/ or Assets/UI Toolkit/. It prevents the recurring problem where a layout tweak in one component silently breaks another — flex-shrink/flex-grow conflicts (the cause of commit 2a668fc), shared class selectors that touch unrelated screens, USS variable changes with wide cascade, and UXML element name/hierarchy edits that break C# Query/Q lookups in matching ScreenController files. Trigger automatically when the user says "fix this layout", "add a section to the dashboard", "the carousel is overlapping", "adjust spacing", "change USS", "edit UXML", "make this responsive", or any request that implies modifying UI Toolkit markup or styles. Apply silently as part of any task that touches *.uxml or *.uss — don't wait for an explicit "check my UI".
---

# unity-uitk-edit-guard

UXML/USS 변경 시 의도치 않게 옆 화면을 깨거나, 같은 USS의 다른 셀렉터를 어긋나게 만드는 것을 막는 스킬. 변경 전 영향 범위를 분석하고, 변경 후 깨질 가능성이 있는 곳을 함께 보고한다.

## 왜 필요한가

UI Toolkit은 USS의 cascade와 UXML/C# 바인딩이 암묵적으로 묶여 있어, 한 곳을 고치면:
- 같은 클래스 셀렉터가 다른 화면에서도 적용되어 광역 영향이 생긴다
- flex 속성(`flex-grow` / `flex-shrink` / `flex-basis`)은 형제 요소들과 함께 풀려야 하는데, 한 요소만 바꾸면 다른 요소의 비율이 무너진다 (commit `2a668fc`의 원인 — 캐러셀이 섹션 헤더를 덮음)
- UXML에서 element name이나 계층을 바꾸면 그걸 `Query<T>("name")` / `Q("name")` 으로 찾는 C# 컨트롤러가 런타임에 null을 만난다 (컴파일 에러가 안 난다)
- USS 변수/공통 유틸 클래스(`.dashboard-pane`, `.flex-row` 같은) 한 값을 바꾸면 그 변수를 쓰는 곳 전부가 같이 움직인다

이 함정들은 모두 "직접 만진 파일"에서는 보이지 않는다. **변경 전후로 영향 범위를 명시적으로 보는 습관**이 핵심이다.

## 언제 발동하나

- `*.uxml`, `*.uss` 파일을 만들거나 수정할 때 (대부분의 경우)
- 사용자가 "레이아웃 수정", "겹친다", "위치 어긋남", "간격 조정", "이 섹션 추가/제거", "반응형으로" 같은 UI 변경 요청을 할 때
- 매칭 ScreenController(`*ScreenController.cs`, `*View.cs`)와 UXML element name이 함께 변할 때
- USS 변수(`--dashboard-*`, `--color-*` 등) 또는 공통 클래스 값을 변경할 때

## 변경 전 — 영향 범위 분석 (3분짜리 사전 점검)

이 스킬이 발동되면 **먼저** 다음을 확인하고 결과를 사용자에게 보고한 뒤에 변경에 들어간다.

### 1. 같은 셀렉터를 다른 곳에서도 쓰는지

수정 대상 USS의 클래스/이름 셀렉터를 추출해 전체 검색:

```bash
# 예: .dashboard-pane 을 수정하려는 경우
grep -rln "dashboard-pane" frontend/Assets --include="*.uxml" --include="*.uss" --include="*.cs"
```

여러 파일이 잡히면 그 클래스는 "공유"다. 사용자에게 알린다:
> ⚠️ `.dashboard-pane`은 `DashboardView.uss` 외에 `XxxView.uxml`에서도 쓰입니다. 전 화면에 영향을 주려는 게 맞나요? 아니면 이 화면 전용으로 더 구체적인 셀렉터(`.dashboard-pane.left-pane`)로 좁힐까요?

### 2. UXML element name이 C# 컨트롤러에 묶여 있는지

UXML 옆 위치(`Presentation/`)에 같은 이름의 ScreenController/View가 거의 항상 있다. 이름을 바꾸거나 계층을 옮기려면:

```bash
# 예: name="follow-stats" 를 옮기거나 이름을 바꾸려는 경우
grep -rn "follow-stats\|FollowStats" frontend/Assets/App --include="*.cs"
```

매칭이 잡히면 변경과 동시에 C# 쪽 `Q("...")` / `Query<T>("...")` / `[UxmlElement]` 등을 같이 손봐야 한다고 알린다. **UXML만 바꾸면 컴파일은 통과하지만 런타임에 조용히 깨진다.**

### 3. flex 형제 관계

flex 속성을 손대기 전에 같은 부모 안의 형제 요소들의 flex 정의를 모두 확인한다:

```bash
# DashboardView.uss 안에서 flex 속성을 가진 셀렉터 모음
grep -nE "flex-grow|flex-shrink|flex-basis|^\s+flex:" frontend/Assets/App/Features/Dashboard/Presentation/DashboardView.uss
```

부모가 `display: flex; flex-direction: column;` 일 때, 자식 한 개의 `flex-grow: 1`이나 `flex-shrink: 0`을 바꾸면 다른 형제의 비율이 자동으로 재분배된다. 변경 전에 형제 전부의 flex 값을 한 화면에 같이 보고 작업한다.

### 4. USS 변수/공통 클래스 변경

`var(--xxx)` 또는 공통으로 쓰이는 클래스(`flex-row`, `card`, `pill` 등)의 값을 바꾸려면 사용처 전부:

```bash
grep -rn "var(--dashboard-spacing-md\|\.card\b" frontend/Assets --include="*.uss" --include="*.uxml"
```

사용처가 1곳이면 안전. 2곳 이상이면 사용자에게 "전역 변경입니다 — 모두 같은 방향으로 움직이게 의도한 게 맞나요?" 확인.

## 변경 시 규칙

### 인라인 스타일 금지

UXML 안에 `style="..."` 속성을 새로 도입하지 않는다 (frontend/CLAUDE.md 명시). 이미 있는 인라인 스타일을 만지러 들어왔다면, 옆에 클래스 추가 + USS 룰 추가 후 인라인 제거를 함께 제안한다 — 단, 사용자 승인 후.

### 셀렉터 구체성

새 룰을 추가할 때, 가능한 한 좁은 셀렉터를 쓴다.

- ✅ `.dashboard-pane.left-pane > .profile-header` — 이 화면, 이 위치만
- ❌ `.profile-header` — 다른 화면에 같은 이름이 있을 가능성

`*` 셀렉터, 태그 셀렉터(`Label`, `VisualElement`)에 속성을 부여하지 않는다. UI Toolkit은 USS cascade가 강하다 — 광역 룰은 디버깅이 어려워진다.

### flex 변경은 형제 동시 검토

flex-grow / flex-shrink / flex-basis 중 하나라도 바꾸면 같은 부모의 다른 자식 정의를 같이 인용해서 보고한다:

```
변경 대상: .header-section { flex-shrink: 0; }
형제 요소들의 현재 flex 정의:
  .carousel-section { flex-grow: 1; flex-shrink: 1; }
  .footer-section { flex-shrink: 0; }
→ flex-shrink:0을 추가하면 carousel이 부모 높이를 다 가져가고 헤더는 컨텐츠 크기로 고정. 의도한 동작이 맞는지 확인.
```

### UXML 이름/계층 변경은 C# 쪽과 동시에

UXML element name 변경 / 부모 변경 / element 추가-제거 → 매칭 ScreenController/View의 `Q("name")` 호출과 한 PR에 같이 들어간다. 따로 들어가지 않는다.

### USS 룰 추가 위치

같은 파일 안에서 관련 룰들 옆에 추가한다 (그룹 보존). 파일 맨 끝에 무작정 append 하지 않는다 — 다음에 그 컴포넌트를 만질 사람이 양쪽을 다 보지 못한다.

## 변경 후 — 사후 검증 보고

변경 보고에 다음 블록을 항상 포함한다.

```
## UI Toolkit edit guard
- ✅ 셀렉터 영향 범위: `.dashboard-pane`은 이 파일에서만 사용
- ⚠️ flex 형제 영향: `.carousel-section`이 같은 부모에 있고 flex-grow:1 — 이번 변경으로 헤더가 고정되고 캐러셀이 남는 공간을 가져감 (의도한 동작)
- ✅ UXML element name 변경 없음 — C# 쪽 Q() 호출 영향 없음
- ✅ USS 변수 변경 없음
- 📝 권장 시각 검증: DashboardScreen 진입 → 헤더가 캐러셀에 가려지지 않는지 확인 (commit 2a668fc 회귀 방지)
```

위반/위험 없이 깨끗하면 한 줄로 줄여도 된다: `✅ UI guard: scoped to DashboardView, no shared selectors / siblings / Q() bindings affected`.

## 흔한 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| "옆 화면이 갑자기 깨짐" | 공통 클래스 셀렉터 수정 | 좁은 셀렉터로 분리하거나 변경을 사용자에게 확인 |
| "캐러셀이 헤더를 덮음" (2a668fc 회귀) | 형제의 flex 속성 충돌 | 부모/형제 flex 정의 같이 보기. 헤더에 `flex-shrink:0` |
| "런타임에 NullReferenceException, UI 요소 못 찾음" | UXML element name 변경, C# Q() 미수정 | UXML 변경 시 매칭 *.cs도 같이 |
| "디자인 토큰 한 개 바꿨더니 전체가 움직임" | USS 변수 광역 영향 | 변경 전 사용처 grep, 의도 확인 |
| "에디터에선 잘 보이는데 빌드/플레이 모드에서 다름" | 인라인 style + USS 룰 동시 적용으로 우선순위 충돌 | 인라인 제거하고 USS 일원화 |
| "USS 룰 추가했는데 안 적용됨" | 셀렉터 구체성이 다른 룰보다 낮음 | UI Toolkit Debugger(Window > UI Toolkit > UI Toolkit Debugger)로 적용 룰 우선순위 확인 |

## 시각 검증 절차

UI 변경은 코드 검사로 끝나지 않는다 — 사용자가 에디터에서 직접 봐야 한다. 변경 보고 마지막에 명시:

- 어떤 씬을 열어야 하는지 (예: `DashboardTestScene`)
- 어떤 화면 흐름을 따라가야 하는지 (예: 룸 진입 → 책장 클릭 → 대시보드 오픈)
- 어떤 해상도/플랫폼에서 봐야 하는지 (예: 모바일 세로, 안드로이드 빌드)
- UI Toolkit Debugger 사용 권장 시점 — 셀렉터/우선순위/계산된 스타일 확인

자동 테스트로 잡히지 않는 변경은 명시적으로 "에디터에서 직접 확인 필요" 라고 알린다. 자동 통과 ≠ 시각적 정상.

## 자료 위치

- UXML/USS: `frontend/Assets/App/Features/*/Presentation/`, `frontend/Assets/UI Toolkit/`
- 매칭 컨트롤러: 같은 디렉터리 또는 한 단계 위 `*ScreenController.cs`, `*View.cs`
- 회귀 참고 커밋: `2a668fc` (flex-shrink 충돌로 캐러셀이 헤더 가림)
