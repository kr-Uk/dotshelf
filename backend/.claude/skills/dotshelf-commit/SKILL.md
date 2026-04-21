---
name: dotshelf-commit
description: SSAFY 팀 컨벤션에 맞춰 백엔드(BE) 변경을 git에 올리는 스킬. 브랜치 생성(feat/be/topic), 커밋 메시지 포맷([BE] Type: ticket-id 제목 + 본문 불릿), push, GitLab MR 생성까지 담당한다. 이 스킬은 반드시 사용자가 백엔드 작업을 "커밋해줘", "푸시해줘", "올려줘", "MR 만들어줘", "브랜치 따줘", "merge request 올려줘"라고 말할 때 트리거되어야 한다. .env.example만 바꾸고 배포 반영을 잊거나, 기존 Flyway migration 파일을 수정하는 실수를 자동으로 잡아낸다. 단순 git 명령 실행이 아니라 컨벤션 강제가 핵심이다.
---

# Backend Git Flow

SSAFY 팀 컨벤션 기반 백엔드 git 워크플로우. 브랜치 이름, 커밋 메시지, MR을 모두 정해진 포맷에 맞춘다.

## 언제 쓰나

사용자가 다음 중 하나를 요청할 때:
- "커밋해줘" / "커밋 메시지 만들어줘"
- "푸시해줘" / "올려줘"
- "MR 만들어줘" / "머지 리퀘스트"
- "브랜치 따줘" / "새 브랜치 만들어줘"
- 변경사항 작업 후 "정리해서 올려줘"

여러 단계(브랜치 생성 + 커밋 + push + MR) 중 일부만 요청하면 해당 단계만 수행한다. "올려줘"처럼 모호하면 전체 플로우로 해석하고 중간 확인을 받는다.

---

## 전체 워크플로우

```
상태 점검 → 브랜치 확인/생성 → 커밋 분류 → 커밋 메시지 작성 → 커밋 → push → (선택) MR 생성
```

각 단계마다 사용자의 명시적 승인을 구한다. 특히 `git push`와 MR 생성은 절대 확인 없이 실행하지 않는다.

### 1단계: 상태 점검

먼저 상황을 파악한다:

```bash
git branch --show-current
git status --short
git log --oneline -5
git diff --stat       # 전체 변경 규모
git diff              # 필요시 내용 확인
```

파악할 것:
- 현재 브랜치가 `dev` / `master`인가? → **바로 커밋하면 안 됨**, 새 feature 브랜치를 먼저 따야 함
- 변경 파일의 성격은? (기능 추가 / 버그 수정 / 설정 변경 / 문서 등) → Type 결정에 사용
- 민감 파일이 섞여있나? (`.env`, `*.key`, `*.pem`, credentials 등) → 경고

### 2단계: 브랜치 확인/생성

#### 브랜치 네이밍 규칙

형식: `{type}/be/{topic-kebab-case}`

- 소문자만, 케밥 케이스
- `type` 예시: `feat`, `fix`, `chore`, `refactor`, `docs`, `test`, `build`
- `topic`은 기능/이슈 키워드 (예: `user-signup`, `redis-config`, `health-endpoint`)

예시:
- `feat/be/user-signup`
- `fix/be/redis-serializer`
- `refactor/be/bookshelf-query`
- `chore/be/gitignore-update`

#### 분기 기준점

기본적으로 `dev`에서 분기한다. 최신성을 확보한 뒤 파생한다:

```bash
git fetch origin
git checkout dev
git pull --ff-only origin dev
git checkout -b feat/be/<topic>
```

이미 기존 브랜치에 올라타 있다면 새 브랜치를 강요하지 말고, 적절한 브랜치인지만 확인한다.

### 3단계: 커밋 타입 분류

변경 내용의 본질을 보고 아래 표에서 고른다. 헷갈리면 `Feat` vs `Fix`가 가장 자주 혼동되는데, 새 기능/기능 수정이면 `Feat`, 잘못된 동작을 바로잡는 것이면 `Fix`다.

| Type | 사용 시점 |
|------|-----------|
| Init | 프로젝트 초기 세팅 |
| Add | 새로운 파일 추가 (기능이 아닌 파일 자체) |
| Delete | 파일 삭제 |
| Feat | 새로운 기능 추가 / 기능 수정 |
| Fix | 버그 수정 |
| Build | 빌드 관련 설정 수정 (build.gradle.kts, Dockerfile 등) |
| Chore | 기타 설정 수정 (.gitignore 등) |
| Docs | 문서 및 주석 수정 |
| Style | 코드 스타일 / 포맷 수정 (동작 변경 없음) |
| Refactor | 기능 변경 없는 리팩토링 |
| Test | 테스트 코드 추가/수정 |
| Release | 버전 릴리즈 |
| Rename | 파일/폴더 이름 변경 |
| Readme | README 수정 |
| Comment | 주석 관련 작업 |

