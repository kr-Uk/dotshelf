---
name: unity-ai-handoff
description: Use this skill whenever the user is deciding which AI tool should do a Unity-related task — Unity AI Assistant (in-Editor) vs Claude Code (this conversation) vs hybrid. Trigger when the user says "should I ask Unity AI for this", "Unity AI랑 너랑 뭐가 달라", "이건 Unity AI한테 시킬까", or when a request matches one tool's strengths better than the other (asset generation, Profiler analysis, in-Editor scene tweaks → Unity AI; multi-file refactor, project policy enforcement, code review, doc/report writing, layer/architecture work, the dotshelf skills → Claude). Also apply proactively when the user starts a task that Unity AI is clearly better suited for, before doing the work — explicitly route to Unity AI rather than racing it. The skill keeps the two assistants from stepping on each other and exploits the strengths of each.
---

# unity-ai-handoff

dotshelf 작업에서 **Unity AI Assistant** (Unity 6.2+ 인-에디터, 6.4+ agentic) 와 **Claude Code** (이 대화) 가 공존하는 환경에서 *어느 쪽이 어떤 일을 할지* 정리하는 메타 스킬. 두 도구가 같은 일을 두 번 하거나, 더 잘 할 수 있는 도구를 두고 다른 쪽에 시키는 비효율을 막는다.

## 왜 필요한가

- Unity 6.2부터 **Unity AI Assistant**가 에디터 내장. 6.4+에서 *agentic* 능력 강화 — 프로젝트 인덱싱, 자산 자동 생성(Generators), Profiler 데이터 해석, UXML 레이아웃 보조, **diff-verified** 다중 파일 수정.
- 두 도구는 정보 접근 모델이 다르다:
  - **Unity AI**: 에디터 안에서 살고, 씬 상태·인스펙터 값·Profiler 캡처·Asset Database에 직접 접근. 단 매뉴얼 인용 — *"Unity AI uses models provided by Unity"* (사용자가 모델 선택 불가, Unity Cloud 연결 필수, 토큰 기반).
  - **Claude Code**: 파일 시스템·터미널·git·웹 검색·이 프로젝트 정책(`CLAUDE.md`, 우리 스킬들)에 접근. 모델 선택·MCP 서버 통합 자유. Unity 에디터 내부 상태에는 직접 못 들어감.
- **잘못된 라우팅 비용**:
  - 자산 생성을 Claude에게 시키면: 텍스트 설명만 주고 끝, 실제 png/sprite 못 만듦
  - 프로젝트 정책 강제(레이어 위반 검출, phase 리포트)를 Unity AI에게 시키면: 그 컨텍스트 모름, 일반론적 답
  - 같은 일을 둘 다에 시키면: 일관성 없는 두 산출물 + 토큰·시간 낭비

## 언제 발동하나

- 사용자가 명시적으로 묻기 — "Unity AI랑 너랑 뭐가 달라", "이거 누가 해야 돼"
- 새 작업이 들어왔는데 *명백히 한쪽 영역* 일 때 — 자산 생성 / Profiler 분석 / UXML 1차 초안 → Unity AI 권고. 다중 파일 리팩토링 / 정책 강제 / 리포트 → Claude (그대로 진행)
- 사용자가 한쪽 도구의 결과를 들고 와서 다른 쪽에 검증/통합 요청할 때
- Unity AI가 만든 코드/자산을 dotshelf의 정책(`unity-layer-check`, `unity-perf-budget`, `unity-uitk-polish`)에 맞춰 정리해야 할 때

## 라우팅 표

작업별로 어느 쪽이 우위인가:

### → Unity AI Assistant 가 더 잘 함

| 작업 | 이유 |
|---|---|
| **자산 생성** (sprite, texture, sound, animation, material via Generators) | Unity AI 전용 기능. Claude는 텍스트만 가능 |
| **Profiler 데이터 해석** | Unity AI가 Profile 캡처에 직접 접근 → "이 spike는 GC.Collect 때문" 같은 답을 캡처 보고 즉답 |
| **씬 안에서 인스펙터 값 조정 / GameObject 배치** | 에디터 컨텍스트가 없으면 Claude는 코드만 만질 수 있음. Unity AI는 직접 GameObject를 옮기고 인스펙터 값을 set |
| **에디터 작업 자동화** (메뉴 항목 클릭, 자산 import 옵션 변경) | Unity AI 직접 실행 가능 |
| **UXML 1차 초안** ("팔로워 카드 레이아웃 만들어줘" 류) | Unity AI는 UI Builder와 묶여 있어 시각 검증 즉시. Claude는 텍스트만 |
| **한 줄 코드 자동완성·짧은 보일러플레이트** | 짧은 요청은 인-에디터 즉답이 빠름 |

