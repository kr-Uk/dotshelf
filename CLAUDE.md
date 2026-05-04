# dotshelf — 디렉터리별 정책 라우팅

이 레포는 여러 하위 프로젝트가 섞여 있다. **하위 프로젝트마다 별도의 `CLAUDE.md`가 있을 수 있고, 거기 정책이 우선이다**. 작업 대상 파일이 어느 디렉터리에 속하는지 먼저 보고, 해당 디렉터리의 `CLAUDE.md`를 **읽고 따른다**.

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

- **호스트 주소(또는 ssh alias)와 pem 키 경로가 주어지면** → 즉시 `ssh -i <pem> <user>@<host>`로 접속해서 직접 명령 실행/로그 확인. 코드만 보고 결론 내리지 말 것.
- **둘 중 하나라도 없으면** → 개발자(사용자)에게 명시적으로 요청한다. 예: *"EC2 호스트와 pem 경로 알려주세요. 직접 까볼게요."* 추측으로 진행하지 말 것.
- 원격에서의 **파괴적 작업**(`rm -rf`, 서비스 재시작, DB 마이그레이션, 컨테이너 강제 종료 등)은 실행 전 별도 컨펌.

이유: 로컬 파일/git만 보고 추론한 가설은 운영 환경의 실제 상태(env, 컨테이너 상태, 디스크/네트워크, 외부 서비스 응답)와 어긋나는 경우가 많아 fake-it 머지의 원인이 된다.

## 디폴트 정책 (하위 CLAUDE.md가 없을 때만)

- 파괴적 작업(`rm -rf`, `git reset --hard`, `git push --force`, 대량 overwrite, 파일 삭제 등)은 실행 전 사용자에게 컨펌.
- 외부 영향(네트워크 호출 추가, 의존성 추가, 빌드 환경/Info.plist/엔타이틀먼트 변경)은 짐작 금지 — 짧은 옵션 + 추천안 + 이유로 질문.
- 어느 디렉터리의 코드를 만지는지 헷갈리면 사용자에게 확인.

## Sub-agent 규칙

`Agent` 도구로 하위 에이전트를 spawn할 때, 작업 대상 디렉터리의 `CLAUDE.md`를 **prompt에 첨부하거나 명시적으로 읽도록 지시**한다. 라우팅 규칙은 메인 에이전트의 책임.

read-only 리서치/탐색 sub-agent는 디렉터리 정책에서 자유롭다(탐색 결과만 정직하게 보고하면 됨).
