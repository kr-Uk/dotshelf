# 2026-04-22 · Flyway 도입 + Actuator Health 강화 + CI 테스트 자동화

오전에 [App/DB 서버 분리 + Tailscale 사설망](./2026-04-22-tailscale-app-db-split.md) 을 완성한 뒤, 같은 날 오후에 이어서 진행한 **앱 계층 운영 성숙도 작업** 기록. 세 축:
1. `ddl-auto: update` → **Flyway** 로 스키마 소유권 이전
2. 빈 껍데기였던 `/api/health` 를 **Spring Boot Actuator** 의 실제 헬스체크로 전환
3. CI 파이프라인에 **`./gradlew check`** 추가 + 첫 유닛 테스트 베이스라인

## TL;DR

- **Flyway**: 34 테이블 스키마를 `pg_dump --schema-only` 로 떠서 `V1__init_schema.sql` 로 채택. prod PG volume 초기화 후 Flyway 가 V1 을 처음 실행 (DB 비어있어 safe). 이후 엔티티 변경 시 **반드시 V2, V3... 파일 쌍으로** 커밋.
- **Actuator**: 하드코딩 `"UP"` 반환하던 커스텀 컨트롤러를 `HealthEndpoint` 위임으로 교체. DB/Redis/Flyway/diskSpace/SSL 모두 실제 상태. DOWN 시 HTTP 503 → CI `curl -fsS` 가 제대로 실패 감지.
- **CI 테스트**: `bootJar` → `check bootJar` 로 변경. JUnit XML artifact 업로드해서 MR UI 에 테스트 결과 표시. `ApiResponseTest` 5 케이스 작성.
- **Spring Boot 4 함정 2개**를 하루에 다 맞았음:
  1. Flyway autoconfig 가 별도 모듈(`spring-boot-flyway`)로 분리됨 → 명시적 추가 필수
  2. Actuator health 패키지가 `org.springframework.boot.actuate.health.*` → `org.springframework.boot.health.*` 로 이동
- **Redis DNS 사건**: Netty DNS 리졸버가 `/etc/hosts` 를 무시 → 컨테이너에서 `dotshelf-prod` 리졸브 실패 → compose `extra_hosts` 로 고정 매핑.

## 1. Flyway 도입

### 1-1. 전환 배경

`application-prod.yml` 원본 주석에 이미 힌트가 있었다: "실서비스 전환 시 none + Flyway 로 이관 필요". 미뤄진 이유는 단순히 **데이터가 없었으니 덜 아팠기 때문**.

| 문제 | `ddl-auto: update` | Flyway |
|---|---|---|
| 컬럼 삭제/타입 변경 | **무시됨** → DB 와 엔티티가 소리없이 어긋남 | 명시적 SQL 이니까 일관성 유지 |
| 변경 이력 추적 | 없음 (엔티티 diff 만 남음) | `flyway_schema_history` 테이블에 기록 |
| 환경 간 스키마 drift | 빈번 (각 환경 기동 순서 의존) | 동일 파일, 동일 순서 보장 |
| 롤백 | 불가능 | 새 V 파일로 "undo" 명시 가능 |
| 팀 작업 | 동시 수정 시 충돌 감지 어려움 | 버전 번호 충돌로 즉시 발견 |

### 1-2. 전환 타이밍이 **지금**이었던 이유

> **DB 가 비어있으면 V1 = 현재 스키마 덤프 한 번으로 끝난다.** 데이터가 쌓인 뒤엔 "운영 중 스냅샷 ↔ 엔티티 정합성" 을 맞추는 추가 작업이 끼어 훨씬 난이도가 올라감.

### 1-3. 적용 단계

**① 의존성**

```kotlin
// build.gradle.kts
// Spring Boot 4 는 Flyway autoconfig 가 별도 모듈(spring-boot-flyway)로 분리됨 → 반드시 함께 포함
implementation("org.springframework.boot:spring-boot-flyway")
implementation("org.flywaydb:flyway-core")
implementation("org.flywaydb:flyway-database-postgresql")
```

⚠️ **첫 배포 실패 원인**: 초기엔 `flyway-core` 만 추가했는데 Flyway 가 아예 실행 안 됨. Spring Boot 4 에서 autoconfig 가 `spring-boot-autoconfigure-4.0.5.jar` 에서 빠져 별도 모듈로 이동해 있었다. 기존 jar 의 `META-INF/spring/...AutoConfiguration.imports` 에 Flyway 가 없는 걸 확인하고 `spring-boot-flyway` 추가 → 해결.

