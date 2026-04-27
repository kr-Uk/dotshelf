# 챕터 4 · 데이터 — 저장과 조회

> 목표: 엔티티를 실제 DB 에 저장하고 원하는 형태로 꺼내오는 기술들 (Repository / QueryDSL / Flyway) 이 각각 뭐 하는지 이해하기.

읽고 나면 답할 수 있는 것:
- **Repository** 가 뭐고 왜 필요한가
- `JpaRepository` 가 공짜로 주는 기능
- 복잡한 쿼리가 필요할 때 **QueryDSL** 을 왜 쓰나
- `Repository Custom + Impl` 패턴의 의도
- **Flyway** 가 스키마 변경을 어떻게 관리하나
- 엔티티를 바꿀 때 반드시 해야 할 일

---

## 1. 알아둘 기초

### 1-1. "Repository" 가 뭐야?

Repository = **DB 와 대화하는 전담 창구**.

비유: 회사의 **서류 담당 비서**.
- 내가 직접 서류철 뒤지는 게 아니라, 비서에게 "홍길동 파일 찾아줘" 라고 부탁
- 비서가 창고 가서 꺼내 옴
- 나는 서류가 어느 선반에 있는지 신경 안 써도 됨

코드로 보면:
```java
// 나쁜 예: Service 가 JDBC 직접
String sql = "SELECT * FROM users WHERE id = ?";
PreparedStatement ps = conn.prepareStatement(sql);
ps.setLong(1, 1);
ResultSet rs = ps.executeQuery();
// ... SQL 처리 코드 지저분
```

```java
// 좋은 예: Repository 에게 맡김
User user = userRepository.findById(1L).orElseThrow();
```

Service 는 **"DB 가 어떻게 생겼는지"** 몰라도 됨. 그냥 Repository 에 부탁.

### 1-2. Spring Data JPA 가 주는 "공짜 메서드"

`JpaRepository<User, Long>` 인터페이스만 상속해도 **기본 CRUD 메서드** 가 자동 구현됨.

```java
public interface UserRepository extends JpaRepository<User, Long> {
    // 비어있지만 아래 메서드들이 이미 있음!
}
```

자동으로 쓸 수 있는 것:
- `save(user)` — INSERT or UPDATE
- `findById(id)` — SELECT ... WHERE id = ?
- `findAll()` — SELECT *
- `delete(user)` — DELETE
- `count()` — COUNT(*)
- `existsById(id)` — EXISTS

Spring Data 가 **런타임에 인터페이스를 읽어서 구현체를 만들어냄**. 개발자는 인터페이스만 선언하면 됨.

### 1-3. 메서드 이름으로 쿼리 만들기

Spring Data 의 마법 2탄: **메서드 이름 규칙대로 쓰면 알아서 쿼리 생성**.

```java
public interface UserRepository extends JpaRepository<User, Long> {
    Optional<User> findByEmail(String email);                  // SELECT * FROM users WHERE email = ?
    List<User> findByNicknameContaining(String keyword);       // WHERE nickname LIKE '%keyword%'
    List<User> findByDeletedAtIsNull();                        // WHERE deleted_at IS NULL
    long countByCreatedAtAfter(Instant since);                 // COUNT WHERE created_at > ?
}
```

- `findBy`, `countBy`, `existsBy` 같은 접두어
- 뒤에 **필드명 + 조건** 을 이어붙임 (`And`, `Or`, `Between`, `GreaterThan` 등)

**장점**: 간단한 쿼리는 메서드 이름만 쓰면 끝.
**단점**: 복잡해지면 메서드 이름이 괴물이 됨 (`findByUserAndCreatedAtBetweenAndDeletedAtIsNullOrderByCreatedAtDesc`).
→ 그래서 복잡한 쿼리는 **QueryDSL** 로 쓴다.

---

## 2. QueryDSL — 복잡한 쿼리를 자바로

### 2-1. 왜 필요한가

