---
name: unity-coreclr-prep
description: Use this skill whenever the user is doing work in the dotshelf project that touches static state, code that runs at editor/play boundaries, scripting runtime assumptions, or anything that will be affected by Unity's CoreCLR migration (Mono → CoreCLR in 6.7 experimental, single runtime in 6.8 LTS later in 2026). Trigger when adding/touching static fields, static events, [InitializeOnLoad], finalizers, AppDomain assumptions, P/Invoke, hot-path code that could benefit from Span<T>/hardware intrinsics, or any third-party DLL whose CoreCLR compatibility is uncertain. Also apply proactively when the user mentions "Unity 6.6/6.7/6.8", "CoreCLR", "domain reload", "Mono deprecation", "IL2CPP vs CoreCLR", or upgrades the editor version. The skill enforces forward-compatible patterns now so the eventual upgrade is mostly a recompile, not a rewrite — and clarifies that IL2CPP (mobile/iOS/WebGL) and CoreCLR (desktop/editor) coexist rather than one replacing the other.
---

# unity-coreclr-prep

dotshelf 코드를 Unity의 **CoreCLR 전환** (6.7 experimental → 6.8 LTS에서 Mono 제거) 에 미리 호환되게 두는 스킬. 우리는 Android 모바일 primary라 *모바일 빌드는 IL2CPP 그대로* 가지만, **에디터/데스크톱 흐름은 CoreCLR로 옮겨 가고 정적 상태 / 라이프사이클 처리 방식이 바뀐다**. 6.8에서 Mono 옵션은 사라지므로, 그 전에 정적 상태 누수와 라이브러리 호환을 정리해 두면 업그레이드가 *재컴파일* 수준에서 끝난다.

## 왜 필요한가

Unity 매뉴얼·공식 로드맵 인용:
- *"Over the course of 2026, Unity is planning changes that lead up to replacement of the scripting runtime in Unity 6.8, making CoreCLR the foundation of our C# layer."*
- *"Unity 6.8 will no longer have Mono as an available option."*
- *"CoreCLR could provide a significant boost in performance, by an order of 2x to 5x compared to the Mono runtime, sometimes up to x10 on some workload."*

CoreCLR가 가져오는 변화:
- **2~10x 빠른 C# 런타임** (벤치마크: 게임 코드 release 30s → 12s, struct 합산 11.5s → 0.75s)
- **`Span<T>`, hardware intrinsics, 모던 SIMD** 정식 지원
- **새 라이프사이클 모델** — Domain Reload 비활성을 **기본 가정**으로, 정적 상태 정리는 새 attribute로 명시
- **Mono 의존 코드 / 외부 DLL 일부 깨짐** — Mono-specific 동작에 의존한 라이브러리는 CoreCLR에서 문제

미리 정리하지 않으면 6.8 업그레이드 시 *프로젝트 전반을 일시 정지하고 정적 상태 추적*해야 함. 미리 가드해 두면 *그 시점에 enum 값 하나 바꾸듯* 끝난다.

## CoreCLR ↔ IL2CPP 관계 (오해 방지)

자주 헷갈리는 부분 정리:

| 백엔드 | 역할 | 6.8 이후 | dotshelf 영향 |
|---|---|---|---|
| **Mono** | C# 런타임. JIT. Editor + Standalone Mono 빌드 | **사라짐** | 모바일 안 씀. Editor에서 Mono 흐름이 CoreCLR로 |
| **CoreCLR** | Mono 대체. 새 C# 런타임. JIT. Editor + Standalone | **유일** | Editor 자체 + standalone desktop 빌드 |
| **IL2CPP** | C# → C++ AOT. iOS/Android/WebGL 등 모바일·웹 필수 | **유지** | Android 출시 빌드는 계속 IL2CPP. 영향 적음 |

→ **모바일 빌드 흐름 (IL2CPP) 은 CoreCLR로 갈아엎지 않는다.** CoreCLR는 desktop/editor 쪽 변화. 단, **같은 C# 소스 코드가 두 백엔드 모두에서 컴파일**되어야 하므로, CoreCLR가 새로 강제하는 패턴(정적 상태, 라이프사이클)은 IL2CPP 경로에도 영향을 준다.

## 언제 발동하나

