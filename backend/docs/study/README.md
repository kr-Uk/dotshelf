# Study Notes

push 단위 변경분에 대한 한국어 학습 노트. 무엇을 / 왜 / 어떻게 바꿨는지, 그리고 그 변경에서 무엇을 배워야 하는지 정리한다.

## 작성 흐름

`dotshelf-study` 스킬이 자동 생성한다. 사용자가 "학습 문서 만들어줘", "push 회고" 같은 트리거 발화를 하면 `origin/dev..HEAD` 범위의 diff/커밋을 읽어 작성.

임의 범위 (특정 커밋, 임의 브랜치 등) 회고는 `study-changelog` 스킬을 쓴다.

## 문서 목록

| 날짜 | 브랜치 | 문서 | 핵심 내용 |
|------|--------|------|----------|
| 2026-04-20 | `chore/be/deploy-infra` | [`2026-04-20-chore-be-deploy-infra.md`](./2026-04-20-chore-be-deploy-infra.md) | EC2 배포 파이프라인 + Cloudflare + GitLab CI 골격 구축 |
| 2026-04-21 | `feat/be/refactoring` | [`2026-04-21-feat-be-refactoring.md`](./2026-04-21-feat-be-refactoring.md) | Swagger / JWT / Cursor / UTC 4-구멍 보강, patterns.md 공식화 |

## 작성 규칙

- 파일명: `YYYY-MM-DD-{브랜치명-슬래시는-하이픈으로}.md`
- 본 README 의 "문서 목록" 표에 새 행을 추가
- 본문은 시간이 지나도 거의 고치지 않는다 (학습 시점의 의도/원리를 그대로 보존). 후속 작업은 새 날짜 파일로.
