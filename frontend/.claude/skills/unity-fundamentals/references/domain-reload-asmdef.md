# Domain Reload & Assembly Definition

> **한 줄 요약**: Unity의 Domain Reload는 Play 진입마다 스크립팅 도메인을 통째로 다시 만드는 무거운 작업이고, asmdef는 이 도메인 안의 어셈블리를 쪼개서 "어디까지 다시 컴파일·로드할지"를 제어한다. dotshelf의 `App.Domain.asmdef` 분리는 이 두 메커니즘에 직접 의존한다.

## 어떤 결정이 이 글로 풀리는가

- **"왜 우리는 Domain layer에 UnityEngine을 안 넣나? 단순한 코딩 컨벤션 아닌가?"** → §메커니즘 + §프로젝트 연결
- **"asmdef를 더 잘게 쪼개야 하나, 합쳐야 하나?"** → §프로젝트 연결
- **"Enter Play Mode를 빠르게 만들고 싶은데 뭐가 깨지나?"** → §함정 카탈로그 (Domain Reload 비활성화)
- **"static 필드/이벤트 함부로 쓰지 말라는 게 진짜 무슨 뜻인가?"** → §함정 카탈로그 (정적 상태)
- **"`[InitializeOnLoad]` 같은 거 Domain에 넣어도 되나?"** → §함정 카탈로그

## 메커니즘

### 1. Scripting Domain이 무엇인가

Unity 매뉴얼이 정의하는 scripting domain은 *"an isolated section of memory dedicated to a particular application, which contains the compiled types required by the application"* — 즉 컴파일된 .NET 타입들이 사는 격리 메모리 공간이다. 이게 한 번에 하나만 살아 있고, 코드를 다시 컴파일하거나 Play 모드에 들어갈 때 통째로 폐기·재생성된다. 그 행위가 Domain Reload.

### 2. Domain Reload가 일어나는 시점과 비용

- **컴파일 후** (코드를 저장하면 자동)
- **Play 모드 진입 시** (기본 설정)
- **Editor 스크립트에서 명시적 트리거 시**

비용: 매뉴얼 표현 그대로 *"a time-consuming operation that negatively impacts iteration times when you frequently switch between Edit and Play mode"*. 프로젝트가 커질수록 1초 → 5초 → 30초로 자라며, 작업 흐름의 가장 큰 마찰이 된다.

### 3. Domain Reload가 활성일 때 reset되는 것

- 모든 **static 변수**가 초기값으로 돌아감
- 등록된 **static 이벤트 핸들러**가 비워짐
- **non-serialized 필드** 값이 지워짐

### 4. Domain Reload를 비활성화하면 (Project Settings → Editor → Enter Play Mode Settings)

매뉴얼 표현:
- *"Non-serialized fields keep the values assigned to them during Play mode on returning to Edit mode"*
- *"Static variables keep their values between Play mode sessions"*
- *"Static events keep their registered subscribers between Play mode sessions"*

→ Edit 모드와 Play 모드 사이에 정적 상태가 살아남는다. 이걸 의식하지 않으면 *"Edit에서 켜고 Play 한 번 돌리면 잘 되는데, 두 번째 Play부터 이벤트가 두 번씩 호출됨"* 같은 함정에 빠진다.

### 5. 도메인이 비활성일 때 직접 정적 상태를 비우게 해주는 attribute 3가지

| Attribute | 어셈블리 | 용도 |
|---|---|---|
| `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` | UnityEngine | 런타임에서 Play 진입 시 정적 상태 초기화 |
| `[InitializeOnLoadAttribute]` | UnityEditor | Editor 스크립트에서 Play 모드 상태 변화 핸들러 등록 |
| `[InitializeOnEnterPlayMode]` | UnityEditor | Editor 전용 코드에서 Play 진입 정리 |

→ Domain Reload를 끈다면 이 셋을 명시적으로 써서 정적 상태를 손으로 reset해야 한다.

### 6. Assembly Definition (asmdef)이 이 그림 안에서 하는 일

asmdef는 어셈블리 단위로 코드를 분할한다. Domain Reload는 *변경된* 어셈블리만 다시 컴파일하므로, 분할이 잘 되어 있을수록 **컴파일 시간**이 줄어든다 (Domain Reload 자체 시간은 안 줄어들지만, 도메인 재구축 전 컴파일 페이즈가 짧아짐).

asmdef 핵심 필드 (출처: 매뉴얼 *Assembly Definition file format reference*):

| 필드 | 타입 | 기본 | 의미 |
|---|---|---|---|
| `name` | string | 필수 | 어셈블리 이름 |
| `references` | string[] | `[]` | 다른 asmdef 어셈블리 참조 |
| `autoReferenced` | bool | `true` | 다른 어셈블리가 자동으로 이 어셈블리를 참조할 수 있는지 |
| `noEngineReferences` | bool | `false` | `true`로 두면 Unity가 **`UnityEditor`/`UnityEngine` 참조를 컴파일 시 추가하지 않음** |
| `overrideReferences` | bool | `false` | 정확한 precompiled DLL 참조를 강제 |
| `precompiledReferences` | string[] | `[]` | DLL 파일 이름 목록 |
| `defineConstraints` | string[] | `[]` | 이 어셈블리가 컴파일되려면 필요한 #define 심볼 |
| `includePlatforms` / `excludePlatforms` | string[] | `[]` | 플랫폼 한정 |
| `allowUnsafeCode` | bool | `false` | unsafe 블록 허용 |

### 7. `noEngineReferences = true` 의 진짜 효과