### → Claude Code (우리)가 더 잘 함

| 작업 | 이유 |
|---|---|
| **다중 파일 리팩토링** (예: 모든 ScreenController를 data binding으로) | 광역 grep + 일관된 변경 + 검증 사이클 |
| **프로젝트 정책 강제** | `CLAUDE.md`, `AGENTS.md`, dotshelf 스킬 12개 — Unity AI는 이 컨텍스트를 모름 |
| **레이어/아키텍처 검증** (`unity-layer-check`) | Domain UnityEngine 누수, UseCase 우회 같은 정책. 이 프로젝트만의 룰 |
| **phase 리포트 / PR 본문 / 문서 작성** | 긴 컨텍스트, 톤 일치, 외부 출처 인용 |
| **외부 자료 조사** (Unity 매뉴얼, Unite 세션 검색·인용) | Web 검색 + fetch 워크플로 |
| **Git 워크플로** (커밋 메시지, PR, 브랜치 전략) | 터미널/CLI 직접 접근 |
| **리뷰** ("이 코드 괜찮아?") | 다중 파일 비교, 정책 적합성, 회귀 가능성 |
| **외부 백엔드/API 통합** | 우리 백엔드 코드와 함께 보고 결정 |
| **CoreCLR / IL2CPP 빌드 호환성** 같은 분야별 **깊이 있는 reference** | `unity-fundamentals/references/` 의 정확한 정보 + 출처 인용 |

### 🤝 둘이 같이 하면 좋은 작업

| 작업 | 협업 흐름 |
|---|---|
| **새 화면 만들기** | (1) Unity AI: UXML/USS 1차 초안 + 더미 자산. (2) Claude: ViewModel/Controller 작성, layer-check, polish 토큰 적용, phase report |
| **자산 통합** | (1) Unity AI: Generators로 sprite/texture 생성. (2) Claude: AssetPostprocessor 작성으로 모바일 ASTC 강제, perf-budget 차감 보고 |
| **성능 최적화** | (1) Unity AI: Profiler 캡처 분석, GC alloc 위치 식별. (2) Claude: 소스 코드 패턴 수정, 회귀 테스트 |
| **UI 정렬·상호작용** | (1) Unity AI: 시각적 위치 조정 / 인스펙터에서 즉시 확인. (2) Claude: USS 토큰 일관성, motion 시스템(`unity-uitk-polish`), 셀렉터 광역 영향 가드 |

## Unity AI 가 *못 하거나 약한* 것 (Claude로 보완)

검색 결과 인용:
> *"Unity AI uses models provided by Unity, which means users cannot choose the optimal model for their needs, and it requires dedicated tokens (points), making it mandatory to connect Unity projects to Unity Cloud."*

- **모델 선택 불가** — 특정 작업에 약한 모델로 묶일 수 있음
- **토큰 기반 사용량 한도** — 무제한 아님 (베타는 free·unlimited지만 정식 도입 시 한도)
- **Unity Cloud 연결 강제** — 오프라인/사내 격리 환경 비호환
- **프로젝트 외부 컨텍스트 제한** — git history, 백엔드 코드, 디자인 문서 같은 외부 컨텍스트 약함
- **MCP/외부 도구 통합 약함** — Claude Code는 GitHub MCP, Linear MCP, 우리 백엔드 MCP 등을 직접 호출 가능

→ 이런 작업은 Unity AI에게 묻기보다 Claude에게 와야 함.

## 협업 패턴 (실용 시나리오)

### 시나리오 1: "대시보드 P0d 추가해줘"

```
1. [사용자 → Claude] "대시보드에 책 검색 섹션 추가하자"
2. [Claude] phase 리포트 형식 응답:
   - Plan: UXML 추가 위치 / ViewModel 필드 / UseCase
   - Decisions: 검색 입력은 즉시 vs 디바운스? 결과 페이지네이션? → 사용자 컨펌
3. [Claude] 사용자 승인 후:
   - "UXML 1차 초안은 Unity AI에 시키는 게 더 빠릅니다. 이 프롬프트 복붙하세요:
      'DashboardView에 검색 입력 필드 + 결과 그리드 섹션 추가. 클래스 .search-section. 입력은 TextField, 결과는 ScrollView 안에 책 카드 리스트.'"
4. [사용자 ← Unity AI] UXML 초안 받음
5. [사용자 → Claude] 초안 보여주고 통합 요청
6. [Claude] 
   - layer-check: ViewModel/UseCase 작성, Application 레이어 위치
   - uitk-polish: --motion-base 적용, 인라인 스타일 제거
   - uitk-edit-guard: 셀렉터 영향 범위 검증
   - data-binding: SearchViewModel + [CreateProperty] + UXML <DataBinding>
   - perf-budget: 검색 결과 100개 가상 스크롤 검토
   - phase-report 갱신
```