- 새 `static` 필드 / static 이벤트 / static 컬렉션 추가
- `[InitializeOnLoad]` / `[InitializeOnLoadMethod]` / `[RuntimeInitializeOnLoadMethod]` 사용
- 클래스에 finalizer (`~ClassName()`) 추가
- `AppDomain` API 호출 / 동적 `Type.GetType` / 리플렉션 광범위 사용
- 외부 DLL (`precompiledReferences`) 추가 / Asset Store 패키지 도입
- hot-path 성능 코드 작성 (배열 처리, 수학 연산) — `Span<T>` / `stackalloc` / hardware intrinsics 도입 기회
- `Project Settings → Editor → Enter Play Mode → Reload Domain: Disabled` 토글 결정
- Unity 버전을 6.6 / 6.7 / 6.8 베타로 올림

## 정적 상태 — CoreCLR가 가장 자주 깨는 영역

### 메커니즘

`unity-fundamentals/references/domain-reload-asmdef.md` 에 정리한 대로, Unity는 Play 진입 시 **Domain Reload**로 정적 상태를 reset해 준다. CoreCLR 전환 흐름은 이 *자동 reset*을 가정하지 않는 방향:

- **Reload Domain 기본 비활성화** — 빠른 iteration이 새 표준
- 정적 상태는 **명시적으로 정리**해야 함. Unity가 자동 처리 안 함
- 새 라이프사이클 API attributes (6.7+에 도입):
  - `[AutoStaticsCleanup]` — 자동 정리 표시
  - `[BeforeCodeUnloading]` — 코드 언로드 직전 호출
  - `[AfterCodeReloadSerialization]` — 코드 reload 후 호출

### 가드 패턴

**Reload Domain Disabled 테스트** (6.6/6.7 단계에서):

`Project Settings → Editor → Enter Play Mode Settings → Reload Domain` 끈 상태로 게임을 **두 번 연속** 플레이. 두 번째에서 다음이 일어나면 정적 상태 버그:
- 이벤트 핸들러가 두 번 호출됨
- 카운터/캐시가 첫 번째 플레이 값에서 시작
- 이전 세션의 인스턴스가 남아 있음

→ 이걸 6.6/6.7에서 미리 잡아 두면, 6.8 도달 시 자동으로 호환.

### 코드 패턴 (6.8 호환 + 6.6/6.7 동작)

```csharp
public static class BookEvents
{
    public static event Action<Book> Placed;

    // 정적 상태 정리 — 모든 Unity 버전에서 동작
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Placed = null;
}
```

→ 이 패턴이면 *Reload Domain 비활성*에도 안전. CoreCLR 전환에도 안전.

**6.8에서 추가될 새 attribute 사용 시** version-gated:
```csharp
public static class BookEvents
{
    public static event Action<Book> Placed;

#if UNITY_6_8_OR_NEWER
    [AutoStaticsCleanup]  // CoreCLR 새 라이프사이클
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Placed = null;
}
```

→ 새 attribute는 6.6/6.7에 존재하지 않으므로 반드시 `#if` 가드. 가드 없으면 6.6 컴파일 실패.

## 외부 DLL / 라이브러리 호환

### 위험 신호

- **Mono-specific 어셈블리** — `Mono.Cecil` 외부 사용, `Mono.Posix` 등
- **`AppDomain.CurrentDomain.GetAssemblies()` 의존** — CoreCLR에서 동작 방식이 다를 수 있음
- **`System.Runtime.Remoting`** — CoreCLR에서 제거됨
- **`System.Reflection.Emit`** 무거운 사용 — IL2CPP에선 이미 제한적, CoreCLR도 일부 제약
- **Reflection 기반 직렬화 (Newtonsoft.Json 등)** — 라이브러리는 동작하지만 IL2CPP의 `[Preserve]` / `link.xml` 가드는 그대로 필요

### 검증 패턴

새 라이브러리 도입 시:
1. 라이브러리 README/release notes에서 *"CoreCLR support"*, *"net8.0 support"* 키워드 검색
2. NuGet `target framework` 확인 — `netstandard2.1`, `net8.0+` 가 안전. `net48` 만 지원하면 위험
3. Unity Discussions / GitHub Issues에서 *"Unity 6.7 CoreCLR"* 으로 사용 사례 확인
4. 도입 후 6.6/6.7 desktop standalone 빌드를 한 번은 실험 — 모바일 IL2CPP 통과해도 CoreCLR에서 깨질 수 있음

