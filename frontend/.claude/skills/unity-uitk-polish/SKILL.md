---
name: unity-uitk-polish
description: Use this skill whenever the user wants to make the dotshelf UI feel "polished", "smooth", "premium", "alive" — adding hover/press/focus feedback, transitions, micro-interactions, animations, easing, motion design, or fixing UI that feels "stiff", "snappy in a bad way", or "abrupt". Trigger when the user says "make this nicer", "add some motion", "the button feels dead", "transition between states", "polish the dashboard". The skill enforces a small, consistent motion system on top of UI Toolkit — motion tokens (--motion-fast/base/slow), USS transitions on transform/color (animatable + GPU-cheap), pseudo-classes (:hover/:active/:focus) for state, mobile haptic on touch confirmations, and accessibility (reduced motion, color-only state avoidance). Apply silently when modifying any UXML/USS or interactive element. Coordinates with unity-uitk-edit-guard (which validates the change is scoped) and unity-perf-budget (which enforces the motion stays cheap).
---

# unity-uitk-polish

dotshelf UI Toolkit 화면을 **일관된 motion 시스템**으로 끌어올리는 스킬. 임의로 transition을 흩뿌리는 게 아니라, 작은 토큰 세트(duration, easing) + 정해진 트리거(pseudo-class / 상태 전환) + 성능 안전 속성(transform / color)에 한정한다. 깊은 USS cascade와 layout 메커니즘은 `unity-fundamentals/references/uitk-internals.md` 에 두고, 이 스킬은 **모션 적용 가이드**에 집중.

## 왜 이 스킬이 필요한가

- "예쁘게"라는 요구가 들어오면 transition·animation을 임의로 추가하기 쉽고, 그 결과 화면마다 timing·easing이 다 다르고 **앱 전체 톤이 흐트러진다**.
- 모바일은 매 프레임 비용이 비싸서 **layout-affecting 속성(width/height/margin)을 transition시키면 GC + 레이아웃 재계산**으로 끊김. transform/opacity/color는 GPU에서 거의 공짜.
- `:hover`는 모바일 터치에서 **press-and-hold**일 때만 잠깐 적용되거나 적용되지 않음. 데스크톱 기준으로만 디자인하면 모바일에서 의도가 사라진다.
- 색상만으로 상태를 표현하면 **접근성**에 걸린다 (색맹·약시).

## 언제 발동하나

- UXML/USS 변경에 hover/focus/active/checked/disabled 등 상태 표현이 들어갈 때
- 사용자가 "polish", "느낌 좋게", "부드럽게", "transition 넣어줘", "버튼 누르는 느낌", "상태 전환" 같은 신호
- 새 인터랙티브 요소(버튼, 카드, 토글) 도입 시 — 모션 토큰을 적용한 표준 형태로 시작
- 기존 인라인 style이나 즉시 변경(`element.style.color = ...`)을 transition으로 바꾸려 할 때

이 스킬은 `unity-uitk-edit-guard`(변경의 영향 범위) 와 `unity-perf-budget`(모션이 예산을 깎지 않는지) 와 협업한다 — polish가 다른 화면을 깨뜨리지 않게, 모션이 frame budget을 갉아먹지 않게.

## Motion Tokens — USS 변수로 정의

이 토큰은 모든 화면이 공유한다. `Assets/UI Toolkit/` 또는 `Assets/App/UI/Tokens.uss` 같은 공통 파일에 한 번만 정의하고 다른 USS에서 `var(--motion-base)`로 참조.

```css
:root {
    /* Duration — 짧을수록 즉시감, 길수록 부드러움. 모바일은 짧게. */
    --motion-fast:   120ms;  /* 즉시 피드백 (press, ripple) */
    --motion-base:   200ms;  /* 일반 상태 전환 (hover, focus) */
    --motion-slow:   320ms;  /* 큰 영역 전환 (panel show/hide) */
    --motion-stagger: 60ms;  /* 리스트 등 연쇄 등장 간격 */

    /* Easing — Material/iOS 스타일에서 검증된 곡선 */
    --easing-standard:    ease-in-out-cubic;  /* 일반 상태 전환 */
    --easing-decelerate:  ease-out-cubic;     /* 등장 (들어옴) */
    --easing-accelerate:  ease-in-cubic;      /* 퇴장 (나감) */
    --easing-emphasized:  ease-in-out-back;   /* 약간 튕기는 강조 */
}
```