**② 설정 (`application-prod.yml`)**

```yaml
spring:
  flyway:
    enabled: true
    locations: classpath:db/migration
    baseline-on-migrate: true

  jpa:
    hibernate:
      # Flyway 가 스키마 소유. Hibernate 는 엔티티 ↔ DB 정합성 검증만.
      ddl-auto: validate
```

로컬(`application-local.yml`) 도 `ddl-auto: create` → `validate` 로 통일해서 환경 일관성 확보.

**③ V1 베이스라인 생성**

```bash
# prod PG 에서 스키마만 덤프 (데이터 없음)
ssh dotshelf "sudo docker exec dotshelf-postgres pg_dump -U dotshelf -d dotshelf \
    --schema-only --no-owner --no-privileges --no-comments" \
    > backend/src/main/resources/db/migration/V1__init_schema.sql
```

pg_dump 가 주입하는 `\restrict ...`, `\unrestrict ...` 같은 **psql 전용 메타 명령**은 Flyway 가 일반 JDBC 로 실행할 때 실패하므로 제거. 그리고 `SELECT pg_catalog.set_config('search_path', '', false)` 도 삭제 (기본 public 스키마 사용).

**④ prod PG volume 초기화**

기존 DB 에 34 테이블이 있는 상태에서 Flyway 가 V1 을 그대로 실행하면 `CREATE TABLE ... already exists` 로 깨짐. 두 가지 길:
- (쉬움) volume 삭제 후 깨끗한 상태에서 Flyway 가 처음 돌게 함 — **DB 가 비어있을 때의 특권**
- (어려움) `baseline-on-migrate: true` + `baseline-version: 1` 로 "V1 은 이미 적용됨" 처리 — 실수 여지 있음

쉬운 길 선택:
```bash
ssh dotshelf "cd /opt/dotshelf && \
    sudo docker compose stop postgres && \
    sudo docker compose rm -f postgres && \
    sudo docker volume rm dotshelf_postgres-data && \
    sudo docker compose up -d postgres"
```

**⑤ 앱 재배포 후 검증**

앱 기동 로그:
```
o.f.core.internal.command.DbMigrate - Current version of schema "public": << Empty Schema >>
o.f.core.internal.command.DbMigrate - Migrating schema "public" to version "1 - init schema"
o.f.core.internal.command.DbMigrate - Successfully applied 1 migration
```

```sql
SELECT installed_rank, version, description, success FROM flyway_schema_history;
-- 1 | 1 | init schema | t
```

### 1-4. 이후 개발 워크플로우

엔티티를 건드릴 때마다 반드시 한 쌍의 커밋:

```
1. User.java 에 @Column private String nickname; 추가
2. backend/src/main/resources/db/migration/V2__add_nickname_to_user.sql 생성:
   ALTER TABLE users ADD COLUMN nickname VARCHAR(30);
3. 두 파일 같은 커밋 / 같은 MR 로 push
```

**한 쪽만 푸시하면**:
- 엔티티 변경만 → DB 에 컬럼 없음 → `ddl-auto: validate` 실패 → 앱 기동 차단
- V 파일만 → Hibernate 가 새 컬럼 매핑 못 함 (또는 런타임 SQL 에러)

### 1-5. 건드리지 말아야 할 규칙

1. **이미 실행된 V 파일은 절대 내용 수정 X** — Flyway 가 체크섬 검증, 불일치 시 기동 실패. 고치려면 **새 V 파일**(예: `V5__fix_v2_column_type.sql`) 로 패치.
2. **파일명 엄격**: `V<숫자>__<설명>.sql` (언더스코어 두 개)
3. **번호 충돌**: 팀원 여러 명이 동시에 `V5` 만들면 충돌. 머지 순서대로 번호 재조정.

## 2. Actuator 기반 `/api/health` 강화

### 2-1. Before — 빈 껍데기

```java
@GetMapping("/health")
public ApiResponse<Map<String, Object>> health() {
    return ApiResponse.success(Map.of(
            "status", "UP",                       // ← 하드코딩
            "service", "dotshelf-api",
            "timestamp", Instant.now().toString()
    ));
}
```

문제:
- **DB / Redis / Flyway 상태 전혀 검증 안 함**
- DB 가 완전히 다운돼도 `"UP"` 반환 + HTTP 200
- CI 의 `curl -fsS /api/health` 가 "컨테이너가 기동은 했다" 정도만 보증
- Docker `HEALTHCHECK` 도 마찬가지로 의미 없음

