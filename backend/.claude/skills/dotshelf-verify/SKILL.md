---
name: dotshelf-verify
description: 깃에 올리기 전 로컬에서 코드가 정말로 돌아가는지 점검하는 스킬. 빌드/테스트/부팅까지 실제로 실행해서 push 전에 실패를 걸러낸다. Gradle/Maven/npm/pnpm/yarn/pip 스택을 자동 감지하고, Spring Boot류면 실제 앱을 띄워 헬스체크 엔드포인트까지 때려본다. 반드시 사용자가 "로컬에서 돌려봐", "테스트해봐", "빌드 확인", "푸시 전에 체크", "문제없는지 확인", "smoke test", "dotshelf-verify", "올리기 전에 점검", "CI 돌기 전에 먼저", "배포 전 확인", "동작 확인" 같은 말을 할 때 트리거되어야 한다. dotshelf-commit 스킬이 push 직전에 이 스킬을 참조해 자동 체인할 수도 있다. 단순 "빌드해줘"와는 달리 결과를 구조화된 리포트로 정리해 무엇이 왜 실패했는지 근거와 함께 돌려주는 게 핵심이다.
---

# Preflight

깃에 올리기 전 로컬에서 코드가 실제로 돌아가는지 점검한다. CI가 빨간불 뜰 상황을 push 전에 잡는 게 목적이다.

## 언제 쓰나

사용자가 다음을 요청할 때:
- "로컬에서 잘 돌아가는지 봐줘"
- "푸시 전에 확인", "올리기 전에 체크"
- "빌드 되는지 보자", "테스트 돌려봐"
- "smoke test", "dotshelf-verify"
- `dotshelf-commit` 같은 상위 스킬이 push 직전에 내부적으로 참조

사용자가 단순히 "빌드해줘"라고 하면 이 스킬이 과한 수 있다. 하지만 "푸시" 맥락이 섞이면 이 스킬로 붙잡는다.

## 철학

**실제로 돌려본다.** `grep`이나 정적 분석으로 때우지 않는다. 빌드 명령을 진짜 실행하고, 테스트를 진짜 돌리고, Spring Boot류는 진짜 JVM을 띄워 HTTP 요청을 보낸다. 그래야 "로컬에선 됐는데 CI에선 깨짐" 상황을 막을 수 있다.

**빠른 실패가 낫다.** 빌드 실패면 테스트로 안 넘어간다. 테스트 실패면 부팅으로 안 넘어간다. 시간 낭비를 막고, 실패 원인이 뚜렷해진다.

**리포트는 근거와 함께.** 단순 PASS/FAIL이 아니라, 실패했으면 어느 단계/어느 테스트/어느 로그 줄에서 터졌는지 사용자가 바로 행동할 수 있게 제시한다.

---

## 전체 워크플로우

```
스택 감지 → 의존성 서비스 확인 → 빌드 → 테스트 → 부팅 smoke → 리포트
```

단계 실패 시 뒤 단계는 건너뛰고, 부분 결과로 리포트한다.

### 0단계: 스택 감지

프로젝트 루트를 보고 가장 구체적인 감지부터 시도. 대부분 한 가지만 감지되지만, 모노레포면 여러 개가 나올 수 있다 (각각 점검).

| 감지 신호 | 스택 | 기본 빌드/테스트 명령 |
|-----------|------|----------------------|
| `gradlew` 존재 | Gradle Wrapper | `./gradlew build` (test 포함) |
| `build.gradle(.kts)` 있는데 wrapper 없음 | Gradle (system) | `gradle build` — wrapper 생성 제안 |
| `mvnw` 존재 | Maven Wrapper | `./mvnw verify` |
| `pom.xml` 있는데 wrapper 없음 | Maven (system) | `mvn verify` — wrapper 생성 제안 |
| `package.json` + `pnpm-lock.yaml` | pnpm | `pnpm install --frozen-lockfile && pnpm build && pnpm test` |
| `package.json` + `yarn.lock` | yarn | `yarn install --frozen-lockfile && yarn build && yarn test` |
| `package.json` + `package-lock.json` | npm | `npm ci && npm run build && npm test` |
| `pyproject.toml` + `poetry.lock` | Poetry | `poetry install && poetry run pytest` |
| `pyproject.toml` (uv) | uv | `uv sync && uv run pytest` |
| `requirements.txt` | pip | `pip install -r requirements.txt && pytest` |

스크립트가 정의돼 있지 않으면 건너뛴다 (예: `package.json`에 `build` 스크립트 없으면 build 단계 스킵).

### 1단계: 의존성 서비스 확인

`docker-compose.yml` 또는 `compose.yaml`이 있으면 의존성 서비스(DB, Redis, Kafka 등)가 필요할 가능성이 높다.

```bash
docker ps --format "{{.Names}}\t{{.Status}}"
```