→ 새 transition을 쓸 때 위 토큰 외 값(`150ms`, `ease-out` 같은 임의값)을 직접 적지 않는다. 새 duration이 정말 필요하면 토큰을 늘리고 사용자 컨펌.

## 안전한 transition 속성 (성능 보증)

매뉴얼 인용 (UI Toolkit Transitions):
> *"Use transitions with USS transform properties"* for performance; color properties also have optimizations, but other properties may cause layout recalculations, reducing frame rates.

| 속성 | 안전성 | 메모 |
|---|---|---|
| `translate`, `scale`, `rotate` (transform) | ✅ 매우 안전 (GPU) | 이동/확대/회전 — 1순위 |
| `opacity` | ✅ 매우 안전 (GPU) | 페이드 |
| `background-color`, `color` | ✅ 안전 (최적화) | 색 전환 |
| `border-color` | ✅ 안전 | 보더 색 |
| `width`, `height`, `min-*`, `max-*` | ❌ 레이아웃 재계산 | 절대 transition 시키지 말 것 |
| `margin`, `padding` | ❌ 레이아웃 재계산 | 위와 동일 |
| `flex-grow`, `flex-shrink`, `flex-basis` | ❌ 레이아웃 재계산 | discrete 전환만 |
| `display` | discrete | smooth 안 됨, on/off만 |
| 폰트 크기 | ⚠ 비싸짐 | layout 재계산 트리거 가능, 피하기 |

→ **확장 효과를 내고 싶으면 width를 transition시키는 대신 `scale`을 transition시킨다.** 시각적 결과 비슷, 비용 1/10.

## Supported timing functions (매뉴얼 정확 인용)

> *"ease, ease-in, ease-out, ease-in-out, linear, ease-in-sine, ease-out-sine, ease-in-out-sine, ease-in-cubic, ease-out-cubic, ease-in-out-cubic, ease-in-circ, ease-out-circ, ease-in-out-circ, ease-in-elastic, ease-out-elastic, ease-in-out-elastic, ease-in-back, ease-out-back, ease-in-out-back, ease-in-bounce, ease-out-bounce, ease-in-out-bounce"*

→ **`cubic-bezier()`는 USS에 없음.** CSS 감각으로 적으면 안 먹는다. 위 키워드 중에서 골라야 함.

기본값: 매뉴얼이 명시하지는 않지만 USS 일반 기본은 `ease`. 토큰을 정의해서 명시 사용 권장.

## Pseudo-classes (매뉴얼 정확 인용)

| 클래스 | 의미 (매뉴얼) | 모바일 주의 |
|---|---|---|
| `:hover` | *"The cursor is positioned over the element."* | 터치 디바이스엔 보통 적용 안 됨 — **의존 금지** |
| `:active` | *"A user interacts with the element."* | press 동안. 모바일에서 동작. 즉시 피드백의 1순위 |
| `:focus` | *"The element has focus."* | 키보드 네비게이션 / 입력 포커스. 모바일에선 input 진입 시 |
| `:checked` | Toggle/RadioButton 선택됨 | 정상 |
| `:disabled` / `:enabled` | disabled 상태 | 색 변화만 말고 opacity로도 표현 |
| `:root` | stylesheet attach된 최상위 | 토큰 정의 위치 |

매뉴얼 명시: *"`:selected` is not supported"* → `:checked`만 쓴다.

→ **모바일이 1차 타깃이라면 `:hover`에만 의존하는 디자인은 하지 않는다.** `:active`로 press 피드백을, `:focus`로 키보드/접근성을 같이 표현.

## 표준 인터랙티브 패턴

### 1. 버튼 (가장 흔한 케이스)

