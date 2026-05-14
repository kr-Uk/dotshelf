package com.dotshelf.domain.user.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.Mock;
import org.mockito.MockitoAnnotations;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import java.util.List;

import com.dotshelf.domain.user.dto.UserDtos.FeedSettingRequest;
import com.dotshelf.domain.user.dto.UserDtos.FeedSettingResponse;
import com.dotshelf.domain.user.dto.UserDtos.NicknameAvailabilityResponse;
import com.dotshelf.domain.user.dto.UserDtos.NotificationSettingRequest;
import com.dotshelf.domain.user.dto.UserDtos.NotificationSettingResponse;
import com.dotshelf.domain.user.dto.UserDtos.UpdateProfileRequest;
import com.dotshelf.domain.user.dto.UserDtos.UserListResponse;
import com.dotshelf.domain.user.dto.UserDtos.UserProfileResponse;
import com.dotshelf.domain.user.dto.UserDtos.UserSummaryResponse;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.GlobalExceptionHandler;

class UserControllerTest {

    @Mock
    private UserService userService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        MockitoAnnotations.openMocks(this);
        mockMvc = MockMvcBuilders
                .standaloneSetup(new UserController(userService))
                .setControllerAdvice(new GlobalExceptionHandler())
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .build();
    }

    @Test
    @DisplayName("내 프로필 조회는 CurrentUser를 서비스에 전달하고 프로필을 반환한다")
    void getMe_withCurrentUser_returnsProfile() throws Exception {
        given(userService.getMe(1L)).willReturn(new UserProfileResponse(
                1L, "user@example.com", "tester", null, null, true, false, false, false
        ));

        mockMvc.perform(get("/v1/users/me").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(1))
                .andExpect(jsonPath("$.result.nickname").value("tester"));

        then(userService).should().getMe(1L);
    }

    @Test
    @DisplayName("프로필 수정 요청은 body validation을 적용한다")
    void updateMe_blankNickname_returnsValidationError() throws Exception {
        mockMvc.perform(patch("/v1/users/me")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"nickname":" "}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("nickname")));
    }

    @Test
    @DisplayName("팔로우 API는 ToggleResponse를 envelope로 반환한다")
    void follow_validRequest_returnsToggleResponse() throws Exception {
        given(userService.follow(1L, 2L)).willReturn(new ToggleResponse(true, 7));

        mockMvc.perform(post("/v1/users/2/follow").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true))
                .andExpect(jsonPath("$.result.count").value(7));
    }

    @Test
    @DisplayName("프로필 수정은 CurrentUser와 request body를 서비스에 전달한다")
    void updateMe_validRequest_callsService() throws Exception {
        given(userService.updateMe(any(), any(UpdateProfileRequest.class))).willReturn(new UserProfileResponse(
                1L, "user@example.com", "new", null, "intro", true, false, false, false
        ));

        mockMvc.perform(patch("/v1/users/me")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"nickname":"new","introduction":"intro"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.nickname").value("new"));

        then(userService).should().updateMe(any(), any(UpdateProfileRequest.class));
    }

    @Test
    @DisplayName("회원 탈퇴 API는 CurrentUser를 서비스에 전달하고 성공 envelope를 반환한다")
    void deleteMe_callsService() throws Exception {
        mockMvc.perform(delete("/v1/users/me").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.result").doesNotExist());

        then(userService).should().deleteMe(1L);
    }

    @Test
    @DisplayName("다른 사용자 프로필 조회는 viewer와 target userId를 전달한다")
    void getProfile_passesViewerAndTarget() throws Exception {
        given(userService.getProfile(1L, 2L)).willReturn(new UserProfileResponse(
                2L, "target@example.com", "target", null, null, false, true, true, false
        ));

        mockMvc.perform(get("/v1/users/2").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(2))
                .andExpect(jsonPath("$.result.mutual").value(true));
    }

    @Test
    @DisplayName("닉네임 중복 확인은 query parameter를 서비스에 전달한다")
    void checkNickname_passesQuery() throws Exception {
        given(userService.checkNickname("tester")).willReturn(new NicknameAvailabilityResponse(true));

        mockMvc.perform(get("/v1/users/nickname/check").param("nickname", "tester"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.available").value(true));
    }

    @Test
    @DisplayName("언팔로우/차단/차단해제 API는 ToggleResponse를 반환한다")
    void relationshipToggles_returnToggleResponses() throws Exception {
        given(userService.unfollow(1L, 2L)).willReturn(new ToggleResponse(false, 6));
        given(userService.block(1L, 2L)).willReturn(new ToggleResponse(true, 1));
        given(userService.unblock(1L, 2L)).willReturn(new ToggleResponse(false, 0));

        mockMvc.perform(delete("/v1/users/2/follow").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(false));

        mockMvc.perform(post("/v1/users/2/block").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true));

        mockMvc.perform(delete("/v1/users/2/block").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(false));
    }

    @Test
    @DisplayName("팔로워/팔로잉/차단 목록 조회는 cursor와 limit을 전달한다")
    void userLists_passPagination() throws Exception {
        UserListResponse list = new UserListResponse(List.of(
                new UserSummaryResponse(2L, "target", null, false, true, false, 10L)
        ), "next");
        given(userService.getFollowers(1L, "c1", 5)).willReturn(list);
        given(userService.getFollowings(1L, "c1", 5)).willReturn(list);
        given(userService.getUserFollowers(1L, 2L, "c1", 5)).willReturn(list);
        given(userService.getUserFollowings(1L, 2L, "c1", 5)).willReturn(list);
        given(userService.getBlocks(1L)).willReturn(new UserListResponse(List.of(
                new UserSummaryResponse(3L, "blocked", null, false, false, true, 11L)
        )));

        mockMvc.perform(get("/v1/users/me/followers").header("X-Test-Admin-User-Id", "1")
                        .param("cursor", "c1").param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.nextCursor").value("next"));

        mockMvc.perform(get("/v1/users/me/followings").header("X-Test-Admin-User-Id", "1")
                        .param("cursor", "c1").param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.users[0].following").value(true));

        mockMvc.perform(get("/v1/users/2/followers").header("X-Test-Admin-User-Id", "1")
                        .param("cursor", "c1").param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.users[0].id").value(2));

        mockMvc.perform(get("/v1/users/2/followings").header("X-Test-Admin-User-Id", "1")
                        .param("cursor", "c1").param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.users[0].id").value(2));

        mockMvc.perform(get("/v1/users/me/blocks").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.users[0].blocked").value(true));
    }

    @Test
    @DisplayName("알림 설정 조회/수정은 CurrentUser와 body를 서비스에 전달한다")
    void notificationSettings_queryAndUpdate() throws Exception {
        NotificationSettingResponse response = new NotificationSettingResponse(true, true, false, true, true, false);
        given(userService.getNotificationSetting(1L)).willReturn(response);
        given(userService.updateNotificationSetting(any(), any(NotificationSettingRequest.class))).willReturn(response);

        mockMvc.perform(get("/v1/users/me/notification-settings").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.notifyReadingRecordReaction").value(false));

        mockMvc.perform(patch("/v1/users/me/notification-settings")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"notifyReadingRecordReaction":false,"notifyBookAddedByOthers":false}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.notifyBookAddedByOthers").value(false));
    }

    @Test
    @DisplayName("피드 설정 조회/수정은 CurrentUser와 body를 서비스에 전달한다")
    void feedSettings_queryAndUpdate() throws Exception {
        FeedSettingResponse response = new FeedSettingResponse(true, false, true, false, true);
        given(userService.getFeedSetting(1L)).willReturn(response);
        given(userService.updateFeedSetting(any(), any(FeedSettingRequest.class))).willReturn(response);

        mockMvc.perform(get("/v1/users/me/feed-settings").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.showReadingRecord").value(false));

        mockMvc.perform(patch("/v1/users/me/feed-settings")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"showReadingRecord":false,"showGuestbook":false}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.showGuestbook").value(false));
    }
}
