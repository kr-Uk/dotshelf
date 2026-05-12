# dotshelf — 디렉터리별 정책 라우팅

이 레포는 여러 하위 프로젝트가 섞여 있다. **하위 프로젝트마다 별도의 `CLAUDE.md`가 있을 수 있고, 거기 정책이 우선이다**. 작업 대상 파일이 어느 디렉터리에 속하는지 먼저 보고, 해당 디렉터리의 `CLAUDE.md`를 **읽고 따른다**.

## 정책 우선순위

세 layer가 있다. 충돌 시 위에서 아래 순서로 적용:

1. **공통 정책 (이 파일의 "공통 정책" 섹션)** — 모든 디렉터리에 적용, 하위 CLAUDE.md보다 우선
2. **하위 CLAUDE.md** — 라우팅 표대로 디렉터리별 정책. 공통 정책과 충돌 시 공통 정책 우선
3. **디폴트 정책 (이 파일의 "디폴트 정책" 섹션)** — 위 두 layer 모두 미적용 시 fallback

## 라우팅 표

| 작업 대상 경로 | 먼저 읽을 CLAUDE.md |
|---|---|
| `apple/DotshelfRealityKit/**` | [`apple/DotshelfRealityKit/CLAUDE.md`](apple/DotshelfRealityKit/CLAUDE.md) — 결정 프로토콜 (Critical/High/Medium/자율 카테고리, 질문 형식 템플릿, sub-agent 규칙) |
| `apple/DotshelfMetal/**` | `apple/DotshelfMetal/CLAUDE.md` (있으면) |
| `apple/Dotshelf_RCP/**` | 해당 패키지 디렉터리의 CLAUDE.md (있으면) |
| `backend/**` | `backend/CLAUDE.md` (있으면) |
| `frontend/**` | `frontend/CLAUDE.md` (있으면) |
| 루트 (`README.md` 등 메타) | 이 파일 |

해당 경로의 CLAUDE.md가 **없는 경우에만** 디폴트(아래)를 따른다.

## 공통 정책 (모든 디렉터리에 적용 — 하위 CLAUDE.md보다 우선)

### 서버 검증은 직접 까본다

BE 배포 검증, 로그 확인, 환경변수/시크릿 누락 디버깅, DB 상태 체크 등 **서버 측 사실 확인이 필요한 작업**에서는 추측·가정 금지하고 직접 SSH로 들어가 확인한다. 작업 디렉터리(BE/FE/메타) 무관.

**적용 케이스 (예시)**:
- BE 배포 결과 검증, application/access 로그 확인
- `/opt/dotshelf/.env` 등 환경변수/시크릿 셋업 상태 확인
- 컨테이너 상태(`docker ps`, `docker logs`, `docker inspect`), systemd 서비스
- nginx access/error 로그
- 외부 서비스(Kakao/Google OAuth, S3 등) 실제 응답
- API endpoint 운영 hit (production reachability)
- DB 상태/마이그레이션 적용 여부

**미적용 케이스**: 코드 수정만, 로컬 빌드/테스트, 정적 분석 — SSH 불필요.

**규칙**:
- **호스트 주소(또는 ssh alias)와 pem 키 경로가 주어지면** → 즉시 `ssh -i <pem> <user>@<host>`로 접속해서 직접 명령 실행/로그 확인. 코드만 보고 결론 내리지 말 것.
- **둘 중 하나라도 없으면** → 개발자(사용자)에게 명시적으로 요청한다. 예: *"EC2 호스트와 pem 경로 알려주세요. 직접 까볼게요."* 추측으로 진행하지 말 것.
- 원격에서의 **파괴적 작업**(`rm -rf`, 서비스 재시작, DB 마이그레이션, 컨테이너 강제 종료 등)은 실행 전 별도 컨펌.

이유: 로컬 파일/git만 보고 추론한 가설은 운영 환경의 실제 상태(env, 컨테이너 상태, 디스크/네트워크, 외부 서비스 응답)와 어긋나는 경우가 많아 fake-it 머지의 원인이 된다.

