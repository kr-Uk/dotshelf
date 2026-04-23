# DB 마이그레이션 작성 규칙

## 왜 이 규칙이 생겼어?

DotShelf는 배포할 때 **앱 두 개가 잠깐 동시에 돌아갑니다** (무중단 배포, blue-green).

```
[배포 전]   [구 버전] ──→ DB
[배포 중]   [구 버전] ──→ DB  ←── [새 버전]   ← 이 순간이 1~2분
[배포 후]                ──→ DB  ←── [새 버전]
```

이 1~2분 동안 **구 버전과 새 버전이 같은 DB**를 씁니다.
즉 **구 버전 코드가 새 스키마에서도 돌 수 있어야** 합니다.
이게 깨지면 배포 중 500 에러가 터지고, 롤백도 막힙니다.

## 한 문장 규칙

> **DB 변경 = "추가"만 OK. "삭제 / 이름변경 / 조건 강화"는 두 번의 배포로 나눠서.**

---

## 🚨 같은 PR에 넣으면 안 되는 DDL

### 1. 컬럼 삭제
```sql
-- ❌
ALTER TABLE users DROP COLUMN nickname;
```
**왜 안 돼?** 구 버전 앱은 아직 `nickname`을 SELECT/INSERT 합니다. 1초 만에 500.

### 2. 컬럼 이름 변경
```sql
-- ❌
ALTER TABLE users RENAME COLUMN name TO display_name;
```
**왜 안 돼?** 구 버전은 `name`을 찾고 새 버전은 `display_name`을 찾습니다. 둘 중 하나는 무조건 터짐.

### 3. NOT NULL 강화 (NULL 허용 → NOT NULL)
```sql
-- ❌
ALTER TABLE books ALTER COLUMN isbn SET NOT NULL;
```
**왜 안 돼?** 구 버전이 `isbn` 없이 INSERT 시도 → DB 거부 → 500.

### 4. 컬럼 타입 변경
```sql
-- ❌
ALTER TABLE orders ALTER COLUMN code TYPE INTEGER USING code::integer;
```
**왜 안 돼?** 락 + 타입 호환 안 됨 + 데이터 변환 실패 가능. 거의 모든 방향에서 위험.

### 5. 기본값 없는 NOT NULL 컬럼 추가
```sql
-- ❌
ALTER TABLE reviews ADD COLUMN rating INTEGER NOT NULL;
```
**왜 안 돼?** 기본값이 없으니 구 버전이 이 컬럼 모르고 INSERT → NULL → 거부.

---

## ✅ 마음껏 해도 되는 것

```sql
-- NULL 허용 컬럼 추가
ALTER TABLE users ADD COLUMN bio TEXT;

-- 기본값 있는 NOT NULL 컬럼 추가
ALTER TABLE reviews ADD COLUMN rating INTEGER NOT NULL DEFAULT 0;

-- 새 테이블 추가
CREATE TABLE tags (id BIGSERIAL PRIMARY KEY, ...);

-- 인덱스 추가
CREATE INDEX idx_users_email ON users(email);
```

구 버전은 이런 변경을 몰라도 정상 동작합니다.

---

## 그래도 컬럼을 지우고 싶다면?

**배포를 두 번**에 나누세요.

### 1차 배포 (Expand — 추가만)
```sql
-- V2__expand_add_display_name.sql
ALTER TABLE users ADD COLUMN display_name TEXT;
```
애플리케이션 코드:
- 쓸 때: `name`, `display_name` **둘 다** 동일 값으로 기록
- 읽을 때: `display_name`이 있으면 그걸, 없으면 `name`

### 시간 경과
- 새 버전이 충분히 돌면서 `display_name` 컬럼이 다 채워지길 대기
- 구 버전 앱이 완전히 사라진 뒤 (보통 다음 스프린트)

### 2차 배포 (Contract — 삭제)
```sql
-- V3__contract_drop_name.sql
ALTER TABLE users DROP COLUMN name;
```

귀찮아 보이지만, 이 대가로 얻는 것:
- 배포 중 **5xx 에러 0건**
- **언제든 롤백 가능**
- 사용자 세션 끊기지 않음

---

## 파일명 규칙

| 접두 | 의미 | 리뷰 |
|---|---|---|
| `V{n}__expand_*.sql` | 추가만 (안전) | 일반 리뷰 |
| `V{n}__contract_*.sql` | 삭제/강화 (위험) | **반드시 백엔드 담당자 확인** |

접두를 안 붙여도 돌긴 합니다. 다만 **리뷰어가 어디를 집중해서 볼지** 알려주는 신호입니다.

---

## 판단이 안 설 때

1. PR 올리기 전에 **백엔드 채널에 한 줄 질문**. 5분이면 답 갑니다.
2. 헷갈리면 일단 "추가"만 하고, 삭제는 다음 배포로 미루세요. **손해 볼 게 없어요.**
