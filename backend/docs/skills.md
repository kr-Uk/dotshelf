# DotShelf 백엔드 AI 스킬 가이드

이 프로젝트에는 **Claude Code**와 **Codex** 같은 AI 에이전트가 일관된 방식으로 일하도록 하는 스킬 5종이 포함돼 있다. 모든 팀원이 같은 결과물을 얻을 수 있게 설계됐다.

## TL;DR

| 스킬 | 단계 | 한 줄 요약 |
|------|------|-----------|
| `dotshelf-code-match` | 🔨 생성 | 새 코드를 팀 스타일대로 |
| `dotshelf-simplify` | 🧹 정리 | 방금 쓴 코드를 팀 규칙 기준 청소 |
| `dotshelf-verify` | ✅ 검증 | 로컬 빌드/테스트/부팅 점검 |
| `dotshelf-commit` | 📦 커밋 | 컨벤션 따른 커밋·브랜치·push·MR |
| `dotshelf-study` | 📚 학습 | push할 변경분으로 학습 문서 작성 |

자연스러운 작업 흐름:

```
code-match (생성) → simplify (정리) → verify (검증) → commit (올리기) → study (정리)
```

---

## 어디에 있나

```
backend/
├── .claude/skills/                 ← Claude Code가 자동 인식
│   ├── dotshelf-code-match/SKILL.md
│   ├── dotshelf-simplify/SKILL.md
│   ├── dotshelf-verify/SKILL.md
│   ├── dotshelf-commit/SKILL.md
│   └── dotshelf-study/SKILL.md
│
├── AGENTS.md                       ← Codex 진입점, SKILL.md를 라우팅
└── docs/architecture/
    ├── patterns.md                 ← 모든 스킬이 참조하는 단일 진실
    ├── checklist.md                ← 셀프 리뷰
    └── exemplars/                  ← 모범 코드
```

Claude Code는 위 구조만 있으면 바로 쓴다.

Codex는 **두 가지 방식**으로 쓸 수 있다:
- 이 저장소 안에서만 쓸 때: `backend/AGENTS.md` 라우터를 통해 `.claude/skills/*/SKILL.md`를 읽어 사용
- Codex 전역 스킬로 자동 발견시키고 싶을 때: `~/.codex/skills` 아래에 링크/복사 설치

---

## 사용법

### Claude Code

키워드만 말하면 자동 트리거된다. 예:

```
"새 도메인 만들어줘"     → dotshelf-code-match 발동
"정리해줘"               → dotshelf-simplify
"빌드 확인해줘"           → dotshelf-verify
"커밋하고 올려줘"         → dotshelf-commit (내부에서 verify, study 권고)
"이번 작업 학습 문서 만들어줘" → dotshelf-study
```

명시적으로 부르려면 `/` 슬래시 + 스킬 이름.

### Codex

이 저장소 안에서는 `AGENTS.md`의 **스킬 라우터** 표를 보고 Codex가 해당 SKILL.md를 직접 읽는다. 키워드는 Claude와 동일하게 작동한다.

키워드가 모호하면 명시적으로 알려주는 게 안전:
```
"backend/.claude/skills/dotshelf-commit/SKILL.md 따라서 커밋해줘"
```

### Codex 전역 스킬로 설치

Codex의 스킬 목록에도 이 5개를 띄우고 싶다면, 현재 `.claude/skills` 폴더를 `~/.codex/skills`로 연결하면 된다. 이 프로젝트에는 이를 위한 스크립트를 추가했다:

```powershell
cd backend
.\scripts\install-codex-skills.ps1
```

기본 동작은 **Junction(link)** 생성이다. 따라서 `.claude/skills` 아래 파일을 수정하면 Codex 쪽에도 즉시 반영된다.

기존 같은 이름의 스킬이 이미 있으면 덮어쓰지 않는다. 다시 연결하려면:

```powershell
cd backend
.\scripts\install-codex-skills.ps1 -Force
```

복사본으로 설치하고 싶다면:

```powershell
cd backend
.\scripts\install-codex-skills.ps1 -Mode Copy -Force
```

