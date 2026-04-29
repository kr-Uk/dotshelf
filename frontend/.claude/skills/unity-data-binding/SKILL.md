---
name: unity-data-binding
description: Use this skill whenever the user is wiring data into a UI Toolkit screen in dotshelf — new ScreenController, refreshing labels/lists, syncing toggles back to state, or any code where a UXML element's text/value reflects a domain or ViewModel field. Trigger when the user says "bind this label to ...", "show this in the UI", "wire up the dashboard", "refresh when the data changes", "two-way binding", "sync the input field", or whenever a controller is about to call `Q().text = ...` / `.value = ...` manually. The skill applies Unity 6.3+ runtime data binding (`[CreateProperty]`, `INotifyBindablePropertyChanged`, `DataBinding`, binding modes, converters) instead of manual Q().property assignments. Coordinate with unity-layer-check (data sources stay in Application/Domain, bindings live in Features) and unity-uitk-edit-guard (UXML element name changes still require Q-binding awareness).
---

# unity-data-binding

UI Toolkit 6.3+의 공식 **Runtime Data Binding** 시스템을 dotshelf에 적용하는 스킬. 현재 `DashboardScreenController` 류가 손으로 `label.text = vm.Nickname` 같이 갱신하는 패턴을 → ViewModel(또는 SO) 데이터 소스 + `[CreateProperty]` + UXML `<DataBinding>` 으로 옮기게 한다.

## 왜 필요한가

- **수동 binding은 코드 결합도가 높다.** ViewModel 필드 하나가 바뀔 때 컨트롤러의 갱신 메서드를 매번 손으로 호출해야 하고, UXML element name과 C# property 이름의 동기화가 코드 곳곳에 흩어진다.
- **공식 binding은 `[CreateProperty]` 컴파일 타임 코드 생성**으로 리플렉션 비용 없이 binding 경로를 수립한다 — 매뉴얼 인용: *"This generates binding code at compile time, eliminating the need for runtime reflection and reducing performance overhead."*
- **two-way binding이 정식으로 작동**해서 입력 필드/토글 같은 폼이 코드 없이 ViewModel과 양방향 동기화된다.
- **`INotifyBindablePropertyChanged`** 로 변경 추적이 정확해져서, 한 필드가 바뀌면 그 필드를 binding한 요소만 갱신된다 (전체 panel rebuild 없음).
- 디자이너가 **UI Builder**에서 binding 경로를 직접 적을 수 있어, 코드 수정 없이 UI ↔ 데이터 매핑을 조정할 수 있다.

## 언제 발동하나

- 새 ScreenController/View 작성 — 처음부터 binding 패턴으로 시작
- 기존 `Q<Label>("name").text = ...` / `Q<TextField>("name").value = ...` 패턴 발견 시 마이그 제안
- 사용자가 "이 라벨에 닉네임 바인딩", "유저 입력 자동 반영", "이 토글 상태 저장" 같이 말할 때
- ViewModel/도메인 필드가 바뀌었을 때 UI가 자동으로 따라가야 하는 흐름 (실시간 통계, 카운터, 좋아요)

## Unity 6.3+ Data Binding 메커니즘 (매뉴얼 인용 기반)

### 1. Data Source가 될 수 있는 것

> *"Any C# object can serve as a data source, including ScriptableObjects, MonoBehaviours, or custom C# objects."*

→ POCO 클래스도 OK. 우리 클린 아키텍처에선 **Application 레이어의 ViewModel POCO** 가 자연스러움 — Domain/UseCase의 출력을 UI에 적합하게 가공한 형태.

### 2. `[CreateProperty]` Attribute (필수)

> *"The CreateProperty attribute is mandatory to make a property bindable in a data source."*

```csharp
using Unity.Properties;

public sealed class DashboardViewModel
{
    string _nickname = "";
    int _followers;

    [CreateProperty]
    public string Nickname
    {
        get => _nickname;
        set { _nickname = value; OnPropertyChanged(nameof(Nickname)); }
    }

    [CreateProperty]
    public int Followers
    {
        get => _followers;
        set { _followers = value; OnPropertyChanged(nameof(Followers)); }
    }
}
```

`[CreateProperty]`가 없는 필드/속성은 **binding 안 됨**. 컴파일러가 source generator로 binding 메타데이터를 만든다.

### 3. `INotifyBindablePropertyChanged` (변경 추적)

```csharp
using System.ComponentModel;
using UnityEngine.UIElements;

public sealed class DashboardViewModel : INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    void OnPropertyChanged(string name) =>
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(name));

    // ... [CreateProperty] 속성들
}
```

매뉴얼: *"This tracks changes at the property level, ensuring that affected bindings are refreshed."*

