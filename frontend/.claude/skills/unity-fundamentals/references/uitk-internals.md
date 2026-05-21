# UI Toolkit Internals

> **한 줄 요약**: UI Toolkit은 retained-mode 트리 + Yoga 기반 Flexbox subset 레이아웃 엔진 + USS cascade로 구성된다. 우리가 자주 마주치는 함정(flex 형제 충돌, USS 광역 영향, `Q()` null, 인라인 스타일 우선순위)은 모두 이 세 메커니즘의 직접 귀결이다.

## 어떤 결정이 이 글로 풀리는가

- **"USS 한 줄 고쳤더니 다른 화면이 깨졌다"** → §USS Cascade & Specificity
- **"flex-shrink:0 추가했더니 캐러셀이 헤더를 덮음"** (실제 회귀: commit 2a668fc) → §Flexbox via Yoga
- **"Q(\"my-button\")이 null이다"** → §UQuery
- **"인라인 style=과 USS 룰이 충돌"** → §Style Precedence
- **"왜 uGUI 대신 UI Toolkit을 쓰는가?"** → §UITK vs uGUI
- **"DashboardView처럼 큰 화면을 어떻게 모듈화하나?"** → §프로젝트 연결

## 메커니즘

### 1. 시각 트리 (Visual Tree)

UI Toolkit은 **retained-mode** UI다. UXML로 선언한 트리가 메모리에 `VisualElement` 객체로 살아 있고, 매 프레임 그리는 게 아니라 변경이 있을 때만 dirty flag가 올라가고 그 부분만 다시 레이아웃·렌더된다. uGUI도 retained지만 UITK는 더 가볍고, IMGUI는 immediate-mode (매 프레임 다시 계산).

`VisualElement`는 다음을 갖는다:
- 이름 (`name` 속성, UXML의 `name="..."`)
- USS 클래스 목록 (`class="..."`)
- 자식 요소
- 계산된 스타일 (`resolvedStyle`)
- 인라인 스타일 (`style`)

### 2. 레이아웃 엔진: Yoga (Flexbox subset)

매뉴얼 표현 그대로:

> *"UI Toolkit includes a layout engine that positions elements based on layout and styling properties. The layout engine uses the layout principles of Yoga, which implements a subset of Flexbox."*

**Subset**이라는 게 중요하다 — CSS Flexbox의 모든 기능이 다 있는 게 아니다 (예: `gap` 속성은 Unity 6 이전 일부 버전에서 미지원/제한). 항상 Unity 매뉴얼의 "USS supported properties"로 확인.

핵심 flex 속성과 매뉴얼 정의:

| 속성 | 정의 (매뉴얼 인용) |
|---|---|
| `flex-direction` | row / column / row-reverse / column-reverse — 주 축 방향 |
| `flex-grow` | *"Defines how much an element grows relative to its siblings along the main-axis. The value is a ratio shared with siblings in the same parent container."* |
| `flex-basis` | *"Defines the initial size of an element along the main-axis. The default is auto, which means the size is based on the content size."* |
| `flex-shrink` | 매뉴얼 본문엔 짧게 언급 — 부모 공간이 부족할 때 줄어드는 비율. 0이면 안 줄어듦 |
| `align-items` / `justify-content` | 교차 축 / 주 축 정렬 |

매뉴얼이 직접 강조한 함정:
> *"To ensure an even split regardless of content, set both flex-grow: 1 and flex-basis: 0."*

→ `flex-grow:1`만 줘서는 콘텐츠 크기에 따라 비율이 어긋난다.

### 3. flex 형제 충돌의 실제 메커니즘

부모가 `display: flex; flex-direction: column;` 일 때, 부모 높이가 H라면:

1. 모든 자식의 `flex-basis` (또는 콘텐츠 크기)를 더한 합이 S
2. **남는 공간** = H - S
3. 남는 공간을 형제들의 `flex-grow` 비율로 분배
4. 부족하면 `flex-shrink` 비율로 형제들이 축소

**핵심 함정**: `flex-shrink` 의 기본 동작은 "축소 가능". 즉 부모 공간이 부족하면 형제들이 자동으로 줄어든다. 한 형제에게 `flex-shrink: 0` 을 주면 그 요소는 안 줄어들지만, 다른 형제들이 더 줄어든다 — 부모 공간 부족이 한쪽으로 몰리면서 **헤더가 캐러셀에 가려지거나, 캐러셀이 부모 영역을 넘어가는** 회귀가 발생한다 (commit 2a668fc 정확히 이 케이스).

대처:
- 콘텐츠 크기로 고정하고 싶은 형제에 `flex-shrink: 0` + `min-height`를 같이 주기
- 비율 분배가 핵심이면 모든 형제에 `flex-grow:1, flex-basis:0` 통일
- 부모에 `min-height` 또는 `height` 명시해서 공간 부족이 일어나지 않게 보장

### 4. Positioning: Relative vs Absolute