서재 피드를 조회한다고 가정:
- 로그인 유저가 팔로우한 사람들의 서재
- 공개 설정된 것만
- 최근 수정된 순
- 페이지네이션 (20개씩)

이걸 메서드 이름으로 쓰면:
```
findByUserInAndVisibilityAndDeletedAtIsNullOrderByUpdatedAtDesc(...)
```

이 시점에 **SQL 직접 짜는 게 더 읽기 쉬움**. 근데 그냥 SQL 쓰면:
- 문법 오류가 런타임에야 드러남 (컴파일 체크 X)
- 리팩토링(필드명 변경) 하면 전부 수동 업데이트
- 타입 안전성 없음

**QueryDSL = SQL 을 자바 문법으로 쓰게 해주는 도우미**.

### 2-2. QueryDSL 맛보기

```java
// 팔로우한 사람들의 공개 서재를 최근순으로 20개
BooleanExpression condition = bookshelf.user.in(followees)
    .and(bookshelf.visibility.eq(PUBLIC))
    .and(bookshelf.deletedAt.isNull());

List<Bookshelf> result = queryFactory
    .selectFrom(bookshelf)
    .where(condition)
    .orderBy(bookshelf.updatedAt.desc())
    .limit(20)
    .fetch();
```

- 자바 코드처럼 생김
- **컴파일 시점에 필드 이름 오타 감지**
- IDE 자동완성 작동
- `bookshelf.user` 같은 건 QueryDSL 이 엔티티 클래스를 보고 **자동 생성한 `QBookshelf` 클래스**

### 2-3. QueryDSL 셋업 (우리 프로젝트)

`build.gradle.kts` 에 이렇게 들어가 있음:
```kotlin
implementation("io.github.openfeign.querydsl:querydsl-jpa:7.1")
annotationProcessor("io.github.openfeign.querydsl:querydsl-apt:7.1:jakarta")
```

- 빌드하면 `build/generated/querydsl/` 밑에 `QUser.java`, `QBookshelf.java` 같은 파일이 자동 생성됨
- 이 `Q-클래스` 들이 QueryDSL 쿼리에서 필드 참조용으로 쓰임
- `./gradlew build` 할 때마다 엔티티에 맞춰 재생성

---

## 3. 우리 프로젝트의 Repository 패턴

### 3-1. 단순한 경우: `JpaRepository` 만 상속

```java
public interface UserRepository extends JpaRepository<User, Long> {
    Optional<User> findByEmail(String email);
    Optional<User> findByKakaoId(String kakaoId);
}
```

이 정도면 충분.

### 3-2. 복잡한 경우: `Custom + Impl` 패턴

복잡한 QueryDSL 쿼리가 필요할 때 3-개 파일 구조로:

```
UserRepository.java          ← Spring Data JPA 인터페이스
UserRepositoryCustom.java    ← 우리가 직접 추가할 메서드의 "약속"
UserRepositoryImpl.java      ← QueryDSL 로 실제 구현
```

```java
// UserRepository.java
public interface UserRepository extends JpaRepository<User, Long>, UserRepositoryCustom {
}

// UserRepositoryCustom.java
public interface UserRepositoryCustom {
    List<User> findActiveFollowees(Long userId, CursorRequest cursor);
}

// UserRepositoryImpl.java
@RequiredArgsConstructor
public class UserRepositoryImpl implements UserRepositoryCustom {

    private final JPAQueryFactory queryFactory;

    @Override
    public List<User> findActiveFollowees(Long userId, CursorRequest cursor) {
        return queryFactory
            .selectFrom(user)
            .join(follow).on(follow.followeeId.eq(user.id))
            .where(follow.followerId.eq(userId),
                   user.deletedAt.isNull(),
                   cursorCondition(cursor))
            .orderBy(user.id.desc())
            .limit(20)
            .fetch();
    }
}
```

