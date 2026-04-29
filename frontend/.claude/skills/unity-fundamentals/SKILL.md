---
name: unity-fundamentals
description: Use this skill whenever a question, code change, or design decision in the dotshelf Unity project depends on how Unity itself actually works under the hood — engine lifecycle, Domain Reload, asmdef compilation, UI Toolkit's layout engine and panel model, the Input System action map runtime, PhysX determinism, GC and memory layout, URP rendering, IL2CPP and Android stripping, asset import and Addressables. Trigger this skill when the user asks "why is this slow / leaking / not working as expected", "should we use X or Y" (e.g., coroutine vs async, uGUI vs UI Toolkit, Resources vs Addressables), "is this safe to call here", or when reviewing code where the correct answer depends on engine internals, not just C# semantics. Also trigger proactively when a design decision will compound (e.g., choosing how state flows between Domain and MonoBehaviour, how UI Toolkit panels are mounted, whether physics is involved in placement). Don't dump all knowledge into the response — route to the relevant references/ file and read only what's needed.
---

# unity-fundamentals

Unity 엔진의 **동작 원리**를 dotshelf 프로젝트의 결정과 묶어서 설명하는 reference 라우터. SKILL.md에는 깊이 있는 지식을 넣지 않는다 — 라우팅과 인덱스만. 깊이는 `references/` 안에 주제별로 분리되어 있고, 필요한 것 하나만 읽으면 된다.

## 사용 원칙

- **상황에 맞는 한 개의 reference만 읽는다.** 한꺼번에 다 로드하면 컨텍스트만 잡아먹는다.
- **이 프로젝트와 결합된 답을 한다.** "Unity는 X로 동작한다" 로 끝내지 말고, "그래서 우리 `App.Domain.asmdef`를 분리한 결정이 Y라는 이득을 만든다 / 만약 Z를 도입하면 그 이득이 깨진다" 까지 묶는다.
- **Claude가 이미 아는 일반 사실은 references에 굳이 옮기지 않는다.** references는 ① 자주 틀리는 부분, ② Unity 버전·플랫폼별 함정, ③ 우리 프로젝트 결정과의 직접 연결, 이 셋에 집중한다.

## 라우팅 표

질문/상황별로 어떤 reference를 읽을지:

| 상황 / 질문 신호 | 읽을 파일 |
|---|---|
| asmdef 분리 효과·Domain Reload 시간·`[InitializeOnLoad]`·정적 상태 / "왜 Domain에 UnityEngine 안 들어가는가" 의 진짜 이유 | `references/domain-reload-asmdef.md` |
| UI Toolkit 레이아웃이 의도와 다르게 풀림 / `Q()` 가 null / panel 어디서 mount되는가 / USS cascade 이상 / IMGUI vs UITK 선택 | `references/uitk-internals.md` |
| 입력 응답이 한 프레임 늦음 / 모바일 터치와 마우스 동작 차이 / `InputSystem_Actions.inputactions` 수정 / `BookInteractionManager.PointerDown` 의 마이그레이션 | `references/input-system.md` |
| 슬롯 vs 물리 결정의 근거 / `FixedUpdate` 와 `Update` 의 진짜 차이 / 같은 입력으로 다른 결과가 나옴 / 책 떨어짐·튕김 회귀 | `references/physics-determinism.md` |
| `Resources.Load` vs Addressables / 빌드 사이즈 폭증 / Importer override / Meshy glb 자동 후처리 / 모바일 ASTC 압축 | `references/asset-pipeline.md` |
| IL2CPP build 실패 / Android 리플렉션 안 됨 / `link.xml` / managed code stripping / `[Preserve]` / Mono vs IL2CPP 결정 | `references/build-il2cpp.md` |
| (예정) GC alloc 스파이크 / `string` 합성 함정 / `List` 재할당 / `NativeArray`·`ArrayPool` | `references/memory-and-gc.md` *(예정)* |
| (예정) draw call 폭증 / SRP Batcher 깨짐 / Shader Graph 사용 결정 / 모바일 셰이더 LOD — 실용 가이드는 `unity-perf-budget` 스킬에 우선 흡수됨 | `references/rendering-urp.md` *(예정)* |