필요한 컨테이너가 떠 있지 않으면:
- 테스트가 DB를 요구한다면 **반드시** 먼저 `docker compose up -d`를 제안하고 사용자 확인 후 실행
- 테스트가 Testcontainers나 in-memory DB를 쓰는 구조면 건너뛰어도 됨 (빌드 파일 읽어 판단)
- 판단 애매하면 사용자에게 물어본다

### 2단계: 빌드

감지된 스택의 빌드 명령을 실행. 실패 시:
- 컴파일 에러는 파일:줄번호 단위로 추출해 리포트
- 의존성 해소 실패면 Gradle/Maven 캐시 문제 가능성 언급
- 뒤 단계(테스트/부팅) 건너뛰고 **여기서 중단**

Gradle 예시:
```bash
./gradlew build -x test --console=plain
# test는 다음 단계에서 따로 돌림
```

Maven 예시:
```bash
./mvnw compile --batch-mode
```

Node 예시:
```bash
npm ci --no-audit --no-fund
npm run build
```

### 3단계: 테스트

빌드가 성공했을 때만 진행.

```bash
./gradlew test --console=plain
# 또는
npm test -- --run   # vitest 등 watch 모드 방지
```

실패한 테스트는:
- 테스트 이름
- 실패 원인 메시지 첫 줄 (stack trace 전체 아님)
- 해당 테스트 파일 경로

를 추출해 리포트한다. 전체 로그 다 붙이지 않는다. 필요하면 "전체 로그 보여줘" 요청에 응답.

### 4단계: 부팅 smoke test (선택)

Spring Boot / FastAPI / Express 등 "서버를 띄워 요청 받는" 앱이면 실제 부팅 후 헬스체크.

사용자가 명시적으로 "부팅까지 확인"을 원하거나, 빌드/테스트가 깨끗이 통과한 뒤 "여기까지 했으니 부팅도 볼까요?"를 **물어본 뒤** 진행한다. 기본값은 **부팅 단계 포함**이다 (이 스킬의 차별화 지점).

#### Spring Boot 흐름

```bash
# 이미 빌드된 jar 사용
JAR=$(ls build/libs/*-SNAPSHOT.jar 2>/dev/null | head -1)
java -jar "$JAR" > /tmp/dotshelf-verify.log 2>&1 &
APP_PID=$!

# 준비 확인 (최대 60초 폴링)
for i in $(seq 1 30); do
  code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/health 2>/dev/null)
  if [ "$code" = "200" ]; then break; fi
  sleep 2
done

# 결과 판정
if [ "$code" = "200" ]; then
  BODY=$(curl -s http://localhost:8080/health)
  echo "BOOT OK: $BODY"
else
  echo "BOOT FAIL"
  tail -40 /tmp/dotshelf-verify.log
fi

# 반드시 정리
kill $APP_PID 2>/dev/null
wait $APP_PID 2>/dev/null
```

**헬스 엔드포인트 탐색 순서** (프로젝트마다 다름):
1. `/health` — 이 프로젝트(DotShelf) 기본 (`HealthController` 매핑)
2. `/actuator/health` — Spring Boot Actuator 기본
3. `/healthz`
4. 사용자에게 확인

**컨테이너 healthcheck 와 일치 확인**: `Dockerfile` 또는 `docker-compose.yml` 의 `HEALTHCHECK` 가 같은 엔드포인트를 폴링하는지 본다. 로컬 verify 는 통과해도 컨테이너 healthcheck 가 다른 path 를 보고 있으면 운영에선 영영 unhealthy 로 남는다 (실사례: `dotshelf-api` 가 `localhost:8080` 폴링 → 앱 부팅 자체가 실패하면 starting 으로만 머묾).

포트도 고정값 아님. `application*.yml`의 `server.port` 또는 환경변수 확인. 기본 8080.

#### Node 서버 흐름

`package.json`의 `start` 스크립트 또는 자주 쓰이는 `dev` 스크립트로 띄우고 같은 폴링 패턴. 대부분 3000 또는 3001.

#### Python 서버 흐름

`uvicorn`, `gunicorn`, `flask run` 등. `pyproject.toml`/`Procfile`에서 추론. 보통 8000.

#### CLI/라이브러리 프로젝트

서버가 아니면 부팅 smoke는 건너뛰고, 대신 "help 출력"으로 검증:
```bash
./bin/my-cli --help
```

### 5단계: 정리

**반드시**:
- 백그라운드로 띄운 앱 프로세스 kill
- 임시로 `docker compose up` 한 경우 사용자에게 "정리할까요?" 확인 (바로 내리지 않음 — 다른 개발 중일 수도)
- 임시 로그 파일 유지 (실패 시 사용자가 재확인 원할 수 있음)

---

## 리포트 포맷