설치 후에는 Codex를 재시작하거나 새 스레드를 열어 스킬 목록을 새로고침한다.

권장 방식은 **Junction 설치 + `AGENTS.md` 라우터 유지**다.
- 전역 설치: Codex 스킬 목록/자동 발견 강화
- AGENTS.md: 이 저장소 안에서 어떤 상황에 어떤 스킬을 읽을지 명확히 보장

---

## 각 스킬 상세

### 1. dotshelf-code-match — 새 코드 작성용

**언제**: 엔티티/서비스/컨트롤러/CRUD 같은 **새 코드 생성**

**작업 흐름**:
1. `docs/architecture/patterns.md` 읽기
2. `docs/architecture/exemplars/` 모범 코드 확인
3. 가장 가까운 기존 도메인 확인
4. 거울처럼 구조 복제하며 작성
5. `checklist.md`로 셀프 리뷰

**자동 차단**:
- Controller에서 Repository 직접 호출
- 엔티티 setter 추가
- `throw new RuntimeException(...)` (→ `CustomException`)
- Jackson 2 API 참조
- 엔티티를 Controller에서 직접 반환

**예**:
> "user 엔티티 만들어줘" → patterns.md의 엔티티 규칙 + User.java exemplar 참조 → BaseEntity 상속, @Builder, setter 없는 코드 생성

---

### 2. dotshelf-simplify — 방금 쓴 코드 정리

**언제**: 코드 작성 직후, 또는 "정리해줘"/"리팩토링 해줘" 요청 시

**원칙**:
- **기능은 절대 건드리지 않음**. 구현 방식만 다듬음
- patterns.md 위반 자동 감지
- 과단순화 지양

**자동 적용**: 미사용 import, 자명 주석 제거
**확인 후 적용**: 메서드 추출, 변수명 개선, 예외 타입 변경
**제안만**: 성능 관련, 큰 구조 변경

**예 출력**:
```
🟢 자동 적용 (2건)
- UserService.java: 미사용 import 1개 제거

🟡 제안 (확인 필요)
1. UserService.signup: 중첩 if 3단 → early return
2. BookshelfService.findAll: 엔티티 직접 반환 → DTO 변환 (patterns.md 위반)
```

---

### 3. dotshelf-verify — 로컬 검증

**언제**: push 전, "테스트 돌려봐", "smoke test" 요청 시

**실제 실행**:
1. 스택 자동 감지 (Gradle/Maven/npm/pip)
2. Docker compose 의존성 서비스 확인
3. 빌드
4. 테스트
5. 앱 부팅 + 헬스체크 (`/api/health` 200 확인)
6. 백그라운드 프로세스 정리

**리포트**:
```
✅ 빌드: 23s
✅ 테스트: 47 passed
✅ 부팅 smoke: /api/health → 200

→ push 안전합니다.
```

실패 시 어느 테스트/파일/줄에서 깨졌는지 정확히 추출.

---

### 4. dotshelf-commit — 커밋·push·MR

**언제**: "커밋해줘", "푸시해줘", "올려줘", "MR 만들어줘"

**컨벤션 강제**:
- 브랜치: `{type}/be/{kebab-topic}` (예: `feat/be/user-signup`)
- 커밋 메시지: `[BE] Type: TICKET-ID 제목` + 줄바꿈 불릿 본문
- `dev`/`master` 직접 commit/push 차단

**자동 엣지 케이스**:
- `.env*` 변경 → GitLab Variables 업데이트 알림 (`ENV_<TARGET>_<KEY>`)
- 기존 Flyway `V*.sql` 수정 → **차단**, 새 `V{n+1}__...sql` 안내
- 민감 파일/하드코딩 시크릿 감지

**자동 권고 체인**: push 직전에 `dotshelf-verify`, 그 후 `dotshelf-study` 호출 제안

**MR 생성**:
- `glab` CLI 있으면 자동
- 없으면 push 응답 URL + 제목/본문 출력

---

### 5. dotshelf-study — 학습 문서 자동 생성

**언제**: "학습 문서 만들어줘", "이번 작업 정리"