하나의 변경이 여러 Type에 걸치면 가장 핵심적인 것을 고르고, 심하게 섞여있다면 커밋을 쪼갤 것을 제안한다.

### 4단계: 커밋 메시지 작성

#### 포맷

```
[BE] Type: 지라스토리/버그key 요구사항ID 대제목

- 상세 설명 1
- 상세 설명 2
- 상세 설명 3
```

- 제목은 **간결**하게 (보통 50자 이내 권장, 한국어라 유연하게)
- 본문은 반드시 **줄바꿈해서 불릿으로**
- 본문은 **왜 / 무엇을 / 어떻게** 중심

#### 티켓 키 추출

"지라스토리/버그key 요구사항ID" 부분은 다음 순서로 확보한다:

1. 현재 브랜치명에서 추출 시도 (예: `feat/be/S14P31F206-110-signup` → `S14P31F206-110`)
2. 최근 커밋 메시지에서 추출 시도 (`git log --oneline -20`)
3. 위 두 곳에서 못 찾으면 **사용자에게 묻는다**: "티켓 키가 뭔가요? (예: S14P31F206-110)"
4. 사용자가 "없다"고 하면 해당 자리를 비우고 제목만 둔다 (`[BE] Feat: user 회원가입 API 추가`)

#### 예시

```bash
git commit \
  -m "[BE] Feat: S14P31F206-110 사용자 회원가입 API 추가" \
  -m "- OAuth2 Google/Kakao 프로바이더 연동
- User 엔티티에 oauth_provider, oauth_provider_id 컬럼 추가
- 중복 가입 방지를 위해 email unique 제약 확인
- 신규 가입 시 기본 서재 자동 생성"
```

본문을 쓸 때, 한 불릿에 한 가지 사실만 넣는다. "왜"를 설명해야 할 때는 "... 때문에 ..."처럼 짧게 근거를 붙인다.

### 5단계: 커밋 실행

**스테이징은 파일을 명시적으로**:

```bash
git add <specific/paths>        # 권장
git add -A                      # 사용자가 명시 승인했을 때만
```

`git add -A`나 `git add .`는 `.env`, 인증 파일 같은 민감 파일을 우발적으로 포함할 위험이 있어 기본값으로 쓰지 않는다. 현재 디렉토리 전체를 올리고 싶다는 의도가 분명하고 untracked 파일 목록을 사용자가 확인한 경우에만 사용한다.

**커밋 실행**:

제목과 본문을 `-m` 두 개로 분리해서 넘긴다. 이스케이프 문제가 생기면 HEREDOC으로 바꾼다:

```bash
git commit -m "$(cat <<'EOF'
[BE] Type: TICKET-ID 제목

- 본문 1
- 본문 2
EOF
)"
```

### 6단계: Push

**push 전 dotshelf-verify 권고**: 가능하면 `dotshelf-verify` 스킬을 먼저 호출해 빌드/테스트/부팅이 통과하는지 확인한다. 사용자가 "그냥 올려"라고 하면 존중하되, 한 번은 "dotshelf-verify 건너뛸까요?" 확인한다. 이 스킬이 설치돼있지 않거나 사용자가 거절하면 바로 push.

**push 전 학습 문서 제안**: preflight가 통과하면 `dotshelf-study` 스킬 호출을 한 번 제안한다 ("이번 push 변경분으로 학습 문서 만들까요?"). Yes면 문서 생성 → `docs/study/` 파일을 별도 `Docs` 타입 커밋으로 추가 → push. No면 바로 push. 학습할 거리가 적은 사소한 변경(lint, 설정 한두 줄)에는 제안하지 않는다.

현재 브랜치를 origin에 업로드. 첫 push면 `-u`로 upstream 설정:

```bash
git push -u origin <branch>
```

**절대 금지:**
- `--force` / `--force-with-lease`는 사용자가 명시적으로 요청했을 때만
- `dev`/`master`로 직접 push 금지 — 항상 feature 브랜치를 통해서

### 7단계: Merge Request (GitLab)

사용자가 MR까지 원하면 아래 템플릿으로 만든다.

#### MR 템플릿

```markdown
## 📄 MR 한 줄 요약
<MR 내용을 한 문장으로 요약>

## 🧑‍💻 MR 세부 내용
- <수정/추가한 내용 1>
- <수정/추가한 내용 2>

## 📎 Issue 번호
<!-- closed #이슈번호 -->
```

- MR 제목은 커밋 제목과 유사한 형태: `[BE] Feat: S14P31F206-110 사용자 회원가입 API`
- 제목만 봐도 무엇을 했는지 알 수 있어야 한다
- Issue 번호는 필수. 없으면 사용자에게 확인.

#### 생성 방법

`glab` CLI가 있으면:

```bash
glab mr create \
  --target-branch dev \
  --title "[BE] Feat: TICKET-ID 제목" \
  --description "$(cat <<'EOF'
## 📄 MR 한 줄 요약
...
EOF
)"
```

