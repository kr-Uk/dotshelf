package com.dotshelf.domain.notification.repository;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class NotificationRepositoryTest {

    @Autowired
    private NotificationRepository notificationRepository;

    @Test
    @DisplayName("알림은 receiverUserId 기준 id 역순으로 조회한다")
    void findByReceiverUserIdOrderByIdDesc() {
        var first = notificationRepository.save(TestFixtures.notification(null, 1L, 2L));
        var second = notificationRepository.save(TestFixtures.notification(null, 1L, 3L));
        notificationRepository.save(TestFixtures.notification(null, 2L, 3L));

        assertThat(notificationRepository.findByReceiverUserIdOrderByIdDesc(1L))
                .extracting("id")
                .containsExactly(second.getId(), first.getId());
    }

    @Test
    @DisplayName("읽지 않은 알림만 조회한다")
    void findUnreadNotifications() {
        var unread = notificationRepository.save(TestFixtures.notification(null, 1L, 2L));
        var read = TestFixtures.notification(null, 1L, 3L);
        read.read();
        notificationRepository.save(read);

        assertThat(notificationRepository.findByReceiverUserIdAndIsReadFalse(1L))
                .extracting("id")
                .containsExactly(unread.getId());
    }

    @Test
    @DisplayName("read 처리 후 unread 조회에서 제외된다")
    void readNotification_removedFromUnreadQuery() {
        var notification = notificationRepository.save(TestFixtures.notification(null, 1L, 2L));
        notification.read();
        notificationRepository.flush();

        assertThat(notificationRepository.findByReceiverUserIdAndIsReadFalse(1L)).isEmpty();
    }
}