```css
.btn {
    background-color: var(--color-surface);
    color: var(--color-on-surface);
    transition-property: background-color, scale, opacity;
    transition-duration: var(--motion-fast);
    transition-timing-function: var(--easing-standard);
}
.btn:hover {  /* 데스크톱에서만 보임, 모바일은 무시됨 — 의존 안 함 */
    background-color: var(--color-surface-hover);
}
.btn:active {  /* 모바일/데스크톱 모두 트리거 */
    background-color: var(--color-surface-pressed);
    scale: 0.97;  /* width/height 대신 scale */
}
.btn:focus {
    background-color: var(--color-surface-hover);
    /* outline 대신 색 + 두께 명시. UI Toolkit엔 outline-offset 등 한정 */
}
.btn:disabled {
    opacity: 0.5;  /* 색만 바꾸지 말고 opacity로도 — 색맹 대비 */
}
```

### 2. 카드 (책 슬롯, 리스트 항목)

```css
.book-card {
    translate: 0 0;
    transition-property: translate, scale, opacity;
    transition-duration: var(--motion-base);
    transition-timing-function: var(--easing-standard);
}
.book-card:hover {
    translate: 0 -2px;  /* margin 대신 translate */
}
.book-card:active {
    scale: 0.98;
}
```

### 3. Panel show/hide (대시보드 진입)

```css
.panel {
    opacity: 0;
    translate: 0 16px;
    transition-property: opacity, translate;
    transition-duration: var(--motion-slow);
    transition-timing-function: var(--easing-decelerate);
}
.panel.visible {
    opacity: 1;
    translate: 0 0;
}
```

C# 쪽:
```csharp
panel.AddToClassList("visible");   // show
panel.RemoveFromClassList("visible"); // hide
```
→ `style.opacity = ...` 직접 set 하지 말 것. 클래스 토글로 표현해야 USS가 우선순위를 잃지 않음 (참고: `unity-fundamentals/references/uitk-internals.md` §Style Precedence).

### 4. 리스트 stagger 등장

```css
.list-item {
    opacity: 0;
    translate: 0 8px;
    transition: opacity var(--motion-base) var(--easing-decelerate),
                translate var(--motion-base) var(--easing-decelerate);
}
.list-item.appear-1 { transition-delay: 0ms; }
.list-item.appear-2 { transition-delay: var(--motion-stagger); }
.list-item.appear-3 { transition-delay: calc(var(--motion-stagger) * 2); }
```

→ C#에서 `appear-N` 클래스를 인덱스 기반 부여하고 `visible` 토글.

## 모바일 햅틱

모바일에서 **확정/취소 같은 의미 있는 피드백**에는 짧은 햅틱을 더한다. 시각·청각이 닿지 않는 순간(주머니 안, 무음, 진동 인지 우선) 사용자 경험을 결정짓는다.

- Android: `Handheld.Vibrate(...)` (단, 강도 조절 없음, deprecated 경향) → 권장 패키지: `MobileNotifications` 또는 커스텀 NDK plugin
- iOS: `UIImpactFeedbackGenerator` (light/medium/heavy) 호출 — Unity → Native plugin
- 또는 Unity Asset Store의 mobile haptic 패키지 (예: *Lofelt Nice Vibrations*)

규칙:
- **확정** (책 배치 commit, 토글 on): `light` 햅틱 + `:active` 시각 피드백
- **취소/에러**: `medium` + 색 변화
- **연속 동작 (스크롤, 드래그)**: 햅틱 X — 진동 누적이 짜증

위치: `Features/Haptics/HapticService.cs` 같은 어댑터를 만들고 Application UseCase에서 호출. Domain은 햅틱 모름.

## 접근성 가드

1. **색만으로 상태 표현 금지** — disabled/error/success는 opacity, icon, scale 같은 *비-색* 차원을 같이.
2. **Reduced motion 존중** — OS 설정으로 사용자가 모션을 줄이고 싶다면 transition을 끈다. iOS `UIAccessibility.IsReduceMotionEnabled`, Android `ACCESSIBILITY_REDUCE_MOTION`. Unity 매뉴얼: *"The Accessibility module APIs are available only for iOS and Android devices for creating mobile applications accessible to people with disabilities."* → reduced motion이면 `--motion-fast/base/slow`를 모두 `0ms`로 덮어쓰는 클래스 토글:
   ```css
   :root.no-motion {
       --motion-fast:  0ms;
       --motion-base:  0ms;
       --motion-slow:  0ms;
   }
   ```