`glab`이 없으면 push 응답에 포함된 `Create a merge request` URL을 사용자에게 전달하고 수동 생성 안내. GitHub이면 `gh pr create`로 대체 가능하지만 이 팀은 GitLab 기준이다.

---

## 반드시 잡아야 할 엣지 케이스

### 1) .env / env 파일 변경 시 GitLab Variables 경고

변경된 파일에 `.env`, `.env.example`, `*.env`, `application-*.yml`(민감 값 포함 가능) 등이 포함되어 있으면 다음을 **반드시** 사용자에게 알린다:

> ⚠️ env 관련 변경 감지. 이 값이 실제 서버 배포에 반영되려면 **GitLab Settings → CI/CD → Variables** 에 같은 키를 추가/갱신해야 합니다. 변수 네이밍·플래그(Protected ✅, Masked ❌, multiline 처리)·값 형식은 [`docs/infra/gitlab-ci-variables.md`](../../../docs/infra/gitlab-ci-variables.md) 를 단일 진실 소스로 따릅니다.
>
> `.env.example` 만 수정하면 배포에 반영되지 않습니다. MR 설명에 GitLab Variables 업데이트 TODO 를 포함하시겠어요?

민감값(Secret, API key, DB 비밀번호)이 diff에 보이면 **커밋을 중단시키고** GitLab Variables로 옮기도록 안내한다.

### 2) Flyway migration 기존 파일 수정 차단

`src/main/resources/db/migration/V*.sql` 경로에서 **이미 존재하던 파일이 수정된 경우**(staged modification, not new file), 커밋을 **중단**하고 다음을 안내한다:

> 🛑 이미 적용된 Flyway migration 파일을 수정했습니다: `<파일명>`
>
> Flyway는 적용된 migration의 체크섬을 검증하므로, 기존 파일을 수정하면 배포 시 실패합니다. 기존 파일 수정 대신 **새 버전 파일**을 추가하세요:
>
> - 형식: `V{다음번호}__{설명}.sql`
> - 예: `V4__add_news_summary_column.sql`
>
> 되돌리기: `git checkout -- <파일명>` 후 새 V 파일 생성

신규 V 파일 추가는 정상 흐름이므로 막지 않는다. 파일명 형식(`V숫자__설명.sql`)만 검증하고 어긋나면 수정 제안.

---

## 안전 원칙

- **민감 파일 스캔**: 스테이징 전 `.env`, `*.key`, `*.pem`, `*credentials*`, `*secret*` 패턴 검사
- **하드코딩 시크릿**: diff에서 `password\s*=\s*["'][^"']+` 같은 패턴 눈치챘다면 경고
- **브랜치 보호**: `dev`, `master`에 직접 commit/push 금지
- **강제 push 금지**: `--force` / `--force-with-lease`는 사용자가 명시 요청한 경우만
- **Hook 우회 금지**: `--no-verify`는 사용자가 명시 승인한 경우에만

확인 없이 실행하지 않을 것:
- `git push` (첫 실행은 반드시 확인)
- `git push --force*`
- MR 생성 (공개 행동)
- `git reset --hard`, `git clean -fd` 등 파괴적 명령

---

## 빠른 참고: 커밋 분류 예시

| 변경 내용 | Type | 브랜치 예 |
|-----------|------|-----------|
| 신규 API 엔드포인트 추가 | Feat | `feat/be/user-signup` |
| NPE 수정 | Fix | `fix/be/review-npe` |
| QueryDSL 설정 빈 추가 | Build 또는 Chore | `chore/be/querydsl-config` |
| `.gitignore` 업데이트 | Chore | `chore/be/gitignore` |
| 메서드 추출 리팩토링 | Refactor | `refactor/be/service-layer` |
| 테스트 추가 | Test | `test/be/user-service` |
| CLAUDE.md 수정 | Docs | `docs/be/claude-md` |
| 신규 Flyway 마이그레이션 | Feat (스키마가 기능 일부) | `feat/be/add-follow-table` |

---

## 의사결정 순서 요약

1. `git status`, `git diff`, `git branch --show-current` 확인
2. 현재 브랜치가 dev/master면 → 새 feature 브랜치 제안
3. 변경에 env/민감값 있으면 → 경고 및 MR TODO
4. 기존 V*.sql 수정 있으면 → 중단 및 새 V 파일 안내
5. Type 분류 → 사용자 확인
6. 티켓 키 확보(브랜치→로그→사용자)
7. 커밋 메시지 드래프트 → 사용자 확인
8. `git commit`
9. push 필요 여부 확인 → `git push -u origin <branch>`
10. MR 필요 여부 확인 → `glab mr create` 또는 URL 전달

각 단계에서 사용자가 "그냥 다 알아서 해"라고 위임하면 가정을 명시적으로 나열한 뒤 한 번에 진행해도 된다. 그러나 가정을 숨기지 않는다.
