---
name: dotshelf-code-match
description: 백엔드 신규 코드 생성 시 프로젝트의 확립된 아키텍처/디자인 패턴을 일관되게 따르도록 강제하는 스킬. 작업 시작 전에 반드시 `docs/architecture/patterns.md`와 `docs/architecture/exemplars/` 디렉토리를 읽어 레이어링/엔티티/DTO/Service/Repository/Controller/예외/네이밍 규칙을 숙지하고, 작성 후 `docs/architecture/checklist.md`로 셀프 리뷰한다. 반드시 사용자가 "엔티티 만들어줘", "서비스 추가", "컨트롤러 구현", "레포지토리 만들어", "새 도메인 만들자", "API 만들어", "기능 추가", "CRUD 만들어", "비슷한 스타일로", "컨벤션 맞춰서", "기존 패턴대로", "일관되게"라고 말할 때 트리거되어야 한다. 단순 리팩토링/버그수정이 아닌 **새 코드 생성** 맥락에서 특히 강하게 발동한다. Codex와도 patterns.md를 공유하므로 에이전트 간 코드 품질이 일관된다. 이 스킬의 핵심은 "exemplar 파일을 거울처럼 따르기"다.
---

# Match Patterns

DotShelf 백엔드의 신규 코드를 작성할 때 **기존 패턴과 품질을 일치**시키기 위한 스킬. 규칙 원문은 `backend/docs/architecture/patterns.md`에 있으며, 이 스킬은 그 문서를 강제하는 얇은 래퍼다.

## 언제 트리거

신규 코드 생성 맥락. 특히:
- "엔티티 만들어줘", "서비스 추가", "컨트롤러 구현"
- "새 도메인 추가", "CRUD 만들어", "API 만들자"
- "비슷한 스타일로", "컨벤션 맞춰서", "기존 패턴대로"

단순 질문(`"이 코드 뭐하는 거야"`)이나 작은 버그 수정은 이 스킬 없이도 되지만, **기능 추가/확장**이면 항상 이 스킬로 붙잡는다.

## 반드시 읽을 문서 (순서)

```
1. backend/docs/architecture/patterns.md
2. backend/docs/architecture/exemplars/README.md
3. backend/docs/architecture/exemplars/<관련 파일들>
4. 유사 기존 도메인 (예: 새 도메인 만들 때 가장 가까운 기존 도메인 전부 읽기)
```

**읽지 않고 코드 쓰지 않는다.** 읽는 데 시간이 든다고 생략하면 드리프트가 누적되어 몇 주 뒤 품질이 흩어진다.

## 작업 흐름

```
1. 요청 이해 → 어느 레이어/도메인이 영향받는지 파악
2. patterns.md 읽기 (해당 섹션)
3. exemplars 및 가장 가까운 기존 도메인 읽기
4. 계획 수립 → 필요 시 사용자에게 짧게 설명
5. 코드 작성 — 애노테이션 순서, 네이밍까지 거울처럼
6. checklist.md로 셀프 리뷰
7. 불합격 항목 수정 → 다시 체크
8. 사용자에게 결과 전달 (어떤 패턴을 따랐는지 짧게 명시)
```

## 작성 시 중점

- **레이어 경계 준수**: Controller → Service → Repository. 우회 금지.
- **엔티티는 `BaseEntity` 상속, setter 금지, `@Builder`만**. 상태 변경은 엔티티 메서드로.
- **DTO는 record**, Request는 Bean Validation, Response는 `static from(...)` 팩토리.
- **모든 응답은 `ApiResponse<T>`로 래핑**.
- **예외는 `CustomException(ErrorCode.*)`**, 새 상황 → `ErrorCode`에 항목 추가.
- **복잡 쿼리는 QueryDSL 커스텀 레포** (`UserRepositoryCustom` + `UserRepositoryImpl` 쌍).

## exemplar 활용법

"엔티티 만들어줘"라는 요청이 오면:
1. `exemplars/User.java` 읽기
2. 구조(필드 선언 순서, 애노테이션 순서, 생성자/빌더 배치, 상태 메서드 위치)를 **그대로** 복제
3. 필드 내용만 새 도메인에 맞게 교체
4. 추가 로직이 필요하면 왜 exemplar와 다른지 주석 또는 patterns.md 업데이트 제안

이렇게 해야 에이전트가 바뀌어도(Claude/Codex/새 모델) 결과물이 일관된다.

## 셀프 리뷰

작성 후 `backend/docs/architecture/checklist.md`를 열어 해당 항목을 **항목별로** 확인한다. 단순 "다 됐어요"가 아니라, 체크리스트 항목을 본인이 밟았는지 대조.

불합격이 있으면 수정한 후 재확인. 사용자에게 "checklist 통과 확인 완료"라고 밝히면 신뢰가 높아진다.

## patterns.md와 어긋날 때

규칙이 현실에 안 맞거나 exemplar와 다른 접근이 필요하면:
1. 왜 어긋나야 하는지 한두 문장 설명
2. 사용자에게 "`patterns.md`를 수정할까요, 이 코드만 예외로 둘까요?" 질문
3. 임의로 무시하지 않음

규칙을 bending하는 것은 괜찮지만 **silent drift**가 가장 나쁘다. 명시적으로 짚고 넘어간다.

## Codex와의 공유

이 스킬은 `backend/AGENTS.md`와 **같은 `patterns.md`를 참조**한다. 즉 Claude가 생성한 코드와 Codex가 생성한 코드가 같은 규칙을 따르므로, 리뷰 시 누가 썼는지 구분할 필요 없이 품질이 일정하다. `patterns.md`만 단일 진실 소스로 유지하면 양쪽 모두 자동 반영.

## 자주 저지르는 실수 (방지 체크)

- ❌ Controller에서 Repository 직접 호출
- ❌ 엔티티를 Controller에서 반환 (LAZY 초기화 예외, 순환 참조 위험)
- ❌ Service에서 `ResponseEntity`, `HttpServletRequest` 참조
- ❌ `throw new RuntimeException(...)` (반드시 `CustomException`)
- ❌ Jackson 2 API 참조 (Spring Boot 4는 Jackson 3 = `tools.jackson.*`)
- ❌ `ddl-auto: create` 상태에서 실제 데이터 넣어놓고 다른 브랜치로 왔다갔다
- ❌ 엔티티 setter 추가 (상태 변경은 반드시 엔티티 메서드로)

## 요약

1. 먼저 `patterns.md` + `exemplars/` 읽기
2. 가장 가까운 기존 도메인 확인
3. 거울처럼 구조 복제하며 작성
4. `checklist.md`로 셀프 리뷰
5. 결과 전달 시 따른 패턴을 짧게 언급