### 2-2. After — HealthEndpoint 위임

```java
@GetMapping("/health")
public ResponseEntity<ApiResponse<Map<String, Object>>> health() {
    HealthDescriptor descriptor = healthEndpoint.health();
    Status status = descriptor.getStatus();

    Map<String, Object> body = new LinkedHashMap<>();
    body.put("status", status.getCode());
    body.put("service", "dotshelf-api");
    body.put("timestamp", Instant.now().toString());
    body.put("details", descriptor);

    if (Status.UP.equals(status)) {
        return ResponseEntity.ok(ApiResponse.success(body));
    }
    // 한 개 이상 의존성 비정상 → 503
    return ResponseEntity.status(503).body(ApiResponse.success(body));
}
```

`HealthEndpoint.health()` 는 다음 컴포넌트를 자동 취합:
- `db` — DataSource 에 대한 `SELECT 1`
- `redis` — Lettuce 연결 + PING
- `diskSpace` — 루트 디스크 여유
- `ssl` — JVM 인증서 만료
- `ping` — JVM 자체
- `livenessState` / `readinessState` — Kubernetes-style probes
- (Flyway 있으면 `flyway` 도 추가됨)

### 2-3. Spring Boot 4 의 두 번째 함정

Spring Boot 3.x 예제대로 코드 작성:
```java
import org.springframework.boot.actuate.health.HealthEndpoint;
import org.springframework.boot.actuate.health.Status;
```

**컴파일 실패**: `package org.springframework.boot.actuate.health does not exist`.

Spring Boot 4 에서 Health 모듈을 분리하면서 패키지도 재배치했음. `javap` 로 실제 분포 확인:

| Before (3.x) | After (4.x) |
|---|---|
| `org.springframework.boot.actuate.health.HealthEndpoint` | `org.springframework.boot.health.actuate.endpoint.HealthEndpoint` |
| `org.springframework.boot.actuate.health.HealthComponent` | `org.springframework.boot.health.actuate.endpoint.HealthDescriptor` |
| `org.springframework.boot.actuate.health.Status` | `org.springframework.boot.health.contributor.Status` |

`HealthComponent` 가 `HealthDescriptor` 로 이름도 바뀌었다. 시그니처도 달라서 기존 예제 코드를 그대로 복붙하면 전부 실패.

### 2-4. Netty DNS + Redis 사건

Actuator 추가 후 재배포 → `/api/health` 가 처음으로 제대로 **DOWN** 반환. 원인을 파니:

```
Caused by: io.lettuce.core.RedisConnectionException: Unable to connect to dotshelf-prod/<unresolved>:6379
Caused by: java.net.UnknownHostException: Failed to resolve 'dotshelf-prod' [A(1)]
Caused by: io.netty.resolver.dns.DnsErrorCauseException: Query failed with SERVFAIL
```

희한한 점: **같은 호스트명(`dotshelf-prod`) 으로 PG 는 잘 연결됨** (Flyway 가 무사히 실행). JDBC 는 되는데 Redis 는 SERVFAIL.

원인 추적:
- PG JDBC 드라이버 → Java 기본 `InetAddress.getByName()` → glibc NSS → **`/etc/hosts` 를 읽음**
- Lettuce (Redis 클라이언트) → Netty 의 비동기 DNS 리졸버 → **`/etc/hosts` 무시**, DNS 서버만 조회
- 컨테이너의 DNS 서버(Docker embedded `127.0.0.11`) 는 `dotshelf-prod` 에 대한 답이 없음 → SERVFAIL

본질: **Tailscale MagicDNS 가 호스트의 `/etc/hosts` 만 업데이트하고 컨테이너까지는 전파되지 않음**. 컨테이너 `/etc/hosts` 에는 그나마 FQDN(`dotshelf-prod.tailf4fba8.ts.net`) 만 있었고, `ndots:0` 옵션 때문에 short name 이 search domain 타지도 못했다. JDBC 는 NSS 가 어떻게 용케 매칭했지만 Netty 는 못 했다.

해결 — SSAFY 의 `/opt/dotshelf/docker-compose.yml` 의 api 서비스에 `extra_hosts` 박음:

```yaml
api:
  image: dotshelf/api:latest
  container_name: dotshelf-api
  restart: unless-stopped
  extra_hosts:
    - "dotshelf-prod:100.86.247.88"      # ← 컨테이너 /etc/hosts 에 고정 매핑
  ports:
    - "100.74.2.103:8080:8080"
  ...
```

재기동 후 health 정상:
```json
{
  "status": "UP",
  "details": {
    "components": {
      "db":       {"status": "UP"},
      "redis":    {"status": "UP"},
      "diskSpace": {"status": "UP"},
      "livenessState": {"status": "UP"},
      "readinessState": {"status": "UP"},
      "ping":     {"status": "UP"},
      "ssl":      {"status": "UP"}
    }
  }
}
```

⚠️ **SSAFY 서버 재구축 시 `extra_hosts` 반드시 복원**. git 에는 없음(compose 는 서버에만 존재).

### 2-5. 이후 이점

- CI `curl -fsS` 가 **실제 장애 감지** (DB/Redis 다운 시 503 → 파이프라인 실패)
- Docker `HEALTHCHECK` 이 진짜로 의미 있음 (unhealthy 전환 시 orchestrator 가 재시작)
- `/actuator/health` 가 공개돼 상세 디버깅 바로 가능

## 3. CI 테스트 자동 실행

### 3-1. 변경 전

```yaml
# .gitlab-ci.yml backend:build
script:
  - ./gradlew bootJar --no-daemon
```

테스트 실행 없음. 깨진 단위 로직이 있어도 CI 는 초록불.

### 3-2. 변경 후

```diff
 script:
-  - ./gradlew bootJar --no-daemon
+  - ./gradlew check bootJar --no-daemon

 artifacts:
   paths:
     - $JAR_ARTIFACT_PATH/*-SNAPSHOT.jar
+    - backend/build/reports/tests/test      # HTML 리포트 보존
+  reports:
+    junit: backend/build/test-results/test/TEST-*.xml    # GitLab MR UI 에 케이스별 표시
+  when: always                               # 테스트 실패해도 리포트 보존
```

- `check` = `test` + 정적 검증 (JavaCompile warnings 등)
- GitLab 의 `artifacts.reports.junit` 은 XML 을 읽어 MR 화면에 "Tests 3 passed, 0 failed" 같은 바를 붙여줌
- `when: always` 로 테스트 실패 시에도 리포트 확보 → 실패 원인 즉시 확인 가능

### 3-3. 첫 테스트 — `ApiResponseTest`

프로젝트에 테스트 파일이 **0개** 였음. `check` 를 의미 있게 만들려고 작은 단위 테스트 한 개 작성.

대상: `ApiResponse` (공통 응답 envelope POJO). Spring 컨텍스트 불필요, DB 불필요, 순수 JUnit.

```java
class ApiResponseTest {
    @Test
    @DisplayName("success(data): 200 / SUCCESS / 기본 메시지로 감싼다")
    void success_wrapsPayloadWithDefaultMessage() {
        ApiResponse<String> response = ApiResponse.success("payload");

        assertThat(response.getHttpStatus()).isEqualTo(200);
        assertThat(response.isSuccess()).isTrue();
        assertThat(response.getCode()).isEqualTo("SUCCESS");
        assertThat(response.getMessage()).isEqualTo("요청이 성공했습니다.");
        assertThat(response.getResult()).isEqualTo("payload");
    }
    // ... success(msg, data), success() no-content, error(msg), error(status, code, msg)
}
```

5 케이스 전부 통과. 빌드 시간에 거의 영향 없음 (~20ms).

### 3-4. 이후 테스트 작성 패턴

**쓸 수 있는 경우 (지금 가능)**:
- 순수 POJO / static method / 도메인 VO 계산 등 — Spring 없이 JUnit + AssertJ

**Spring 필요한 경우 (향후 과제)**:
- Service / Repository / Controller 통합 테스트
- `@SpringBootTest` + **Testcontainers** 로 실제 PG/Redis 띄워서 검증
- 이건 별도 초기 설정 작업이라 이번 스코프 밖

### 3-5. 깨진 테스트가 올라올 때 동작

1. 개발자 push → CI 실행
2. `./gradlew check` 실행 → **테스트 실패**
3. `bootJar` 는 실행 안 됨 (gradle chain 실패)
4. `artifacts: when: always` 덕에 테스트 리포트는 업로드됨
5. GitLab MR 화면에 실패 테스트 이름/메시지 표시
6. deploy 단계로 넘어가지 않음 → SSAFY 서버의 기존 컨테이너는 안전
7. 개발자가 수정 → 재푸시 → 초록불

