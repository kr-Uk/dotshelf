# frontend — Unity 3D Book Room MVP

이 디렉터리는 Unity 기반 3D 책장 룸 MVP다. 루트 `../CLAUDE.md`의 라우팅 표에서 `frontend/**` 경로일 때 **이 파일이 우선**한다. `AGENTS.md`(이 디렉터리)와 본 파일의 내용이 어긋나면 본 파일을 따르고, 어긋남을 발견하면 사용자에게 즉시 알린다.

## 아키텍처 (불변 제약)

레이어 구조는 `Assets/App/` 아래에 다음과 같이 고정한다.

```
Assets/App/
├── Domain/          App.Domain.asmdef        — 순수 C# (UnityEngine 금지)
├── Application/     App.Application.asmdef   — UseCase, Domain만 의존
├── Infrastructure/  App.Infrastructure.asmdef — API 어댑터, Repo 구현
├── Features/        App.Features.asmdef       — MonoBehaviour, View, 입력
├── Core/            App.Core.asmdef           — 공용 유틸 (UnityEngine 허용)
├── UI/                                        — UXML/USS, UIDocument 호스트
├── Bootstrap/                                 — 컴포지션 루트, DI 등록
├── Editor/                                    — 에디터 전용 툴
└── Tests/EditMode/  App.Tests.EditMode.asmdef
```

### 코드가 절대 깨면 안 되는 규칙

1. **Domain은 `UnityEngine`을 참조하지 않는다.** `using UnityEngine;`, `MonoBehaviour`, `ScriptableObject`, `Vector3` 등 어떤 Unity 타입도 들어가면 안 된다.
   - 현재 `App.Domain.asmdef`의 `noEngineReferences`가 `false`로 되어 있어 컴파일 차단이 안 걸려 있다 — 이 값을 `true`로 바꿔야 안전하지만, 영향 범위가 있어 단독으로 수정하지 말고 사용자에게 먼저 확인한다.
2. **Feature 코드는 Application/UseCase 경계를 거쳐서 Infrastructure/API에 닿는다.** `Features/`에서 `Infrastructure/`나 `*Api*`/`*Repository*` 구현체를 직접 참조하지 않는다.
3. **드래그/배치는 슬롯 기반이다.** 물리(Rigidbody/Collider 충돌 응답) 기반 배치를 도입하지 않는다.
4. **Preview 상태는 Domain 데이터를 커밋하지 않는다.** 미리보기(시각적 임시 상태)와 실제 상태 변경은 분리된다 — 커밋 시점에만 Domain/Repository에 반영한다.
5. **Mock API와 Real API는 base URL만 다르다.** 분기 로직(코드 흐름의 차이)이 들어가면 안 된다. 환경 분기는 설정/조립 시점에서 끝낸다.

## 작업 시 출력 순서 (AGENTS.md에서 확정)

비-trivial 작업은 다음 순서로 응답한다. 사용자가 명시적으로 단계를 건너뛰라고 하기 전에는 줄이지 않는다.

1. **Plan** — 어떤 변경을 할지 짧게
2. **Decisions needed** — 모호한 선택지가 있으면 옵션 + 추천 + 이유
3. **File changes** — 사용자 승인 또는 명시적 구현 요청 후
4. **Changed file summary** — 무엇을 어디에 바꿨는지
5. **Verification method** — 어떻게 검증했는지 (Unity 에디터 동작, EditMode 테스트, 빌드, 등)

trivial 한 변경(오타, 한 줄 fix)은 위 순서를 생략하고 바로 진행해도 된다.

## 자율 vs 컨펌