### 시나리오 2: "프레임 끊기는데 왜 그런지 모르겠어"

```
1. [사용자 → Unity AI] "Profiler 캡처 보고 끊김 원인 찾아줘" 
   (에디터에서 Profiler 캡처를 Unity AI에 직접 첨부)
2. [Unity AI] "DashboardScreenController.OnEnable의 Q().Children() 호출이 매번 트리 traversal — GC 1.2KB"
3. [사용자 → Claude] "이거 어떻게 고쳐?"
4. [Claude] 
   - uitk-internals reference 인용해서 원리 설명 (Q()는 매 호출 traversal)
   - 캐싱 패턴 코드 제안
   - 회귀 방지로 perf-budget 보고에 GC alloc / frame = 0 검증 항목 추가
```

### 시나리오 3: "책 표지 placeholder 50개 만들어줘"

```
1. [사용자 → Unity AI] Generators (Texture/Sprite)에 프롬프트로 50개 생성
2. [Unity AI] Assets/Arts/BookCovers/Generated/ 에 sprite 50개 저장
3. [사용자 → Claude] "이거 import 설정 통일해줘"
4. [Claude] 
   - asset-pipeline reference 적용: AssetPostprocessor 스크립트 작성
   - 모바일 ASTC 6x6, max 512, GPU Instancing 호환 머티리얼
   - perf-budget 차감 보고: 텍스처 메모리 +X MB, 50 sprite로 atlas 묶을지 검토
```

## 안티패턴 (피하기)

- ❌ **Claude에게 sprite/texture 생성 요청** — 텍스트로 묘사만, 실제 산출물 없음. Unity AI Generators로 가야 함
- ❌ **Unity AI에게 phase-XX-report.md 작성** — 우리 포맷·톤·`unity-phase-report` 스킬 미준수
- ❌ **Unity AI에게 `App.Domain` 레이어 정합성 검증** — 우리 정책 컨텍스트 없음
- ❌ **Claude에게 Profiler 캡처 분석** — 캡처 파일 직접 못 읽음 (요약 텍스트로 받아 해석은 가능, 1차 분석은 Unity AI)
- ❌ **두 도구에 같은 작업 중복 요청** — 결과 일관성 깨짐. 한쪽에 위임하고 다른 쪽이 검증하는 모델로

## 출력 형식

라우팅 결정이 필요한 순간 응답에 한 블록:

```
## AI handoff
- 이 작업: 책 표지 placeholder 50개 생성
- 권장: Unity AI Generators (Texture/Sprite) — Claude는 자산 생성 불가
- 후속 (Claude 영역): 생성된 자산의 import 설정 통일 (AssetPostprocessor), perf-budget 차감 보고
- 사용자 액션: 아래 프롬프트로 Unity AI에 요청 후 결과 알려주세요:
  > "책 표지 placeholder 50개 생성. 정사각형, 단색 배경+책 제목 글자, 다양한 색상."
```

상황이 명확히 Claude 단독이면 라우팅 블록 생략하고 그냥 진행. 명확히 Unity AI 단독이면 작업 시작 전 라우팅 블록만 내고 멈춤.

## 변화 추적

Unity AI는 빠르게 발전 중 — 이 스킬의 라우팅 표는 **6~12개월마다 업데이트** 권장. 새 Generator 타입 추가, agentic 능력 확장, MCP 호환성 도입 등이 일어나면 그쪽이 더 잘 하게 되는 영역이 늘어난다. 라우팅 표가 사실과 안 맞을 때 사용자가 "Unity AI도 X 됨" 이라고 알려주면 즉시 갱신.

## 출처

- Unity Manual — [Unity AI](https://docs.unity3d.com/6000.3/Documentation/Manual/unity-ai.html)
- Unity Discussions — [Unity AI Beta 2026 is here!](https://discussions.unity.com/t/unity-ai-beta-2026-is-here/1703625)
- Unity — [Unity AI: AI Game Development Tools](https://unity.com/features/ai)
- Unity — [Unity AI Guiding Principles](https://unity.com/legal/unityai-guiding-principles)
- Unity Support — [Getting started with Unity AI: private beta user guide](https://support.unity.com/hc/en-us/articles/48060149523476-Getting-started-with-Unity-AI-private-beta-user-guide)
- GitHub — [UnityAgentClient (Gemini/Claude/Codex via ACP)](https://github.com/nuskey8/UnityAgentClient)
- GitHub — [Unity MCP (CoplayDev)](https://github.com/CoplayDev/unity-mcp)
- Unity Connect — [What's New With Agentic AI in 2026](https://unity-connect.com/our-resources/blog/agentic-ai-updates/)
