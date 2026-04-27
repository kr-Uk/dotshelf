# DotShelf Backend Docs

DotShelf 백엔드 문서 색인. 새 문서를 만들 때는 아래 분류 기준에 맞는 폴더에 넣고, 해당 폴더의 README "문서 목록"에 한 줄 추가한다.

## 폴더 구조

| 폴더 | 성격 | 안에 들어가는 것 |
|------|------|---|
| [`architecture/`](./architecture/README.md) | **영구 규칙** + 시점별 리뷰 | 코딩 패턴, 셀프리뷰 체크리스트, 모범 코드, 아키텍처 감사 리포트 |
| [`infra/`](./infra/README.md) | **운영 인프라** | EC2 / Nginx / GitLab Runner / CI 변수 등 운영 환경 가이드와 작업 일지 |
| [`study/`](./study/README.md) | **학습 회고** | push 단위 변경분에 대한 한국어 학습 노트 |
| [`skills.md`](./skills.md) | AI 스킬 가이드 | Claude Code / Codex 가 사용하는 스킬 5종 사용법 |

## 두 종류의 문서

이 docs 안에는 두 성격의 문서가 섞여 있다. 읽거나 만들 때 의식적으로 구분한다.

- **영구 문서** — `patterns.md`, `checklist.md`, `exemplars/`, `infra/README.md`, `infra/gitlab-ci-variables.md`, `skills.md`. 규칙이 바뀌면 본문을 갱신한다.
- **회고/스냅샷** — 파일명에 `YYYY-MM-DD` 가 붙은 모든 문서. 해당 시점의 상태/작업을 동결해서 남기는 기록. 사후에 본문을 거의 고치지 않는다. 후속 변경은 새 날짜 파일로.

## 새 문서를 추가할 때

1. 위 표에서 성격에 맞는 폴더로 들어간다.
2. 회고/작업 노트면 파일명을 `YYYY-MM-DD-{topic}.md` 로.
3. 그 폴더 README 의 "문서 목록" 표에 새 행을 한 줄 추가한다.