**왜 이런 구조?**
- Spring Data 가 `UserRepository` 를 구현할 때 **`Impl` 로 끝나는 클래스를 자동으로 끼워 붙여줌**
- 그래서 Service 는 `UserRepository` 만 주입하면 `findById` (기본) + `findActiveFollowees` (커스텀) 둘 다 쓸 수 있음
- 파일 이름 규칙(`Custom`, `Impl`) 은 **중요**. 이름 틀리면 자동 연결 안 됨.

### 3-3. 왜 CA(Clean Architecture) 까지 안 갔나

[2026-04-21 리팩토링 학습 노트](../study/2026-04-21-feat-be-refactoring.md) 에서 공식화:

- CA 는 "JPA 에 종속되지 않게 DB 계층을 포트/어댑터로 분리" 가 핵심
- 우리는 프레임워크 교체 계획 없음 → 그 비용 불필요
- 대신 **"Repository Custom + Impl"** 로 "복잡 쿼리 캡슐화" 라는 CA 의 실용적 부분만 취함

패턴 문서: [patterns.md §1](../architecture/patterns.md)

---

## 4. Flyway — 스키마 변경을 관리하기

### 4-1. 문제 상황

개발하다 보면 **엔티티 구조를 바꾸는 일** 이 자주 생김:
- `nickname` 필드 추가
- `email` 최대 길이 100 → 255
- 더 이상 안 쓰는 `legacy_phone` 컬럼 삭제
- 새 테이블 `bookmarks` 추가

**매번 누가 어디서 DB 를 어떻게 고칠까?**

- 로컬 DB? 프로덕션 DB? 테스트 DB?
- 전 팀원이 다 똑같이 적용했나?
- 새 서버 세웠을 때 어떤 순서로 적용해야 되나?
- 실수로 같은 변경을 두 번 돌리면?

→ 이걸 체계적으로 해결하는 도구가 **Flyway**.

### 4-2. 비유: DB 의 Git

Git 이 코드 변경 이력을 관리하듯, Flyway 는 **스키마 변경 이력** 을 관리:

```
src/main/resources/db/migration/
├── V1__init_schema.sql            ← 최초 상태 (테이블 34개 생성)
├── V2__add_nickname_to_user.sql    ← 2번째 변경: 유저에 닉네임 컬럼 추가
├── V3__add_bookmark_table.sql       ← 3번째 변경: 북마크 테이블 추가
└── V4__drop_legacy_phone.sql        ← 4번째 변경: 불필요 컬럼 삭제
```

각 파일이 **"한 단계의 변경"** 을 SQL 로 기술.

### 4-3. Flyway 가 자동으로 하는 일

앱이 기동할 때:

```
1. DB 에 접속
2. flyway_schema_history 테이블이 있나 확인 (없으면 만듦)
3. 지금까지 적용된 버전을 기록에서 읽음 (예: "V2 까지 적용됨")
4. resources/db/migration/ 를 스캔 → "아 V3, V4 아직 안 돌았네"
5. V3, V4 를 순서대로 실행
6. 기록에 "V3, V4 적용 완료" 추가
7. 완료 → 이후 앱 정상 기동
```

즉 **"어디서든 앱 기동 = DB 스키마가 최신"** 이 보장됨. 개발자가 "DB 마이그레이션 스크립트 돌려야 해" 외칠 일 없음.

### 4-4. flyway_schema_history 테이블

Flyway 가 자체적으로 만드는 기록 테이블:

```
flyway_schema_history
┌────────────────┬─────────┬────────────────────────┬──────┬─────────┐
│ installed_rank │ version │ description            │ type │ success │
├────────────────┼─────────┼────────────────────────┼──────┼─────────┤
│ 1              │ 1       │ init schema            │ SQL  │ t       │
│ 2              │ 2       │ add nickname to user   │ SQL  │ t       │
└────────────────┴─────────┴────────────────────────┴──────┴─────────┘
```

- `success = t` 이면 정상 적용 완료
- `f` 이면 실패한 마이그레이션 → 앱 기동 차단 (수동 개입 필요)

### 4-5. 체크섬 — "이미 돌린 파일은 바뀌면 안 됨"

