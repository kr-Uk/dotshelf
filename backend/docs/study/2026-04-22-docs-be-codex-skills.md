# Codex 스킬 설치 흐름과 DB 스키마 문서화

- **브랜치**: `docs/be/codex-skills`
- **범위**: `origin/dev..HEAD` (커밋 1개)
- **기간**: 2026-04-22 ~ 2026-04-22
- **티켓**: 없음

## TL;DR

DotShelf 백엔드의 Claude/Codex 스킬 문서를 Codex 사용 흐름에 맞게 정리했다. Codex 전역 스킬 목록에 프로젝트 스킬을 연결할 수 있도록 PowerShell 설치 스크립트를 추가했고, 스킬 문서의 경로 기준을 `backend/` 프로젝트 루트 기준으로 통일했다. 함께 PostgreSQL 실행용 DB 스키마와 ERDCloud 가져오기용 MySQL DDL 문서를 추가해 데이터 모델 검토 자료를 보강했다.

## 무엇을 했나

### 1. Codex 스킬 설치 안내 정리

- 사용자 관점 효과: Codex에서 DotShelf 전용 스킬을 어떻게 발견시키고 다시 로드해야 하는지 문서만 보고 따라 할 수 있다.
- 시스템 관점 효과: `.claude/skills`를 `~/.codex/skills`로 연결하는 설치 흐름이 명시되어 Claude와 Codex가 같은 스킬 원본을 공유할 수 있다.
- 주요 파일: `backend/docs/skills.md`, `backend/scripts/install-codex-skills.ps1`

### 2. 스킬 문서 경로 기준 보정

- 사용자 관점 효과: 저장소 루트에서 작업하든 `backend` 디렉토리에서 작업하든 어떤 문서를 읽어야 하는지 혼동이 줄어든다.
- 시스템 관점 효과: `dotshelf-code-match`, `dotshelf-study`가 프로젝트 루트 기준 경로를 안내하므로 에이전트가 잘못된 `backend/backend/...` 경로를 찾을 가능성이 낮아진다.
- 주요 파일: `backend/.claude/skills/dotshelf-code-match/SKILL.md`, `backend/.claude/skills/dotshelf-study/SKILL.md`

### 3. DB 스키마 문서 추가

- 사용자 관점 효과: 백엔드 구현 전에 전체 데이터 모델을 SQL로 검토하고 ERDCloud에 가져가 볼 수 있다.
- 시스템 관점 효과: 실행 기준 PostgreSQL DDL과 설계 도구용 MySQL DDL을 분리해 목적별 스키마 문서를 유지할 수 있다.
- 주요 파일: `backend/docs/database/schema.sql`, `backend/docs/database/schema_erdcloud.sql`

## 왜 했나

기존 스킬 문서는 Claude Code 기준 설명이 중심이었고, Codex에서 전역 스킬로 발견시키려면 사용자가 직접 링크/복사 구조를 만들어야 했다. 이번 변경은 그 과정을 스크립트와 문서로 고정해 반복 실수를 줄이기 위한 것이다.

또한 스킬 문서 안에 `backend/docs/...`처럼 저장소 루트 기준 경로와 백엔드 프로젝트 루트 기준 경로가 섞여 있었다. 실제 스킬 실행 시 프로젝트 루트를 어떻게 잡느냐에 따라 파일 탐색이 실패할 수 있으므로, `gradlew`와 `build.gradle.kts`가 있는 `backend`를 기준으로 `docs/...`를 찾도록 설명을 통일했다.

## 어떻게 구현했나

### Codex 전역 스킬 설치 스크립트

`install-codex-skills.ps1`은 `.claude/skills` 아래의 각 스킬 디렉토리를 검사하고, 기본적으로 `~/.codex/skills` 아래에 Junction을 만든다.

```powershell
$sourceRoot = Join-Path $backendRoot ".claude\skills"
$targetRoot = Join-Path $env:USERPROFILE ".codex\skills"

$skillDirs = Get-ChildItem -LiteralPath $sourceRoot -Directory | Sort-Object Name
foreach ($skillDir in $skillDirs) {
    $targetPath = Join-Path $targetRoot $skillDir.Name
    New-Item -ItemType Junction -Path $targetPath -Target $skillDir.FullName | Out-Null
}
```

