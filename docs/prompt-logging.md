# DotShelf 프롬프트 자동 로깅

Claude Code 와 Codex 세션이 끝날 때마다(`Stop` 훅) 마지막 사용자 프롬프트와 어시스턴트 응답을 Notion DB 에 자동으로 기록하는 시스템이다. cwd 와 편집된 파일 경로를 보고 워크스페이스(`backend` / `apple` / `front`)별 DB 로 라우팅한다.

## 보안 및 opt-in 원칙

- **fresh clone 상태에서는 Stop 훅이 동작하지 않는다.** repo 에 commit 된 5개 `settings.json` 과 `.codex/hooks.json.example` 는 비활성 상태이고, 본인이 명시적으로 `scripts/setup-prompt-logging.{sh,ps1}` 를 실행해서 동의해야만 hook 이 활성화된 로컬 파일이 생성된다.
- **Notion integration token, Anthropic API key 는 절대 commit 되지 않는다.** 셋업 스크립트가 입력받아 `.claude/notion-config.json` (gitignored) 에만 쓴다. repo 에는 [.claude/notion-config.example.json](../.claude/notion-config.example.json) 템플릿만 commit 된다.
- 시크릿 마스킹은 패턴 기반(아래 표) 이라 누락 가능성이 있다. 토큰/패스워드를 프롬프트에 직접 붙여넣는 행위는 처음부터 피한다.

## 동작 원리

1. 세션이 응답을 마치면 Claude Code/Codex 가 `Stop` 훅을 발사한다.
2. 훅은 `node "$(git rev-parse --show-toplevel)/.claude/log-to-notion.mjs"` 를 실행한다.
3. 스크립트는 두 모드로 동작한다.
   - **Hook 모드** (인자 없이 호출): stdin 페이로드를 임시 파일에 저장 → detached 자식 프로세스(`--worker`)를 spawn → 즉시 종료. Claude Code 가 Notion 응답을 기다리지 않는다.
   - **Worker 모드** (`--worker <payload-path>`): 임시 파일을 읽어 본 작업을 수행 후 파일 정리.
4. Worker 는 시작 시 재시도 큐(`%TEMP%/dotshelf-notion-queue.jsonl`) 가 있으면 먼저 flush 시도.
5. transcript 를 파싱해 마지막 user/assistant 메시지를 뽑고, 시크릿 패턴을 마스킹(아래 참조)한 뒤, IDE 메타 태그(`<ide_opened_file>` 등) 를 제거한다.
6. cwd 가 어느 워크스페이스 디렉토리에 속하는지, 편집된 파일이 어느 워크스페이스에 있는지를 모아 라우팅 대상 DB 를 정한다 (`.claude/`, `.codex/`, `.git/` 같은 인프라 경로는 제외).
7. 워크스페이스별로 한 번씩 Notion `pages` API 에 row 를 POST. 같은 (session, content, workspace) 조합은 30초 안에 한 번만 기록(dedup). POST 가 실패하면 큐에 적재 → 다음 worker 실행 때 자동 재시도.

## 레포 구조

```
S14P31F206/
├── .claude/
│   ├── log-to-notion.mjs              # 훅 본체 (committed)
│   ├── notion-config.example.json     # 템플릿 (committed, 빈 토큰)
│   ├── notion-config.json             # 실제 토큰 (★ gitignored, setup 이 생성)
│   ├── settings.json                  # 빈 {} (committed, hook 비활성)
│   └── settings.local.json            # 실제 hook 정의 (★ gitignored, setup 이 생성)
├── .codex/
│   ├── hooks.json.example             # 템플릿 (committed)
│   └── hooks.json                     # 실제 hook (★ gitignored, setup 이 생성)
├── apple/.claude/
│   ├── settings.json                  # {} (committed)
│   └── settings.local.json            # ★ gitignored
├── backend/.claude/
│   ├── settings.json                  # {} (committed)
│   └── settings.local.json            # ★ gitignored
├── frontend/.claude/
│   ├── settings.json                  # {} (committed)
│   └── settings.local.json            # ★ gitignored
├── scripts/
│   ├── setup-prompt-logging.sh
│   └── setup-prompt-logging.ps1
└── docs/prompt-logging.md             # 이 문서
```

`.gitignore` 에 다음 패턴이 등록돼 있다:

