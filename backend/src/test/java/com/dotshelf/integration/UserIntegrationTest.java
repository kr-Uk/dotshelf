package com.dotshelf.integration;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.repository.UserFeedSettingRepository;
import com.dotshelf.domain.user.repository.UserNotificationSettingRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.support.IntegrationTestSupport;
import com.dotshelf.support.TestFixtures;

class UserIntegrationTest extends IntegrationTestSupport {

    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private UserRepository userRepository;

    @Autowired
    private FollowRepository followRepository;

    @Autowired
    private UserBlockRepository userBlockRepository;

    @Autowired
    private UserNotificationSettingRepository notificationSettingRepository;

    @Autowired
    private UserFeedSettingRepository feedSettingRepository;

    @Test
    @DisplayName("HTTP 팔로우/언팔로우는 멱등으로 처리되고 follows 테이블을 변경한다")
    void followAndUnfollow_areIdempotent() throws Exception {
        var viewer = userRepository.save(TestFixtures.user("viewer"));
        var target = userRepository.save(TestFixtures.user("target"));
        userRepository.flush();

        mockMvc.perform(post("/v1/users/{targetUserId}/follow", target.getId())
                        .header(TEST_USER_HEADER, viewer.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true))
                .andExpect(jsonPath("$.result.count").value(1));

        mockMvc.perform(post("/v1/users/{targetUserId}/follow", target.getId())
                        .header(TEST_USER_HEADER, viewer.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.count").value(1));

        assertThat(followRepository.countByFollowingUserId(target.getId())).isEqualTo(1L);

        mockMvc.perform(delete("/v1/users/{targetUserId}/follow", target.getId())
                        .header(TEST_USER_HEADER, viewer.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(false))
                .andExpect(jsonPath("$.result.count").value(0));
        assertThat(followRepository.existsByFollowerUserIdAndFollowingUserId(viewer.getId(), target.getId())).isFalse();
    }

    @Test
    @DisplayName("HTTP 차단은 양방향 팔로우를 제거하고 프로필 blocked=true를 반환한다")
    void block_removesMutualFollowsAndMarksProfileBlocked() throws Exception {
        var viewer = userRepository.save(TestFixtures.user("viewer"));
        var target = userRepository.save(TestFixtures.user("target"));
        userRepository.flush();
        followRepository.save(TestFixtures.follow(viewer.getId(), target.getId()));
        followRepository.save(TestFixtures.follow(target.getId(), viewer.getId()));
        followRepository.flush();

        mockMvc.perform(post("/v1/users/{targetUserId}/block", target.getId())
                        .header(TEST_USER_HEADER, viewer.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true))
                .andExpect(jsonPath("$.result.count").value(1));

        assertThat(userBlockRepository.existsByBlockerUserIdAndBlockedUserId(viewer.getId(), target.getId())).isTrue();
        assertThat(followRepository.existsByFollowerUserIdAndFollowingUserId(viewer.getId(), target.getId())).isFalse();
        assertThat(followRepository.existsByFollowerUserIdAndFollowingUserId(target.getId(), viewer.getId())).isFalse();

        mockMvc.perform(get("/v1/users/{userId}", target.getId())
                        .header(TEST_USER_HEADER, viewer.getId().toString()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.blocked").value(true));
    }

    @Test
    @DisplayName("알림/피드 설정 수정은 DB에 partial update로 저장된다")
    void settings_patchPersistPartialUpdates() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("settings"));

        mockMvc.perform(patch("/v1/users/me/notification-settings")
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"notifyNewGuestbook":false,"notifyNewFollower":false}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.notifyNewGuestbook").value(false))
                .andExpect(jsonPath("$.result.notifyGuestbookReply").value(true));

        mockMvc.perform(patch("/v1/users/me/feed-settings")
                        .header(TEST_USER_HEADER, user.getId().toString())
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"showBookAdded":false,"showGuestbook":false}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.showBookAdded").value(false))
                .andExpect(jsonPath("$.result.showReadingRecord").value(true));

        var notificationSetting = notificationSettingRepository.findByUserId(user.getId()).orElseThrow();
        var feedSetting = feedSettingRepository.findByUserId(user.getId()).orElseThrow();
        assertThat(notificationSetting.isNotifyNewGuestbook()).isFalse();
        assertThat(notificationSetting.isNotifyGuestbookReply()).isTrue();
        assertThat(feedSetting.isShowBookAdded()).isFalse();
        assertThat(feedSetting.isShowReadingRecord()).isTrue();
    }

    @Test
    @DisplayName("회원 탈퇴는 users row를 삭제하지 않고 status/deletedAt만 변경한다")
    void deleteMe_marksUserDeleted() throws Exception {
        var user = userRepository.saveAndFlush(TestFixtures.user("delete-me"));

        mockMvc.perform(delete("/v1/users/me")
                        .header(TEST_USER_HEADER, user.getId().toString()))
                .andExpect(status().isOk());

        var deletedUser = userRepository.findById(user.getId()).orElseThrow();
        assertThat(deletedUser.getStatus()).isEqualTo("DELETED");
        assertThat(deletedUser.getDeletedAt()).isNotNull();
    }
}
