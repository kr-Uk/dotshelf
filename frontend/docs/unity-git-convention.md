# Unity Git Convention

이 문서는 Dotshelf Unity 프로젝트에서 사용할 브랜치, 커밋, MR 규칙을 정리한다.
이 프로젝트에서는 `FE` 대신 `Unity` 스코프를 사용한다.

## Branch Rules

### Branch Name Format

```text
feat/unity/<task-name>
fix/unity/<task-name>
chore/unity/<task-name>
docs/unity/<task-name>
```

규칙:
- 전체 소문자를 사용한다.
- 단어 구분은 케밥 케이스를 사용한다.
- 기능 단위로 브랜치를 분리한다.
- Unity 프로젝트 작업은 두 번째 경로에 항상 `unity`를 사용한다.

예시:

```text
feat/unity/main-scene
feat/unity/bookshelf-focus-view
fix/unity/book-drag-rotation
fix/unity/notebook-placement
chore/unity/gitignore
docs/unity-progress-report
```

### Branch Workflow

```text
feature branch
    |
    v
dev
    |
    v
master
```

역할:
- `feat/unity/*`, `fix/unity/*`: 개인 기능 단위 작업 브랜치
- `dev`: 통합 및 배포 검증 브랜치
- `master`: 검증 완료 후 실운영 자동 배포 브랜치

원칙:
- 개인 작업은 직접 `dev` 또는 `master`에서 하지 않는다.
- 작업 브랜치에서 MR을 생성해 `dev`로 병합한다.
- `master` 병합은 검증 완료 후 진행한다.

## Commit Convention

### Commit Types

| Type | 사용 시점 |
| --- | --- |
| Init | 프로젝트 초기 세팅 |
| Add | 새로운 파일 추가 |
| Delete | 파일 삭제 |
| Feat | 새로운 기능 추가 또는 기능 수정 |
| Fix | 버그 수정 |
| Build | 빌드 관련 설정 수정 |
| Chore | 기타 설정 수정 |
| Docs | 문서 및 주석 수정 |
| Style | 코드 스타일 또는 포맷 수정 |
| Refactor | 기능 변경 없는 리팩토링 |
| Test | 테스트 코드 추가 또는 수정 |
| Release | 버전 릴리즈 |
| Rename | 파일 또는 폴더 이름 변경 |
| Readme | README 수정 |
| Comment | 주석 관련 작업 |

### Commit Message Format

```text
[Unity] Type: 지라스토리/버그key 요구사항ID 대제목

- 상세 설명 1
- 상세 설명 2
- 상세 설명 3
```

규칙:
- 제목은 간결하게 작성한다.
- 본문은 반드시 줄바꿈해서 상세히 작성한다.
- 본문은 왜, 무엇을, 어떻게 변경했는지 중심으로 작성한다.
- Unity 프로젝트에서는 커밋 스코프를 `[Unity]`로 통일한다.
- 연결할 이슈가 없다면 임시로 `NO-ISSUE`를 사용할 수 있지만, MR 전에는 가능한 실제 이슈와 연결한다.

예시:

```bash
git commit -m "[Unity] Feat: S14P21D208-110 책장 포커스 뷰 추가" -m "- 쿼터뷰에서 책장 클릭 시 책장뷰로 전환되도록 구현
- 뒤로가기 버튼으로 쿼터뷰 복귀 흐름 추가
- 책장 포커스 전환과 HUD 표시 상태를 검증"
```

```bash
git commit -m "[Unity] Fix: S14P21D208-111 노트북 배치 기준 수정" -m "- Play 모드에서 책상과 노트북 위치가 MainScene과 다르던 문제 수정
- NotebookStation을 GuestbookBoard 하위 좌표 기준으로 정규화
- Play 모드 캡처로 책상과 노트북 표시를 확인"
```

## Merge Request Rules

### MR Title

MR 제목만 보고도 무엇을 했는지 알 수 있게 작성한다.

예시:

```text
[Unity] Feat: 책장 포커스 뷰 및 뒤로가기 구현
[Unity] Fix: 책 클릭 후 확대 및 드래그 회전 복구
```

### MR Template

```markdown
## MR 한 줄 요약
MR 내용을 한 문장으로 요약해주세요.

## MR 세부 내용
- 수정/추가한 내용을 상세히 작성해주세요.

## Issue 번호
<!-- closed #이슈번호 -->
```

