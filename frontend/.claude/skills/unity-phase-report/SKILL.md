---
name: unity-phase-report
description: Use this skill whenever the user is wrapping up a chunk of dotshelf Unity work and needs a phase report under frontend/docs/, or when they say "write a phase report", "update the phase report", "report what we did in phase XX", "P0c report", or any variant. Also apply when the user finishes a feature milestone (e.g., "we just shipped the dashboard P0c") even without an explicit report request — offer the report as a follow-up. The skill produces or updates frontend/docs/phase-XX-report.md in the project's established three-section format (Completed / Current Behavior / Pending), and it forces the AGENTS.md output order (Plan → Decisions → Changes → Summary → Verification) when the user is about to commit or summarize work. Don't invoke for trivial fixes or single-line changes.
---

# unity-phase-report

`frontend/docs/phase-XX-report.md`를 일관된 포맷으로 작성·갱신한다. 또한 사용자가 "정리해줘", "마무리하자", "리포트 써줘" 같은 신호를 보내면 AGENTS.md가 강제하는 출력 순서를 따라 응답을 구성한다.

## 왜 필요한가

- 페이즈 리포트는 phase-00 ~ phase-03이 모두 같은 3단 포맷이다. 새 페이즈가 다른 모양으로 들어오면 검색·비교가 깨지고, 새로 들어온 사람이 진척을 한눈에 못 본다.
- AGENTS.md의 출력 순서(Plan → Decisions → Changes → Summary → Verification)는 이 프로젝트의 협업 규약이다. 사용자가 매번 상기시키지 않아도 자동으로 지켜져야 한다.
- "지난 페이즈에서 뭐 했더라"를 git log만으로 복원하기 어려운 경우가 많다 — 의도, 미루기로 한 것, 수동 검증 절차가 코드에 안 남기 때문. 리포트가 그걸 메운다.

## 언제 발동하나

다음 중 하나라도 해당하면 이 스킬을 적용한다.

- 사용자가 "phase report", "페이즈 리포트", "phase XX 정리", "P0c 정리", "리포트 써줘" 등 명시적으로 요청
- 사용자가 "마무리하자", "이번 작업 정리", "여기까지 한 것 보고해줘" 라고 할 때
- 큰 단위(여러 커밋, 여러 파일, 여러 기능) 작업이 끝나갈 때 — 명시적 요청이 없어도 마지막에 "phase 리포트 갱신할까요?" 라고 제안
- 사용자가 PR을 열려고 할 때 — PR 본문 초안과 phase 리포트를 같이 만들 수 있음

trivial한 단발 변경(한 줄 수정, 오타, 사소한 USS 한 속성)에는 적용하지 않는다.

## 페이즈 번호 결정

```bash
ls frontend/docs/phase-*-report.md | sort
```

새 페이즈를 추가하는지, 기존 페이즈를 갱신하는지 사용자에게 확인한다. 사용자가 "P0c", "P1a" 같은 마이크로 페이즈 명을 쓰면 그대로 따른다 — `phase-0c-report.md`처럼 파일명을 일치시킨다.

## 리포트 포맷 (엄격 준수)

기존 phase-00 ~ phase-03이 모두 따르는 3단 구조. 정확히 이 포맷을 따른다.

```markdown
# Phase {NN} Report - {Title}

## Completed
- {짧은 동사 시작 불릿. 이미 머지/저장/커밋된 것만}
- {새로 만든 파일/스크립트는 백틱 경로로}

## Current Behavior
- {이번 페이즈가 지나간 후 시스템이 실제로 어떻게 행동하는지}
- {플레이 모드/UI에서 무엇이 보이고 무엇이 동작하는지}

## Pending
- {다음 페이즈 또는 이후로 미룬 항목}
- {수동 단계가 남아 있다면 정확한 메뉴/도구 경로까지 명시 (예: "Tools > Project Setup > Build MainScene Content 실행")}
```

규칙:
- 제목은 `# Phase {NN} Report - {Title}` — 하이픈 양옆 한 칸씩, 타이틀은 짧은 명사구
- 모든 섹션은 `##` 헤더, 본문은 불릿(`- `)
- 불릿 안에 코드/경로는 백틱
- 섹션 사이 빈 줄 한 개
- 마지막 줄 개행 1개

## 작성 절차

