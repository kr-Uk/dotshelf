# 결정 프로토콜 (DotshelfRealityKit)

이 디렉터리에서 작업할 때는 **애매한 결정을 절대 단독으로 내리지 마라**. 짐작해서 진행했다가 되돌리는 비용이 묻는 비용보다 항상 크다.

## 1. 질문 우선순위

### 🔴 Critical — 짐작 금지. 무조건 질문 후 진행
- **API 스펙**: public function/property 이름, 인자 시그니처, return 타입, throws 여부, async 여부
- **비즈니스 로직 분기**: 조건 판정 기준, 엣지 케이스 처리 방향 (예: "X일 때 무시할까 throw할까")
- **외부 영향이 있는 작업**: 네트워크 호출 추가/변경, 디스크 쓰기, 빌드 환경/`Info.plist`/엔타이틀먼트 수정, 의존성 추가, 빌드 설정 변경
- **파괴적 operation**: 파일 삭제, 대량 overwrite, `git reset --hard`, `git push --force`, 브랜치 삭제, DerivedData 통째 삭제, 시뮬레이터 erase, 키체인 변경

### 🟡 High — 한 가지 명확한 답이 안 보이면 질문
- 아키텍처 결정 (새 Component를 어디에? 새 System을 만들지 기존 확장할지?)
- 에러 처리 방식 (`throws` vs `Result` vs optional)
- 테스트 작성 여부 / 어디까지 커버할지

### 🟢 Medium — 합리적 default가 있으면 진행하되, 이유 한 줄로 보고
- 네이밍 (관례 따라가면 진행, 충돌하면 질문)
- 파일 위치 (기존 패턴 명백하면 진행)

### ⚪ 자율 OK — 질문 skip
- **오타·명백한 버그 수정**
- **read-only 탐색** (검색·파일 열람·로그 확인)
- 사용자가 직전에 명시적으로 위임한 경우 ("알아서 해", "네가 판단해", "그냥 해")
- 사용자가 이미 답을 준 결정의 기계적 후속 작업

## 2. 질문 형식

상황에 맞게 골라 쓴다.

### 옵션이 2~5개로 깔끔하게 나뉘는 결정 → `AskUserQuestion` 도구
사용자가 클릭만으로 답변 가능. trade-off 짧게 한 줄씩.

### 옵션이 흐릿하거나 trade-off가 복잡한 결정 → 자유 텍스트로 질문
다음 템플릿 따라:

```
**[결정 카테고리: API 스펙 / 비즈니스 로직 / 외부 영향 / ...]**

상황: [무엇을 결정해야 하는지 1~2줄]

옵션:
(a) ... — 장점 / 단점
(b) ... — 장점 / 단점
(c) ... (필요 시 더)

추천: (?) ← 한 줄 이유
질문: [정확히 어떤 답을 원하는지 — yes/no가 아니라 구체적인 선택지나 값]
```

추천안과 이유는 **항상** 같이 제시. "어떻게 할까요?"만 던지지 말 것.

## 3. 언제 질문해도 되는가

- **계획 단계** — 작업 시작 전, plan 잡으면서 애매한 부분 미리 물어봐도 OK
- **작업 중간** — 코드 절반 짜다가 분기점 마주치면 그 자리에서 멈추고 질문해도 OK. "여기까지 했고, 이제 결정 필요"라고 컨텍스트만 같이 줄 것
- **작업 직전** — 막 destructive op 실행 전 한 번 더 컨펌 받기

질문 한 번에 답이 다 나오기 어려운 결정이면, 한 번에 한 가지씩 분리해서 질문.

## 4. Sub-agent도 동일 적용

`Agent` 도구로 하위 에이전트를 spawn 할 때, **prompt에 이 프로토콜을 명시적으로 전달**한다:
- "Critical 카테고리(API 스펙 / 비즈 로직 분기 / 외부 영향 / 파괴적 op)는 짐작 금지"
- 하위 에이전트가 결정에 막히면 보고로 올려보내고, 메인이 사용자에게 질문
- 단, **read-only 리서치/탐색** subagent는 자율 OK (탐색 결과만 정직하게 보고하면 됨)

## 5. 안티 패턴

- ❌ "일단 X로 진행할게요" 후 돌이킬 수 없는 변경
- ❌ "보통 Y로 합니다" — 일반론으로 결정 회피
- ❌ "두 옵션 다 만들어 둘게요" — 결정 미루기. 꼭 필요한 경우 외에는 한 가지로 결정
- ❌ TODO/FIXME 남기고 결정 회피 (정말 후속 작업이 별도 task인 경우 외에는)
- ❌ 사용자가 답하기 어려운 vague한 질문 ("어떻게 할까요?", "괜찮나요?")

---

# 프로젝트 컨텍스트

dotshelf는 visionOS / iOS / macOS 멀티플랫폼 RealityKit 앱. 자세한 빌드·씬·아키텍처 정보는 같은 디렉터리의 `.claude/skills/` 안 스킬들 참조:
- `dotshelf-build-run` — 빌드/실행 가이드, 자주 겪는 빌드 환경 이슈
- `dotshelf-scene-geometry` — 월드 좌표·룸 치수·엔티티 네이밍 컨벤션
- `dotshelf-spatial-design` — 공간/건축/조명 시각 판단
- `dotshelf-object-aesthetics` — 개별 오브젝트 미적 튜닝
- `dotshelf-ecs-pattern` — Component/System 관례
- `dotshelf-performance` — 성능/전력/메모리 튜닝
- `realitykit` — RealityKit 프레임워크 전반
- `swift-concurrency` — async/await/actor/Sendable 등 동시성 전반
- `xcode-build`, `xcode-toolkit` — Xcode CLI

`SUPPORTED_PLATFORMS = "macosx iphoneos iphonesimulator"` (visionOS는 destination으로 별도 지정).
워크스페이스 없음 — `xcodebuild -project DotshelfRealityKit.xcodeproj` 형태.
