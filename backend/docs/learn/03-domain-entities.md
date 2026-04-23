# 챕터 3 · 엔티티 — 자바 객체와 DB 는 어떻게 이어져 있나

> 목표: `User` 라는 자바 클래스 한 개가 어떻게 DB `users` 테이블이 되고, 왜 이런 식으로 코드를 짜는지 이해하기.

읽고 나면 답할 수 있는 것:
- **엔티티(Entity)** 가 정확히 뭐야
- 자바 클래스가 **어떻게 DB 테이블과 이어지는가** (JPA, ORM)
- 우리가 쓰는 **BaseEntity, @Builder, PROTECTED 생성자** 패턴은 왜?
- **`Instant`** 를 쓰는 이유 (`LocalDateTime` 말고)
- **비즈니스 메서드** 가 엔티티에 있는 이유

---

## 1. 알아둘 기초

### 1-1. "엔티티" 가 뭐야?

**엔티티 = DB 테이블 하나에 대응하는 자바 클래스**.

예:

DB 에 이런 테이블이 있다고 하면:
```
users 테이블
┌────┬──────────┬──────────────┐
│ id │ nickname │ email        │
├────┼──────────┼──────────────┤
│ 1  │ 민욱     │ min@aa.com   │
│ 2  │ 현지     │ hj@bb.com    │
└────┴──────────┴──────────────┘
```

이에 대응하는 자바 엔티티:
```java
@Entity
@Table(name = "users")
public class User {
    @Id
    private Long id;
    private String nickname;
    private String email;
}
```

같은 의미지만 표현 방식이 다른 것뿐:
- **DB 세계** = 행(row) + 열(column). 관계형.
- **자바 세계** = 객체 + 필드. 객체지향.

### 1-2. "ORM" 과 "JPA" 가 뭐야?

**ORM** = **Object-Relational Mapping**. 자바 객체 ↔ DB 테이블을 자동으로 왕복시켜주는 기술.

비유: **통역사**.
- 자바 개발자는 자바어로 말함 (`userRepository.save(user)`)
- DB 는 SQL어로 들음 (`INSERT INTO users (...) VALUES (...)`)
- **ORM 이 둘 사이에서 통역**

**JPA** = **Java Persistence API**. 자바의 ORM 표준 명세 (규격).
- 명세만 있으면 실제 동작은 못 함 → 구현체 필요
- **Hibernate** 가 가장 유명한 JPA 구현체. 우리도 이걸 씀.
- **Spring Data JPA** = JPA 를 더 쉽게 쓰게 해주는 Spring 의 래퍼. Repository 자동 생성 등.

우리가 실제로 쓰는 것: **Spring Data JPA + Hibernate + PostgreSQL**.

### 1-3. 기본적인 어노테이션 몇 개

엔티티 클래스에 붙는 마법 같은 `@어쩌고` 들:

| 어노테이션 | 뜻 |
|---|---|
| `@Entity` | "나 DB 테이블이야" |
| `@Table(name = "users")` | "테이블 이름은 users" (생략 시 클래스명 사용) |
| `@Id` | "이 필드가 Primary Key (고유 식별자)" |
| `@GeneratedValue(strategy = IDENTITY)` | "id 는 DB 가 자동 증가로 만들어줘" |
| `@Column(nullable = false, length = 100)` | 컬럼 세부 설정 |
| `@ManyToOne`, `@OneToMany` | 다른 엔티티와의 관계 |

---

## 2. 우리 프로젝트 엔티티의 공통 형태

`User` 한 개 놓고 하나씩 분석해봅시다. 우리 프로젝트의 **모범 엔티티**([`exemplars/User.java`](../architecture/exemplars/User.java)) 를 간소화하면:

```java
@Entity
@Table(name = "users")
@Getter                                            // ③ Lombok: getter 자동 생성
@NoArgsConstructor(access = AccessLevel.PROTECTED) // ④ 기본 생성자는 protected
public class User extends BaseEntity {              // ① BaseEntity 상속

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, length = 50)
    private String nickname;

    @Column(nullable = false, unique = true, length = 255)
    private String email;

    private Instant lastLoginAt;                    // ② Instant (LocalDateTime 아님)
    private Instant deletedAt;

    @Builder                                        // ⑤ Builder 패턴
    private User(String nickname, String email) {
        this.nickname = nickname;
        this.email = email;
    }

    // ⑥ 비즈니스 메서드
    public void updateProfile(String newNickname) {
        this.nickname = newNickname;
    }

    public void markLogin() {
        this.lastLoginAt = Instant.now();
    }

    public void delete() {
        this.deletedAt = Instant.now();
    }
}
```

6 가지 특징 하나씩 살펴봅니다.

