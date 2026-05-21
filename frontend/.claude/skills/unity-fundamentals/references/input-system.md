# Input System

> **한 줄 요약**: 새 Input System은 InputAction(자산 또는 코드)을 중심으로 동작하고, **3 phases (started / performed / canceled)** 콜백 또는 폴링으로 값을 받는다. 모바일 터치는 `Touchscreen` (액션 바인딩) vs `EnhancedTouch.Touch` (폴링 + GC-free)로 두 갈래. 우리 프로젝트의 `BookInteractionManager.PointerDown/Up` 메서드 호출 방식은 둘 중 어느 쪽도 아닌 **수동 호출** 패턴이고, phase-03 Pending에 마이그레이션이 명시되어 있다.

## 어떤 결정이 이 글로 풀리는가

- **"`PointerDown`/`PointerUp` 메서드 호출 방식을 InputAction으로 옮길 때 무엇으로 옮기나?"** → §메커니즘 + §프로젝트 연결
- **"버튼 input은 `started`인가 `performed`인가?"** → §Action Phases
- **"폴링이 안 좋다는 말 들었는데, 진짜인가?"** → §콜백 vs 폴링
- **"모바일 터치는 `Touchscreen`인가 `EnhancedTouch`인가? UI Toolkit과 충돌하나?"** → §모바일 터치
- **"입력이 한 프레임 늦는다"** → §함정 카탈로그

## 메커니즘

### 1. 두 입력 시스템

Unity는 두 가지 입력 시스템을 제공하고 둘 다 활성화 가능 (Project Settings → Player → Active Input Handling = Both):

- **Legacy Input Manager** (`UnityEngine.Input.GetAxis(...)` 등) — 단순하지만 액션 추상화 없음. 모바일 터치는 `Input.touches`.
- **Input System Package** (`com.unity.inputsystem`) — 권장. ActionMap 기반, 디바이스 추상화, 리바인딩, 멀티플레이어 지원.

이 프로젝트의 `Assets/InputSystem_Actions.inputactions` 자산은 후자다.

### 2. InputAction의 구성

```
InputAction (action)
├── Bindings (어떤 컨트롤이 이 액션을 트리거하는가)
│   ├── <Keyboard>/space
│   ├── <Mouse>/leftButton
│   └── <Touchscreen>/primaryTouch/press
├── Type: Button | Value | PassThrough
└── Phases (런타임 상태): Disabled → Waiting → Started → Performed → Canceled → Waiting → ...
```

### 3. Action Phases — 매뉴얼 인용

> *"The Started, Performed, and Canceled phases each have a callback associated with them."*

- **`started`**: input이 들어오기 시작 (예: 버튼 누름 시작)
- **`performed`**: 액션이 "수행됨" (Type 별로 의미가 다름, 아래 §4)
- **`canceled`**: started 이후 performed 도달 못 하고 끝남, 또는 performed 이후 입력 종료

매뉴얼 인용 (Discussions 보충):
> *"The canceled callback is triggered when the action has been started but then canceled before being fully performed."*

### 4. Type별 phase 동작 차이

매뉴얼 표현 정확히:

- **Value Action** (기본): *"continuously monitor all the Controls which are bound to the Action"* → 값이 바뀔 때마다 `performed`
- **Button Action**: *"don't perform an initial state check like Value Actions do"* — 임계 통과 시 한 번 `performed`, 떼면 `canceled`
- **PassThrough**: 모든 변화에 대해 `performed`만, started/canceled 없음 — 다중 디바이스 동시 처리에 유리

→ 일반적인 "버튼 누르기" 의미면 **Type=Button** 으로 두고 `performed`만 구독하는 게 의도와 일치.

### 5. 콜백 vs 폴링

매뉴얼 표현:
> *"You can poll the current value of an Action using `InputAction.ReadValue<>()`"*
> *"`InputAction.WasPerformedThisFrame()` [returns] True if the `InputAction.phase` of the action has, at any point during the current frame, changed to Performed"*

```csharp
// 콜백 (event-driven)
moveAction.performed += ctx => _velocity = ctx.ReadValue<Vector2>();
moveAction.canceled  += _   => _velocity = Vector2.zero;

// 폴링 (Update에서 매 프레임 읽기)
void Update() {
    _velocity = moveAction.ReadValue<Vector2>();
    if (jumpAction.WasPerformedThisFrame()) Jump();
}
```