## 4. 관련 파일 / 명령 요약

| 항목 | 위치 |
|---|---|
| Flyway 마이그레이션 디렉토리 | `backend/src/main/resources/db/migration/` |
| 현재 baseline | `V1__init_schema.sql` (34 테이블, 1581 줄) |
| 수동 백업 (daily cron) | prod `/opt/dotshelf/backups/pg/` |
| 헬스체크 엔드포인트 | `/api/health` (커스텀, Actuator 위임), `/actuator/health` (Actuator 원본) |
| CI 설정 | `.gitlab-ci.yml` (레포 루트) |
| 테스트 리포트 | 빌드 시 `backend/build/reports/tests/test/index.html` |
| SSAFY compose 의 extra_hosts | `/opt/dotshelf/docker-compose.yml` (서버에만, git 없음) |

## 5. 점검 체크리스트 (서버 재구축 또는 신규 환경 설정 시)

- [ ] SSAFY 앱 서버의 `docker-compose.yml` api 서비스에 `extra_hosts: - "dotshelf-prod:100.86.247.88"` 박혀있는지
- [ ] prod PG volume 정말 비어야 V1 이 처음 돈다 (기존 데이터 있으면 `baseline-on-migrate` 로 갈 것)
- [ ] `spring.jpa.hibernate.ddl-auto` 가 `validate` 인지 (`update` 면 Flyway 와 경쟁)
- [ ] `/api/health` 호출 시 `components` 에 `db`, `redis` 가 둘 다 `UP` 인지
- [ ] MR 머지 직후 파이프라인의 `backend:build` 로그에 `BUILD SUCCESSFUL` 가 보이는지, `Tests: 5 succeeded, 0 failed`

## 6. TODO (이어지는 과제)

- [ ] **Testcontainers 기반 통합 테스트** — Service/Repository 단 테스트, 지금은 스코프 밖
- [ ] **Flyway undo/repair 경험 쌓기** — 체크섬 불일치 시 대응 런북 작성
- [ ] **Spring Boot 4 업그레이드 충격 총정리** — 오늘 발견한 두 함정(Flyway/Actuator 모듈 분리) 외에도 또 있을 수 있음. AutoConfiguration.imports 전수 조사
- [ ] **Grafana + Loki** 관측성 스택 — 현재 Actuator 는 단일 시점 진단만. 시계열/대시보드/알림은 별도 스택 필요 (infra README 에 subdomain 만 예약돼 있음)
- [ ] 한국어 테스트 DisplayName 컨벤션 정착 — 이번 `ApiResponseTest` 의 서술형 DisplayName 스타일을 팀 규칙으로 굳힐지 확정

## 7. 배운 것

- **Spring Boot 4 의 autoconfig 모듈 분리**는 문서보다 실행 로그로 먼저 체감된다. 기대한 로그(예: `Flyway Community Edition ... by Redgate`) 가 아예 안 보이면 **의존성 자체가 활성화 안 된 것**. META-INF 의 `AutoConfiguration.imports` 를 뒤져서 확인 가능.
- **"health 200 OK 가 앱 정상의 증거" 는 거짓말**. Spring 의 기본 actuator 쓰거나 커스텀 컨트롤러라도 실제 의존성을 찔러봐야 한다. 안 그러면 CI/CD 가 배포 후 망가진 걸 감지 못 한다.
- **DNS 는 라이브러리마다 구현체가 다르다**. Java 기본 InetAddress vs Netty DnsNameResolver vs getaddrinfo — 각자 `/etc/hosts` / NSS / DNS 서버 우선순위가 달라서 "A 는 되는데 B 는 안 됨" 현상이 쉽게 발생. Docker + Tailscale 조합에선 특히 주의.
- **DB 가 비어있을 때 인프라 성숙도 작업을 몰아서 하는 게 정답**. Flyway 전환, 스키마 덤프, 권한 구조 검토 — 데이터 들어오면 전부 난이도 2-3 배.
- **첫 유닛 테스트 1개의 심리적 문턱**이 제일 크다. 일단 `ApiResponseTest` 같은 걸 심어두면 다음 사람은 패턴 복붙으로 쉽게 추가.

## 변경 이력

- 2026-04-22 · 최초 작성 (Flyway 도입 / Actuator health / Redis DNS extra_hosts / CI 테스트 자동화)