3. **터치 영역 최소 44×44 px** (iOS HIG / Material) — 시각 사이즈와 hit area 분리. UI Toolkit은 `picking-mode`, `padding`으로 hit 영역 확장.
4. **콘트라스트** — 텍스트와 배경 4.5:1 (WCAG AA). 모션 피드백은 콘트라스트 유지된 색으로.

## 출력 형식 (변경 보고에 한 블록)

```
## UI polish (motion)
- 적용 토큰: --motion-base, --easing-standard
- transition 속성: scale, background-color (모두 GPU-안전)
- pseudo-classes: :active (모바일/데스크톱 모두 동작), :hover (데스크톱 보조)
- 햅틱: 책 배치 commit 시 light (Features/Haptics 경유)
- 접근성: opacity로도 disabled 표현, .no-motion 클래스 토글 지원

✅ Perf budget: layout-affecting 속성 0개, frame budget 무영향
✅ Edit guard: DashboardView.uss 안에서만 변경, 셀렉터 광역 영향 없음
```

폴리시가 작은 한 줄짜리 변경이면 한 줄로 줄여도 OK:
```
✅ Polish: --motion-fast on .btn:active scale (GPU-safe, no layout change)
```

## 흔한 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| 호버 효과가 모바일에서 안 보임 | `:hover` 의존 | `:active`로 같은 시각 효과를, `:hover`는 데스크톱 보조로만 |
| 카드 펼치는 transition이 끊김 | `width/height` transition | `scale`로 대체 |
| `cubic-bezier()` 적었는데 안 먹음 | USS 미지원 | 매뉴얼 timing function 키워드 중 선택 |
| 같은 화면에 transition duration이 80ms, 150ms, 300ms 섞여 있음 | 토큰 부재 | --motion-* 토큰 도입, 임의값 제거 |
| `style.opacity = ...` 후 USS transition 안 먹음 | C# style이 USS를 무력화 | 클래스 토글로 표현 (`AddToClassList`) |
| 햅틱이 데스크톱 빌드에서 NRE | 플랫폼 분기 부재 | `Application.platform` 가드 또는 어댑터에서 no-op |
| Reduced motion 켰는데 여전히 모션 보임 | OS 설정 미반영 | startup에서 `:root`에 `.no-motion` 클래스 토글 |
| disabled 버튼이 보이긴 보이는데 클릭됨 | `picking-mode`가 Position 그대로 | `picking-mode: ignore` 또는 클릭 가드 |
| 스크롤 중 햅틱이 진동 폭탄 | 연속 이벤트마다 햅틱 호출 | 햅틱은 *의미 있는 확정 시점*만, 디바운스 |

## 협업 — 다른 스킬과의 경계

- **`unity-uitk-edit-guard`** — polish가 다른 화면을 깨지 않게 영향 범위 가드. polish 적용 전 USS 셀렉터/UXML name 사용처 확인 위임.
- **`unity-perf-budget`** — polish가 frame budget을 깎지 않게 검증. transition 속성이 layout-affecting인지 자동 체크 위임.
- **`unity-fundamentals/references/uitk-internals.md`** — Style Precedence, USS cascade, `Q()` 작동 원리 — 이 스킬이 가정하는 메커니즘의 근거.

## 출처

- Unity Manual — [USS transition](https://docs.unity3d.com/6000.2/Documentation/Manual/UIE-Transitions.html)
- Unity Manual — [USS Pseudo-classes](https://docs.unity3d.com/6000.2/Documentation/Manual/UIE-USS-Selectors-Pseudo-Classes.html)
- Unity Manual — [Transition events](https://docs.unity3d.com/6000.2/Documentation/Manual/UIE-Transition-Events.html)
- Unity Manual — [UI Toolkit styling best practice](https://docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/styling.html)
- Unity Manual — [Accessibility for mobile applications](https://docs.unity3d.com/6/Documentation/Manual/mobile-accessibility.html)
- Unity Blog — [Best practices for mobile UI design – part 2](https://blog.unity.com/games/mobile-ui-design-best-practices-part-2)
- Unity Discussions — [Announcing USS transition](https://discussions.unity.com/t/announcing-uss-transition/863464)
- Unity 무료 e-book — *User interface design and implementation in Unity* (`resources.unity.com/games`)
- Apple — Human Interface Guidelines (touch target, reduce motion)
- Material Design — Motion guidelines (reference for token sizing)