**범위**: `origin/dev..HEAD` (해당 브랜치의 신규 변경분)

**저장 위치**: `docs/study/{YYYY-MM-DD}-{브랜치명}.md`

**섹션**:
- TL;DR (3~5줄)
- 무엇을 했나
- 왜 했나
- 어떻게 구현했나 (핵심 코드 인용)
- 배운 것
- 더 공부할 것 (필수 2~5개)

학습할 게 적은 사소한 변경(설정 한두 줄)에는 "스킵하시겠어요?"를 먼저 묻는다.

---

## patterns.md / checklist.md / exemplars/

스킬들이 공통으로 참조하는 **단일 진실 소스**:

| 파일 | 역할 |
|------|------|
| `docs/architecture/patterns.md` | 모든 아키텍처/네이밍/예외 규칙. 새 패턴 정해질 때마다 업데이트 |
| `docs/architecture/checklist.md` | 코드 작성 후 항목별 셀프 리뷰 |
| `docs/architecture/exemplars/*` | "이대로 쓰면 된다" 모범 코드. 도메인 슬라이스 완성될 때마다 추가 |

**규칙이 바뀌면 여기 한 곳만 수정한다.** SKILL.md를 안 건드려도 모든 스킬이 자동으로 새 규칙을 따른다.

---

## 새 스킬 추가/수정 가이드

### 수정
- `.claude/skills/dotshelf-*/SKILL.md` 직접 수정 → 다음 세션부터 자동 반영

### 추가
1. `.claude/skills/<스킬이름>/SKILL.md` 생성
2. YAML frontmatter에 `name`, `description` 필수
3. description은 트리거 키워드를 적극적으로 명시 (Claude는 description 보고 자동 발동 결정)
4. Codex에서도 이 저장소 안에서 쓰려면 `AGENTS.md`의 라우팅 표에 항목 추가
5. Codex 전역 스킬 목록에도 띄우려면 `.\scripts\install-codex-skills.ps1 -Force` 다시 실행

### 패턴 변경
- `docs/architecture/patterns.md` 수정
- PR 리뷰로 팀 합의
- 머지 시 모든 스킬에 자동 반영 (한 곳만 수정해도 됨)

---

## FAQ

**Q. 스킬이 자동으로 안 켜져요**
A. 키워드가 description과 일치하는지 확인. 모호하면 명시적으로 호출:
- Claude: `/dotshelf-commit` 같이 슬래시
- Codex: "`.claude/skills/dotshelf-commit/SKILL.md` 보고 따라줘"

**Q. 내장 `simplify`와 `dotshelf-simplify`가 둘 다 발동되면?**
A. 보통 description이 더 구체적인 `dotshelf-simplify`가 우선. 헷갈리면 "dotshelf-simplify로 해줘"라고 명시.

**Q. 내가 SKILL.md를 수정해도 되나요?**
A. 네. 다만 PR로 팀 합의 후 머지하는 게 좋습니다. 라우팅이나 트리거 키워드를 바꾸면 다른 팀원의 작업 흐름에 영향이 가요.

**Q. patterns.md와 SKILL.md가 충돌하면?**
A. **patterns.md가 우선**. SKILL.md는 patterns.md를 강제하는 절차 매뉴얼이므로, patterns.md를 먼저 업데이트하고 필요하면 SKILL.md도 맞춰 수정.

**Q. `dotshelf-` 외의 스킬도 사용되나요?**
A. 네. Anthropic 공식 (`simplify`, `skill-creator`), 사용자 개인 (`study-changelog`), Codex 플러그인 (`codex:*`) 등이 함께 떠 있습니다. `docs/skills.md`(이 문서)에서는 **DotShelf 전용 스킬만** 다룹니다.

---

## 관련 문서

- `docs/architecture/patterns.md` — 코드 규칙
- `docs/architecture/checklist.md` — 셀프 리뷰
- `docs/architecture/exemplars/` — 모범 코드
- `AGENTS.md` — Codex 진입점, 스킬 라우팅
- 각 `SKILL.md` — 개별 스킬의 상세 절차
