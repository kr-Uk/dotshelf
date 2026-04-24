# 챕터 1 · 요청이 걸어가는 길

> 목표: "사용자가 앱에서 버튼 한 번 눌렀을 때, 서버에서 답이 돌아오기까지 안에서 뭐가 일어나는가" 를 머릿속에 한 장의 그림으로 그리기.

읽고 나면 아래 질문에 답할 수 있게 됩니다:
- 브라우저에 URL 을 치면 **처음 누가 받는지**
- **왜 Cloudflare 를 거치나**
- **왜 서버가 두 개인지**
- **"필터", "컨트롤러", "서비스", "레포지토리"** 가 무슨 순서로 일하는지

---

## 1. 알아둘 기초 3개

### 1-1. "HTTP 요청" 이 뭐야?

웹에서 **"야 나한테 이거 좀 줘봐"** 하고 말을 거는 방식입니다. 편지라고 생각하면 됨:

```
[편지 주소]  GET https://api.dotshelf.digital/api/books/1
[보내는 이]  내 브라우저 (IP: 121.160.xx.xx)
[편지 내용]  "/api/books/1" 번 책 정보를 달라
[봉투 끝]
```

- **GET** = 동사. "읽어 와" 라는 뜻. `POST`(만들어줘) / `PUT`(수정해) / `DELETE`(지워) 도 있음
- **URL** = "어디로 보낼지" 주소
- **Body** = 편지 본문. GET 은 보통 없고, POST 는 `{"title": "해리포터"}` 같은 내용 담아감

서버는 편지 받고 처리해서 **응답(response)** 을 돌려줍니다:

```
[응답 코드]  200 OK
[본문]      {"id": 1, "title": "해리포터", ...}
```

- 200 = 성공
- 404 = 못 찾음
- 500 = 서버 에러
- 401 = 인증 필요
- 403 = 권한 없음

### 1-2. "서버" 와 "포트" 가 뭐야?

**서버** = 요청 받으려고 **항상 켜져있는 컴퓨터**.
인터넷에서 컴퓨터를 찾으려면 **IP 주소** 라는 번호가 필요해요. 예: `3.38.240.110`.

같은 컴퓨터 안에서도 여러 프로그램이 돌 수 있으니까 **포트** 로 구분:

```
같은 IP (3.38.240.110) 안에
  :22   → SSH (원격 접속)
  :80   → Nginx (웹 HTTP)
  :443  → Nginx (웹 HTTPS)
  :5432 → PostgreSQL (DB)
```

**아파트 + 호수** 비유: IP = 아파트 주소, 포트 = 호수.

### 1-3. "DNS" 가 뭐야?

URL 에 있는 **`api.dotshelf.digital`** 같은 이름은 사람 읽기용입니다. 컴퓨터끼리 통신하려면 결국 숫자 IP 가 필요해요.

그 **이름 → 숫자 변환기** 를 **DNS** 라고 합니다.

```
"api.dotshelf.digital" 이 뭐야? → DNS 한테 물어봄
DNS 답: "3.38.240.110 이야"
```

**전화번호부** 비유: 이름으로 찾아서 번호를 알려주는 책.

---

## 2. 전체 여정 미리 보기

우리 프로젝트에서 **`curl https://api.dotshelf.digital/health`** 한 번이 갈 길:

```
 ①              ②                   ③                 ④
사용자  →   DNS 조회   →   Cloudflare 엣지   →   영구 서버 (prod)
                                                   │
                                                   │ (Tailscale)
                                                   ▼
                                               SSAFY 서버 (app) = Spring Boot
                                                   │
                                                   │ (Tailscale)
                                                   ▼
                                               영구 서버 DB/Redis
                                                   │
                                                   ▼
                                               응답 돌아감
```

8 단계라니 많아 보이죠? 각 단계가 각자의 이유가 있어요. 하나씩 가봅시다.

---

## 3. 단계별 자세히

### 단계 1 — 사용자가 주소 입력 / 버튼 클릭

모바일 앱의 "로그인" 버튼이든, 브라우저 주소창에 URL 타이핑이든, 똑같이 **HTTP 요청** 이 생성됩니다.

예:
```
GET https://api.dotshelf.digital/health
```

> **왜 `https://` ?**
> HTTPS = HTTP + 암호화. 중간에서 누가 엿봐도 내용을 못 알아봄. 요즘은 거의 HTTPS 가 기본.

### 단계 2 — DNS: 이름을 숫자로 바꾸기

브라우저: "어 `api.dotshelf.digital` 이 어딨지?"
→ OS 의 DNS 리졸버한테 물어봄
→ 최종적으로 **Cloudflare 의 DNS 서버** 에게 도달 (우리가 가비아에서 네임서버를 Cloudflare 로 지정해뒀으니까)
→ `api.dotshelf.digital` 에 연결된 IP 주소들이 돌아옴: `104.21.72.144`, `172.67.151.96` 등

