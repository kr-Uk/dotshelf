# Architecture

DotShelf 백엔드 아키텍처/컨벤션 문서. 새 코드를 생성할 때 Claude/Codex 가 가장 먼저 읽는 폴더.

## 영구 규칙 (살아있는 single source of truth)

| 문서 | 역할 |
|------|------|
| [`patterns.md`](./patterns.md) | 레이어 / 엔티티 / DTO / Service / Repository / 예외 / 네이밍 규칙. **모든 신규 코드의 기준** |
| [`checklist.md`](./checklist.md) | 코드 작성 후 통과해야 할 셀프리뷰 항목 |
| [`exemplars/`](./exemplars/README.md) | "이 스타일로 써라" 모범 코드 모음 (현재 `User.java` 1건) |

규칙이 바뀌면 위 문서 본문을 갱신한다. patterns.md 와 실제 코드가 어긋나면 둘 중 하나를 맞춘다.

## 회고 / 리뷰 리포트 (시점 스냅샷)

| 날짜 | 문서 | 요약 |
|------|------|------|
| 2026-04-21 | [`reviews/2026-04-21-review-report.md`](./reviews/2026-04-21-review-report.md) | 4-에이전트 병렬 감사 결과. Swagger 전무, JWT 미적용, Cursor 페이지네이션 허위, UTC 명세 위반 등 4개 핵심 구멍 식별 |

이 문서들은 작성 시점 상태를 동결한 기록이다. 후속 변경은 본문 수정이 아니라 새 날짜 파일을 추가한다.

## 새 리뷰/감사를 추가할 때

- 파일명: `YYYY-MM-DD-{topic}.md` (예: `2026-05-15-security-audit.md`)
- 위 "회고 / 리뷰 리포트" 표에 새 행을 추가