### ① BaseEntity 상속

많은 엔티티가 공통으로 갖는 필드(`createdAt`, `updatedAt`) 를 부모 클래스에 모아둠. 모든 테이블에 "생성/수정 시각" 이 있으니까 반복 제거.

```java
// BaseEntity.java (의사코드)
@MappedSuperclass
@EntityListeners(AuditingEntityListener.class)
public abstract class BaseEntity {

    @CreatedDate
    @Column(updatable = false)
    private Instant createdAt;

    @LastModifiedDate
    private Instant updatedAt;
}
```

- `@MappedSuperclass` = 이 클래스 자체는 테이블 없음. 자식 엔티티가 필드 상속해감.
- `@EntityListeners(AuditingEntityListener.class)` = 자동으로 생성/수정 시각 기록.

즉 `User` 를 저장하면:
- INSERT 시 createdAt 자동 세팅
- UPDATE 시 updatedAt 자동 갱신
- 개발자는 이 필드 직접 건드릴 필요 없음

### ② `Instant` 사용 (왜 `LocalDateTime` 아닌가)

**`Instant`** = 타임존 없는 "절대 순간" (UTC 기준).
**`LocalDateTime`** = 타임존 없는 "어떤 지역의 시간" (애매).

```java
LocalDateTime.now()  // 지금이 10:30인데 이게 서울 시간인지 UTC인지 코드만 봐선 모름
Instant.now()        // 이건 정확히 UTC 기준 "이 순간" 이라고 정의됨
```

### 왜 Instant 가 안전한가

시나리오: 서버가 이사 감
- 이사 전: AWS 서울 (KST 시간대)
- 이사 후: AWS 오레곤 (PST 시간대)

DB 에 `LocalDateTime` 을 쓰면:
- 이사 전 데이터는 KST 로 해석되던 거
- 이사 후 신규 저장은 PST 로 해석됨
- → **같은 컬럼 안에 두 가지 시간이 섞임**. 재앙.

`Instant` 는 모두 UTC 기준이라 서버 위치가 어디든 같은 의미.

> 모바일 앱이 여러 국가에서 쓰일 가능성 생각하면 더 그렇습니다. 한국 유저 / 미국 유저가 같은 시각에 행동해도 서버는 둘 다 UTC 로 저장. 화면에 보여줄 때만 각자 지역 시간으로 변환.

우리 프로젝트는 [2026-04-21 리팩토링](../study/2026-04-21-feat-be-refactoring.md)에서 `LocalDateTime` → `Instant` 로 전환.

### ③ `@Getter` (Lombok)

Lombok = 자바의 반복 코드(boilerplate)를 줄여주는 라이브러리.

`@Getter` 없으면:
```java
public String getNickname() { return nickname; }
public String getEmail() { return email; }
public Instant getLastLoginAt() { return lastLoginAt; }
// ... 필드마다 반복
```

`@Getter` 한 줄로 모두 자동 생성. Lombok 이 컴파일 시점에 삽입.

### ④ `@NoArgsConstructor(access = PROTECTED)`

"기본 생성자를 protected 로 만든다."

**왜?**
- JPA 는 엔티티를 DB 에서 가져올 때 **"빈 객체 만들고 → 필드 채우기"** 순서. 즉 기본 생성자가 필요.
- 그렇다고 `public` 으로 열어두면 외부에서 `new User()` 로 빈 객체를 쉽게 만들 수 있음 → 유효하지 않은 상태의 유저가 돌아다닐 위험.
- `protected` 면 외부에선 호출 불가. JPA / 같은 패키지만 사용 가능 → **"엔티티 생성은 Builder 로 해"** 강제.

### ⑤ `@Builder` 패턴

객체 생성을 읽기 쉽게 만들어주는 기법.

**Builder 없이**:
```java
new User("민욱", "min@aa.com");
// 파라미터 순서를 외워야 함. 필드 많으면 실수 유발
new Book("해리포터", "소설", "Rowling", 2000, "어린이", "9788984312...");
// 어느 게 저자? 장르? 알기 어려움
```

**Builder 로**:
```java
User user = User.builder()
    .nickname("민욱")
    .email("min@aa.com")
    .build();
// 무엇에 무엇을 넣는지 명시적
```

- `@Builder` 를 private 생성자에 붙이면 → `.builder()` static 메서드 자동 생성
- 필드 순서 외울 필요 없음
- 옵셔널 필드 생략 쉬움

### ⑥ 비즈니스 메서드

엔티티는 **데이터 + 그 데이터에 대한 행동** 을 담는 게 이상적.

나쁜 예:
```java
// Service 가 전부 처리
user.setNickname("새닉네임");
user.setUpdatedAt(Instant.now());
userRepo.save(user);
```

