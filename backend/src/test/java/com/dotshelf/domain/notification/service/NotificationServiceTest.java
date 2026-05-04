package com.dotshelf.domain.notification.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.times;

import java.util.List;
import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.notification.entity.Notification;
import com.dotshelf.domain.notification.repository.NotificationRepository;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.entity.UserNotificationSetting;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.repository.UserNotificationSettingRepository;
import com.dotshelf.domain.user.service.UserService;

@ExtendWith(MockitoExtension.class)
class NotificationServiceTest {

    @Mock
    private NotificationRepository notificationRepository;

    @Mock
    private UserNotificationSettingRepository notificationSettingRepository;

    @Mock
    private FollowRepository followRepository;

    @Mock
    private UserService userService;

    @InjectMocks
    private NotificationService notificationService;

    @Test
    @DisplayName("익명 방명록 알림은 작성자 닉네임과 senderUserId를 노출하지 않는다")
    void notifyGuestbookCreated_anonymous_masksSender() {
        given(notificationSettingRepository.findByUserId(1L))
                .willReturn(Optional.of(UserNotificationSetting.builder()
                        .userId(1L)
                        .build()));
        ArgumentCaptor<Notification> captor = ArgumentCaptor.forClass(Notification.class);

        notificationService.notifyGuestbookCreated(1L, 2L, 30L, true);

        then(notificationRepository).should().save(captor.capture());
        Notification notification = captor.getValue();
        assertThat(notification.getReceiverUserId()).isEqualTo(1L);
        assertThat(notification.getSenderUserId()).isNull();
        assertThat(notification.getType()).isEqualTo("GUESTBOOK_CREATED");
        assertThat(notification.getTargetType()).isEqualTo("GUESTBOOK");
        assertThat(notification.getTargetId()).isEqualTo(30L);
        assertThat(notification.getMessage()).isEqualTo("익명 사용자가 방명록을 남겼습니다.");
        then(userService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("독서 기록 좋아요 알림 설정이 켜져 있으면 Notification을 저장한다")
    void notifyReadingRecordLiked_settingOn_savesNotification() {
        given(userService.getUser(2L)).willReturn(user("민수"));
        given(notificationSettingRepository.findByUserId(1L))
                .willReturn(Optional.of(UserNotificationSetting.builder()
                        .userId(1L)
                        .build()));
        ArgumentCaptor<Notification> captor = ArgumentCaptor.forClass(Notification.class);

        notificationService.notifyReadingRecordLiked(1L, 2L, 10L);

        then(notificationRepository).should().save(captor.capture());
        Notification notification = captor.getValue();
        assertThat(notification.getReceiverUserId()).isEqualTo(1L);
        assertThat(notification.getSenderUserId()).isEqualTo(2L);
        assertThat(notification.getType()).isEqualTo("READING_RECORD_LIKED");
        assertThat(notification.getTargetType()).isEqualTo("READING_RECORD");
        assertThat(notification.getTargetId()).isEqualTo(10L);
    }

    @Test
    @DisplayName("독서 기록 댓글 알림 설정이 꺼져 있으면 Notification을 저장하지 않는다")
    void notifyReadingRecordCommented_settingOff_skipsNotification() {
        UserNotificationSetting setting = UserNotificationSetting.builder()
                .userId(1L)
                .build();
        setting.update(null, null, null, false, null, null);
        given(userService.getUser(2L)).willReturn(user("민수"));
        given(notificationSettingRepository.findByUserId(1L)).willReturn(Optional.of(setting));

        notificationService.notifyReadingRecordCommented(1L, 2L, 20L);

        then(notificationRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("책 추가 알림은 책을 추가한 사용자의 팔로워 중 설정이 켜진 사용자에게만 저장한다")
    void notifyBookAddedToFollowers_savesOnlyEnabledFollowers() {
        UserNotificationSetting enabled = UserNotificationSetting.builder()
                .userId(1L)
                .build();
        UserNotificationSetting disabled = UserNotificationSetting.builder()
                .userId(2L)
                .build();
        disabled.update(null, null, null, null, null, false);
        given(userService.getUser(7L)).willReturn(user("지은"));
        given(followRepository.findFollowerUserIdsByFollowingUserId(7L)).willReturn(List.of(1L, 2L));
        given(notificationSettingRepository.findByUserIdIn(List.of(1L, 2L))).willReturn(List.of(enabled, disabled));
        ArgumentCaptor<Notification> captor = ArgumentCaptor.forClass(Notification.class);

        notificationService.notifyBookAddedToFollowers(7L, 100L, "데미안");

        then(notificationRepository).should(times(1)).save(captor.capture());
        Notification notification = captor.getValue();
        assertThat(notification.getReceiverUserId()).isEqualTo(1L);
        assertThat(notification.getType()).isEqualTo("BOOK_ADDED");
        assertThat(notification.getTargetType()).isEqualTo("USER_BOOK");
        assertThat(notification.getTargetId()).isEqualTo(100L);
    }

    @Test
    @DisplayName("설정 row가 없으면 기본 ON으로 간주하되 새 설정 row를 생성하지 않는다")
    void notifyReadingRecordLiked_missingSetting_savesWithoutCreatingSetting() {
        given(userService.getUser(2L)).willReturn(user("민수"));
        given(notificationSettingRepository.findByUserId(1L)).willReturn(Optional.empty());
        ArgumentCaptor<Notification> captor = ArgumentCaptor.forClass(Notification.class);

        notificationService.notifyReadingRecordLiked(1L, 2L, 10L);

        then(notificationRepository).should().save(captor.capture());
        then(notificationSettingRepository).should().findByUserId(1L);
        then(notificationSettingRepository).shouldHaveNoMoreInteractions();
        assertThat(captor.getValue().getReceiverUserId()).isEqualTo(1L);
    }

    @Test
    @DisplayName("댓글 답글 알림은 댓글 알림과 다른 type으로 저장한다")
    void notifyReadingRecordCommentReplied_savesReplyType() {
        given(userService.getUser(2L)).willReturn(user("민수"));
        given(notificationSettingRepository.findByUserId(1L))
                .willReturn(Optional.of(UserNotificationSetting.builder()
                        .userId(1L)
                        .build()));
        ArgumentCaptor<Notification> captor = ArgumentCaptor.forClass(Notification.class);

        notificationService.notifyReadingRecordCommentReplied(1L, 2L, 20L);

        then(notificationRepository).should().save(captor.capture());
        assertThat(captor.getValue().getType()).isEqualTo("READING_RECORD_COMMENT_REPLIED");
    }

    @Test
    @DisplayName("발신자와 수신자가 같으면 Notification을 저장하지 않는다")
    void notifyReadingRecordLiked_selfNotification_skipsNotification() {
        notificationService.notifyReadingRecordLiked(1L, 1L, 10L);

        then(userService).shouldHaveNoInteractions();
        then(notificationSettingRepository).shouldHaveNoInteractions();
        then(notificationRepository).shouldHaveNoInteractions();
    }

    private static User user(String nickname) {
        return User.builder()
                .email(nickname + "@example.com")
                .nickname(nickname)
                .build();
    }
}