- **자율로 진행** — 코드 읽기, 단일 파일 수정, EditMode 테스트 추가, 새 클래스 생성, USS/UXML 편집, `phase-XX-report.md` 초안 작성.
- **사용자 컨펌 필수** —
  - asmdef 의존성 추가/삭제 (특히 `App.Domain.asmdef`의 `references`, `noEngineReferences` 변경)
  - `Packages/manifest.json` 추가/제거
  - `ProjectSettings/*.asset` (Quality, Player, Graphics, Input, Build) 변경
  - 씬 파일(`Assets/Scenes/*.unity`) 자동 재생성/대량 수정
  - keystore/Android 서명 관련, `SOCIAL_LOGIN_SETUP.md`에 영향 가는 설정
  - 파괴적 git 작업 (`reset --hard`, `push --force`, `clean -fd`)
- **Sub-agent에 위임 시** — 이 파일을 prompt에 첨부하거나 명시적으로 읽도록 지시한다. read-only 탐색 sub-agent는 예외.

## 규약

- C# 네이밍은 .NET 표준(파스칼 케이스 클래스/메서드, 캐멀 케이스 로컬, `_camelCase` 또는 `camelCase` 필드)을 따르되 같은 파일/모듈의 기존 스타일을 우선한다.
- 새 MonoBehaviour는 `Features/` 또는 `UI/`에만. Domain/Application에는 절대 없다.
- UI Toolkit USS는 인라인 스타일(`style="..."`) 대신 클래스 기반으로 작성한다.
- 텍스트/메시 자산은 `Assets/Arts`, 씬은 `Assets/Scenes`, UXML/USS는 `Assets/UI Toolkit` 또는 `Assets/App/UI`에 둔다.

## 작업 검증

- 코드 변경 — `dotnet build` 또는 Unity 에디터에서 컴파일 확인. EditMode 테스트가 있으면 실행.
- 씬/프리팹 — Unity 에디터에서 Play 모드 진입 후 핵심 시나리오(룸 진입 → 책 슬롯 배치 → Preview → 커밋) 한 번 돈다.
- UI Toolkit — `DashboardTestScene` 등 테스트 씬에서 시각 확인. UXML 인스펙터(Window > UI Toolkit > UI Toolkit Debugger)로 계층 검사.
- Unity GUI 동작은 자동 테스트로 잡히지 않으므로, 변경의 시각적 검증을 사용자가 해야 하면 명시적으로 알린다 ("이 변경은 에디터에서 직접 확인 필요").

### Unity Console 로그 읽기

- Console 패널의 로그 list 는 한 줄로 잘려 표시된다. 진단 목적으로 로그를 읽을 때는 **반드시 해당 줄을 클릭해서 하단 detail panel 을 펼쳐** 본다 — 전체 메시지 + stack trace + 호출 site 가 거기에 들어 있다.
- screenshot 만 보고 잘린 list 에서 결론 내리지 말 것. 짧게 잘린 한 줄로 원인 추정하다 시간 낭비하는 경우가 잦다.
- detail panel 의 stack trace 는 클릭 가능 — 해당 라인으로 IDE 가 점프한다. text 는 그대로 복사해서 grep / 분석 가능.
- Console 우상단 카운터 (`X errors / Y warnings / Z logs`) 를 토글 클릭하면 각 type 만 필터된다. error 만 보고 싶을 때 유용.

### Unity Editor 코드 변경 후 재컴파일 확인

- C# 파일 수정 후 Unity 가 자동으로 재컴파일하지만, **재컴파일이 실제로 끝났는지** Play 들어가기 전에 한 번 확인:
  - `Library/ScriptAssemblies/Assembly-CSharp.dll` 의 mtime 이 소스 수정 시각보다 최신인지
  - Console 우하단의 spinning indicator (compiling) 사라졌는지
- 재컴파일이 실제로 일어나지 않았는데 Play 시작 → stale 코드 사용 → 디버깅 시 fix 가 안 먹는 것처럼 보임. 의심되면 `Assets > Refresh` (Cmd+R) 명시적 호출.

## 참고 문서