핵심 포인트:
- Junction 모드는 원본 스킬을 하나만 유지하기 위한 기본값이다.
- `-Mode Copy`를 제공해 전역 스킬을 복사본으로 분리해야 하는 경우도 지원한다.
- `-Force` 없이는 기존 대상 디렉토리를 덮어쓰지 않아 사용자의 전역 스킬을 보호한다.

### 스킬 문서의 프로젝트 루트 기준 통일

`dotshelf-code-match`는 기존에 `backend/docs/architecture/patterns.md`를 직접 가리켰지만, 이제 프로젝트 루트를 설명한 뒤 `docs/architecture/patterns.md`를 읽도록 안내한다.

```markdown
프로젝트 루트는 보통 `gradlew`, `pom.xml`, `build.gradle(.kts)`가 있는 디렉토리다.

1. docs/architecture/patterns.md
2. docs/architecture/exemplars/README.md
3. docs/architecture/exemplars/<관련 파일들>
```

핵심 포인트:
- 스킬이 저장소 루트와 백엔드 루트를 혼동하지 않게 한다.
- Codex와 Claude가 같은 `patterns.md`를 단일 진실 소스로 보도록 문장을 정리했다.

### DB 스키마 문서 분리

`schema.sql`은 PostgreSQL 실행 기준 문서이고, `schema_erdcloud.sql`은 ERDCloud 가져오기용 MySQL DDL이다. 같은 논리 모델을 두 형식으로 나눈 이유는 실제 DB와 설계 도구의 SQL 지원 범위가 다르기 때문이다.

핵심 포인트:
- PostgreSQL 문서는 `JSONB`, `TIMESTAMPTZ`, `BIGSERIAL`처럼 실제 운영 DB 문법을 사용한다.
- ERDCloud 문서는 `AUTO_INCREMENT`, `COMMENT`, `ENGINE=InnoDB`처럼 가져오기 호환성을 우선한다.
- 실행용 마이그레이션이 아니라 검토용 문서이므로 `src/main/resources/db/migration`이 아닌 `docs/database`에 둔다.

## 배운 것

- **Codex 스킬 발견 방식**: 프로젝트 내부 라우팅은 `AGENTS.md`로 해결할 수 있지만, 전역 스킬 목록에 나타나게 하려면 `~/.codex/skills` 아래에 스킬 디렉토리가 있어야 한다.
- **Junction vs Copy**: Junction은 원본 변경이 즉시 반영되어 팀 스킬 유지보수에 유리하고, Copy는 특정 사용자의 전역 스킬을 독립적으로 고정해야 할 때 유리하다.
- **스킬 문서의 경로 기준**: 에이전트가 자동으로 파일을 찾는 흐름에서는 상대 경로의 기준을 명확히 적는 것이 중요하다.
- **설계용 SQL과 실행용 SQL 분리**: ERD 도구가 요구하는 SQL 방언과 실제 DB 방언이 다를 수 있으므로 목적별 문서를 분리하는 편이 안전하다.

## 더 공부할 것

- [ ] Codex 전역 스킬 설치 시 Junction이 아닌 symlink를 사용할 경우 Windows 권한 정책과 개발자 모드 요구사항은 어떻게 달라지는가?
- [ ] 팀 공용 스킬을 저장소 안에 둘 때 버전 관리, 개인 커스터마이징, 전역 설치 경로를 어떻게 분리하는 것이 좋은가?
- [ ] PostgreSQL DDL을 ERDCloud용 MySQL DDL로 변환할 때 자동화 가능한 매핑 규칙은 어디까지인가?
- [ ] `docs/database/schema.sql`과 실제 JPA 엔티티 사이의 drift를 감지하는 검증 방법은 무엇이 적절한가?

## 참고

- 관련 커밋: `f0ac318`
- 관련 문서: `backend/docs/skills.md`
- 관련 스크립트: `backend/scripts/install-codex-skills.ps1`