Flyway 는 각 V 파일의 **체크섬(checksum)** 을 기록. 이미 적용된 V2 파일 내용을 나중에 수정하면:

```
Validate failed: Migration checksum mismatch for migration V2
```

앱 기동 실패. 이유:
- 이미 DB 에 적용된 변경이니까 다시 적용 불가
- 내용이 바뀌면 "뭐가 진짜인지" 모호해짐

**대응 규칙**: **이미 머지된 V 파일은 절대 수정 X**. 고칠 일 생기면 **새 V 파일** (예: `V5__fix_v2_column_type.sql`) 로 보정.

### 4-6. `ddl-auto: validate` 와 짝꿍

우리 `application-prod.yml`:
```yaml
spring:
  jpa:
    hibernate:
      ddl-auto: validate    # ← Flyway 가 스키마 소유. Hibernate 는 검증만.
```

세 가지 옵션 중:
- `create` — 기동 시마다 스키마 재생성 (데이터 날림) — 테스트용
- `update` — 엔티티 보고 알아서 ALTER 수행 — 편하지만 제멋대로라 위험
- **`validate`** — 엔티티 ↔ DB 정합성만 확인. 불일치 시 기동 실패 — **우리 선택**

왜 validate?
- Flyway 가 스키마 소유 → Hibernate 가 맘대로 고치면 안 됨
- 엔티티 변경하고 V 파일 안 만든 실수를 **기동 시점에 잡아줌** (안 그럼 런타임에 쿼리 실패)

### 4-7. 엔티티 수정 시 반드시 하는 것

```
엔티티 수정             + 마이그레이션 파일 추가         = ✅ 정상
(User 에 nickname 추가)  (V2__add_nickname.sql)

엔티티만 수정           + V 파일 없음                    = ❌ validate 실패 → 앱 기동 X
V 파일만 추가           + 엔티티 반영 안 함               = ❌ 런타임에 Hibernate 가 컬럼 매핑 못 함
```

**짝으로 커밋** 이 철칙.

---

## 5. 실제 흐름 예시

"유저에 `bio` (자기소개) 필드를 추가" 가 업무라고 치면:

### 1단계: 엔티티 수정
```java
// User.java
@Column(length = 500)
private String bio;
```

### 2단계: 마이그레이션 파일 작성
```sql
-- V5__add_bio_to_user.sql
ALTER TABLE users ADD COLUMN bio VARCHAR(500);
```

### 3단계: 로컬에서 테스트
```bash
cd backend
docker compose up -d
./gradlew bootRun
```

앱 기동 로그에 Flyway 가 V5 실행:
```
o.f.core.internal.command.DbMigrate - Migrating schema "public" to version "5 - add bio to user"
o.f.core.internal.command.DbMigrate - Successfully applied 1 migration
```

### 4단계: 비즈니스 로직 추가
```java
public void updateBio(String newBio) {
    this.bio = newBio;
}
```

### 5단계: Repository/Service/Controller 가 이 필드를 쓰는 코드

### 6단계: 커밋 + 푸시
- `git add src/main/java/...User.java`
- `git add src/main/resources/db/migration/V5__add_bio_to_user.sql`
- 같이 커밋
- 푸시 → CI 가 테스트 → dev 머지 → 자동 배포
- 배포 시 Flyway 가 prod DB 에 V5 자동 적용

---

## 6. 자주 헷갈리는 것들

### Q. `JpaRepository` 인터페이스에 구현 클래스가 없는데 어떻게 동작해?
- Spring Data JPA 가 **런타임에 프록시(proxy) 클래스를 자동 생성**.
- 메서드 시그니처를 보고 쿼리를 유추해서 구현체를 내부적으로 만듦.
- 우리는 인터페이스만 선언하면 됨.

### Q. `findByEmail(email)` 이 여러 결과 반환하면 어떻게 돼?
- 반환 타입이 `Optional<User>` 면 하나만 기대 → 여러 개면 `IncorrectResultSize` 예외
- `List<User>` 로 하면 여러 개 허용
- 유일해야 할 경우는 DB 에 `UNIQUE` 제약 걸어두는 게 좋음 (`@Column(unique = true)`)