이 IP 들은 **Cloudflare 엣지 서버** 들의 주소예요. 우리 진짜 서버는 아직 안 보임.

### 단계 3 — Cloudflare: 엣지에서 먼저 받음

요청은 가장 가까운 Cloudflare 데이터센터에 먼저 도착해요. Cloudflare 가 해주는 일:

- **TLS 종결**: 사용자 ↔ Cloudflare 구간 암호화 해제
- **WAF(방화벽)**: 공격 패턴(SQL injection 등) 이면 차단
- **DDoS 보호**: 비정상적인 대량 요청 필터링
- **캐싱**: 같은 요청이 최근에 있었으면 우리 서버 안 거치고 바로 응답
- **봇 차단**: 자동 공격 툴 차단

Cloudflare 가 "이 요청은 정상이고 우리 오리진까지 가야겠다" 판단하면:

→ **우리 영구 서버의 실제 IP(`3.38.240.110`) 로 다시 요청을 보냄**

이 구간도 암호화돼 있어요 (**Cloudflare Origin Certificate** 라는 걸 우리 서버에 미리 깔아둠).

> **왜 굳이 Cloudflare 를 거치나?**
> 우리 서버 IP 를 직접 노출하면 공격받기 쉬움. Cloudflare 뒤에 숨으면 공격이 Cloudflare 에서 걸러짐.
> 보디가드가 먼저 사람들을 만나고, 진짜 VIP(우리 서버)는 뒤에 있는 셈.

### 단계 4 — 영구 서버 도착: Nginx 가 먼저 받음

영구 서버에는 여러 프로그램이 돌고 있어요. 80/443 포트로 들어오는 웹 요청은 **Nginx** 라는 프로그램이 먼저 받습니다.

Nginx 가 하는 일:

1. **어느 도메인에 온 요청인지 확인** (`api.dotshelf.digital`)
2. 그 도메인에 맞는 **규칙** 을 찾음 (우리는 `/opt/dotshelf/nginx/conf.d/default.conf` 에 설정)
3. **"이 요청은 Spring Boot 앱으로 보내야겠다"** 판단
4. 앱 서버로 **다시 요청을 전달**

```nginx
# 설정 예시
server {
    server_name api.dotshelf.digital;
    location / {
        proxy_pass http://100.74.2.103:8080;  # ← SSAFY 서버의 Tailscale IP
    }
}
```

> **왜 Nginx 가 또 있나?**
> 1. **HTTPS 인증서 관리** 를 Nginx 가 담당 (앱이 직접 안 해도 됨)
> 2. **여러 도메인** 을 한 서버에서 처리 가능 (예: `api.dotshelf.digital`, `grafana.dotshelf.digital`)
> 3. **정적 파일** 은 Nginx 가 바로 응답 (앱 안 거쳐서 빠름)
> 4. Nginx 는 많은 연결을 효율적으로 처리 → **부하 분산**
> 프론트 데스크 있는 건물 비유: 손님이 직접 각 사무실 돌아다니는 게 아니라, 데스크에서 "3층 회의실로 가세요" 안내받는 구조.

### 단계 5 — Tailscale 터널: 영구 서버 → SSAFY 서버

Nginx 는 요청을 `100.74.2.103:8080` 로 보냅니다. 이 주소는 **SSAFY 서버** 인데, 공용 인터넷 IP 가 아니에요. **Tailscale 이라는 사설 네트워크 안의 주소**.

Tailscale = 두 서버 사이에 만든 **비밀 터널**. 이 터널 안에서만 이 IP 가 의미 있어요. 외부에선 `100.x.x.x` 로 누가 접속해도 안 닿음.

> **왜 이렇게 복잡하게?**
> DB 랑 앱이 **다른 서버에 있기 때문**. 두 서버가 "공용 인터넷" 을 거쳐 통신하게 두면, 누군가 포트 열어두면 공격받기 쉬움.
> Tailscale 이라는 사설 터널만 통하게 하면, 외부에선 아예 보이지도 않음.
> 왜 서버 2대 구조인지는 [infra doc](../infra/2026-04-22-tailscale-app-db-split.md) 참고.

### 단계 6 — Spring Boot: 실제로 일하는 곳

요청이 드디어 **우리가 쓴 자바 코드** 에 도달합니다. Spring Boot 안에서 일어나는 일은 크게 **필터 체인 → 컨트롤러 → 서비스 → 리포지토리** 순서.