좋은 예:
```java
// User 엔티티가 자기 상태 관리
user.updateProfile("새닉네임");  // ← 내부에서 nickname 변경
userRepo.save(user);
```

이걸 **"풍부한 도메인 모델"** 이라고 부름. 이렇게 하는 이유:
- 유효성 검증 로직이 엔티티 안에 캡슐화됨 (외부에서 이상한 값 못 넣음)
- "유저가 할 수 있는 일" 이 메서드 목록으로 깔끔히 드러남
- 테스트 쓰기 편함

예시:
```java
public void delete() {
    this.deletedAt = Instant.now();    // 물리 삭제 X, soft delete
}

public void markLogin() {
    this.lastLoginAt = Instant.now();
}

public boolean isDeleted() {
    return deletedAt != null;
}
```

---

## 3. 우리 프로젝트의 엔티티 규칙 (patterns.md 요약)

[patterns.md](../architecture/patterns.md) 와 [checklist](../architecture/checklist.md) 에 정리돼 있는 합의:

1. **기본 필드는 BaseEntity 상속** 으로 createdAt/updatedAt 자동화
2. **시간 필드는 모두 `Instant`**
3. **기본 생성자 `PROTECTED`** — JPA 용, 외부 접근 차단
4. **생성은 `@Builder`** — `new User(...)` 금지
5. **비즈니스 메서드로 상태 변경** — setter 남발 금지
6. **물리 삭제 대신 soft delete** — `deletedAt` 세팅, row 는 남김
7. **외래키 매핑은 `@ManyToOne(fetch = LAZY)`** — 성능상 기본. Eager 는 꼭 필요할 때만
8. **DTO ↔ Entity 변환은 Service 계층에서** — 컨트롤러는 DTO 만 다룸

---

## 4. 관계 매핑 — `@ManyToOne` / `@OneToMany` 간단히

엔티티끼리 "누가 누구 소유" 같은 관계를 표현.

예: 서재(Bookshelf) 는 유저(User) 한 명 소유.

```java
@Entity
public class Bookshelf extends BaseEntity {

    @Id
    @GeneratedValue(strategy = IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)        // ← 관계 매핑
    @JoinColumn(name = "user_id", nullable = false)
    private User user;

    private String title;
}
```

DB 에선:
```
bookshelves 테이블
┌────┬─────────┬──────────────┐
│ id │ user_id │ title        │   ← user_id 가 users.id 를 가리키는 외래키
├────┼─────────┼──────────────┤
│ 1  │ 1       │ 소설책장     │
│ 2  │ 1       │ 에세이책장   │
└────┴─────────┴──────────────┘
```

**`fetch = LAZY`** = "User 객체는 실제로 쓸 때만 DB 에서 가져와." 미리 안 가져오면 쿼리 수가 줄어듦.

### N+1 문제 — 조심할 함정

```java
List<Bookshelf> shelves = bookshelfRepo.findAll();
for (Bookshelf s : shelves) {
    System.out.println(s.getUser().getNickname());  // ← LAZY 니까 여기서 매번 쿼리
}
```

서재 100 개 있으면 쿼리 **1 (서재) + 100 (유저) = 101 회**. 이게 N+1.

해결: **fetch join** 또는 **Repository Custom** 에서 JOIN 을 명시.

```java
// QueryDSL 예시
queryFactory
    .selectFrom(bookshelf)
    .join(bookshelf.user, user).fetchJoin()   // ← 한 쿼리로 JOIN
    .fetch();
```

이건 [챕터 4](./04-data-persistence.md) 에서 다룸.

---

## 5. 자주 헷갈리는 것들

### Q. `setId()` 가 없는데 어떻게 id 가 채워지나?
- DB 가 저장 시 자동 생성 (`@GeneratedValue(IDENTITY)` 덕분)
- JPA 가 INSERT 후 생성된 id 를 받아 엔티티에 몰래 세팅 (리플렉션으로)
- 그래서 `setId` 가 public 일 필요 없음

### Q. `@Entity` 에는 setter 가 없어도 되는 거야?
- **없는 게 좋음**. 있으면 아무 데서나 값 바꿀 수 있어 데이터 일관성 깨지기 쉬움.
- 변경은 비즈니스 메서드를 통해. 예: `user.updateProfile(...)`

### Q. 엔티티를 Controller 응답으로 그대로 보내도 돼?
- **안 됨**. DTO 로 변환해서 보내야 함.
- 이유: 엔티티에 있는 민감 필드 (예: password 해시) 가 JSON 에 섞일 위험. 그리고 API 스펙이 DB 구조랑 묶이면 DB 바꿀 때 API 깨짐.
- 우리 코드에선 `UserDtos.UserResponse` 같은 별도 DTO 클래스를 둠.

