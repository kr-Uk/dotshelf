# 코드 생성 후 셀프 리뷰 체크리스트

새 코드를 작성한 뒤 아래 항목을 통과하는지 **반드시** 확인한다. 불합격 항목이 있으면 수정 후 다시 체크.

## 엔티티
- [ ] `BaseEntity` 상속
- [ ] `@NoArgsConstructor(access = AccessLevel.PROTECTED)`
- [ ] setter 없음, 외부 new 없음
- [ ] 생성자 private, `@Builder` 포함
- [ ] 테이블 이름 snake_case 복수형
- [ ] 연관관계 LAZY
- [ ] 상태 변경은 엔티티 메서드 (setter 아님)

## DTO
- [ ] `record` 사용
- [ ] Request: Bean Validation 애노테이션
- [ ] Response: `static from(...)` 팩토리
- [ ] Controller/Service 경계에서 엔티티 직접 노출 없음

## Service
- [ ] `@Service` + `@RequiredArgsConstructor`
- [ ] 클래스 `@Transactional(readOnly = true)`, 쓰기 메서드만 `@Transactional`
- [ ] 예외는 `CustomException(ErrorCode.*)` — `RuntimeException` 금지
- [ ] Entity ↔ DTO 변환이 여기서 일어남
- [ ] HTTP 타입 미참조

## Repository
- [ ] `extends JpaRepository<T, Long>`
- [ ] 복잡 쿼리는 Custom + Impl 분리
- [ ] N+1 대비 (EntityGraph 또는 fetchJoin)
- [ ] 비즈니스 로직 없음

## Controller
- [ ] `@RestController` + `@RequestMapping("/api/<복수형>")`
- [ ] 요청: `@RequestBody @Valid`
- [ ] 응답: `ApiResponse<T>`로 래핑
- [ ] Service만 호출 (Repository 직접 호출 금지)
- [ ] 상태 코드 적절 (생성 시 201)

## 예외 처리
- [ ] 새 에러 상황 → `ErrorCode` 추가
- [ ] `CustomException`만 던짐
- [ ] `GlobalExceptionHandler`로 처리되는지 확인

## 네이밍
- [ ] 클래스: PascalCase, 의미 있는 이름
- [ ] 메서드: camelCase, 동사부터
- [ ] 엔드포인트: 복수형 kebab-case
- [ ] 테이블/컬럼: snake_case

## 보안/안전
- [ ] 민감값 하드코딩 없음 (환경변수/GitLab Variables)
- [ ] SQL Injection 가능성 차단 (JPA/QueryDSL만 사용, 문자열 쿼리 concat 금지)
- [ ] 인증/권한 필요한 엔드포인트는 `SecurityConfig`에 반영 요청

## 테스트
- [ ] 주요 케이스 (성공/실패/경계값) 테스트
- [ ] 테스트명: 한국어 BDD 허용 (`{상황}_{조건}_{결과}`)
- [ ] given/when/then 구조

## 문서/주석
- [ ] 비즈니스 의도가 드러나지 않는 부분만 주석 (코드로 자명하면 금지)
- [ ] Javadoc은 public API에만, 내부 헬퍼는 생략
