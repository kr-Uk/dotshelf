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
| 2026-04-22 | `docs/be/codex-skills` | [`2026-04-22-docs-be-codex-skills.md`](./2026-04-22-docs-be-codex-skills.md) | Codex 스킬 설치 흐름과 DB 스키마 문서화 |
| 2026-04-22 | `feat/be/social_login` | [`2026-04-22-feat-be-social-login.md`](./2026-04-22-feat-be-social-login.md) | Google/Kakao/Apple 소셜 로그인 토큰 검증과 Unity 클라이언트 설정 흐름 정리 |
| 2026-04-23 | (설계) | [`2026-04-23-blue-green-rationale.md`](./2026-04-23-blue-green-rationale.md) | Blue-Green 무중단 배포 도입의 설계/트레이드오프/측정 계획·결과 박제 (rationale) |
| 2026-04-23 | 5개 MR 통합 | [`2026-04-23-blue-green-rollout-retro.md`](./2026-04-23-blue-green-rollout-retro.md) | 하루 5개 MR (JVM→핫픽스→Blue-Green→실전 수정 2건→측정) 전체 회고 + 실전 실패 2건 배운 것 |
| 2026-04-24 | `feat/be/port` | [`2026-04-24-feat-be-port.md`](./2026-04-24-feat-be-port.md) | 프론트 로컬 테스트용 localhost:3001 CORS 허용 |
| 2026-04-24 | `fix/be/health-endpoint` | [`2026-04-24-fix-be-health-endpoint.md`](./2026-04-24-fix-be-health-endpoint.md) | 헬스체크 공개 경로를 /health로 통일 |

## 작성 규칙

- 파일명: `YYYY-MM-DD-{브랜치명-슬래시는-하이픈으로}.md`
- 본 README 의 "문서 목록" 표에 새 행을 추가
- 본문은 시간이 지나도 거의 고치지 않는다 (학습 시점의 의도/원리를 그대로 보존). 후속 작업은 새 날짜 파일로.
