# 알림 설정 기반 Notification 발신 로직 추가

- **브랜치**: `feat/be/alert`
- **범위**: `origin/dev..HEAD` (기능 커밋 1개)
- **기간**: 2026-05-04 ~ 2026-05-04
- **관련 커밋**: `19a67b0`

## TL;DR

FE 알림 설정 화면에서 사용하는 5개 토글에 맞춰 BE의 실제 Notification 생성 흐름을 추가했다.
기존에는 `UserNotificationSetting` 저장과 알림 조회/읽음 처리만 있었고, 이벤트 발생 시 `notifications` 테이블에 저장하는 발신 로직이 비어 있었다.
이번 변경으로 방명록 새 글/답글, 책 추가, 독서 기록 좋아요, 독서 기록 댓글/대댓글 이벤트가 대상자의 설정을 확인한 뒤 알림을 저장한다.
독서 기록 댓글 토글 분리를 위해 `notify_reading_record_comment` 컬럼도 Flyway 마이그레이션으로 추가했다.

## 무엇을 했나

### 1. 알림 설정 필드 확장

- 사용자 관점 효과: 독서 기록 좋아요와 댓글 알림을 별도 토글로 제어할 수 있다.
- 시스템 관점 효과: `notifyReadingRecordReaction`은 좋아요, `notifyReadingRecordComment`는 댓글/대댓글을 담당한다.
- 주요 파일: `UserNotificationSetting.java`, `UserDtos.java`, `UserService.java`, `V14__add_reading_record_comment_notification_setting.sql`

### 2. Notification 발신 서비스 추가

- 사용자 관점 효과: 설정이 켜져 있는 경우 실제 알림 목록에 이벤트가 나타난다.
- 시스템 관점 효과: 각 도메인 서비스는 이벤트 발생 사실만 전달하고, 설정 확인/자기 자신 제외/메시지 생성/저장은 `NotificationService`가 담당한다.
- 주요 파일: `NotificationService.java`, `NotificationServiceTest.java`

### 3. 5개 이벤트 발생 지점 연결

- 방명록 새 글: 공간 주인에게 알림
- 방명록 답글: 원 방명록 작성자에게 알림
- 책 추가: 책을 추가한 사용자의 팔로워들에게 알림
- 독서 기록 좋아요: 기록 작성자에게 알림
- 독서 기록 댓글/대댓글: 기록 작성자와 부모 댓글 작성자에게 알림
- 주요 파일: `GuestbookService.java`, `BookService.java`, `ReadingRecordService.java`, `ReadingRecordCommentService.java`

## 왜 했나

FE는 이미 알림 설정 GET/PATCH와 토글 UI를 구현했지만, BE에는 실제 이벤트에서 `Notification`을 생성하는 로직이 없었다.
그 상태에서는 사용자가 토글을 켜도 알림 목록에 새 데이터가 생기지 않으므로, 설정 저장 API와 알림 조회 API 사이의 핵심 동작이 빠져 있었다.

또한 FE 요구사항은 독서 기록 좋아요와 댓글을 별도 토글로 나누고 있다.
기존 BE의 `notifyReadingRecordReaction` 하나로는 좋아요와 댓글을 독립적으로 끌 수 없기 때문에, 댓글 전용 필드를 추가했다.

## 어떻게 구현했나

### 설정 기반 생성 책임을 NotificationService로 모았다

각 도메인 서비스가 설정 컬럼을 직접 알게 만들면 알림 정책이 여러 서비스로 흩어진다.
그래서 도메인 서비스는 `notifyGuestbookCreated(...)` 같은 의도 중심 메서드만 호출하고, 실제 설정 확인은 `NotificationService`에서 처리하도록 했다.

```java
private void createIfEnabled(Long receiverUserId, Long senderUserId,
                             Predicate<UserNotificationSetting> enabled,
                             String type, String targetType, Long targetId, String message) {
    if (receiverUserId == null || senderUserId == null || receiverUserId.equals(senderUserId)) {
        return;
    }
    UserNotificationSetting setting = notificationSettingRepository.findByUserId(receiverUserId)
            .orElseGet(() -> notificationSettingRepository.save(UserNotificationSetting.builder()
                    .userId(receiverUserId)
                    .build()));
    if (!enabled.test(setting)) {
        return;
    }
    create(receiverUserId, senderUserId, type, targetType, targetId, message);
}
```