각 reference는 **상단에 1줄 요약 + 어떤 결정이 이 글로 풀리는지 인덱스**가 있다. 거기서 답이 충분하면 본문을 다 읽지 않아도 된다.

## 응답 패턴

이 스킬이 발동하면 응답은 다음 골조를 따른다.

1. **메커니즘** — Unity가 실제로 그 부분을 어떻게 처리하는지 한 단락. 추측은 금지, 모르면 reference를 읽고 답한다.
2. **이 프로젝트와의 연결** — 메커니즘이 우리의 어떤 결정을 정당화하거나, 어떤 변경을 위험하게 만드는지.
3. **권장 행동 / 함정** — 코드/설계 차원에서 무엇을 하고 무엇을 피해야 하는지.
4. **출처** — reference 파일명과, 그 안의 출처 링크.

예시 질문: "유저가 책 들고 있는 동안 Update에 매 프레임 위치 갱신해도 돼?"

```
[메커니즘] Update는 가변 dt(보통 60Hz, 모바일 30/60 변동), FixedUpdate는 고정 dt
(기본 0.02s = 50Hz). Transform 직접 조작은 Update에서 OK — Physics 응답 없음.

[프로젝트 연결] 책 배치는 슬롯 기반(=물리 응답 없음)이라 Update에서 Transform 갱신
이 정확. 만약 Rigidbody가 끼어 있다면 FixedUpdate 또는 Rigidbody.MovePosition으로
가야 보간이 깨지지 않음 — 우리는 그 함정을 피했음 (frontend/CLAUDE.md 규칙 3).

[권장] Preview 위치 갱신은 Update에서. 단, 매 프레임 GC alloc이 생기지 않도록
Vector3 합성 시 임시 객체 생성 금지.

[출처] references/physics-determinism.md
```

## 새 reference 추가하기

새 토픽이 반복적으로 나오면 `references/<topic>.md` 추가하고 위 라우팅 표에 한 줄 등록한다. 한 reference는 보통 150~300줄, 다음 구조를 따른다:

```markdown
# <topic>

> 한 줄 요약: 이 글이 답해주는 핵심 질문.

## 어떤 결정이 이 글로 풀리는가
- "X해도 되나?" → §메커니즘
- "왜 우리가 Y를 했나?" → §프로젝트 연결
- "Z 함정 피하기" → §함정 카탈로그

## 메커니즘
{Unity가 실제로 어떻게 동작하는지. 버전 명시: Unity 6 LTS / 2022.3 LTS 등.}

## 이 프로젝트와의 연결
{우리 결정·파일 경로·asmdef·씬 이름과 직접 연결.}

## 함정 카탈로그
| 증상 | 원인 | 대처 |
|---|---|---|

## 출처
- Unity Manual: <url>
- Blog/Unite session: <title> (<year>)
- 무료 e-book: "<title>"
```

## 이미 작성된 reference 6개

다른 스킬·CLAUDE.md 규칙과 직접 묶여 있는 순서:

1. **`references/domain-reload-asmdef.md`** — Domain Reload 메커니즘. 왜 `App.Domain.asmdef` 분리가 빌드 시간을 줄이고, `noEngineReferences` 가 진짜 의미하는 것. `unity-layer-check` 스킬의 이론적 근거.
2. **`references/uitk-internals.md`** — UI Toolkit panel·yoga 레이아웃·USS cascade·`Q()` 작동. `unity-uitk-edit-guard`·`unity-uitk-polish` 의 이론적 근거.
3. **`references/input-system.md`** — InputSystem ActionMap, `*.inputactions` 자산, 폴링 vs 콜백, 모바일 터치. phase-03 의 `BookInteractionManager.PointerDown` 마이그레이션 가이드.
4. **`references/physics-determinism.md`** — PhysX·FixedUpdate·결정성·왜 슬롯 기반인가. CLAUDE.md 규칙 3의 근거.
5. **`references/asset-pipeline.md`** — Resources / Addressables / AssetBundle, 모바일 ASTC, AssetPostprocessor, Meshy glb 후처리. `unity-perf-budget` 의 자산 임포트 정책 근거.
6. **`references/build-il2cpp.md`** — IL2CPP vs Mono, AOT 함정, link.xml / `[Preserve]`, Android 64-bit 빌드 요건. 릴리스 빌드 검증의 근거.
