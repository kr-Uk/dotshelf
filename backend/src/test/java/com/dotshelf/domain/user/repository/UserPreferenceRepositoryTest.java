package com.dotshelf.domain.user.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.dao.DataIntegrityViolationException;

import com.dotshelf.support.RepositoryTestSupport;
import com.dotshelf.support.TestFixtures;

class UserPreferenceRepositoryTest extends RepositoryTestSupport {

    @Autowired
    private UserNotificationSettingRepository notificationSettingRepository;

    @Autowired
    private UserFeedSettingRepository feedSettingRepository;

    @Test
    @DisplayName("알림 설정은 userId로 조회되고 기본값은 모두 true다")
    void notificationSetting_findByUserId_defaultsTrue() {
        notificationSettingRepository.saveAndFlush(TestFixtures.notificationSetting(1L));

        var setting = notificationSettingRepository.findByUserId(1L);

        assertThat(setting).isPresent();
        assertThat(setting.get().isNotifyNewGuestbook()).isTrue();
        assertThat(setting.get().isNotifyBookAddedByOthers()).isTrue();
    }

    @Test
    @DisplayName("알림 설정 userId는 중복 저장할 수 없다")
    void notificationSetting_uniqueUserId() {
        notificationSettingRepository.saveAndFlush(TestFixtures.notificationSetting(1L));

        assertThatThrownBy(() -> notificationSettingRepository.saveAndFlush(TestFixtures.notificationSetting(1L)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("알림 설정은 여러 userId를 한 번에 조회한다")
    void notificationSetting_findByUserIdIn() {
        notificationSettingRepository.save(TestFixtures.notificationSetting(1L));
        notificationSettingRepository.save(TestFixtures.notificationSetting(2L));
        notificationSettingRepository.save(TestFixtures.notificationSetting(3L));
        notificationSettingRepository.flush();

        var settings = notificationSettingRepository.findByUserIdIn(java.util.List.of(1L, 3L));

        assertThat(settings)
                .extracting("userId")
                .containsExactlyInAnyOrder(1L, 3L);
    }

    @Test
    @DisplayName("피드 설정은 userId로 조회되고 partial update에서 null 값은 기존 값을 유지한다")
    void feedSetting_findByUserIdAndPartialUpdate() {
        var setting = feedSettingRepository.saveAndFlush(TestFixtures.feedSetting(1L));

        setting.update(false, null, false, null, true);
        feedSettingRepository.flush();

        var result = feedSettingRepository.findByUserId(1L);

        assertThat(result).isPresent();
        assertThat(result.get().isShowBookAdded()).isFalse();
        assertThat(result.get().isShowReadingRecord()).isTrue();
        assertThat(result.get().isShowSpaceVisit()).isFalse();
        assertThat(result.get().isShowGuestbook()).isTrue();
        assertThat(result.get().isShowReadingRecordComment()).isTrue();
    }

    @Test
    @DisplayName("피드 설정 userId는 중복 저장할 수 없다")
    void feedSetting_uniqueUserId() {
        feedSettingRepository.saveAndFlush(TestFixtures.feedSetting(1L));

        assertThatThrownBy(() -> feedSettingRepository.saveAndFlush(TestFixtures.feedSetting(1L)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }
}
