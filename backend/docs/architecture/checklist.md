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
- [ ] 시간 필드는 `Instant` (LocalDateTime 금지)

## DTO
- [ ] `record` 사용
- [ ] Request: Bean Validation 애노테이션 + 커스텀 메시지
- [ ] Response: `static from(...)` 팩토리
- [ ] Controller/Service 경계에서 엔티티 직접 노출 없음
- [ ] 공개 API 는 `@Schema(description=..., example=...)` (최소 description)

## Service
- [ ] `@Service` + `@RequiredArgsConstructor`
- [ ] 클래스 `@Transactional(readOnly = true)`, 쓰기 메서드만 `@Transactional`
- [ ] 예외는 `CustomException(ErrorCode.*)` — `RuntimeException` 금지
- [ ] Entity ↔ DTO 변환이 여기서 일어남
- [ ] HTTP 타입 미참조
- [ ] 반환 타입이 DTO (Entity 누출 금지)

## Repository
- [ ] `extends JpaRepository<T, Long>`
- [ ] 메서드명 쿼리 조건이 3개 이내 (이상은 Custom + Impl)
- [ ] 복잡 조회는 Custom + Impl 분리 (QueryDSL)
- [ ] N+1 대비 (EntityGraph 또는 fetchJoin)
- [ ] loop 안에서 `findById` 패턴 금지
- [ ] 비즈니스 로직 없음

## Controller
- [ ] `@RestController` + `@RequestMapping("/api/<복수형>")`
- [ ] 요청: `@RequestBody @Valid`
- [ ] 응답: `ApiResponse<T>`로 래핑
- [ ] Service만 호출 (Repository 직접 호출 금지)
- [ ] 상태 코드 적절 (생성 시 201)
- [ ] `@Tag` (클래스) + `@Operation` (메서드) 작성
- [ ] 비로그인 API 는 `@SecurityRequirements({})` 명시
- [ ] 사용자 식별은 `@CurrentUser Long userId` (헤더 직접 파싱 금지)

## 예외 처리
- [ ] 새 에러 상황 → `ErrorCode` 추가
- [ ] `CustomException`만 던짐
- [ ] `GlobalExceptionHandler`로 처리되는지 확인

## 페이지네이션
- [ ] 리스트 조회는 **Cursor 기반** (`CursorResponse<T>`)
- [ ] 기본 `limit = 20`, 최대 `limit = 50` 강제
- [ ] QueryDSL 로 `limit+1` 조회하여 `hasNext` 판정

## 인증/인가
- [ ] `SecurityContext` 의존. `X-User-Id` 같은 헤더 직접 신뢰 금지
- [ ] 로그인/리프레시/헬스체크/Swagger 외 엔드포인트는 `authenticated()` 에 포함
- [ ] 도메인 소유권 검증은 Service 가 책임

## 시간 / 타임존
- [ ] 엔티티/DTO 의 시간 필드는 `Instant`
- [ ] DB 에 UTC 로 저장 (Hibernate `jdbc.time_zone=UTC`)
- [ ] 서버에서 타임존 변환 책임 없음 (클라이언트가 처리)

## 네이밍
- [ ] 클래스: PascalCase, 의미 있는 이름
- [ ] 메서드: camelCase, 동사부터
- [ ] 엔드포인트: 복수형 kebab-case
- [ ] 테이블/컬럼: snake_case

## 보안/안전
- [ ] 민감값 하드코딩 없음 (환경변수/GitLab Variables)
- [ ] SQL Injection 가능성 차단 (JPA/QueryDSL만 사용, 문자열 쿼리 concat 금지)
- [ ] JWT 비밀키는 `${JWT_SECRET}` 로만 주입
- [ ] 새 엔드포인트 추가 시 Security 화이트리스트 재검토

## 관찰성
- [ ] `@Slf4j` 로그 사용. `System.out.println` 금지
- [ ] MDC `requestId` 가 요청 내내 유지되는지 확인
- [ ] 에러 로그에 스택트레이스 포함 (`log.error("...", e)`)

## 테스트
- [ ] 주요 케이스 (성공/실패/경계값) 테스트
- [ ] 테스트명: 한국어 BDD 허용 (`{상황}_{조건}_{결과}`)
- [ ] given/when/then 구조

## 문서/주석
- [ ] 비즈니스 의도가 드러나지 않는 부분만 주석 (코드로 자명하면 금지)
- [ ] Javadoc은 public API에만, 내부 헬퍼는 생략
- [ ] 신규 도메인 수직 슬라이스 완성 시 `exemplars/` 갱신 검토
