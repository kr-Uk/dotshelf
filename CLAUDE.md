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

## 디폴트 정책 (하위 CLAUDE.md가 없을 때만)

- 파괴적 작업(`rm -rf`, `git reset --hard`, `git push --force`, 대량 overwrite, 파일 삭제 등)은 실행 전 사용자에게 컨펌.
- 외부 영향(네트워크 호출 추가, 의존성 추가, 빌드 환경/Info.plist/엔타이틀먼트 변경)은 짐작 금지 — 짧은 옵션 + 추천안 + 이유로 질문.
- 어느 디렉터리의 코드를 만지는지 헷갈리면 사용자에게 확인.

## Sub-agent 규칙

`Agent` 도구로 하위 에이전트를 spawn할 때, 작업 대상 디렉터리의 `CLAUDE.md`를 **prompt에 첨부하거나 명시적으로 읽도록 지시**한다. 라우팅 규칙은 메인 에이전트의 책임.

read-only 리서치/탐색 sub-agent는 디렉터리 정책에서 자유롭다(탐색 결과만 정직하게 보고하면 됨).
