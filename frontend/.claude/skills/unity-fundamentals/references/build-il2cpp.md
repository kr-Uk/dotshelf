# IL2CPP & Build

> **한 줄 요약**: IL2CPP는 C#을 → IL → C++ → 네이티브로 **AOT(ahead-of-time) 컴파일**하는 백엔드. Android 64-bit에 필수이고 Mono보다 빠르지만, 빌드가 길어지고 **리플렉션·제네릭의 일부 패턴이 런타임에 `ExecutionEngineException`으로 터진다**. managed code stripping과 짝으로 동작하며, 이게 안 잡으면 동적으로 호출되는 코드가 사라진다.

## 어떤 결정이 이 글로 풀리는가

- **"IL2CPP를 꼭 써야 하나? Mono로 빌드하면 빠르던데"** → §언제 IL2CPP를 강제하는가
- **"에디터에선 잘 되는데 Android 빌드에서 `ExecutionEngineException`"** → §AOT 함정
- **"link.xml은 언제 필요한가?"** → §Managed Code Stripping
- **"빌드 시간이 너무 길다"** → §빌드 시간 줄이기
- **"JSON 라이브러리 / DI 라이브러리가 빌드에서 안 됨"** → §함정 카탈로그

## 메커니즘

### 1. Mono vs IL2CPP

| 항목 | Mono | IL2CPP |
|---|---|---|
| 컴파일 모델 | JIT (런타임에 IL → 네이티브) | AOT (빌드 타임에 IL → C++ → 네이티브) |
| 런타임 성능 | 보통 | 빠름 (특히 hot path, GC 최적화) |
| 빌드 시간 | 짧음 | 길음 (전체 C++ 빌드) |
| 빌드 사이즈 | 작음 | 큼 (네이티브 코드 + 메타데이터) |
| 리플렉션/제네릭 | 자유로움 | 제약 있음 (§AOT 함정) |
| 디버깅 | 쉬움 | C++ 레벨로 떨어짐 |
| 보안 | IL DLL 그대로 노출 (decompile 쉬움) | C++ 네이티브, decompile 어려움 |

매뉴얼 표현:
> *"IL2CPP is an alternative to Mono that compiles code ahead-of-time (AOT) and supports a wider range of platforms."*

### 2. 언제 IL2CPP를 강제하는가

- **Android 64-bit (arm64)**: Google Play는 **64-bit 지원 필수**. Unity 6 LTS 기준 Android arm64는 **IL2CPP만 지원**. 따라서 Play 스토어 배포는 IL2CPP 필수.
- **iOS**: Apple이 AOT만 허용하므로 **IL2CPP 필수** (Mono 지원 없음).
- **WebGL**: IL2CPP 필수 (브라우저 환경).
- **Standalone (Windows/Mac/Linux)**: 둘 다 가능. Mono로 빠르게 반복 후 릴리스에서 IL2CPP로 빌드하는 흐름이 일반적.

→ **dotshelf의 Android 출시 빌드는 IL2CPP 필수**. 개발 중 Mono로 빠르게 빌드해 본다 해도, 릴리스 직전 IL2CPP로 한 번은 빌드해서 §AOT 함정에 안 걸리는지 확인 필수.

### 3. Managed Code Stripping

IL2CPP를 쓰면 *managed bytecode stripping*이 항상 켜진다 (매뉴얼: *"Managed bytecode stripping is always enabled when the IL2CPP scripting backend is used."*). 동작:

1. 빌드 시작 시점에 Unity가 **루트 진입점**(씬에서 참조되는 MonoBehaviour, ScriptableObject, 코드에서 직접 호출되는 타입)을 식별
2. **그래프 traversal** — 루트에서 도달 가능한 모든 코드를 보존 대상으로 마킹
3. 마킹 안 된 코드는 빌드에서 **제거** → 사이즈 ↓, 빌드 ↓

문제: **리플렉션으로만 호출되는 코드는 정적 분석으로 안 잡힘.** 예:
- `Type.GetType("Foo.Bar")` 로 동적 로드되는 클래스
- `Activator.CreateInstance(...)` 로 동적 생성
- JSON 직렬화 라이브러리가 필드 이름 기반으로 reflection
- DI 컨테이너가 인터페이스 → 구현 매핑

빌드는 통과하지만 런타임에:
- `Type.GetType` → `null`
- `JsonUtility.FromJson<T>` → 필드가 비어 있음
- 디미티 라이브러리 → "type not registered" 같은 에러

### 4. link.xml — 보존 강제