### Q. QueryDSL 의 `QUser` 는 어디서 오는 거야? 안 만들었는데?
- `./gradlew build` 또는 IDE 의 annotation processor 가 자동 생성
- 경로: `build/generated/querydsl/com/dotshelf/...`
- 실수로 안 생기면 `./gradlew clean build` 한 번 돌리기
- Git 에 안 올림 (`.gitignore` 로 제외)

### Q. Flyway 가 자동으로 스키마를 바꿔주면 `ddl-auto: update` 랑 똑같은 거 아니야?
- 다름.
- `update`: Hibernate 가 **자기 판단** 으로 스키마 변경 (삭제 무시 등 애매함)
- Flyway: **개발자가 명시한 SQL** 만 실행. 정확하고 추적 가능
- "엔티티 보고 추측" vs "내가 쓴 SQL 명시적 실행" 의 차이

### Q. 이미 prod 에 올라간 V3 에 오타가 있어서 수정하고 싶어
- **절대 V3 를 수정하지 말 것** (체크섬 실패)
- 대신 `V4__fix_typo_in_v3.sql` 을 만들어 교정
- "DB 마이그레이션은 일방향" 이 원칙. 뒤로 돌아가지 않고 앞으로 덮어씀.

### Q. 여러 명이 동시에 V 파일을 만들면?
- 민욱이 `V5__a.sql` 작성, 현지도 `V5__b.sql` 작성 → 충돌
- 늦게 머지하는 쪽이 `V6` 으로 번호 조정
- 팀 컨벤션으로 "V 파일 번호는 MR 올릴 때 최종 확정" 같은 룰 권장

### Q. 테스트 DB 에도 Flyway 가 돌아?
- 돌게 할 수 있음. Testcontainers 로 실제 PG 띄우고 Flyway 적용 → 검증용.
- 우리는 아직 Testcontainers 도입 안 함. TODO.

---

## 7. 실제 코드에서 확인하기

| 개념 | 파일 |
|---|---|
| 단순 Repository | `src/main/java/com/dotshelf/domain/user/repository/UserRepository.java` |
| QueryDSL 커스텀 예시 | `src/main/java/com/dotshelf/domain/user/repository/UserRepositoryImpl.java` (있다면) |
| 베이스 마이그레이션 | [`src/main/resources/db/migration/V1__init_schema.sql`](../../src/main/resources/db/migration/V1__init_schema.sql) |
| Flyway 설정 | [`src/main/resources/application-prod.yml`](../../src/main/resources/application-prod.yml) (spring.flyway 블록) |
| 로컬 설정 | [`src/main/resources/application-local.yml`](../../src/main/resources/application-local.yml) |
| 커스텀 패턴 공식 문서 | [`docs/architecture/patterns.md §3`](../architecture/patterns.md) |

---

## 8. 챕터 4 정리

- ✅ Repository = DB 와 대화하는 창구. Service 가 SQL 몰라도 됨
- ✅ `JpaRepository` 상속만으로 기본 CRUD 자동
- ✅ 메서드 이름 규칙으로 간단한 쿼리 자동 생성
- ✅ 복잡한 쿼리는 QueryDSL 로 타입 안전하게
- ✅ Custom + Impl 패턴으로 커스텀 쿼리를 인터페이스에 묶음
- ✅ Flyway = DB 의 Git. `V숫자__설명.sql` 파일로 스키마 버전 관리
- ✅ `ddl-auto: validate` + Flyway 조합으로 스키마 드리프트 방지
- ✅ 엔티티 변경 시 **V 파일 짝지어 커밋** 이 철칙

다음 챕터 👉 [**챕터 5: 에러가 나면 어떻게 되나**](./05-error-handling.md)

지금까지 "정상 흐름" 을 봤으니, 이제 **예외 상황** 을 어떻게 일관되게 처리하는지를 다룹니다.
