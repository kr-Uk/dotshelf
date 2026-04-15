---
name: dotshelf-simplify
description: 방금 작성하거나 수정한 백엔드 코드를 DotShelf 팀 규칙(`docs/architecture/patterns.md`)에 맞춰 정리하는 스킬. 기능은 절대 건드리지 않고 "구현 방식"만 다듬는다. 중복 제거, 중첩 삼항/깊은 분기 평탄화, 자명한 주석 제거, 네이밍/예외/응답 컨벤션 어긋남 교정이 핵심. 반드시 사용자가 "정리해줘", "깔끔하게 해줘", "리팩토링 해줘", "방금 쓴 거 한번 봐줘", "simplify", "정돈", "개선할 부분 있어?", "이 코드 다듬어줘", "스타일 맞춰줘", "한번 검토해줘"라고 말할 때 트리거되어야 한다. 또한 `dotshelf-code-match`로 코드 생성 직후나 작은 수정 후에도 자동으로 한 번 돌리기 좋다. 단순 "더 짧게 만들기"가 아니라 **팀 컨벤션을 참고한 사후 청소**가 목적이다. 내장 `simplify` 스킬과 달리 `patterns.md`/`checklist.md`를 기준으로 동작하므로 DotShelf 프로젝트에서 더 적합하다.
---

# DotShelf Simplify

방금 작성/수정한 코드를 **팀 규칙에 맞춰 청소**한다. 기능은 보존하고, 구현 방식만 다듬는다.

## 언제 쓰나

사용자가 다음을 요청할 때:
- "정리해줘", "깔끔하게 해줘"
- "방금 쓴 거 한번 봐줘"
- "리팩토링 해줘"
- "개선할 부분 있어?"
- "스타일 맞춰줘", "컨벤션 맞게 정리"
- `dotshelf-code-match`가 코드 생성 직후 연속 호출하기도 함

범용 리팩토링이 아니라, **최근 변경 범위**에 집중한다. 전체 코드베이스를 훑지 않는다.

## 철학

**기능은 건드리지 않는다.** 동작이 달라질 수 있는 변경은 금지. 리팩토링/이름 변경/로직 재배치까지가 최대 범위. "이 부분은 성능상 다르게 짜는 게 낫습니다"는 제안으로만 내놓고 실행은 사용자 승인 후.

**팀 규칙 우선**. 일반적 "clean code" 조언보다 `docs/architecture/patterns.md`가 우선. 상충하면 patterns.md가 이긴다.

**과단순화 지양**. 한 줄로 만들려고 가독성 떨어뜨리는 건 역효과. "이게 더 읽히나?"를 기준으로 삼는다.

**최근 변경 중심**. 전체 리팩토링 모드는 사용자가 명시 요청해야 발동.

---

## 작업 흐름

```
1. 작업 대상 식별
   - 현재 세션에서 방금 작성/수정한 파일
   - 또는 `git diff`의 staged/unstaged 변경
   - 사용자가 특정 파일 지정하면 그걸로

2. 규칙 읽기
   - docs/architecture/patterns.md (해당 레이어 섹션)
   - docs/architecture/checklist.md

3. 개선 후보 추출 (아래 섹션 참조)
4. 사용자에게 "이 부분 다듬을까요?" 변경 요약 제시
5. 승인 시 적용, 거절 시 유지
```

---

## 개선 후보

### 제거/축약 (기계적, 확신 높음)
- **자명한 주석**: 코드만 봐도 아는 `// save user` 같은 것
- **중복 로직**: 같은 형태 반복 → 메서드 추출
- **미사용 import / 필드 / 파라미터**
- **빈 catch 블록**: 원인 파악 후 의미 있는 처리 또는 rethrow
- **불필요한 null 체크**: Optional 활용 또는 사전 조건 재설계
- **중첩 삼항 연산자**: if/else 체인이나 switch로
- **deeply nested if**: early return 패턴

### 네이밍
- Boolean은 `is`, `has`, `can`, `should` prefix
- 메서드는 동사부터 (`findById`, `saveAndPublish`)
- 모호한 변수(`data`, `tmp`, `info`) → 의미 있는 이름
- DTO는 `<Verb>Request` / `<Noun>Response`