```
**/.claude/settings.local.json
.claude/notion-config.json
.codex/hooks.json
```

`settings.local.json` 은 Claude Code 가 `settings.json` 위에 자동으로 덧씌우는 per-user override 슬롯이다. 5개 `settings.local.json` + 1개 `.codex/hooks.json` 모두 `git rev-parse --show-toplevel` 로 레포 루트를 찾아 동일한 스크립트를 호출한다. 이 패턴 덕분에 Claude Code 가 `$CLAUDE_PROJECT_DIR` 을 settings.json 발견 위치로 푸는 함정을 피할 수 있다.

## 신규 팀원 셋업

레포를 clone 한 뒤 한 번만 실행하면 된다.

### Mac / Linux / Git Bash

```bash
bash scripts/setup-prompt-logging.sh
```

### Windows PowerShell

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-prompt-logging.ps1
```

스크립트가 하는 일:

1. Node 18+, git, codex CLI 존재 확인.
2. **opt-in 확인** — "활성화하시겠습니까?" 에 `y` 입력 안 하면 즉시 종료, 어떤 파일도 안 만든다.
3. `~/.codex/config.toml` 에 `[features] codex_hooks = true` 가 없으면 추가 (Codex 훅 기능 토글).
4. **Notion integration token 비공개 입력** (`Read-Host -AsSecureString` / 셸의 `read -rs`) → `.claude/notion-config.json` (gitignored) 에 작성.
5. **Anthropic API key 비공개 입력** (선택, enter 로 건너뛰기 가능) → 같은 파일의 `llm_token` 필드.
6. `.claude/settings.local.json`, `apple/.claude/settings.local.json`, `backend/.claude/settings.local.json`, `frontend/.claude/settings.local.json`, `.codex/hooks.json` 5+1 개 로컬 hook 정의 생성 (모두 gitignored).
7. `LOG_USER` 환경변수를 묻고:
   - Mac/Linux: 셸 rc 파일(`~/.zshrc`, `~/.bashrc`) 에 `export LOG_USER=...` 추가
   - Windows: `setx LOG_USER ...` 로 사용자 레지스트리에 등록
   - 그리고 **모든 OS 공통으로 `~/.dotshelf/log-user` 파일에도 같은 값 기록** (환경변수 stale 폴백)
8. `notion-config.json` 의 토큰으로 `https://api.notion.com/v1/users/me` 를 호출해 200 확인.

**비활성화 방법**: `.claude/settings.local.json`, `apple/.claude/settings.local.json`, `backend/.claude/settings.local.json`, `frontend/.claude/settings.local.json`, `.codex/hooks.json` 5+1 개 파일을 삭제하면 hook 이 즉시 멈춘다 (다음 세션부터 적용). `.claude/notion-config.json` 도 같이 지우면 토큰까지 정리.

`log-to-notion.mjs` 는 `process.env.LOG_USER` 가 비었으면 `~/.dotshelf/log-user` 를 읽어서 폴백한다. 따라서 환경변수가 stale 한 상황(특히 Windows 에서 `setx` 후 explorer 재시작 전)이라도 setup 직후 다음 Stop 훅부터 즉시 `User` 가 박힌다.

`LOG_USER` 값이 Notion DB 의 `User` Select 필드에 처음 등장하는 이름이면 Notion 이 옵션을 자동으로 만들어 준다. 즉 팀원이 새로 들어와도 사전 등록 없이 본인 setup 만 돌리면 옵션이 알아서 생긴다. 색상은 Notion 이 임의 배정하므로 일관성 신경 쓰면 본인이 노션 UI 에서 한 번 정리하면 된다.

## Notion DB 매핑

| 워크스페이스 | DB ID | 용도 |
|---|---|---|
| backend | `9c725dcc-f90e-4240-933e-86fcd0fa02a1` | Spring 백엔드 |
| apple   | `f8c99ee0-0bee-418e-b3a6-456b8217f0d0` | iOS / SwiftUI |
| front   | `ba924330-011a-4f2b-b1d9-f35b34990f64` | 프론트(이름은 Unity DB 그대로 둠) |

부모 페이지: "프롬프트 히스토리 자동화" (`34769b2a-131a-81f3-8dea-cfd02dc64328`).

각 DB 에는 다음 properties 가 있어야 한다.