단계별로 ✅/❌/⏭️ 하나씩. 실패 있으면 원인 요약을 맨 위로.

```
Preflight 결과 — DotShelf backend

요약: ❌ 테스트 2건 실패로 중단

✅ 스택 감지: Gradle (Spring Boot 4.0.5 / Java 21)
✅ 의존성 서비스: postgres, redis healthy
✅ 빌드: ./gradlew build -x test (23s)
❌ 테스트: ./gradlew test (실패 2건)
   - UserServiceTest.회원가입_중복이메일시_예외
     → expected IllegalStateException but was null
     → src/test/java/com/dotshelf/domain/user/service/UserServiceTest.java:42
   - BookshelfRepositoryTest.목록조회_기본정렬
     → 0 rows returned, expected 3
     → src/test/java/com/dotshelf/domain/bookshelf/repository/BookshelfRepositoryTest.java:28
⏭️ 부팅 smoke: 테스트 실패로 건너뜀

추천 조치:
1. UserService에 중복 이메일 검증 로직 확인
2. BookshelfRepositoryTest의 @Sql 또는 Fixture 확인
```

통과한 경우:
```
Preflight 결과 — DotShelf backend

요약: ✅ 전부 통과 (총 1분 42초)

✅ 스택 감지: Gradle
✅ 의존성 서비스: postgres, redis healthy
✅ 빌드: 23s
✅ 테스트: 47 passed
✅ 부팅 smoke: /health → 200 {"success":true,"data":{"status":"UP",...}}

→ push 안전합니다.
```

---

## dotshelf-commit과의 연동

`dotshelf-commit` 스킬이 push 단계 직전에 이 스킬을 참조할 수 있다. 연동 시:
1. 사용자가 "커밋하고 올려줘" 하면 `dotshelf-commit` 트리거
2. `dotshelf-commit`이 push 직전에 dotshelf-verify 권고
3. 사용자 승인 시 이 스킬 실행 → 결과에 따라 push 진행 여부 결정
4. 실패 시 push 차단하고 리포트 보여주기

사용자가 "dotshelf-verify 건너뛰고 그냥 올려"라고 하면 존중하되, 한 번만 "정말로 건너뛸까요?"를 확인한다.

---

## 안전 원칙

- **백그라운드 프로세스 누수 금지** — 띄운 앱은 반드시 kill. 스크립트 중단/에러에도 `trap` 또는 명시 cleanup.
- **사용자 DB 파괴 금지** — `ddl-auto: create-drop`이거나 Flyway clean이 실행되는 설정이면 경고. 개발 DB에 유효한 데이터가 있을 수 있음.
- **포트 충돌 감지** — 부팅 smoke 전 대상 포트가 이미 바인딩돼 있으면 "기존 프로세스 kill할까요?"를 **물어본다**, 자동으로 죽이지 않음.
- **CI 시크릿 노출 금지** — 로그를 사용자에게 보여줄 때 `*_SECRET`, `*_KEY`, `*_TOKEN` 같은 패턴을 마스킹 시도.
- **Docker 의존성 파괴 금지** — `docker compose down -v`는 절대 자동 실행하지 않음. 볼륨 날아감.

---

## 자주 마주치는 실패 패턴

**"CI는 통과하는데 로컬에서 빌드 실패"**
→ Gradle/Maven 캐시 문제. `--refresh-dependencies` 또는 `~/.gradle/caches` 초기화 제안.

**"빌드는 되는데 부팅 실패"**
→ 보통 환경변수/application.yml 문제. 로그에서 `Failed to configure`, `No qualifying bean`, `Access denied` 패턴 스캔.

**"테스트가 로컬 DB를 건드려 다른 실패 유발"**
→ `@Transactional` 없거나 Testcontainers 미사용. 테스트 격리 점검 제안.

**"부팅은 되는데 /health가 404"**
→ 엔드포인트 경로 오타, Security 설정으로 차단, Actuator 미포함. 3가지 모두 체크.

---

## 의사결정 순서 요약

1. 스택 감지 → 여러 스택이면 각각 또는 사용자에게 선택 요청
2. 의존성 서비스 확인 → 필요하면 docker compose 기동 제안
3. 빌드 → 실패 시 중단
4. 테스트 → 실패 시 중단
5. 부팅 smoke → 기본 포함, 서버 아닌 프로젝트면 스킵
6. 정리 → 백그라운드 프로세스 kill
7. 리포트 → 근거 있는 구조화된 출력

불명확한 부분은 매번 사용자에게 묻기보다 **합리적 기본값으로 일단 시도**하고, 결과에 "다음은 이렇게 해결 가능"이라는 제안까지 붙이는 쪽이 낫다. 다만 위험한 동작(DB 초기화, 포트 프로세스 kill, 볼륨 삭제)은 반드시 확인을 구한다.