## 모던 C# 패턴 — CoreCLR가 강해지는 영역

CoreCLR가 정식 지원하는 패턴은 hot-path에서 큰 효과:

### `Span<T>` / `stackalloc`

```csharp
// Mono / IL2CPP에서도 컴파일되지만 CoreCLR에서 격차 큼
public static int Sum(ReadOnlySpan<int> values)
{
    var sum = 0;
    foreach (var v in values) sum += v;
    return sum;
}

public void ProcessSlots()
{
    Span<int> stackBuffer = stackalloc int[16];  // GC alloc 0
    // ...
}
```

→ **모바일에서도 IL2CPP 컴파일 가능**. CoreCLR 전환 시 자동으로 더 빨라짐. 새 hot-path 코드는 처음부터 `Span` 패턴 권장.

### Hardware intrinsics

```csharp
using System.Numerics;
using System.Runtime.Intrinsics;

// Vector<T>는 모든 백엔드에서 OK
// Vector128/256은 CoreCLR에서 진가
```

→ CoreCLR 전환 후 본격 활용. 지금은 도입하지 말고 hot-path 표시만.

### `readonly struct` + `in` 파라미터

```csharp
public readonly struct SlotPosition
{
    public readonly int Row;
    public readonly int Col;
    public SlotPosition(int row, int col) { Row = row; Col = col; }
}

void Place(in SlotPosition slot) { ... }  // 복사 없이 ref-style 전달
```

→ 모든 백엔드에서 OK. CoreCLR에서 inlining 더 적극적.

## 이 프로젝트와의 연결

### 현재 상태 점검 체크리스트

(우리 프로젝트가 6.8 호환되는지 사전 진단)

- [ ] `App.Domain/`, `App.Application/`, `App.Features/` 어셈블리에 **정적 상태 감사** — `static` 필드/이벤트 모두 reset 메커니즘 있는지
- [ ] `[InitializeOnLoad]` / `[RuntimeInitializeOnLoadMethod]` 사용처 정리 — Domain에 절대 없는지 (`unity-layer-check` 가드와 결합)
- [ ] `Reload Domain: Disabled` 토글로 *두 번 연속 플레이* 회귀 테스트 — phase 마무리 단계에 추가
- [ ] 외부 패키지 호환성 — `Packages/manifest.json`의 각 패키지가 CoreCLR/.NET 8+ 호환인지 README 확인
- [ ] hot-path 성능 코드는 처음부터 `Span<T>` / `readonly struct` / 0-alloc 패턴

### 권장 액션 (시점별)

| 시점 | 액션 |
|---|---|
| **지금 (6.0~6.5)** | 정적 상태 정리, `[RuntimeInitializeOnLoadMethod]` 일관 적용. 새 정적 추가 시 `unity-coreclr-prep` 자동 발동 |
| **6.6 베타 사용 시** | `Reload Domain: Disabled` 토글 후 두 번 연속 플레이 테스트 — phase 리포트 검증 항목에 추가 |
| **6.7 (CoreCLR experimental desktop)** | desktop standalone 빌드 한 번은 CoreCLR로 → 외부 라이브러리 호환 점검 |
| **6.8 (Mono 제거)** | 위에서 모두 통과했다면 자동으로 호환. 새 attribute (`[AutoStaticsCleanup]`) 명시 적용 |

### Mock/Real API 흐름의 영향

`CLAUDE.md` 규칙 5 — *"Mock API와 Real API는 base URL만 다르다"* — 가 이미 정적 분기를 막고 있어서 CoreCLR 전환에 자연스럽게 호환. 단, Mock 구현이 정적 컬렉션(`static List<Book> _mockBooks`)으로 데이터를 들고 있으면 reset 가드 필수.

