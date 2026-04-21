package com.dotshelf.domain.user.controller;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.user.dto.UserDtos.FeedSettingRequest;
import com.dotshelf.domain.user.dto.UserDtos.FeedSettingResponse;
import com.dotshelf.domain.user.dto.UserDtos.NicknameAvailabilityResponse;
import com.dotshelf.domain.user.dto.UserDtos.NotificationSettingRequest;
import com.dotshelf.domain.user.dto.UserDtos.NotificationSettingResponse;
import com.dotshelf.domain.user.dto.UserDtos.UpdateProfileRequest;
import com.dotshelf.domain.user.dto.UserDtos.UserListResponse;
import com.dotshelf.domain.user.dto.UserDtos.UserProfileResponse;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/users")
public class UserController {

    private final UserService userService;

    @GetMapping("/me")
    public ApiResponse<UserProfileResponse> getMe(@CurrentUser Long userId) {
        return ApiResponse.success(userService.getMe(userId));
    }

    @PatchMapping("/me")
    public ApiResponse<UserProfileResponse> updateMe(
            @CurrentUser Long userId,
            @RequestBody @Valid UpdateProfileRequest request
    ) {
        return ApiResponse.success("프로필이 수정되었습니다.", userService.updateMe(userId, request));
    }

    @DeleteMapping("/me")
    public ApiResponse<Void> deleteMe(@CurrentUser Long userId) {
        userService.deleteMe(userId);
        return ApiResponse.success();
    }

    @GetMapping("/{userId}")
    public ApiResponse<UserProfileResponse> getProfile(@PathVariable Long userId) {
        return ApiResponse.success(userService.getProfile(userId));
    }

    @GetMapping("/nickname/check")
    public ApiResponse<NicknameAvailabilityResponse> checkNickname(@RequestParam String nickname) {
        return ApiResponse.success(userService.checkNickname(nickname));
    }

    @PostMapping("/{targetUserId}/follow")
    public ApiResponse<ToggleResponse> follow(@CurrentUser Long userId, @PathVariable Long targetUserId) {
        return ApiResponse.success(userService.follow(userId, targetUserId));
    }

    @DeleteMapping("/{targetUserId}/follow")
    public ApiResponse<ToggleResponse> unfollow(@CurrentUser Long userId, @PathVariable Long targetUserId) {
        return ApiResponse.success(userService.unfollow(userId, targetUserId));
    }

    @GetMapping("/me/followers")
    public ApiResponse<UserListResponse> getFollowers(@CurrentUser Long userId) {
        return ApiResponse.success(userService.getFollowers(userId));
    }

    @GetMapping("/me/followings")
    public ApiResponse<UserListResponse> getFollowings(@CurrentUser Long userId) {
        return ApiResponse.success(userService.getFollowings(userId));
    }

    @PostMapping("/{targetUserId}/block")
    public ApiResponse<ToggleResponse> block(@CurrentUser Long userId, @PathVariable Long targetUserId) {
        return ApiResponse.success(userService.block(userId, targetUserId));
    }

    @DeleteMapping("/{targetUserId}/block")
    public ApiResponse<ToggleResponse> unblock(@CurrentUser Long userId, @PathVariable Long targetUserId) {
        return ApiResponse.success(userService.unblock(userId, targetUserId));
    }

    @GetMapping("/me/blocks")
    public ApiResponse<UserListResponse> getBlocks(@CurrentUser Long userId) {
        return ApiResponse.success(userService.getBlocks(userId));
    }

    @GetMapping("/me/notification-settings")
    public ApiResponse<NotificationSettingResponse> getNotificationSetting(@CurrentUser Long userId) {
        return ApiResponse.success(userService.getNotificationSetting(userId));
    }

    @PatchMapping("/me/notification-settings")
    public ApiResponse<NotificationSettingResponse> updateNotificationSetting(
            @CurrentUser Long userId,
            @RequestBody NotificationSettingRequest request
    ) {
        return ApiResponse.success(userService.updateNotificationSetting(userId, request));
    }

    @GetMapping("/me/feed-settings")
    public ApiResponse<FeedSettingResponse> getFeedSetting(@CurrentUser Long userId) {
        return ApiResponse.success(userService.getFeedSetting(userId));
    }

    @PatchMapping("/me/feed-settings")
    public ApiResponse<FeedSettingResponse> updateFeedSetting(
            @CurrentUser Long userId,
            @RequestBody FeedSettingRequest request
    ) {
        return ApiResponse.success(userService.updateFeedSetting(userId, request));
    }
}
