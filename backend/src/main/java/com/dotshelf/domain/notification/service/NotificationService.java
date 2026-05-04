package com.dotshelf.domain.notification.service;

import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.function.Predicate;
import java.util.stream.Collectors;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.notification.dto.NotificationDtos.NotificationListResponse;
import com.dotshelf.domain.notification.dto.NotificationDtos.NotificationResponse;
import com.dotshelf.domain.notification.entity.Notification;
import com.dotshelf.domain.notification.repository.NotificationRepository;
import com.dotshelf.domain.user.entity.Follow;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.entity.UserNotificationSetting;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.repository.UserNotificationSettingRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class NotificationService {

    private static final int MAX_MESSAGE_LENGTH = 255;

    private static final String TYPE_GUESTBOOK_CREATED = "GUESTBOOK_CREATED";
    private static final String TYPE_GUESTBOOK_REPLY_CREATED = "GUESTBOOK_REPLY_CREATED";
    private static final String TYPE_BOOK_ADDED = "BOOK_ADDED";
    private static final String TYPE_READING_RECORD_LIKED = "READING_RECORD_LIKED";
    private static final String TYPE_READING_RECORD_COMMENTED = "READING_RECORD_COMMENTED";

    private static final String TARGET_GUESTBOOK = "GUESTBOOK";
    private static final String TARGET_GUESTBOOK_REPLY = "GUESTBOOK_REPLY";
    private static final String TARGET_USER_BOOK = "USER_BOOK";
    private static final String TARGET_READING_RECORD = "READING_RECORD";
    private static final String TARGET_READING_RECORD_COMMENT = "READING_RECORD_COMMENT";

    private final NotificationRepository notificationRepository;
    private final UserNotificationSettingRepository notificationSettingRepository;
    private final FollowRepository followRepository;
    private final UserService userService;

    public NotificationListResponse getNotifications(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        return new NotificationListResponse(notificationRepository.findByReceiverUserIdOrderByIdDesc(currentUserId)
                .stream()
                .map(NotificationResponse::from)
                .toList());
    }

    @Transactional
    public NotificationResponse read(Long userId, Long notificationId) {
        Long currentUserId = userService.requireUserId(userId);
        Notification notification = notificationRepository.findById(notificationId)
                .orElseThrow(() -> new CustomException(ErrorCode.NOTIFICATION_NOT_FOUND));
        if (!notification.getReceiverUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        notification.read();
        return NotificationResponse.from(notification);
    }

    @Transactional
    public void readAll(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        notificationRepository.findByReceiverUserIdAndIsReadFalse(currentUserId).forEach(Notification::read);
    }

    @Transactional
    public void notifyGuestbookCreated(Long receiverUserId, Long senderUserId, Long guestbookId) {
        User sender = userService.getUser(senderUserId);
        createIfEnabled(
                receiverUserId,
                senderUserId,
                UserNotificationSetting::isNotifyNewGuestbook,
                TYPE_GUESTBOOK_CREATED,
                TARGET_GUESTBOOK,
                guestbookId,
                sender.getNickname() + "님이 방명록을 남겼습니다."
        );
    }

    @Transactional
    public void notifyGuestbookReplyCreated(Long receiverUserId, Long senderUserId, Long replyId) {
        User sender = userService.getUser(senderUserId);
        createIfEnabled(
                receiverUserId,
                senderUserId,
                UserNotificationSetting::isNotifyGuestbookReply,
                TYPE_GUESTBOOK_REPLY_CREATED,
                TARGET_GUESTBOOK_REPLY,
                replyId,
                sender.getNickname() + "님이 방명록에 답글을 남겼습니다."
        );
    }

    @Transactional
    public void notifyBookAddedToFollowers(Long senderUserId, Long userBookId, String bookTitle) {
        User sender = userService.getUser(senderUserId);
        List<Long> receiverUserIds = followRepository.findByFollowingUserId(senderUserId).stream()
                .map(Follow::getFollowerUserId)
                .toList();
        if (receiverUserIds.isEmpty()) {
            return;
        }
        Map<Long, UserNotificationSetting> settingByUserId = notificationSettingRepository
                .findByUserIdIn(receiverUserIds)
                .stream()
                .collect(Collectors.toMap(UserNotificationSetting::getUserId, Function.identity()));
        String message = sender.getNickname() + "님이 \"" + displayTitle(bookTitle) + "\" 책을 책장에 추가했습니다.";
        for (Long receiverUserId : receiverUserIds) {
            UserNotificationSetting setting = settingByUserId.get(receiverUserId);
            if (setting != null && !setting.isNotifyBookAddedByOthers()) {
                continue;
            }
            create(
                    receiverUserId,
                    senderUserId,
                    TYPE_BOOK_ADDED,
                    TARGET_USER_BOOK,
                    userBookId,
                    message
            );
        }
    }

    @Transactional
    public void notifyReadingRecordLiked(Long receiverUserId, Long senderUserId, Long readingRecordId) {
        User sender = userService.getUser(senderUserId);
        createIfEnabled(
                receiverUserId,
                senderUserId,
                UserNotificationSetting::isNotifyReadingRecordReaction,
                TYPE_READING_RECORD_LIKED,
                TARGET_READING_RECORD,
                readingRecordId,
                sender.getNickname() + "님이 독서 기록을 좋아합니다."
        );
    }

    @Transactional
    public void notifyReadingRecordCommented(Long receiverUserId, Long senderUserId, Long commentId) {
        User sender = userService.getUser(senderUserId);
        createIfEnabled(
                receiverUserId,
                senderUserId,
                UserNotificationSetting::isNotifyReadingRecordComment,
                TYPE_READING_RECORD_COMMENTED,
                TARGET_READING_RECORD_COMMENT,
                commentId,
                sender.getNickname() + "님이 독서 기록에 댓글을 남겼습니다."
        );
    }

    @Transactional
    public void notifyReadingRecordCommentReplied(Long receiverUserId, Long senderUserId, Long commentId) {
        User sender = userService.getUser(senderUserId);
        createIfEnabled(
                receiverUserId,
                senderUserId,
                UserNotificationSetting::isNotifyReadingRecordComment,
                TYPE_READING_RECORD_COMMENTED,
                TARGET_READING_RECORD_COMMENT,
                commentId,
                sender.getNickname() + "님이 댓글에 답글을 남겼습니다."
        );
    }

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

    private void create(Long receiverUserId, Long senderUserId,
                        String type, String targetType, Long targetId, String message) {
        if (receiverUserId == null || senderUserId == null || receiverUserId.equals(senderUserId)) {
            return;
        }
        notificationRepository.save(Notification.builder()
                .receiverUserId(receiverUserId)
                .senderUserId(senderUserId)
                .type(type)
                .targetType(targetType)
                .targetId(targetId)
                .message(truncateMessage(message))
                .build());
    }

    private static String displayTitle(String bookTitle) {
        if (bookTitle == null || bookTitle.isBlank()) {
            return "새 책";
        }
        return bookTitle.trim();
    }

    private static String truncateMessage(String message) {
        if (message.length() <= MAX_MESSAGE_LENGTH) {
            return message;
        }
        return message.substring(0, MAX_MESSAGE_LENGTH);
    }
}