핵심 포인트:
- 수신자와 발신자가 같으면 알림을 만들지 않는다.
- 설정 row가 없으면 기본 ON 설정으로 생성한다.
- `type`과 `targetType`은 기존 `Notification` 구조를 그대로 활용했다.

### 책 추가 알림은 팔로워 기준으로 발송했다

요구사항에서 "책을 추가한 사용자의 팔로워들"이 수신자라고 확정했기 때문에 `FollowRepository.findByFollowingUserId(...)`로 팔로워를 조회한다.
팔로워가 많을 수 있으므로 설정은 `findByUserIdIn(...)`으로 한 번에 조회하고, 설정 row가 없는 사용자는 기본 ON으로 간주했다.

```java
List<Long> receiverUserIds = followRepository.findByFollowingUserId(senderUserId).stream()
        .map(Follow::getFollowerUserId)
        .toList();
Map<Long, UserNotificationSetting> settingByUserId = notificationSettingRepository
        .findByUserIdIn(receiverUserIds)
        .stream()
        .collect(Collectors.toMap(UserNotificationSetting::getUserId, Function.identity()));
```

기존 `BookService.placeBookOnShelf(...)`에서는 이미 보유한 책을 다시 배치하는 경우도 있으므로, 알림은 새 `UserBook`이 생성된 경우에만 보낸다.

### 대댓글은 기록 작성자와 부모 댓글 작성자를 구분했다

댓글 생성 시에는 항상 독서 기록 작성자를 알림 수신자로 본다.
대댓글인 경우에는 부모 댓글 작성자에게도 알림을 보내되, 부모 댓글 작성자가 기록 작성자와 같으면 중복 알림을 피한다.

```java
notificationService.notifyReadingRecordCommented(recordOwnerUserId, currentUserId, comment.getId());
if (parent != null && !parent.getUserId().equals(recordOwnerUserId)) {
    notificationService.notifyReadingRecordCommentReplied(parent.getUserId(), currentUserId, comment.getId());
}
```

## 배운 것

- **설정 저장과 이벤트 발신은 별개의 기능이다**: 토글 API가 있어도 이벤트 발생 지점에서 저장 로직을 호출하지 않으면 사용자에게 보이는 알림은 생기지 않는다.
- **알림 정책은 한 서비스에 모으는 편이 낫다**: 설정 확인, 자기 자신 제외, 메시지 길이 제한, `type` 지정 같은 정책이 흩어지면 이벤트가 늘어날수록 누락이 생기기 쉽다.
- **팔로워 대상 발신은 배치 조회가 필요하다**: 팔로워마다 설정을 `findByUserId`로 조회하면 N+1 구조가 된다. 수신자 목록을 만든 뒤 설정을 한 번에 가져오는 편이 낫다.
- **대댓글 알림은 중복 수신자를 신경 써야 한다**: 기록 작성자가 부모 댓글 작성자이기도 하면 같은 이벤트에서 동일 사용자에게 두 번 알림이 갈 수 있다.
- **Flyway 컬럼 추가는 기본값을 함께 고민해야 한다**: 기존 사용자 row가 이미 있으므로 `NOT NULL DEFAULT true`를 줘야 마이그레이션이 안정적으로 통과한다.

## 더 공부할 것

- [ ] Notification `type`을 문자열 상수로 둘지 enum으로 모델링할지, JPA 저장 방식까지 포함해 비교하기
- [ ] 팔로워가 많은 사용자의 알림 fan-out을 동기 저장으로 처리해도 되는지, 비동기 이벤트/큐 도입 기준 정리하기
- [ ] 알림 중복 방지 정책이 필요한 이벤트와 필요 없는 이벤트를 구분하는 기준 세우기
- [ ] 알림 메시지를 서버에서 고정 문자열로 만들지, 클라이언트 i18n 키 + payload로 내려줄지 비교하기

## 참고

- 관련 커밋: `19a67b0`
- 검증: `./gradlew.bat test`, `./gradlew.bat build`
