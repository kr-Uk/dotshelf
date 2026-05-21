# 알림 설정 기반 Notification 발신 로직 추가

- **브랜치**: `feat/be/alert`
- **범위**: `origin/dev..HEAD` (알림 기능 및 리뷰 반영 변경)
- **기간**: 2026-05-04 ~ 2026-05-04
- **주요 변경**: 알림 발신 로직 추가, 트랜잭션 분리, 알림 설정 backfill, 익명 방명록 알림 마스킹

## TL;DR

FE 알림 설정 화면에서 사용하는 5개 토글에 맞춰 BE의 실제 `Notification` 생성 흐름을 추가했다.
방명록 새 글/답글, 책 추가, 독서 기록 좋아요, 독서 기록 댓글/대댓글 이벤트가 발생하면 대상자의 알림 설정을 확인한 뒤 알림을 저장한다.

리뷰 반영으로 알림 저장은 원 액션 트랜잭션 밖에서 실행되도록 `ApplicationEventPublisher`와 `@TransactionalEventListener(phase = AFTER_COMMIT)`, `@Async`를 사용했다.
또한 독서 기록 댓글 토글 분리를 위해 `notify_reading_record_comment` 컬럼을 추가했고, 기존 사용자 설정 row는 V15 migration으로 backfill한다.

## 무엇을 했나

### 1. 알림 설정 필드 확장

- 사용자 관점 효과: 독서 기록 좋아요와 댓글 알림을 별도 토글로 제어할 수 있다.
- 시스템 관점 효과: `notifyReadingRecordReaction`은 좋아요, `notifyReadingRecordComment`는 댓글/대댓글을 담당한다.
- 주요 파일: `UserNotificationSetting.java`, `UserDtos.java`, `UserService.java`, `V14__add_reading_record_comment_notification_setting.sql`

### 2. 설정 row 생성 책임 정리

- 신규 회원은 `AuthService`에서 사용자 생성과 함께 기본 `UserNotificationSetting` row를 만든다.
- 기존 회원은 `V15__backfill_user_notification_settings.sql`에서 누락 row를 한 번에 보강한다.
- 알림 발신 경로에서는 설정 row를 silent 생성하지 않고, 없으면 기본 ON으로만 간주한다.

### 3. AFTER_COMMIT 비동기 이벤트로 알림 발신 분리

- 도메인 서비스는 알림 저장을 직접 호출하지 않고 `NotificationEvents` record를 발행한다.
- `NotificationEventListener`는 트랜잭션 commit 이후 전용 `notificationTaskExecutor`에서 `NotificationService`를 호출한다.
- 알림 저장 실패가 책 추가, 좋아요, 댓글 작성 같은 원 액션을 rollback시키지 않는다.

### 4. 5개 이벤트 발생 지점 연결

- 방명록 새 글: 공간 주인에게 알림
- 방명록 답글: 원 방명록 작성자에게 알림
- 책 추가: 책을 추가한 사용자의 팔로워들에게 알림
- 독서 기록 좋아요: 기록 작성자에게 알림
- 독서 기록 댓글/대댓글: 기록 작성자와 부모 댓글 작성자에게 알림

### 5. 리뷰 반영 보완

- 책 재진열 케이스도 새 책장 추가 이벤트로 판단해 팔로워 알림을 발행한다.
- 대댓글 알림 type을 `READING_RECORD_COMMENT_REPLIED`로 분리했다.
- self-notification 가드를 `isInvalidPair(...)`로 모으고 public notify 진입점에서 먼저 적용한다.
- 책 제목이 비어 있을 때 메시지를 자연스럽게 분기한다.
- 메시지 255자 절단 시 UTF-16 surrogate pair 중간에서 자르지 않도록 보정한다.
- 익명 방명록 알림은 메시지와 `senderUserId` 모두 작성자를 노출하지 않도록 마스킹한다.

## 어떻게 구현했나

### 도메인 서비스는 이벤트만 발행한다

원 액션 트랜잭션 안에서는 알림 저장을 하지 않는다.
예를 들어 방명록 생성은 저장 후 익명 여부까지 이벤트에 담아 발행한다.

```java
eventPublisher.publishEvent(new GuestbookCreated(
        space.getOwnerUserId(),
        currentUserId,
        guestbook.getId(),
        guestbook.isAnonymous()
));
```

### 이벤트 리스너는 commit 이후 비동기로 실행된다

```java
@Async("notificationTaskExecutor")
@TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
public void handle(GuestbookCreated event) {
    notificationService.notifyGuestbookCreated(
            event.receiverUserId(),
            event.senderUserId(),
            event.guestbookId(),
            event.anonymous()
    );
}
```

이 구조에서는 원 액션이 commit된 뒤에만 알림 저장이 시작된다.
알림 저장은 `NotificationService`의 별도 `@Transactional` 메서드에서 처리되므로 원 액션과 트랜잭션 경계가 분리된다.

### 설정은 조회만 하고 없으면 기본 ON으로 간주한다

```java
boolean notificationEnabled = notificationSettingRepository.findByUserId(receiverUserId)
        .map(enabled::test)
        .orElse(true);
if (!notificationEnabled) {
    return;
}
```

알림 발신 중 수신자의 설정 row를 새로 만들지 않는다.
권한과 race 문제를 피하기 위해 row 생성은 회원가입과 migration이 담당한다.

### 익명 방명록은 저장 데이터도 마스킹한다

익명 방명록 알림은 메시지를 `익명 사용자가 방명록을 남겼습니다.`로 저장하고, 클라이언트가 `senderUserId`로 작성자를 역추적하지 못하도록 `Notification.senderUserId`도 `null`로 저장한다.
단, self-notification 판단과 설정 확인에는 실제 작성자 ID를 계속 사용한다.

## 배운 것

- 알림 설정 저장과 이벤트 발신은 별개의 기능이다.
- 알림 fan-out은 원 액션 트랜잭션과 분리해야 사용자 응답 지연과 rollback 전파를 줄일 수 있다.
- 설정 row auto-create는 간단해 보이지만 권한, 감사 로그, unique race 이슈를 만들 수 있다.
- 익명 정책은 화면 응답뿐 아니라 알림 메시지와 식별자 필드까지 함께 검토해야 한다.
- 대댓글처럼 비슷해 보이는 이벤트도 클라이언트 deep link와 grouping 기준이 다르면 type을 분리하는 편이 낫다.

## 더 공부할 것

- [ ] follower 수가 매우 큰 계정의 알림 fan-out을 청크 처리 또는 메시지 큐로 전환하는 기준 정리
- [ ] Notification `type`을 문자열 상수로 둘지 enum으로 모델링할지 비교
- [ ] 알림 메시지를 서버 고정 문자열로 둘지, 클라이언트 i18n key와 payload로 내려줄지 비교
- [ ] `ReadingRecordService`가 `BookService.getUserBook(...)`에 의존하는 구조를 repository projection으로 낮출 수 있는지 검토

## 참고

- 검증: `./gradlew.bat test`, `./gradlew.bat build`