규칙:
- **연속 값**(이동, 카메라, 드래그 위치): 폴링 또는 Value Action 콜백 둘 다 OK
- **이벤트성**(버튼 클릭, 액션 트리거): 콜백 권장. 매 프레임 폴링은 `WasPressedThisFrame`/`WasReleasedThisFrame`에 한정
- **CallbackContext 수명**: 매뉴얼 경고 — *"The contents of the structure are only valid for the duration of the callback. In particular, it isn't safe to store the received context and later access its properties from outside the callback."* → ctx에서 값을 **콜백 안에서 읽어** 멤버 필드에 저장. ctx 자체를 캡쳐하지 말 것.

### 6. PlayerInput vs 직접 InputAction

- **PlayerInput 컴포넌트**: 매뉴얼 표현 *"the simplest way to set up Action callbacks. It provides an interface in the inspector"*. 인스펙터에서 Behavior=Send Messages / Invoke Unity Events / Invoke C# Events 선택.
- **직접 InputAction**: `InputActionReference` 또는 `InputActionAsset`을 코드에서 `Enable()`/`Disable()`/콜백 구독.

선택 가이드:
- 단일 플레이어, 인스펙터 친화적 흐름 → PlayerInput
- 명시적 lifecycle 제어, 테스트 가능성, DI에 통합 → 직접 InputAction (이 프로젝트의 클린 아키텍처와 더 정합)

### 7. 모바일 터치 — `Touchscreen` vs `EnhancedTouch.Touch`

매뉴얼이 직접 경고:
> *"You should not use Touchscreen for polling. If you want to read out touches similar to UnityEngine.Input.touches, see EnhancedTouch."*

| 용도 | 권장 |
|---|---|
| ActionMap 바인딩으로 단일 터치 / 핀치 / 드래그 | `<Touchscreen>/primaryTouch/press` 같은 바인딩 |
| 매 프레임 모든 활성 터치를 폴링 (예: 멀티터치 제스처) | `EnhancedTouch.Touch.activeTouches` |

`EnhancedTouch`는 **명시적 활성화 필요**: `EnhancedTouchSupport.Enable()`. 그리고 매뉴얼 보장:
> *"The Touch and Finger APIs don't generate GC garbage. The bulk of the data is stored in unmanaged memory that is indexed by wrapper structs. All arrays are pre-allocated."*

→ 모바일 매 프레임 멀티터치 처리는 `EnhancedTouch`가 GC 안전.

### 8. UI 시스템과 입력의 충돌

UI Toolkit / uGUI는 자체 포인터 처리를 갖는다. 새 Input System을 쓸 때:
- **EventSystem + Input System UI Input Module**을 쓰면 UI 영역의 클릭이 게임 액션으로 새지 않음
- UI 위에서 클릭한 경우 게임 액션을 무시하려면 `EventSystem.current.IsPointerOverGameObject()` 또는 UI Toolkit의 `panel.Pick()` 결과로 가드

## 이 프로젝트와의 연결

### 현재 상태 (phase-03 리포트)

> *"Input is still exposed as methods (`PointerDown`, `PointerUp`) rather than wired to a final Input System action map."*
> *Pending: Wire Input System actions to `BookInteractionManager`.*

즉 현재는 어디선가 직접 `BookInteractionManager.PointerDown(...)`을 호출해주는 방식. 이건 단위 테스트하기는 좋지만, 디바이스 추상화/리바인딩/멀티플레이어 같은 Input System의 이득을 못 본다.

### 마이그레이션 가이드

`Assets/InputSystem_Actions.inputactions`에 다음 액션을 둔다 (또는 이미 있으면 매핑 확인):

| 액션 이름 | Type | 바인딩 | 호출 시점 |
|---|---|---|---|
| `Book/Press` | Button | `<Mouse>/leftButton`, `<Touchscreen>/primaryTouch/press` | `started` → `BookInteractionManager.PointerDown(rayOrigin)` |
| `Book/Release` | (위 액션의 `canceled`) | 동일 | `canceled` → `BookInteractionManager.PointerUp()` |
| `Book/Position` | Value (Vector2) | `<Mouse>/position`, `<Touchscreen>/primaryTouch/position` | `performed` → 드래그 좌표 갱신 |

권장 패턴 (직접 InputAction, 클린 아키텍처와 정합):