매뉴얼:
> *"Absolute makes an element invisible to the default Flexbox-based layout engine. It's as if it no longer takes any space."*

→ `position: absolute`는 *형제와의 레이아웃 흐름에서 빠져나오는* 옵션. 모달/툴팁/오버레이에 적합. 일반 콘텐츠에는 쓰지 말 것 (다른 형제와의 정합성이 깨짐).

### 5. USS Cascade & Specificity

매뉴얼이 명시한 specificity 위계:

> *"**Name** selectors are more specific than **Class** selectors. **Class** selectors are more specific than **Type** selectors. **Type** selectors are more specific than the **Universal** (`*`) selector."*

→ `#nickname-label` > `.nickname-label` > `Label` > `*`

같은 specificity끼리 충돌하면:
- 사용자 정의 stylesheet가 Unity 기본 stylesheet보다 우선
- 같은 종류 stylesheet 안에서는 specificity 높은 것
- 같은 specificity면 **요소 계층에서 더 아래에 attach된** stylesheet 우선
- 같은 stylesheet 안에서는 **나중에 작성된 룰**이 우선

**중요**: USS는 **`!important` 룰을 지원하지 않는다.** CSS 비상 오버라이드 메커니즘이 없다.

### 6. Style Precedence (전체 순위)

매뉴얼이 명시한 위계, 낮은 → 높은 우선순위:

1. **Inherited styles** (부모로부터 상속, 예: `color`, `font-size`)
2. **USS 셀렉터** (specificity + cascade 규칙으로 결정)
3. **Inline UXML 스타일** (`<ui:VisualElement style="..." />`)
4. **C# 스타일** (`element.style.color = ...`) — *"have the highest specificity"*

→ C#에서 `element.style.height = ...`로 한 번 건드리면 **그 후로는 USS가 무력화**된다 (C#이 명시적으로 다시 `StyleKeyword.Null` 처리하지 않는 한). 예측 못 한 회귀의 단골.

### 7. UQuery (Q / Query)

`Q<T>("name")`, `Q<T>(className: "...")` 로 트리에서 요소를 찾는다.

매뉴얼이 명시한 사실:
- *"UQuery traverses through the hierarchy to find elements by name, class or type. Cache results from UQuery at initialization."*
- 즉 매 호출마다 **트리 traversal**. 매 프레임 호출하면 비용이 누적된다.
- `Q`는 "first match"를 반환. **매칭 없으면 `null`**. 컴파일러는 못 잡는다 — 런타임 NRE의 단골.

올바른 패턴: `OnEnable` 또는 panel attach 시점에 한 번 `Q()`로 캐시 → 멤버 필드에 보관 → 이후 그 필드만 쓴다.

```csharp
// ❌ 매 프레임 트리 traversal
void Update() {
    var label = root.Q<Label>("nickname-label");
    label.text = ...;
}

// ✅ 한 번 캐시
Label _nicknameLabel;
void OnEnable() {
    _nicknameLabel = _document.rootVisualElement.Q<Label>("nickname-label");
}
```

### 8. UITK vs uGUI

| 항목 | uGUI (Canvas) | UI Toolkit |
|---|---|---|
| 기반 | MeshRenderer 기반 Canvas, RectTransform | retained-mode VisualElement 트리, Yoga |
| 레이아웃 | LayoutGroup (수동 배치 위주) | Flexbox subset (자동 배치) |
| 스타일 | 컴포넌트 인스펙터 / `Image`, `Text` | USS (CSS-like) |
| 데이터 바인딩 | 수동 | 6 LTS부터 공식 데이터 바인딩 (UI Toolkit) |
| 런타임 비용 | Canvas rebuild 비용 큼 (요소 변경 시 광역 rebuild) | 변경된 부분만 dirty 처리 |
| 에디터 툴 | uGUI Inspector | UI Builder |
| 모바일 적합도 | 검증된 도구, 광범위한 자료 | 6 LTS에서 안정. 모바일 입력·DPI 핸들링 추가 주의 |

게임 월드 위 HUD/이펙트는 uGUI나 World-space Canvas가 더 자연스러울 수 있고, **메뉴/대시보드/리스트 같은 데이터 중심 UI는 UI Toolkit이 압도적으로 유리**. 우리 프로젝트는 후자라 UI Toolkit이 정합.

## 이 프로젝트와의 연결

### 디렉터리 구조

```
Assets/App/Features/Dashboard/Presentation/
├── DashboardView.uxml   (~ 트리 정의)
└── DashboardView.uss    (~ 365줄, 클래스 셀렉터 기반)
```

USS 셀렉터는 모두 클래스(`.dashboard-pane`, `.profile-header` 등)로 작성되어 있고, UXML은 같은 클래스를 부여한다. 좋은 출발점.

### 주의해야 할 패턴