1. **무엇이 일어났는지 수집** — 다음 중 가능한 것 중심으로:
   - `git log --oneline {이전 페이즈 종료 시점}..HEAD frontend/`
   - 변경된 파일들의 `git diff --stat`
   - 사용자가 알려준 컨텍스트
   - 이번 세션에서 수정한 파일/수행한 수동 단계
2. **3단으로 분류**
   - **Completed**: 이미 코드/씬에 들어간 것. "추가했다", "연결했다", "수정했다" 동사형.
   - **Current Behavior**: 사용자/플레이어 관점에서 지금 무엇이 보이고 동작하는가. "이제 ~할 수 있다", "씬을 열면 ~ 가 표시된다".
   - **Pending**: 의도적으로 미룬 것. 단순히 안 한 게 아니라 "다음에 할 것 / 외부 의존 대기 / 수동 단계 필요". 막연한 TODO는 넣지 않는다.
3. **검증** — 작성 후 다른 phase 리포트 한 개를 옆에 두고 톤·길이·구체성 수준이 맞는지 본다. phase-00~03이 평균 5~10불릿이다. 30불릿짜리는 정보 과잉.
4. **커밋 컨벤션** — `docs/unity-git-convention.md`를 따라 `[UNITY] Docs: phase XX 리포트 추가/갱신` 형태. 사용자가 명시적으로 커밋 요청하기 전엔 커밋하지 않는다.

## AGENTS.md 출력 순서 강제 (응답 구조)

사용자가 큰 변경을 끝낸 직후나 페이즈 리포트를 갱신하는 자리에서, **응답 자체를** 다음 순서로 구성한다. 이건 마크다운 헤더로 명시적으로 표시한다 — 사용자가 어느 단계인지 한눈에 보여야 함.

```markdown
## Plan
{무엇을 할지 짧게 — 이미 한 일이라면 "한 일" 으로 다시 쓴다}

## Decisions needed
{모호한 선택이 있으면 옵션 + 추천 + 이유. 없으면 "없음 — 진행" 한 줄}

## File changes
{경로별로 무엇을 만들었/바꿨는지. 본 스킬에서는 phase report 파일 자체와, 같이 변경된 코드/자산}

## Changed file summary
- `frontend/docs/phase-0c-report.md` — 신규
- `frontend/Assets/App/...` — 변경 요약
{각 항목은 한 줄}

## Verification
- {이 변경을 어떻게 검증했는지. Unity 에디터 동작? EditMode 테스트? 빌드?}
- {수동 검증이 필요한데 못 한 것이 있으면 명시}
```

trivial한 단순 갱신(오타 한 줄)이라면 이 구조를 생략하고 한 줄로 끝낸다. 판단 기준: 사용자에게 "검증 어떻게 했는지" 알려줄 가치가 있는 변경이면 위 구조를 쓴다.

## 갱신 vs 신규 작성

- **신규**: 마지막 phase가 03이고 새로 04를 시작했다 → 새 파일 작성
- **갱신**: 진행 중인 phase의 Completed/Pending이 변동 → 기존 파일 Edit. Pending에서 Completed로 옮기고, 새 Pending 추가.

갱신 시 기존 항목 표현을 함부로 다듬지 않는다 — 사용자가 그 표현을 그대로 다른 곳에 인용했을 수 있다. 변동된 부분만 손댄다.

## 흔한 함정

- **추측 금지**: git log/diff/사용자 설명에 없는 동작을 "이렇게 됐을 것"이라고 쓰지 않는다. 모르면 사용자에게 묻거나 빈 칸으로 둔다.
- **목적과 행동 혼동 금지**: Completed에 "유저가 책을 빠르게 찾을 수 있다"는 들어가지 않는다. 그건 Current Behavior. Completed는 "검색 인덱스를 추가했다".
- **씬/프리팹 변경은 명시**: 자동 재생성 가능한 씬 콘텐츠라면 재생성 도구 경로(`Tools > ... > Build MainScene Content` 등)를 Pending에 남긴다 — phase-01 리포트가 좋은 예.
- **외부 의존 대기**: "Cinemachine 패키지 해결 후" 같은 외부 조건은 Pending에 그 조건을 명시한다.

## 자료 위치

- 기존 페이즈 리포트: `frontend/docs/phase-{00..03}-report.md`
- 진척 감사: `frontend/docs/progress-audit.md`
- 커밋 컨벤션: `frontend/docs/unity-git-convention.md`
- 와이어프레임: `frontend/docs/wireframes/`