| Property | 타입 | 설명 |
|---|---|---|
| Name | Title | 마지막 사용자 메시지 첫 줄 80자 |
| Agent | Select | `Claude Code` 또는 `Codex` |
| User | Select | `LOG_USER` 환경변수 값 (팀원 식별) |
| Project | Rich text | cwd 의 basename |
| Session | Rich text | session_id |
| Command | Rich text | 마지막 사용자 메시지(자동 주입 태그 제거 + 마스킹) |
| Summary | Rich text | 마지막 어시스턴트 메시지(마스킹) |
| Brief | Rich text | LLM 1~2 문장 요약 (Phase 5-1, llm_token 있을 때만) |
| Files | Rich text | 편집된 파일 경로 콤마 join |
| Timestamp | Date | 기록 시각 |

## 라우팅 규칙

```
WORKSPACE 환경변수가 있으면 → 그 값으로 강제
없으면:
  cwd 가 apple/backend/frontend 안이면 그 워크스페이스 추가
  편집된 파일 경로 중 인프라 경로(.claude/.codex/.git)가 아닌 것들에서
    apple/backend/frontend 잡아서 추가
  결과 집합이 비면 backend 로 fallback
```

같은 세션이 여러 워크스페이스를 건드리면 각 워크스페이스 DB 에 row 가 한 행씩 생긴다(예: backend cwd 에서 apple 파일 수정 → backend + apple 양쪽).

## 디버깅

- 로그 파일: `os.tmpdir()/dotshelf-notion-hook.log` (Windows: `%TEMP%\dotshelf-notion-hook.log`).
- 한 줄 JSON 형식이며 `level=error` 줄을 먼저 보면 된다.
- "no token configured" → `.claude/notion-config.json` 의 `token` 비었거나 파싱 실패.
- "no db for workspace" → `notion-config.json` 의 `databases` 키에 해당 워크스페이스가 없음.
- "nothing to log; skipping" → transcript 가 비어 있거나 못 읽음(첫 메시지 직후 등 정상 케이스도 있음).

## 자주 만나는 함정

- **글로벌 settings.json 에 hooks 박지 말 것.** `~/.claude/settings.json` 에 박으면 다른 프로젝트 세션도 전부 이 훅이 발사돼 엉뚱한 DB 에 기록된다. 항상 프로젝트 단위 `<repo>/.claude/settings.json` 또는 `<repo>/<workspace>/.claude/settings.json` 에만 둔다.
- **settings.json 변경 후엔 Claude Code 재시작.** Claude Code 는 세션 시작 시점에만 settings 를 읽는다. 반면 `log-to-notion.mjs` 자체 변경은 매 Stop 마다 재로드되므로 재시작 불필요.
- **`$CLAUDE_PROJECT_DIR` 신뢰 금지.** settings.json 발견 디렉토리로 풀리지 git 루트가 아니다. 명령은 항상 `git rev-parse --show-toplevel` 패턴을 쓴다.
- **인프라 파일 편집은 워크스페이스 신호로 안 잡힘.** `.claude/`, `.codex/`, `.git/` 안의 변경은 라우팅에서 제외돼야 backend 작업이 apple/front DB 에 잘못 박히는 걸 막는다.

## 시크릿 마스킹 (Phase 4-1)

`maskSecrets()` 가 마지막 user/assistant 메시지 본문에 적용된다. 다음 패턴이 자동으로 `[REDACTED:*]` 로 치환된다.

| 종류 | 패턴 | 치환 |
|---|---|---|
| Anthropic API key | `sk-ant-*` | `[REDACTED:anthropic-key]` |
| OpenAI API key | `sk-*`, `sk-proj-*` | `[REDACTED:openai-key]` |
| Notion integration | `ntn_*` | `[REDACTED:notion-token]` |
| Slack | `xoxb/p/a/r/s-*` | `[REDACTED:slack-token]` |
| GitHub | `ghp_*`, `ghs_*`, ... | `[REDACTED:github-token]` |
| GitLab PAT | `glpat-*` | `[REDACTED:gitlab-token]` |
| AWS access key | `AKIA[A-Z0-9]{16}` | `[REDACTED:aws-key]` |
| JWT | 3-segment base64url | `[REDACTED:jwt]` |
| PEM private key | `-----BEGIN ... PRIVATE KEY-----` | `[REDACTED:private-key-block]` |
| 환경변수 스타일 | `*SECRET*=...`, `*TOKEN*=...`, `*API_KEY*=...`, `*PASSWORD*=...`, `*PRIVATE_KEY*=...` (대문자 키) | 값만 `[REDACTED]` |