### 레이어/컨벤션 어긋남 (patterns.md 기준)
- Controller에서 Repository 직접 호출 → Service로 우회
- 엔티티 setter 발견 → 도메인 메서드로 교체 제안
- `throw new RuntimeException(...)` → `CustomException(ErrorCode.*)` + 필요 시 ErrorCode 추가 제안
- Controller가 엔티티 반환 → DTO로 변환
- Service에서 HttpServletRequest/ResponseEntity 참조 → Controller로 이동
- Jackson 2 API 참조 → Jackson 3 (`tools.jackson.*`)
- 쿼리 문자열 concat → JPQL/QueryDSL

### 구조 개선 (판단 필요, 사용자 확인)
- 메서드가 너무 길다(50줄 이상) → 의미 단위로 분해
- 파라미터 5개 이상 → Parameter Object 또는 DTO로
- if-else로 타입 분기 10개 이상 → Strategy/enum method로
- 반복되는 validation → 공통 validator 또는 annotation

---

## 자동 대 확인

| 변경 유형 | 자동 적용 | 사용자 확인 |
|-----------|-----------|-------------|
| 미사용 import 제거 | ✅ | - |
| 자명한 주석 제거 | ✅ | - |
| 변수명 미세 개선 | - | ✅ |
| 메서드 추출 | - | ✅ |
| 예외 타입 변경 (`CustomException` 전환) | - | ✅ |
| 레이어 위반 수정 | - | ✅ (큰 변경이면 사용자 승인) |
| 성능 관련 변경 | - | 제안만, 실행은 별도 |

---

## 리포트 포맷

```
🧹 DotShelf Simplify 결과

🟢 자동 적용 (3건)
- `UserService.java`: 미사용 import 2개 제거
- `UserController.java`: 자명 주석 1건 제거

🟡 제안 (확인 필요)
1. `UserService.signup` (line 23-35): 중첩 if 3단 → early return 패턴
   (변경안 미리보기 있음)
2. `BookshelfService.findAll`: 엔티티 직접 반환 → `BookshelfResponse`로 변환
   → patterns.md "6. Controller 규칙" 위반
3. `ReviewService.publish`: 메서드 72줄 → `validateDraft()`, `applyPublishedAt()` 분해 제안

🔴 차단 (기능 변경 위험)
- 없음

적용할 제안 번호를 알려주세요. (예: "1,3 적용" / "전부 적용" / "pass")
```

---

## 범위 제한

이 스킬이 **하지 않는** 것:
- **아키텍처 변경** (레이어드 → 핵사고날 같은 큰 그림)
- **도메인 모델링 재설계** (엔티티 구조 변경)
- **성능 최적화 실행** (제안만 하고 실행은 별도 스킬/논의)
- **의존성 추가/삭제** — 라이브러리 추가는 신중
- **테스트 수정** — 테스트는 따로 검토 필요

위 범위는 사용자가 별도로 요청해야 한다.

---

## patterns.md와 어긋난 기존 코드를 만났을 때

기존 코드가 patterns.md를 따르지 않는 경우:

1. **명확히 어긋남** (setter 있음, Controller에서 Repository 호출 등) → 제안
2. **회색 지대** (판단 필요) → patterns.md에 명시 안 된 거면 제안하지 않음
3. **patterns.md 자체가 틀린 경우** → "이 코드가 더 나아 보입니다. patterns.md 업데이트를 제안할까요?"

본인이 틀린 판단으로 강행하지 않는다. 어긋난 이유가 있을 수 있음.

---

## 다른 스킬과의 관계

- **`dotshelf-code-match`** (생성 단계) — 새 코드 쓸 때 규칙 강제
- **`dotshelf-simplify`** (정리 단계) — 이미 쓴 코드 청소
- **`simplify`** (내장) — 일반 청소. DotShelf 맥락은 없음. 있으면 겹치지만 이 스킬이 더 구체적이므로 우선 발동됨
- **`dotshelf-verify`** — 정리 후 빌드/테스트 돌려 회귀 없는지 확인 (정리 → verify 체인 권장)

---

## 안전 원칙

- **기능 변경 금지**: 동작이 달라질 수 있는 변경은 제안으로만
- **테스트 동반 권장**: 변경 후 `dotshelf-verify` 또는 `./gradlew test` 실행 권유
- **한번에 과하게 변경 금지**: 5건 이상 수정 시 청크로 쪼개어 사용자 검토 가능하게
- **원본 백업 필요 없음**: git이 있으니 변경 후 `git diff`로 확인 가능. 단, 사용자가 아직 커밋하지 않은 변경 위에 덮어쓰지 않게 주의