1. **공유 셀렉터 광역 영향**
   - `.dashboard-pane`, `.card` 같은 공통적인 이름이 다른 화면에서도 쓰일 수 있음. USS 룰을 만지기 전에 `unity-uitk-edit-guard` 가드대로 grep으로 사용처 확인.
   - 안전 패턴: `.dashboard-pane.left-pane > .profile-header` 처럼 좁히기.

2. **flex 형제 동시 검토**
   - `DashboardView.uss`에 flex 속성이 20개 가량. 한 셀렉터의 flex 변경 전 같은 부모 안의 다른 자식 정의 같이 보기.
   - 캐러셀 같은 가변 콘텐츠는 `flex-grow:1; flex-basis:0;`, 헤더 같은 고정 콘텐츠는 `flex-shrink:0; min-height:<값>;`.

3. **Q() 캐싱**
   - `*ScreenController.cs` 에서 `OnEnable`/`Awake`에 한 번 캐시. UXML element name을 바꾸면 컴파일은 통과하므로 커밋 직전 grep 확인 (`unity-uitk-edit-guard` 가드).

4. **인라인 스타일 금지**
   - `frontend/CLAUDE.md` 규약. UXML에 `style="..."` 새로 추가하지 않는다. 발견되면 클래스 + USS 룰 추가 후 인라인 제거.

5. **C# style 사용 금지에 가깝게**
   - `element.style.height = 100;` 같은 직접 조작은 USS를 무력화시킴. 가능하면 클래스 토글(`AddToClassList` / `RemoveFromClassList`)로 상태를 표현하고, 스타일은 USS에 맡김.

## 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| 캐러셀이 헤더를 덮음 (commit 2a668fc) | 한 형제만 `flex-shrink:0`, 부모 공간 부족이 다른 형제로 몰림 | 부모/형제 flex 정의 같이 보고 일관된 분배 (`flex-grow:1, flex-basis:0` 통일 또는 명시 height) |
| `flex-grow:1` 줬는데 비율이 안 맞음 | `flex-basis: auto` (콘텐츠 크기) 가 영향 | `flex-basis: 0` 같이 줘서 콘텐츠 무관 분배 |
| `Q("name")` null | UXML name 변경 후 C# 미수정, 또는 panel 미attach 시점 호출 | name grep으로 동시 변경. 캐싱은 `OnEnable` 또는 attach 시점 이후 |
| USS 변경했는데 적용 안 됨 | 더 specificity 높은 셀렉터가 다른 stylesheet에 존재 | UI Toolkit Debugger로 적용 룰 확인. 셀렉터 좁히거나 추가 |
| 인라인 style이 USS를 이김 | 매뉴얼 명시 — 인라인이 USS 위 | 인라인 제거하고 USS로 일원화 |
| C# style 한 번 set 후 USS 안 먹음 | C# style이 최고 우선순위 | `element.style.<prop> = StyleKeyword.Null` 로 release하거나, 처음부터 클래스 토글로 표현 |
| !important로 강제하려 함 | USS는 `!important` 미지원 | 셀렉터 specificity를 높이거나, C# style로 fallback (권장 X) |
| 모달/툴팁이 형제 레이아웃을 깨뜨림 | `position: relative` 기본값 | `position: absolute` 로 레이아웃 흐름에서 빠져나오게 |
| Edit 모드에선 정상, 빌드/플레이에서 다름 | Editor stylesheet 영향 또는 panel scaling 차이 | UI Builder의 Preview 모드에서 미리 확인. 모바일 DPI는 PanelSettings의 ScaleMode로 제어 |

## 디버깅 도구

- **UI Toolkit Debugger**: `Window > UI Toolkit > UI Toolkit Debugger`. 트리 / 적용된 USS 룰 / 계산된 스타일 / matched selectors까지 다 보여줌. USS 회귀의 1순위 도구.
- **UI Builder**: 시각적으로 USS 변경. 단, USS 파일을 직접 수정하는 PR과 섞이면 diff가 지저분해질 수 있으므로 손으로 USS 편집하는 흐름과 일관되게 운영.

## 출처

- Unity Manual — [Position element with the layout engine](https://docs.unity3d.com/Manual/UIE-LayoutEngine.html)
- Unity Manual — [Layouts (UI Toolkit best practices)](https://docs.unity3d.com/6000.4/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/layouts.html)
- Unity Manual — [USS Selectors (Selector precedence)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-uss-selector-precedence.html)
- Unity Manual — [Find visual elements with UQuery](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-UQuery.html)
- Unity Manual — [Introduction to UI Toolkit](https://docs.unity3d.com/6000.0/Documentation/Manual/ui-systems/introduction-ui-toolkit.html)
- Unity Manual — [USS common properties (supported)](https://docs.unity.cn/Manual/UIE-USS-SupportedProperties.html)
- Unity Manual — [Best practices for USS](https://docs.unity.cn/Manual/UIE-USS-WritingStyleSheets.html)
- GitHub — [Unity-Technologies/ui-toolkit-manual-code-examples](https://github.com/Unity-Technologies/ui-toolkit-manual-code-examples)