규칙:
- Issue 또는 Jira 스토리/버그 연동은 필수다.
- Unity 동작 변경은 가능하면 캡처, 영상, 또는 Play 모드 검증 결과를 첨부한다.
- 씬, 프리팹, `.meta` 파일 변경 여부를 MR 본문에 명시한다.

## Unity Repository Rules

### Commit해야 하는 항목

```text
Assets/
Packages/
ProjectSettings/
docs/
README.md
AGENTS.md
.gitignore
.gitattributes
```

### Commit하지 않는 항목

```text
Library/
Temp/
Logs/
UserSettings/
Build/
Builds/
obj/
.appdata/
.dotnet/
.nuget/
*.csproj
*.sln
*.slnx
```

### Unity Meta File Rule

`Assets` 아래 파일을 추가, 이동, 삭제할 때는 대응되는 `.meta` 파일도 함께 관리한다.

예시:

```text
Assets/Scenes/MainScene.unity
Assets/Scenes/MainScene.unity.meta
```

### Git LFS Rule

대용량 바이너리 에셋은 Git LFS로 관리한다.

대상 예시:

```text
*.fbx
*.glb
*.gltf
*.png
*.jpg
*.jpeg
*.tga
*.exr
*.psd
*.wav
*.mp3
```

최초 1회 실행:

```bash
git lfs install
```

## GitLab Variables Rule

Unity 프로젝트에서 서버 주소, API 키, 빌드 환경값처럼 환경별로 달라지는 값은 Git에 직접 올리지 않는다.

변수 이름 규칙:

```text
ENV_BASE_*
ENV_DEV_UNITY_*
ENV_RELEASE_UNITY_*
```

예시:

```text
ENV_DEV_UNITY_API_BASE_URL=https://dev-api.example.com
ENV_DEV_UNITY_AUTH_USE_MOCK=true
ENV_RELEASE_UNITY_API_BASE_URL=https://api.example.com
```

배포 시 변환 예시:

```text
ENV_DEV_UNITY_AUTH_USE_MOCK=true
```

위 변수가 Unity dev 배포 env에 반영될 때:

```text
AUTH_USE_MOCK=true
```

주의:
- `.env.example`만 수정해도 서버 배포에는 반영되지 않는다.
- 새 env 변수가 실제 배포에 필요하면 GitLab Variables에도 추가한다.
- Secret, API Key, DB 비밀번호는 Git에 올리지 않는다.
- Unity 코드나 빌드 파이프라인이 해당 env를 실제로 읽도록 연결되어 있어야 한다.

추가 순서:

1. GitLab `Settings > CI/CD > Variables`에서 변수 추가
2. 이름은 `ENV_<TARGET>_<KEY>` 규칙으로 작성
3. 파이프라인 배포
4. 필요하면 서버의 env 파일에서 반영 확인

## Flyway Rule

이 Unity 저장소에서는 직접 DB migration을 관리하지 않는다.
다만 같은 프로젝트의 backend 스키마 변경 작업을 같이 다루는 경우 아래 규칙을 따른다.

역할:
- `infra-common`: PostgreSQL, Redis 같은 base 서버와 extension 준비
- `backend`: Flyway migration으로 테이블, 인덱스, 제약조건 변경

Migration 파일 위치:

```text
services/backend/src/main/resources/db/migration
```

파일 이름 규칙:

```text
V버전__설명.sql
```

예시:

```text
V1__baseline.sql
V2__add_watchlist_table.sql
V3__add_signal_index.sql
```

중요:
- 이미 적용된 `V1`, `V2` 같은 migration 파일은 수정하지 않는다.
- 스키마 변경이 필요하면 새 버전 파일을 추가한다.
- `pgvector` extension 생성은 Flyway가 아니라 `infra-common`에서 담당한다.
- migration에는 테이블, 인덱스, 제약조건 같은 스키마 변경만 넣는다.

## Codex Work Rule

Codex에게 Git 작업을 지시할 때는 이 문서를 기준으로 한다.

기본값:
- Branch: `feat/unity/<task-name>` 또는 `fix/unity/<task-name>`
- Commit scope: `[Unity]`
- Commit body: 줄바꿈된 bullet list
- MR target: `dev`
- Unity 검증: 가능한 경우 Play 모드, 캡처, 로그 확인 포함