→ 구현 안 하면 **polling** 모드로 떨어져 매 프레임 dirty 체크 발생. 모바일에선 반드시 구현.

### 4. UXML에서 binding 선언 (권장)

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement>
        <ui:Label name="nickname-label">
            <Bindings>
                <ui:DataBinding property="text" data-source-path="Nickname"/>
            </Bindings>
        </ui:Label>
        <ui:Label name="followers-label">
            <Bindings>
                <ui:DataBinding property="text" data-source-path="Followers"/>
            </Bindings>
        </ui:Label>
    </ui:VisualElement>
</ui:UXML>
```

→ data source 자체는 코드에서 `rootVisualElement.dataSource = vm` 또는 element별 `element.dataSource = ...` 로 주입.

### 5. C#에서 binding 선언 (동적 케이스)

```csharp
using UnityEngine.UIElements;
using Unity.Properties;

label.SetBinding("text", new DataBinding {
    dataSource     = vm,
    dataSourcePath = new PropertyPath(nameof(DashboardViewModel.Nickname)),
    bindingMode    = BindingMode.ToTarget,  // 단방향 (vm → UI)
});
```

UXML로 선언 가능하면 그쪽이 우선. 디자이너가 binding 경로를 보고 수정할 수 있어야 함.

### 6. Binding Mode (매뉴얼 인용)

| 모드 | 흐름 | 용도 |
|---|---|---|
| `TwoWay` (기본) | source ↔ UI 양방향 | 입력 필드, 토글 — 사용자가 UI에서 바꾼 값이 ViewModel로 |
| `ToTarget` | source → UI | 라벨, 카운터 — read-only 표시 |
| `ToSource` | UI → source | 흔치 않음. 한쪽 이벤트만 |
| `ToTargetOnce` | 1회만 source → UI | 정적 라벨, 초기화 후 안 바뀜 |

→ 라벨은 `ToTarget`, 토글/슬라이더는 `TwoWay`. 잘못 두면 사용자 입력이 무시되거나 (`ToTarget`) 의도치 않은 source 변경 (`TwoWay`).

### 7. Converter (타입 변환)

`int Followers` → `string` 변환 자동으로 안 됨. converter 등록:

```csharp
var group = new ConverterGroup("FollowersFormat");
group.AddConverter((ref int v) => $"팔로워 {v:N0}");
ConverterGroups.RegisterConverterGroup(group);
```

UXML에서:
```xml
<ui:DataBinding property="text" data-source-path="Followers"
                source-to-ui-converters="FollowersFormat"/>
```

### 8. 성능 함정

매뉴얼 직접 경고:
> *"If your data source uses value types (e.g., int, float, struct), there is a boxing cost because the dataSource property operates as an object, and frequent conversions from value types can add overhead."*

→ ViewModel의 dataSource 자체는 **참조 타입**이어야 함 (class). 안에 `int`/`float` 속성을 두는 건 OK이지만, dataSource 자체를 struct로 두면 매 변경마다 boxing.

## 이 프로젝트와의 연결

### 권장 레이어 배치

```
Domain        — Book, User 같은 도메인 모델 (UnityEngine 무관)
              ↓
Application   — DashboardUseCase (책 목록 로드, 통계 집계)
              + DashboardViewModel (POCO, [CreateProperty] + INotifyBindablePropertyChanged)
              ↓
Features/UI   — DashboardScreenController (MonoBehaviour)
                ├─ UseCase 호출 → ViewModel 갱신
                └─ rootVisualElement.dataSource = viewModel
```

→ `[CreateProperty]` 와 `INotifyBindablePropertyChanged`는 **Application 레이어**에 둔다. Domain은 여전히 순수 (이 두 attribute는 UnityEngine 의존이지만 `Unity.Properties` 어셈블리이므로 Domain에 안 새는 게 정책 — `unity-layer-check`가 가드).

대신 Domain 모델을 ViewModel로 **매핑**한다:
```csharp
// Application/DashboardViewModel.cs
public sealed class DashboardViewModel : INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    [CreateProperty] public string Nickname { get; private set; }
    [CreateProperty] public int Followers { get; private set; }
    [CreateProperty] public int Following { get; private set; }
    [CreateProperty] public string Introduction { get; private set; }

    public void Apply(User user)  // Domain.User → ViewModel
    {
        Nickname = user.Nickname;
        Followers = user.FollowerCount;
        Following = user.FollowingCount;
        Introduction = user.Bio ?? "";
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(""));  // 모두 갱신 신호
    }
}
```

### Controller 패턴 (변경 후)

```csharp
// Features/Dashboard/DashboardScreenController.cs
public sealed class DashboardScreenController : MonoBehaviour
{
    [SerializeField] UIDocument _document;
    [Inject] ILoadDashboardUseCase _loadDashboard;