기본은 `false`라서 이 어셈블리가 자동으로 `UnityEngine.dll`, `UnityEngine.UI.dll` 등을 참조한다 → 코드에 `using UnityEngine;` 안 써도 사용자가 **실수로 `MonoBehaviour`를 상속하면 그냥 컴파일이 됨**. `true`로 바꾸면 그 자동 참조가 끊겨, Domain 어셈블리에서 Unity 타입을 쓰는 순간 **컴파일 에러**가 난다 — 컨벤션이 컴파일러에 의해 강제됨.

## 이 프로젝트와의 연결

`frontend/Assets/App/Domain/App.Domain.asmdef`:

```json
{
  "name": "App.Domain",
  "rootNamespace": "App.Domain",
  "references": [],
  "noEngineReferences": false,
  ...
}
```

### 이 분리가 만드는 실질 이득

1. **컴파일 격리** — Features의 코드를 수정해도 Domain은 다시 컴파일되지 않음. asmdef가 늘어날수록 평균 변경당 컴파일 어셈블리 수가 줄어 Enter Play Mode가 빨라진다.
2. **테스트 격리** — `App.Tests.EditMode`가 Unity 런타임 의존 없이 도메인 로직만 검증할 수 있음. 단, 이 이득은 `noEngineReferences`가 강제되지 않으면 깨질 수 있음 (Domain에 `MonoBehaviour`가 새지 않을 거라는 보장이 컨벤션에만 의존).
3. **재사용 가능성** — Domain 코드가 순수 C#이면 백엔드/CLI 툴에서 같은 모델을 가져다 쓸 수 있음.

### 알려진 부채

`App.Domain.asmdef`의 `noEngineReferences`가 **`false`** 다. 즉 컨벤션은 있지만 컴파일러가 잡아주지 않는다. 누군가 `using UnityEngine;` 을 Domain에 적으면 그냥 빌드된다 — 그래서 `unity-layer-check` 스킬이 grep 기반 가드를 따로 돌린다.

이걸 `true`로 바꾸려면 *현재 Domain에 Unity 타입이 하나도 없는지* 먼저 확인해야 하고, 누군가 `[Serializable]`만 가져다 쓰고 있어도 컴파일 에러가 난다 (그것도 UnityEngine). 단독으로 바꾸지 말고 사용자 컨펌과 함께 진행.

## 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| Enter Play Mode가 느림 | 어셈블리가 한 덩어리, 모든 변경이 전체 재컴파일 트리거 | asmdef로 변경 빈도 높은 부분(Features)을 분리. Domain은 거의 안 변하므로 격리 효과 큼 |
| Domain Reload 끈 뒤 두 번째 Play부터 이벤트 2번 호출 | Static 이벤트 핸들러가 첫 Play의 등록을 유지 | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 해당 정적 상태를 명시 reset |
| Domain Reload 끈 뒤 카운터·캐시가 누적됨 | static 필드가 reset 안 됨 | 위와 동일. 가능하면 정적 상태 자체를 줄이기 |
| Domain에 `MonoBehaviour` 컴파일 통과 | `noEngineReferences = false` (자동 UnityEngine 참조) | 위반은 `unity-layer-check`가 grep으로 잡고, 정리 후 `true`로 잠그는 것이 최종 목표 |
| Domain만 수정했는데 다른 어셈블리도 다시 컴파일됨 | `references`가 양방향으로 잘못 묶여 있거나 `autoReferenced = true`로 광역 참조 | asmdef 의존성 그래프 단방향(Features→Application→Domain) 유지, `autoReferenced`를 필요한 곳에서만 |
| Editor 스크립트에서 정적 핸들러를 등록했는데 Edit 모드 진입 시 안 붙음 | `[InitializeOnLoad]`가 빠짐 | `[InitializeOnLoad]` 또는 `[InitializeOnLoadMethod]` 명시. **Domain 어셈블리에는 절대 넣지 말 것** — UnityEditor 의존이 새기 시작함 |
| asmdef에 `precompiledReferences`만 추가했는데 효과 없음 | `overrideReferences = true` 안 켰음 | 둘은 한 쌍 — 한쪽만 두면 무시됨 |

## Quick reference: 우리 결정의 한 줄 요약

- **Domain은 순수 C#**: Domain Reload 후 재컴파일 시간 줄이고, 테스트/재사용 가능. `noEngineReferences=true`로 잠그는 게 이상적이지만 현재 `false` (부채).
- **`InitializeOnLoad` / `RuntimeInitializeOnLoadMethod`는 Features/Bootstrap에만**: Domain에 새지 않게.
- **Enter Play Mode 비활성화는 신중히**: 정적 상태를 명시 reset하지 않으면 미묘한 버그가 누적됨. 도입 전 정적 상태 감사 필요.

## 출처

- Unity Manual — [Enter Play mode with domain reload disabled](https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html)
- Unity Manual — [Configuring how Unity enters Play mode](https://docs.unity3d.com/6000.4/Documentation/Manual/configurable-enter-play-mode.html)
- Unity Manual — [Details of disabling domain and scene reload](https://docs.unity3d.com/6000.1/Documentation/Manual/configurable-enter-play-mode-details.html)
- Unity Manual — [Assembly Definition file format reference](https://docs.unity3d.com/6000.4/Documentation/Manual/assembly-definition-file-format.html)
- Unity Manual — [Referencing assemblies](https://docs.unity3d.com/6000.3/Documentation/Manual/assembly-definitions-referencing.html)
- Unity Learn — [Working with Assembly Definitions](https://learn.unity.com/tutorial/working-with-assembly-definitions)