**정책 적용 사례**: [MR #79](https://lab.ssafy.com/s14-final/S14P31F206/-/merge_requests/79) — Mac localhost 버그를 진단 LogWarning 추가 + 양 OS에서 데이터 확보 후 root cause(Mono Uri.TryCreate file URI) 발견. fake-it 머지 회피한 케이스.

### `.claude/skills/` 동기화

`frontend/.claude/skills/` 와 `backend/.claude/skills/` 의 실제 스킬 정의를 루트 `.claude/skills/`에서 심볼릭 링크로 참조해 루트 cwd에서 자동 발견되게 한다.

- **새 스킬 추가 시**: 해당 sub-project의 `.claude/skills/<name>/SKILL.md` 작성 → `./scripts/sync-claude-skills.sh` 실행 → 변경 git add/commit
- **Windows 사용자**: `git config core.symlinks true` + Developer Mode 활성화 필요 (or WSL/Git Bash 사용). 미설정 시 심링크가 text 파일로 변환됨.

### MR 생성 시 이슈 / Atlassian 연결 확인

사용자가 MR 생성을 요청하면 (push 전 또는 push 후) **연결할 Jira 이슈 번호를 먼저 묻는다**. 대답 받기 전 `NO-ISSUE` 로 진행하지 말 것 — 사후에 이슈를 별도 갱신해야 하는 후속 노이즈가 발생한다.

- **이슈 번호 받은 경우**: MR 제목/본문/commit 메시지에 일관되게 포함. 본문의 `## Issue 번호` 섹션에 `[<KEY> — <summary>](<jira-url>)` 형태로 링크.
- **사용자가 명시적으로 "NO-ISSUE"라고 답한 경우만**: `NO-ISSUE` 로 진행 OK.
- **이슈는 있는데 번호를 모르겠다고 하는 경우**: Atlassian MCP 가 연결되어 있으면 `searchJiraIssuesUsingJql` 또는 assignee 기반 검색으로 후보 제시.

**Atlassian MCP 미연결 시**: 이슈 이름 수정 / 상태 transition / 댓글 추가 등 후속 작업이 불가하므로, 사용자에게 **연결을 명시적으로 권장**한다. 예: *"Atlassian MCP 가 연결돼 있으면 Jira 이슈 summary 자동 갱신 / status transition / MR 머지 시 자동 close 같은 후속 작업이 가능해요. 연결 권장합니다."*

**정책 적용 사례**: [MR !170](https://lab.ssafy.com/s14-final/S14P31F206/-/merge_requests/170) — 재로그인 책장 회귀 fix 시 처음에 `NO-ISSUE` 로 commit/push 후 사용자가 따로 Jira 이슈 번호와 summary 갱신을 요청 → 본문 description 재update 라운드 발생. 처음부터 물었으면 한 번에 끝났을 케이스.

## 디폴트 정책 (하위 CLAUDE.md가 없을 때만)

- 파괴적 작업(`rm -rf`, `git reset --hard`, `git push --force`, 대량 overwrite, 파일 삭제 등)은 실행 전 사용자에게 컨펌.
- 외부 영향(네트워크 호출 추가, 의존성 추가, 빌드 환경/Info.plist/엔타이틀먼트 변경)은 짐작 금지 — 짧은 옵션 + 추천안 + 이유로 질문.
- 어느 디렉터리의 코드를 만지는지 헷갈리면 사용자에게 확인.

## Sub-agent 규칙

`Agent` 도구로 하위 에이전트를 spawn할 때, 작업 대상 디렉터리의 `CLAUDE.md`를 **prompt에 첨부하거나 명시적으로 읽도록 지시**한다. 라우팅 규칙은 메인 에이전트의 책임.

read-only 리서치/탐색 sub-agent는 디렉터리 정책에서 자유롭다(탐색 결과만 정직하게 보고하면 됨).
