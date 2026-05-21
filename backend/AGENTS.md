# Agent Instructions — DotShelf Backend

이 리포에서 AI 에이전트(Codex, Claude, 기타)가 코드를 작성할 때 따라야 하는 규칙.

## 단일 진실 소스

**모든 아키텍처/디자인/네이밍 규칙은 `docs/architecture/patterns.md`에 있다.**

새 코드를 작성하기 전에:
1. `docs/architecture/patterns.md` 전체 읽기
2. `docs/architecture/exemplars/README.md` 읽고 관련 exemplar 파일 확인
3. 가장 가까운 기존 도메인 코드 확인 (예: 새 도메인 = user/book/bookshelf/review/follow 중 유사한 것)
4. 작성 후 `docs/architecture/checklist.md`로 셀프 리뷰

**읽지 않고 쓰지 않는다.** 문서를 건너뛰면 드리프트가 쌓여 몇 주 뒤 리뷰/유지보수 비용이 급증한다.

## 핵심 규칙 요약 (patterns.md 발췌)

- **레이어**: Controller → Service → Repository, 우회 금지
- **엔티티**: `BaseEntity` 상속, `@NoArgsConstructor(PROTECTED)`, `@Builder`, setter 금지
- **DTO**: record, 입력은 Bean Validation, 출력은 `static from(...)` 팩토리
- **응답**: 모든 Controller는 `ApiResponse<T>` 리턴
- **예외**: `CustomException(ErrorCode.*)` 외 금지, 새 상황 → ErrorCode 항목 추가
- **쿼리**: 단순 조회 = Spring Data, 복잡 조회 = QueryDSL Custom/Impl 분리
- **Spring Boot 4 기준**: Jackson 3 (`tools.jackson.*`), Jackson 2 API 참조 금지
- **테이블**: snake_case 복수형, 컬럼: snake_case

## 어긋나야 할 때

패턴을 bending해야 하면 **사일런트 드리프트 금지**:
1. 왜 어긋나야 하는지 한두 문장 설명
2. "patterns.md 수정 or 이 코드만 예외"를 사용자에게 질문
3. 독단 결정 금지

## 빌드/실행

- 빌드: `./gradlew build`
- 테스트: `./gradlew test`
- 로컬 기동: `docker compose up -d` (PG 17, Redis 7.4) → IDE에서 `local` 프로필로 Spring Boot 실행
- 헬스체크: `GET /health`

## 커밋/브랜치 컨벤션

팀 git 컨벤션은 Claude 스킬 `dotshelf-commit` (저장소 `.claude/skills/dotshelf-commit/SKILL.md`)에 정리돼 있다. Codex에서도 참고:
- 브랜치: `{type}/be/{kebab-topic}` (예: `feat/be/user-signup`)
- 커밋: `[BE] Type: TICKET-ID 제목` + 불릿 본문
- Flyway 기존 마이그레이션 파일 **수정 금지**, 새 `Vn+1__...sql` 추가
- `.env*` 변경 시 GitLab Variables(`ENV_<TARGET>_<KEY>`) 업데이트 필요

## 스킬 라우터 (Codex가 Claude 스킬을 빌려 쓰는 방법)

이 리포에는 Claude Code용 스킬 5개가 `.claude/skills/`에 있다. **Codex는 SKILL.md를 자동 인식하지 않지만, 본 라우팅 표를 보고 상황에 맞는 SKILL.md를 직접 읽어서 그 절차를 따라야 한다.**

| 사용자 의도 | 읽을 파일 | 트리거 표현 예 |
|-------------|-----------|----------------|
| 새 도메인/엔티티/서비스/컨트롤러 만들기 | `.claude/skills/dotshelf-code-match/SKILL.md` | "엔티티 만들어줘", "CRUD 만들어", "API 추가" |
| 방금 쓴 코드 청소/리팩토링 | `.claude/skills/dotshelf-simplify/SKILL.md` | "정리해줘", "깔끔하게", "검토해줘" |
| push 전 로컬 빌드/테스트/부팅 검증 | `.claude/skills/dotshelf-verify/SKILL.md` | "빌드 확인", "테스트 돌려봐", "smoke test" |
| 커밋 / 브랜치 / push / MR | `.claude/skills/dotshelf-commit/SKILL.md` | "커밋해줘", "올려줘", "MR 만들어줘" |
| push할 변경분 학습 문서 생성 | `.claude/skills/dotshelf-study/SKILL.md` | "학습 문서 만들어줘", "이번 작업 정리" |

**Codex의 사용 방식:**
1. 사용자 요청이 위 트리거 중 하나에 해당하면, 먼저 해당 SKILL.md를 `cat` / 파일 읽기로 로드
2. SKILL.md의 frontmatter는 무시하고 본문 절차를 그대로 수행
3. 절차가 다른 스킬 호출을 권장하면(예: `dotshelf-commit`이 push 전 `dotshelf-verify` 권고) 동일하게 그 파일도 읽고 따른다

이렇게 하면 SKILL.md가 **Claude/Codex 공통 진실 소스**가 되어 한 곳만 수정하면 양쪽에 반영된다. AGENTS.md는 *어떤 상황에 어떤 파일을 읽을지* 안내하는 라우터 역할만 한다.

## 단일 진실 소스 정리

| 종류 | 위치 | 역할 |
|------|------|------|
| 아키텍처/네이밍 규칙 | `docs/architecture/patterns.md` | 코드 작성/리뷰의 절대 기준 |
| 코드 셀프 리뷰 체크리스트 | `docs/architecture/checklist.md` | 작성 후 확인 |
| 모범 코드 예시 | `docs/architecture/exemplars/` | 새 코드의 형판 |
| 워크플로우(커밋/검증/문서/생성/정리) | `.claude/skills/dotshelf-*/SKILL.md` | 각 단계별 절차 |

수정할 때는 **이 4곳 중 하나만** 손대면 양쪽 에이전트(Claude·Codex)에 즉시 반영된다.