마스킹은 보수적이다. 길이 임계치(8자 이상)와 식별 가능한 prefix 가 있어야 매치되므로 일반 텍스트가 잘못 가려질 가능성은 낮다. 새 패턴이 노출되면 [.claude/log-to-notion.mjs](../.claude/log-to-notion.mjs) 의 `maskSecrets` 에 추가하면 된다.

## 백그라운드 비동기 (Phase 4-2)

훅 모드는 stdin 을 임시 파일(`%TEMP%/dotshelf-payload-<ts>-<pid>.json`)에 저장한 뒤 `node log-to-notion.mjs --worker <payload>` 를 detached 로 spawn 하고 즉시 종료한다. Claude Code 의 Stop 이벤트는 보통 100ms 이내에 마무리된다(파일 쓰기 + spawn 만 함). 실제 transcript 파싱과 Notion POST 는 자식 프로세스가 백그라운드에서 처리.

spawn 이 실패하면 자동으로 inline(blocking) 폴백이 동작하므로 이벤트가 유실되진 않는다.

## 재시도 큐 (Phase 4-3)

Notion POST 가 HTTP 에러나 네트워크 타임아웃으로 실패하면 해당 row 의 `{dbId, properties, ts}` 를 `%TEMP%/dotshelf-notion-queue.jsonl` 에 1줄 JSON 으로 적재한다. Worker 가 다음 번 실행될 때 큐를 먼저 flush — 성공한 항목은 제거, 실패한 항목은 다시 큐에 남겨 둔다. 큐는 100개로 capped (오래된 항목부터 drop).

운영 측면 수동 액션:
- 큐에 쌓이고 있는데 안 빠지면: `Get-Content $env:TEMP\dotshelf-notion-queue.jsonl` 로 내용 확인.
- 비우고 싶으면: `Remove-Item $env:TEMP\dotshelf-notion-queue.jsonl`.

## LLM 요약 + 내용 기반 분류 (Phase 5-1, 5-3)

`.claude/notion-config.json` 의 `llm_token` 에 Anthropic API 키를 넣으면 두 기능이 활성화된다. 빈 문자열이면 두 기능 모두 우회 (gracefully degrade) — 기존 동작에 영향 없음.

### 5-1 Brief

매 Stop 마다 마스킹 처리된 user/assistant 본문을 Haiku 4.5 (`claude-haiku-4-5-20251001`) 에 던져 1~2문장 한국어 요약을 받아 Notion `Brief` 컬럼에 박는다. 호출은 `decideWorkspaces` 와 병렬로 이뤄지므로 추가 지연이 거의 없다. 비용은 Stop 1회당 약 $0.0001 수준.

### 5-3 워크스페이스 분류

`decideWorkspaces` 가 cwd/편집파일 어디에도 워크스페이스 신호를 못 잡으면(주로 루트 cwd 에서 순수 질문/검색 세션), 같은 Haiku 모델에게 user 메시지만 보내 `apple/backend/frontend` 한 단어로 답하게 한다. 답이 유효하면 그 워크스페이스 DB 로 라우팅, 아니면 기존 backend fallback.

LLM 콜이 실패하면 `logLocal("warn", ...)` 으로만 기록하고 두 기능 모두 조용히 스킵 — Stop 훅 동작 자체는 막지 않는다.

## 토큰 회전이 필요할 때

1. Notion → Settings & Members → Integrations → 해당 integration → Secrets 탭에서 **Revoke** 또는 새 시크릿 발급.
2. 본인 머신의 `.claude/notion-config.json` 의 `token` 직접 갱신 (gitignored 라 commit 안 됨), 또는 setup 스크립트 재실행해 다시 입력.
3. 팀원에게 슬랙/텔레그램 등 비공개 채널로 새 토큰 공유. 받은 사람도 setup 스크립트 재실행 또는 본인 `.claude/notion-config.json` 직접 갱신.
4. **commit 절대 금지.** `.gitignore` 가 막아주지만 의식적으로 확인.