Stripping이 못 잡는 코드를 명시 보존하려면 `Assets/` 아래 어딘가에 `link.xml` 추가.

```xml
<linker>
  <!-- 어셈블리 전체 보존 -->
  <assembly fullname="Newtonsoft.Json" preserve="all"/>

  <!-- 특정 타입만 보존 -->
  <assembly fullname="App.Domain">
    <type fullname="App.Domain.Models.Book" preserve="all"/>
  </assembly>

  <!-- 와일드카드로 네임스페이스 단위 -->
  <assembly fullname="App.Infrastructure">
    <type fullname="App.Infrastructure.Dto.*" preserve="all"/>
  </assembly>
</linker>
```

`preserve` 옵션:
- `all` — 타입의 모든 것 (메서드/필드/제네릭 인스턴스)
- `fields` — 필드만 (직렬화 용도)
- `methods` — 메서드만
- 미지정 — 타입 자체만 (메타데이터)

**소스 코드 단위 보존** — `[UnityEngine.Scripting.Preserve]` attribute. 더 좁은 범위(특정 클래스/메서드)에 자연스럽게 붙는다.

```csharp
using UnityEngine.Scripting;

[Preserve]
public class BookDto {
    [Preserve] public string title;
    [Preserve] public int slotIndex;
}
```

### 5. AOT 함정 — Generic + Reflection

IL2CPP의 정적 분석은 **사용된 generic instance만 코드 생성**한다. 리플렉션으로 generic을 만들면 코드가 없다.

검색 결과의 정확한 에러 형태:
> *"ExecutionEngineException: Attempting to call method 'AOTProblemExample::OnMessage<AOTProblemExample+AnyEnum>' for which no ahead of time (AOT) code was generated."*

원인 패턴:
- `Type.MakeGenericType(typeof(Foo))` — IL2CPP에서 동작 안 함 (회피책: 소스에서 `Foo`로 명시적 사용을 한 번 넣어 두면 코드 생성됨)
- `MethodInfo.MakeGenericMethod(...)` — 마찬가지
- enum을 generic 타입 매개변수로 dispatch — value type별로 다른 코드 필요

회피 패턴:
1. **명시적 dummy 호출** — 컴파일러가 generic instance를 보게 하기:
   ```csharp
   #if UNITY_IL2CPP
   private static void AOTHints() {
       new Foo<MyEnumA>(); new Foo<MyEnumB>();
   }
   #endif
   ```
2. **`[Preserve]` + link.xml** 로 보존
3. **리플렉션 대신 Source Generator / Roslyn** 으로 컴파일 타임 코드 생성 (현대적 접근)

매뉴얼 보충 (검색 결과):
> *"For generic types and methods, the compiler must determine which generic instances are used because different generic instances might require different code... However IL2CPP will share code for usages for reference types, so the same code will be used for List<object> and List<string>."*

→ reference type generic은 코드 공유 (안전), value type generic은 인스턴스마다 코드 (위험).

### 6. 빌드 시간 줄이기

- **Incremental build**: Unity 6 LTS에서 IL2CPP 점진 빌드 지원 — 코드 변경 작은 경우 전체 C++ 빌드 회피
- **Script-only build** (Build Settings → Build): 자산 변경 없을 때 코드만 다시. 압도적으로 빠름
- **Mono로 개발, IL2CPP로 릴리스**: 단 릴리스 전 IL2CPP 빌드 한 번은 필수
- **Strip Engine Code**: Player Settings에 더 공격적 stripping 옵션. 단 잘 쓰면 사이즈 ↓ 잘못 쓰면 회귀

### 7. Android 64-bit / app bundle 관련

- Google Play는 **AAB (Android App Bundle)** 권장 형식
- 64-bit ARM (`arm64-v8a`) 필수, 32-bit (`armeabi-v7a`) 옵션
- Player Settings → Other Settings:
  - `Scripting Backend = IL2CPP`
  - `Target Architectures = ARMv7 + ARM64` (또는 ARM64만, 모던 기기 대상)
  - `Api Compatibility Level = .NET Standard 2.1` (Unity 6 권장)

## 이 프로젝트와의 연결

### 현재 추정 상태

`Assets/SOCIAL_LOGIN_SETUP.md`, `ANDROID_DEBUG_KEYSTORE.md` 가 있음 — Android 빌드를 실제로 하는 흐름. Player Settings는 미확인이지만 Android 출시 의지가 있다면 IL2CPP 필수 경로.

### 권장 액션