    DashboardViewModel _vm;

    async void OnEnable()
    {
        _vm = new DashboardViewModel();
        _document.rootVisualElement.dataSource = _vm;

        var snapshot = await _loadDashboard.Execute();
        _vm.Apply(snapshot.User);
    }
}
```

→ `Q().text = ...` 호출 0개. element name과 controller의 결합이 사라짐.

### 마이그레이션 단계

기존 화면이 있다면 한 번에 다 옮기지 말고 단계적으로:

1. **ViewModel 클래스 만들기** — Application 레이어, `[CreateProperty]` + `INotifyBindablePropertyChanged`
2. **수동 갱신 코드 격리** — 컨트롤러의 `Q()` 호출들을 한 메서드로 모음
3. **UXML 한 element씩 binding 선언** — 라벨/카운터 같은 read-only부터
4. **two-way 항목 (입력 필드/토글)** 옮길 때 사용자 입력 흐름이 ViewModel을 통해 도메인까지 도달하는지 통합 테스트
5. **Q().property 호출 모두 제거** 후 element name 직접 접근 패턴이 안 남았는지 grep 검증

## 흔한 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| binding이 적용 안 됨 | `[CreateProperty]` 누락 | 모든 노출 속성에 attribute |
| ViewModel 값을 바꿨는데 UI가 안 변함 | `INotifyBindablePropertyChanged` 미구현 → polling fallback인데 element가 dirty 검사 시점 못 잡음 | 인터페이스 구현 + setter에서 propertyChanged 호출 |
| 입력 필드 변경이 ViewModel에 안 반영 | `bindingMode = ToTarget` 또는 setter가 private | `TwoWay` 모드 + public setter |
| boxing GC alloc 매 프레임 | dataSource를 struct로 두거나, value type 변경마다 binding refresh | dataSource는 class, 변경 통지는 `INotifyBindablePropertyChanged`로 좁히기 |
| binding 경로에 오타 | `data-source-path`가 컴파일러 검증 안 됨 (문자열 매칭) | `nameof(VM.Property)` 사용, UI Builder로 작성, PR 직전 grep 검증 |
| Domain에 `[CreateProperty]` 들어감 | 매핑 누락 | ViewModel 별도 정의, `unity-layer-check`가 잡음 |
| 부모 dataSource를 자식이 자동 상속 안 받음 | 자식 element의 dataSource가 명시 설정 | 부모만 설정하면 cascading 됨 — 단 자식이 다른 source면 자식에서 override |
| converter 등록 안 함 | int → string 같은 자동 변환 기대 | `ConverterGroup` 명시 등록 + UXML에서 참조 |

## 출력 형식 (변경 보고에 포함)

```
## Data binding
- ViewModel: `Application/DashboardViewModel.cs` ([CreateProperty]: 4, INotifyBindablePropertyChanged ✓)
- UXML bindings: nickname-label.text, followers-label.text, following-label.text, intro-label.text (모두 ToTarget)
- Controller: `Features/Dashboard/DashboardScreenController.cs` (Q().text 호출 0개)
- 검증: 닉네임 변경 시 라벨 1회 갱신 (panel rebuild 없음, Profiler 확인)
```

## 협업 — 다른 스킬과의 경계

- **`unity-layer-check`** — `[CreateProperty]` / `INotifyBindablePropertyChanged`가 Domain에 새지 않는지 검증
- **`unity-uitk-edit-guard`** — UXML element name 변경 / `data-source-path` 오타 grep 검증
- **`unity-uitk-polish`** — 클래스 토글로 상태 표현이 binding과 잘 묶이는지 (e.g., `[CreateProperty] bool IsLoading` → UXML에서 `class` 토글)
- **`unity-fundamentals/references/uitk-internals.md`** — Style Precedence, Q() 작동 원리. Q()를 줄이고 binding으로 옮기는 근거.

## 출처

- Unity Manual — [Data binding (UI Toolkit best practices)](https://docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/data-binding.html)
- Unity Manual — [Define a data source for runtime binding](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-runtime-binding-define-data-source.html)
- Unity Discussions — [Introducing the Runtime Bindings API in Unity 2023.2](https://discussions.unity.com/t/introducing-the-runtime-bindings-api-in-unity-2023-2/921903)
- Unity Discussions — [Mini-tutorial: Data binding with UI Builder and C# in 5 minutes](https://discussions.unity.com/t/mini-tutorial-data-binding-with-ui-builder-and-c-in-5-minutes/1544817)
- Unity Discussions — [UI Toolkit DataBinding](https://discussions.unity.com/t/ui-toolkit-databinding/1504374)
- YouTube — [Runtime UI Data Binding with UI Toolkit](https://www.youtube.com/watch?v=_FlgT0bB_pY)