```csharp
public sealed class BookInputBinder : MonoBehaviour {
    [SerializeField] InputActionReference _press;
    [SerializeField] InputActionReference _position;
    BookInteractionManager _manager;  // Application 경계의 인터페이스로 받는 게 더 깔끔

    void OnEnable() {
        _press.action.started   += OnPressStarted;
        _press.action.canceled  += OnPressCanceled;
        _press.action.Enable();
        _position.action.Enable();
    }
    void OnDisable() {
        _press.action.started   -= OnPressStarted;
        _press.action.canceled  -= OnPressCanceled;
        _press.action.Disable();
        _position.action.Disable();
    }

    void OnPressStarted(InputAction.CallbackContext _) {
        var pos = _position.action.ReadValue<Vector2>();
        // ctx 캡쳐하지 않고 즉시 값을 읽어 도메인 호출
        _manager.PointerDown(pos);
    }
    void OnPressCanceled(InputAction.CallbackContext _) => _manager.PointerUp();
}
```

이 어댑터 자체는 `Features/`에 둔다 (MonoBehaviour). `BookInteractionManager`는 Application/UseCase 경계에 두고, 인터페이스로 주입받게 해서 InputSystem 의존이 Application으로 새지 않게.

### UI Toolkit 위 클릭 무시

대시보드 UI 위에서 클릭한 경우 책장 raycast가 트리거되지 않게:
```csharp
if (panel.Pick(mousePos) != null) return; // UI 위 클릭 무시
```
또는 ActionMap을 *씬 모드*에 따라 enable/disable (예: Dashboard ActionMap은 dashboard 화면일 때만 Enable, Bookshelf ActionMap은 책장 모드일 때만 Enable).

## 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| 입력이 한 프레임 늦음 | Update에서 폴링 → FixedUpdate에서 처리 | 입력은 항상 Update 또는 콜백, 물리 적용은 FixedUpdate. 또는 둘을 묶지 않기 |
| 콜백 안의 ctx를 다른 함수에서 쓰니 값이 이상 | ctx 수명은 콜백 내부에 한정 | 콜백 안에서 `ReadValue<>()` 해 멤버 필드에 저장 후 다른 곳에서 사용 |
| 버튼 누른 직후 `performed` 이벤트가 두 번 옴 | Type=Value인 상태로 버튼처럼 사용 | Type=Button으로 변경 |
| 모바일 터치 시 GC 스파이크 | 매 프레임 새 List 생성 또는 `Input.touches`(legacy) | `EnhancedTouch.activeTouches` (GC-free) 또는 ActionMap 바인딩 |
| UI 클릭이 게임 액션으로 샘 | UI 영역 가드 없음 | `IsPointerOverGameObject` 또는 UI Toolkit `panel.Pick`, 또는 ActionMap 모드 분리 |
| `OnDisable`에서 콜백 detach 안 함 → Domain Reload 후 두 번 호출 | 정적 이벤트 패턴과 동일 | `OnEnable`/`OnDisable` 짝으로 attach/detach. action.Enable/Disable도 짝으로 |
| Touchscreen에서 직접 폴링했더니 값이 한 프레임 늦거나 lossy | 매뉴얼 명시 — Touchscreen은 폴링 비추 | EnhancedTouch.Touch 또는 ActionMap 콜백 |
| 액션이 호출되긴 하는데 `performed`만 잡고 끝났더니 드래그 unstuck | press 동안 release를 못 잡음 | `started`/`canceled` 둘 다 구독 |

## 디버깅 도구

- **Input Debugger** (`Window > Analysis > Input Debugger`): 디바이스 / 액션 / 바인딩 / 라이브 phase 변화를 다 볼 수 있음. ActionMap 마이그레이션할 때 1순위 도구.
- 액션 자산 인스펙터의 **"Run in Editor"** 토글로 Edit 모드에서도 입력 시뮬레이션 가능.

## 출처

- Unity Manual / Input System — [Responding to Actions](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.8/manual/RespondingToActions.html)
- Unity Manual / Input System — [Actions](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.8/manual/Actions.html)
- Unity Manual / Input System — [Touch support](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.8/manual/Touch.html)
- GitHub — [Unity-Technologies/InputSystem](https://github.com/Unity-Technologies/InputSystem)
- Unity Discussions — [Cancelled callback usage](https://discussions.unity.com/t/learning-new-inputsystem-when-and-how-is-the-cancelled-callback-used/808578)