```
요청 도착
  │
  ▼
[필터 체인]         ← 여러 필터가 차례로 요청을 검사
  ├─ MdcRequestIdFilter      ← 추적용 ID 찍기
  ├─ JwtAuthenticationFilter ← "너 누구야? 토큰 줘봐"
  └─ (Spring Security 기본 필터들)
  │
  ▼
[컨트롤러 Controller]       ← "이 URL 은 내가 처리!"
  │
  ▼
[서비스 Service]            ← 실제 비즈니스 로직
  │
  ▼
[리포지토리 Repository]     ← DB 에 질문 던지기
  │
  ▼
[DB]                        ← Tailscale 건너 영구 서버로
```

각 계층이 뭐 하는지:

#### 필터(Filter) — 입구 검색대

공항 보안 검색대처럼 **모든 요청이 지나야 하는 공통 검사**:

- `MdcRequestIdFilter` — 요청마다 고유 ID(`X-Request-Id`) 찍어줌. 나중에 로그 추적 편함. 비유: 주문서에 일련번호 붙이기
- `JwtAuthenticationFilter` — "너 로그인했어? JWT 토큰 내놔" → 토큰 없으면 401 응답. 비유: 입장권 검사

검색대를 통과하면 다음으로.

#### 컨트롤러(Controller) — 접수처

URL 마다 담당 직원이 있어요. 각 URL 에 어느 함수가 반응할지 미리 정의:

```java
// 예시
@RestController
@RequestMapping("/api/books")
public class BookController {

    @GetMapping("/{id}")
    public ApiResponse<BookDto> getBook(@PathVariable Long id) {
        // /api/books/1 요청이 오면 여기로
    }
}
```

컨트롤러는 "URL 과 메서드 연결" + "들어오는 값 받기/나가는 값 포장" 담당. **실제 일은 서비스한테 시킴**.

#### 서비스(Service) — 실무자

비즈니스 로직의 본체. "책 1번을 조회할 때 → 삭제된 책이면 null, 아니면 DTO 로 변환, 조회수 +1" 같은 판단을 여기서 함.

```java
@Service
public class BookService {

    public BookDto findById(Long id) {
        Book book = bookRepository.findById(id)
            .orElseThrow(() -> new BookNotFoundException());
        if (book.isDeleted()) throw new BookDeletedException();
        return BookDto.from(book);
    }
}
```

#### 리포지토리(Repository) — DB 전담 비서

DB 에 직접 말 거는 곳. SQL 을 직접 쓰지 않고, 자바 메서드만 호출하면 됨 (JPA 가 번역해줌).

```java
public interface BookRepository extends JpaRepository<Book, Long> {
    Optional<Book> findById(Long id);  // 자동으로 SELECT * FROM books WHERE id = ?
}
```

> **왜 이렇게 계층을 나누나?**
> "누가 뭘 하는지" 명확해서 코드 읽기 쉽고, 바꾸기 쉬움.
> 예: DB 를 PostgreSQL → MySQL 로 바꿔도, Repository 만 바꾸면 됨. Controller/Service 는 그대로.

### 단계 7 — DB 를 만나러 (다시 Tailscale)

Repository 가 DB 에 질의하려면 또 Tailscale 을 건너야 함. PG 가 영구 서버에 있으니까.

```
SSAFY Spring Boot → Tailscale → 영구 서버 PostgreSQL
```

DB 는 질의 받고 → 결과 행(row) 들을 돌려줌 → JPA 가 **자바 객체로 자동 변환** → Service 로.

### 단계 8 — 응답이 같은 길로 돌아감

이제 역순:

```
Service → Controller → (응답 포장) → Filter 를 다시 빠져나감
       → Spring Boot 반환 → Tailscale → Nginx
       → Cloudflare → 사용자
```

Controller 가 내보내는 데이터는 `ApiResponse` 라는 공통 포장지로 감싸짐 (여기선 자세히 안 다룸. [챕터 5](./05-error-handling.md) 에서).

---

## 4. 실제 예시: `/health` 호출하면?

간단한 헬스체크 요청을 따라가보면:

1. `curl https://api.dotshelf.digital/health`
2. DNS → Cloudflare IP 받음
3. Cloudflare → 통과 (캐시 안 되는 동적 엔드포인트)
4. 영구 서버 443 → Nginx
5. Nginx → proxy_pass → `100.74.2.103:8080` (Tailscale)
6. SSAFY Spring Boot 도착 → 필터 통과 (health 는 인증 면제)
7. `HealthController.health()` 실행
8. 내부적으로 Actuator `HealthEndpoint.health()` 호출 → DB/Redis 핑
   - DB 핑 → Tailscale → 영구 PG → 응답
   - Redis 핑 → Tailscale → 영구 Redis → 응답
9. 모두 UP 이면 `{"status": "UP", ...}` 반환
10. Controller → Nginx → Cloudflare → 내 터미널
11. 화면에 응답 뜸 ✅