1. **릴리스 빌드 IL2CPP 검증 자동화** — phase-XX 단위로 IL2CPP 빌드를 한 번 돌려보고 런타임 테스트. 에디터/Mono에서 통과한 게 IL2CPP에서 깨지는 건 흔하므로.
2. **link.xml 후보 사전 식별**:
   - 외부 직렬화 라이브러리(Newtonsoft.Json 등) 사용 시 어셈블리 보존
   - `App.Domain` DTO에 `[Preserve]` 일관 적용 (백엔드 API JSON 직렬화에 필요한 필드들)
3. **AOT-안전 패턴 채택**:
   - `Type.MakeGenericType` / `Activator.CreateInstance<T>(typeof(...))` 회피
   - DI 컨테이너 — IL2CPP 호환 라이브러리만 사용 (Zenject, VContainer 등은 호환 OK이지만 설정에 따라 link.xml 필요)
4. **Mock/Real API 흐름** (`CLAUDE.md` 규칙 5)도 IL2CPP에서 같은 코드 경로를 타야 함. 환경 분기에 리플렉션 끼우면 stripping이 한쪽을 날릴 수 있음.

## 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| 에디터/Mono OK, IL2CPP Android에서 `ExecutionEngineException` | Generic AOT 코드 미생성 | dummy 호출 or `[Preserve]` 또는 Source Generator로 회피 |
| `JsonUtility.FromJson` 결과 필드가 비어 있음 | Stripping이 DTO 필드 제거 | `[Preserve]` on fields 또는 link.xml |
| `Type.GetType("My.Class")` 가 null 반환 | 타입 stripping | link.xml로 타입 보존 |
| DI 컨테이너 등록한 인터페이스 → 구현 매핑이 깨짐 | 구현 클래스 stripping | `[Preserve]` on 구현 클래스 또는 link.xml |
| 빌드 시간이 30분 넘음 | 전체 C++ 빌드, asset 동시 빌드 | Script-only build, incremental, 자산 변경 격리 |
| Mono 빌드는 빠른데 IL2CPP만 사이즈 폭증 | 디버그 심볼 / engine code 미stripping | "Strip Engine Code" 활성, IL2CPP Code Generation = "Faster (smaller) builds" 옵션 |
| Mac에서 iOS 빌드 OK, Windows에서 Android 빌드 NG | C++ 빌드 환경 차이 | 빌드 머신 설정 통일, Unity Hub로 IL2CPP 모듈 정확히 설치 |
| Newtonsoft.Json 같은 외부 라이브러리가 깨짐 | 라이브러리 자체가 reflection 의존 | 라이브러리 제공 link.xml 사용 또는 AOT-friendly 대안 (System.Text.Json) |

## Quick reference: 우리의 빌드 가이드라인

- **개발**: Mono로 빠르게 반복
- **PR 머지 전**: IL2CPP 빌드 한 번 (릴리스 환경 검증)
- **DTO/직렬화 클래스**: `[Preserve]` 일관 적용
- **외부 라이브러리 도입 시**: AOT 호환성 사전 확인 (라이브러리 README의 "Unity IL2CPP support" 항목)
- **`Type.MakeGenericType` 패턴 발견 시**: 즉시 회피책 적용 (이 글의 §AOT 함정)

## 출처

- Unity Manual — [IL2CPP scripting back end](https://docs.unity3d.com/Manual/scripting-backends-il2cpp.html)
- Unity Manual — [IL2CPP limitations / scripting restrictions](https://docs.unity3d.com/6000.3/Documentation/Manual/scripting-restrictions.html)
- Unity Manual — [Managed code stripping](https://docs.unity3d.com/2020.3/Documentation/Manual/ManagedCodeStripping.html)
- Unity Manual — [Managed bytecode stripping with IL2CPP](https://docs.unity3d.com/2017.4/Documentation/Manual/IL2CPP-BytecodeStripping.html)
- Unity Blog — [Better managed code stripping with Unity 2020 LTS](https://blog.unity.com/engine-platform/tales-from-the-optimization-trenches-better-managed-code-stripping-with-unity-2020)
- Unity Discussions — [IL2CPP AOT generic virtual methods](https://discussions.unity.com/t/il2cpp-aot-generic-virtual-methods-no-more-a-problem/789746)
- GitHub (Unity sample) — [link.xml](https://github.com/Unity-Technologies/EndlessRunnerSampleGame/wiki/Link.xml)
- Android Developers — [Android App Bundle texture compression](https://developer.android.com/guide/playcore/asset-delivery/texture-compression)