### Q. 엔티티에 로직이 들어가면 서비스는 뭘 하지?
- 서비스는 **여러 엔티티 간 조율, 트랜잭션, 외부 API 호출** 을 함.
- 엔티티 하나 안의 변경은 엔티티가, 여러 엔티티 + 외부 I/O 는 서비스가.
- 예: "책 구매" 는 `User.deductPageBalance()` + `Book.decreaseStock()` + 결제 로그 저장. 이 조합은 Service 가.

### Q. `@Id` 는 왜 `Long` 을 많이 써? `UUID` 쓰면 안 돼?
- `Long` (auto increment) — 숫자라 인덱스 효율 ↑, 순차 저장
- `UUID` — 서버 여러 대에서 ID 충돌 없이 생성 가능. 단 랜덤이라 인덱스 비효율
- 우리 스케일엔 `Long` 충분.
- refresh token 처럼 추측 못 하게 해야 하는 경우만 UUID.

### Q. `@Getter` 는 있는데 `@Setter` 는 왜 없어?
- 위 답과 같음. 외부에서 마음대로 상태 바꾸는 걸 막고, 비즈니스 메서드를 통해서만 변경시키려고.

---

## 6. 우리 프로젝트의 엔티티 분포

총 **34개 엔티티** 를 도메인별로 분리:

| 도메인 | 주요 엔티티 | 의미 |
|---|---|---|
| `user` | `User`, `Follow`, `UserBlock`, `UserFeedSetting`, `UserNotificationSetting` | 유저 기본 + 관계 + 설정 |
| `auth` | `AuthRefreshToken`, `UserSocialAccount`, `GuestMigrationImport` | 인증/OAuth |
| `book` | `Book`, `UserBook` | 책 + 유저가 등록한 책 |
| `bookshelf` | `Bookshelf`, `BookshelfSection`, `BookshelfItem` | 서재 + 칸 + 칸 안의 책 |
| `space` | `Space`, `SpaceTemplate`, `SpaceLayoutSnapshot`, `SpaceLike`, `SpaceVisit` | 방/꾸밈/방문 기록 |
| `readingrecord` | `ReadingRecord`, `ReadingRecordComment`, `ReadingRecordLike`, `ReadingRecordCommentLike` | 독서기록 + 댓글 + 좋아요 |
| `feed` | `FeedEvent`, `BookPromotion` | 피드 이벤트 |
| `asset` | `Asset`, `AssetCategory`, `AssetPurchase`, `AssetWishlist`, `UserAssetInventory` | 아이템(가구 등) 상점 |
| `payment` | `PageWallet` | 결제 |
| `notification` | `Notification` | 알림 |
| `guestbook` | `Guestbook`, `GuestbookReply` | 방명록 |
| `report` | `Report` | 신고 |

→ 하나의 앱이 이 많은 도메인을 조합해서 "서재 꾸미기 + 독서기록 + 소셜" 을 구성.

---

## 7. 실제 코드에서 확인하기

| 개념 | 파일 |
|---|---|
| 모범 엔티티 | [`docs/architecture/exemplars/User.java`](../architecture/exemplars/User.java) |
| 실제 `User` | `src/main/java/com/dotshelf/domain/user/entity/User.java` |
| `BaseEntity` | `src/main/java/com/dotshelf/global/common/BaseEntity.java` |
| 관계 매핑 예시 | `src/main/java/com/dotshelf/domain/bookshelf/entity/Bookshelf.java` |
| 컨벤션 문서 | [`docs/architecture/patterns.md`](../architecture/patterns.md) |
| 셀프 리뷰 | [`docs/architecture/checklist.md`](../architecture/checklist.md) |

---

## 8. 챕터 3 정리

- ✅ 엔티티 = DB 테이블 하나에 대응하는 자바 클래스
- ✅ JPA / Hibernate 가 자바 ↔ SQL 자동 통역
- ✅ `BaseEntity` 상속으로 createdAt/updatedAt 공통 처리
- ✅ 시간은 **`Instant`** (타임존 독립)
- ✅ 기본 생성자 **PROTECTED** + **`@Builder`** 조합으로 안전한 객체 생성
- ✅ 비즈니스 메서드로 상태 변경 (setter 남발 금지)
- ✅ 관계 매핑은 `@ManyToOne(LAZY)` 기본, N+1 조심
- ✅ 엔티티 ↔ DTO 는 반드시 분리

다음 챕터 👉 [**챕터 4: 데이터 — 저장과 조회**](./04-data-persistence.md)

엔티티가 정의됐으니 이제 "Repository", "QueryDSL", "Flyway" 로 실제 DB 와 어떻게 상호작용하는지 봅니다.
