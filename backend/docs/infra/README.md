# DotShelf Infrastructure

DotShelf 백엔드 운영 인프라 문서. 앞으로 인프라 관련 작업은 이 폴더에 누적해서 관리한다.

## 문서 목록

| 날짜 | 문서 | 요약 |
|------|------|------|
| 2026-04-20 | [2026-04-20-initial-setup.md](./2026-04-20-initial-setup.md) | EC2/Cloudflare/Nginx/Docker/GitLab CI 초기 구축 |
| 2026-04-21 | [2026-04-21-runner-and-jwt.md](./2026-04-21-runner-and-jwt.md) | GitLab Runner EC2 자립 + hairpin NAT 회피 + JWT env 세팅 |

## 작성 규칙

- 파일명: `YYYY-MM-DD-{topic}.md` (예: `2026-05-01-grafana.md`)
- 본 README의 "문서 목록" 표에 새 행을 추가
- 비밀(키, 비밀번호, 프라이빗 IP가 아닌 공용 식별자 이상) 은 본문에 절대 기재하지 말 것. 위치/이름만 남기기
- 변경 이력은 각 문서 하단 `## 변경 이력` 섹션에 날짜 + 한 줄 요약으로 추가