한 번 요청에 **Tailscale 왕복 2번 + 인터넷 왕복 2번** 발생. 그래도 평균 ~13ms 안에 끝남.

---

## 5. 자주 헷갈리는 것들

### Q. Cloudflare 가 우리 서버 대신 응답하기도 한다고? 그럼 DB 조회는 어떻게?
- Cloudflare 는 **정적 파일** (이미지/JS/CSS) 만 캐시. 우리 `/api/*` 같은 API 는 매번 오리진 서버로 감.
- API 를 캐싱하고 싶으면 Cloudflare 설정에 명시해야 함.

### Q. Nginx 도 서버고 Spring Boot 도 서버면, 뭐가 진짜 서버?
- 둘 다 서버. 다만 역할이 다름.
- Nginx 는 **"문지기"** — 외부에서 오는 요청을 맞이하고, 적절한 곳으로 안내.
- Spring Boot 는 **"실무자"** — 실제 로직 처리.
- 현대 웹에선 이 둘이 분리된 게 표준.

### Q. 서버가 두 대로 나뉘어 있는데, 그냥 하나로 합치면 안 돼?
- 가능하지만, 각자 이유 있음:
  - **영구 서버**: DB 가 여기 있음. "잃으면 안 되는 데이터" 를 안정적인 곳에.
  - **SSAFY 서버**: 스펙 더 좋고(4vCPU/15GB), 앱(메모리 많이 먹음)이 여기 있는 게 적합.
  - SSAFY 는 언젠가 회수됨 → 거기 있던 앱이 사라져도 DB 는 영구 서버에 남아있음.
- 자세히는 [infra 문서](../infra/2026-04-22-tailscale-app-db-split.md) 참조.

### Q. 왕복 2번이면 느린 거 아니야?
- 두 서버가 같은 AWS 서울 리전에 있어서 **Tailscale 왕복 ~2ms**. 사람 체감 불가.
- 대신 **DB 쿼리가 많은 요청** (예: N+1 패턴) 은 왕복이 곱해져서 아플 수 있음 → 개발할 때 조심.

### Q. 필터 / 컨트롤러 / 서비스 / 리포지토리가 다 필요해?
- 작은 프로젝트는 생략 가능하지만, 도메인 많아지면 이 계층 분리가 **"바꾸기 쉬운 코드"** 를 만들어줌.
- Spring 쓰면 이 패턴이 자연스러워짐.

---

## 6. 실제 코드에서 확인하기

궁금증 생기면 아래 파일들 열어보세요:

| 개념 | 실제 파일 |
|---|---|
| Nginx 설정 | `/opt/dotshelf/nginx/conf.d/default.conf` (서버에만 있음) |
| Spring Boot 진입점 | [`src/main/java/com/dotshelf/DotshelfApiApplication.java`](../../src/main/java/com/dotshelf/DotshelfApiApplication.java) |
| 필터: 요청 ID | [`src/main/java/com/dotshelf/global/filter/MdcRequestIdFilter.java`](../../src/main/java/com/dotshelf/global/filter) (패키지 기준) |
| 필터: JWT 인증 | [`src/main/java/com/dotshelf/global/security/JwtAuthenticationFilter.java`](../../src/main/java/com/dotshelf/global/security) |
| 컨트롤러 예시 | [`src/main/java/com/dotshelf/domain/user/controller/`](../../src/main/java/com/dotshelf/domain/user/controller) |
| 서비스 예시 | [`src/main/java/com/dotshelf/domain/user/service/`](../../src/main/java/com/dotshelf/domain/user/service) |
| 리포지토리 예시 | [`src/main/java/com/dotshelf/domain/user/repository/`](../../src/main/java/com/dotshelf/domain/user/repository) |

---

## 7. 챕터 1 정리

이 챕터에서 익힌 것:

- ✅ HTTP 요청/응답의 모양
- ✅ DNS 가 이름을 IP 로 바꿔준다
- ✅ Cloudflare 가 우리 서버 앞에서 방패 역할
- ✅ Nginx 는 영구 서버의 프론트 데스크
- ✅ Tailscale 은 두 서버 간 비밀 터널
- ✅ Spring Boot 안에서 필터 → 컨트롤러 → 서비스 → 리포지토리 순서로 일이 이어짐
- ✅ 응답은 같은 길로 되돌아옴

다음 챕터 👉 [**챕터 2: 인증 — 누구인지 어떻게 알까**](./02-authentication.md)

챕터 1 의 흐름 속에서 "JWT 인증 필터" 가 어떻게 작동하는지, `Authorization: Bearer ...` 헤더가 무엇인지, 로그인이 어떻게 이뤄지는지를 깊게 다룹니다.