## 흔한 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| Reload Domain 끈 뒤 두 번째 Play부터 이벤트 2번 호출 | 정적 이벤트 핸들러 누적 | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 reset |
| 같은 코드 6.5에선 OK, 6.7 desktop 빌드에서 NRE | Mono 특화 라이브러리 / API | 라이브러리 CoreCLR 호환 확인, 대체 |
| `[AutoStaticsCleanup]` 적었더니 6.6 컴파일 실패 | 6.7+ attribute, 6.6엔 없음 | `#if UNITY_6_8_OR_NEWER` 가드 |
| Span<T> 도입했더니 IL2CPP Android 빌드 실패 | 매우 드물지만 일부 패턴 IL2CPP 미지원 가능 | 빌드 직후 실기 테스트, 안 되면 `T[]` fallback |
| 외부 직렬화 라이브러리가 CoreCLR에서 다른 결과 | reflection 동작 차이 | `[Preserve]` + link.xml 그대로 + CoreCLR에서도 한 번 검증 |
| Editor가 6.7부터 갑자기 느려짐 | CoreCLR experimental에 패치되지 않은 부분 | beta 단계 — 정식 6.8 LTS까지 main 작업은 6.6 LTS |
| `AppDomain.CurrentDomain` API가 일부 동작 안 함 | CoreCLR에서 의미 다른 API | Unity 매뉴얼의 `UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()` (6.8+) 사용. 그 전엔 fallback |
| 정적 캐시가 Edit 모드에서 부풀어 오름 | Domain Reload 비활성 가정 안 함 | 캐시 위치를 정적 → ScriptableObject 인스턴스로 옮기거나 명시 reset |

## 출력 형식

CoreCLR 영향 가능한 변경에 한 블록:

```
## CoreCLR readiness
- 정적 상태: 새 static event `BookEvents.Placed` — RuntimeInitializeOnLoadMethod로 reset ✓
- Reload Domain Disabled 테스트: phase 마무리 시 검증 (Pending에 추가)
- 외부 라이브러리 추가 없음
- 모던 패턴: Span<T> 도입 (BookSorter.Sort hot-path) — IL2CPP/CoreCLR 모두 지원
```

위반/위험 없으면 한 줄:
```
✅ CoreCLR readiness: no static state, no foreign libs, no [InitializeOnLoad]
```

## 협업 — 다른 스킬과의 경계

- **`unity-layer-check`** — Domain에 `[InitializeOnLoad]` / static 상태 들어가지 않게 (CoreCLR 호환 + 클린 아키텍처 동시 강제)
- **`unity-fundamentals/references/domain-reload-asmdef.md`** — Domain Reload·정적 상태 메커니즘의 깊은 근거
- **`unity-fundamentals/references/build-il2cpp.md`** — IL2CPP가 모바일에 그대로 남는 이유, link.xml은 CoreCLR 전환 후에도 유효
- **`unity-perf-budget`** — Span<T> 도입으로 GC alloc / frame = 0 유지 검증

## 출처

- Unity Discussions — [Path to CoreCLR, 2026: Upgrade Guide](https://discussions.unity.com/t/path-to-coreclr-2026-upgrade-guide/1714279)
- Unity Discussions — [CoreCLR, Scripting, and ECS Status Update — March 2026](https://discussions.unity.com/t/coreclr-scripting-and-ecs-status-update-march-2026/1711852)
- Unity Discussions — [Expected performance of new CoreCLR vs Mono vs IL2CPP](https://discussions.unity.com/t/expected-performance-of-new-coreclr-vs-mono-vs-il2cpp/910032)
- Unity Discussions — [Planned breaking changes in Unity 6.5](https://discussions.unity.com/t/planned-breaking-changes-in-unity-6-5-updated-2026-03-27/1694205)
- Digital Production — [Unity's 2026 Roadmap: CoreCLR, Verified Packages, Fewer Surprises](https://digitalproduction.com/2025/11/26/unitys-2026-roadmap-coreclr-verified-packages-fewer-surprises/)
- byteiota — [Unity's 8-Year CoreCLR Wait: 2-10x Performance Tax](https://byteiota.com/unitys-8-year-coreclr-wait-2-10x-performance-tax/)
- Marek Fišer — [Unity's Mono problem: Why your C# code runs slower than it should](https://marekfiser.com/blog/mono-vs-dot-net-in-unity/)
- Unity Manual — [IL2CPP Overview](https://docs.unity3d.com/6000.1/Documentation/Manual/scripting-backends-il2cpp.html)