- `AGENTS.md` — 본 파일과 같은 정책의 더 짧은 요약 (오케스트레이터/외부 에이전트용).
- `docs/phase-00-report.md` ~ `docs/phase-03-report.md` — 페이즈별 진행 기록. 새 페이즈는 동일 포맷으로 추가한다.
- `docs/unity-git-convention.md` — 커밋 메시지 규약 (`[UNITY] Feat:`, `[UNITY] Fix:` 등).
- `docs/progress-audit.md` — 현재까지의 진척 감사.
- `docs/exterior-conventions.md` — Exterior(창밖 환경) 옵션 디자이너 contract. 좌표계/거리 zone(근 5~15m / 중 20~80m / 원 skybox)/금지사항/5단계 신규 옵션 만들기. ExteriorAsset SO + scenePrefab 작업 전 필독. 노션 [Exterior 통합 시스템 (260510)](https://www.notion.so/35c69b2a131a816da21ccdc908f39b6d) 도 참고.
- `SOCIAL_LOGIN_SETUP.md`, `ANDROID_DEBUG_KEYSTORE.md` — 외부 통합 설정.

## 프로젝트 로컬 스킬

이 프로젝트 전용 스킬은 `frontend/.claude/skills/` 아래에 둔다. 현재 등록된 것:

- `unity-layer-check` — `App.Domain` UnityEngine 누수 / 레이어 경계 위반 감지
- `unity-phase-report` — `docs/phase-XX-report.md` 초안 작성, AGENTS.md 출력 순서 강제
- `unity-uitk-edit-guard` — UXML/USS 변경 시 영향 범위 분석, flex 형제 충돌·공통 셀렉터 광역 영향·UXML name과 C# Q() 바인딩 깨짐 사전 가드
- `unity-fundamentals` — Unity 엔진 동작 원리 reference 라우터. `references/` 하위에 6개 주제별 문서 (domain-reload-asmdef, uitk-internals, input-system, physics-determinism, asset-pipeline, build-il2cpp). 코드 결정이 엔진 메커니즘에 의존할 때 발동.
- `unity-perf-budget` — 디바이스 등급(low/mid/high)별 frame budget. 자산·라이트·post-FX 추가 시 예산 차감 자동 보고, URP/Quality 권장 설정표 제공.
- `unity-uitk-polish` — UI Toolkit motion 시스템. duration/easing 토큰, transform/opacity/color 한정 transition, pseudo-class 모바일 차이, 햅틱, 접근성 (reduced motion).
- `unity-data-binding` — UI Toolkit 6.3+ Runtime Data Binding 적용. `[CreateProperty]` + `INotifyBindablePropertyChanged` ViewModel, UXML `<DataBinding>`, two-way 동기화. 수동 `Q().text =` 패턴 대체.
- `unity-ai-handoff` — Unity AI Assistant(인-에디터) vs Claude Code(이 대화) 작업 라우팅. 자산 생성·Profiler 분석·UXML 1차 초안 → Unity AI / 다중 파일 리팩토링·정책 강제·문서·리뷰 → Claude.
- `unity-coreclr-prep` — Mono → CoreCLR 전환(6.7 experimental → 6.8 LTS) 사전 가드. 정적 상태 정리, 새 라이프사이클 attribute, IL2CPP/CoreCLR 공존 모델, Span<T>/모던 C# 패턴 도입.
- `unity-on-device-ai` — Inference Engine(구 Sentis, `com.unity.ai.inference`)으로 ONNX 모델 단말 추론. 책 추천·유사도·텍스트/이미지 분류. 클린 아키텍처 격리 강제.
- `unity-package-security` — 패키지 도입 시 6게이트 (서명 Tier / 라이선스 / 활성도 / IL2CPP 호환 / 빌드 사이즈 / 의존성 그래프). lockfile 리뷰, scoped registry, Tier 4 GitHub 의존 정책.

새 스킬을 만들 때는 `/anthropic-skills:skill-creator`를 사용해 형식 표준을 맞춘다.
