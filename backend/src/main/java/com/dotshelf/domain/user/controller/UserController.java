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
import com.dotshelf.domain.user.dto.UserDtos.ProfileImageConfirmRequest;
import com.dotshelf.domain.user.dto.UserDtos.ProfileImageUploadUrlRequest;
import com.dotshelf.domain.user.dto.UserDtos.ProfileImageUploadUrlResponse;
import com.dotshelf.domain.user.dto.UserDtos.UpdateProfileRequest;
import com.dotshelf.domain.user.dto.UserDtos.UserListResponse;
import com.dotshelf.domain.user.dto.UserDtos.UserProfileResponse;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirements;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "User", description = "사용자 프로필 / 팔로우 / 차단 / 알림·피드 설정")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/users")
public class UserController {

    private final UserService userService;

    @Operation(summary = "내 프로필 조회", description = "현재 로그인한 사용자의 프로필 정보를 조회합니다.")
    @GetMapping("/me")
    public ApiResponse<UserProfileResponse> getMe(@CurrentUser Long userId) {
        return ApiResponse.success(userService.getMe(userId));
    }

    @Operation(summary = "내 프로필 수정", description = "현재 로그인한 사용자의 닉네임, 소개 등 프로필 정보를 수정합니다.")
    @PatchMapping("/me")
    public ApiResponse<UserProfileResponse> updateMe(
            @CurrentUser Long userId,
            @RequestBody @Valid UpdateProfileRequest request
    ) {
        return ApiResponse.success("프로필이 수정되었습니다.", userService.updateMe(userId, request));
    }

    @Operation(summary = "회원 탈퇴", description = "현재 사용자를 soft delete 처리합니다. 실제 데이터 삭제는 별도 배치에서 처리합니다.")
    @DeleteMapping("/me")
    public ApiResponse<Void> deleteMe(@CurrentUser Long userId) {
        userService.deleteMe(userId);
        return ApiResponse.success();
    }

    @Operation(
            summary = "프로필 이미지 업로드 URL 발급",
            description = "클라가 contentType / byteLength 를 신고하면 BE 가 S3 presigned PUT URL 을 발급한다. "
                    + "클라는 발급된 URL 로 S3 에 직접 PUT 한 뒤 PATCH /me/profile-image 로 confirm 호출."
    )
    @PostMapping("/me/profile-image/upload-url")
    public ApiResponse<ProfileImageUploadUrlResponse> createProfileImageUploadUrl(
            @CurrentUser Long userId,
            @RequestBody @Valid ProfileImageUploadUrlRequest request
    ) {
        return ApiResponse.success(userService.createProfileImageUploadUrl(userId, request));
    }

    @Operation(
            summary = "프로필 이미지 업로드 확정",
            description = "presigned PUT 으로 S3 직업로드를 마친 뒤 호출. BE 가 객체 존재를 검증하고 사용자 profileImageUrl 을 갱신한다."
    )
    @PatchMapping("/me/profile-image")
    public ApiResponse<UserProfileResponse> confirmProfileImage(
            @CurrentUser Long userId,
            @RequestBody @Valid ProfileImageConfirmRequest request
    ) {
        return ApiResponse.success(userService.confirmProfileImage(userId, request));
    }

    @Operation(summary = "다른 사용자 프로필 조회", description = "사용자 ID로 다른 사용자의 공개 프로필과 현재 사용자 기준 관계 상태를 조회합니다.")
    @GetMapping("/{userId}")
    public ApiResponse<UserProfileResponse> getProfile(
            @CurrentUser Long currentUserId,
            @PathVariable Long userId
    ) {
        return ApiResponse.success(userService.getProfile(currentUserId, userId));
    }

    @Operation(summary = "닉네임 중복 확인", description = "회원가입 또는 프로필 수정 전에 닉네임 사용 가능 여부를 확인합니다.")
    @SecurityRequirements({})
    @GetMapping("/nickname/check")
    public ApiResponse<NicknameAvailabilityResponse> checkNickname(@RequestParam String nickname) {
        return ApiResponse.success(userService.checkNickname(nickname));
    }

    @Operation(summary = "팔로우", description = "대상 사용자를 팔로우합니다. 이미 팔로우 중이어도 에러 없이 현재 상태를 반환합니다.")
    @PostMapping("/{targetUserId}/follow")
    public ApiResponse<ToggleResponse> follow(@CurrentUser Long userId, @PathVariable Long targetUserId) {
        return ApiResponse.success(userService.follow(userId, targetUserId));
    }

    @Operation(summary = "팔로우 취소", description = "대상 사용자 팔로우를 취소합니다. 이미 취소된 상태여도 현재 상태를 반환합니다.")
    @DeleteMapping("/{targetUserId}/follow")
    public ApiResponse<ToggleResponse> unfollow(@CurrentUser Long userId, @PathVariable Long targetUserId) {
        return ApiResponse.success(userService.unfollow(userId, targetUserId));
    }

    @Operation(summary = "내 팔로워 목록", description = "현재 사용자를 팔로우하는 사용자 목록을 커서 기반으로 조회합니다.")
    @GetMapping("/me/followers")
    public ApiResponse<UserListResponse> getFollowers(
            @CurrentUser Long userId,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "30") int limit
    ) {
        return ApiResponse.success(userService.getFollowers(userId, cursor, limit));
    }

    @Operation(summary = "내 팔로잉 목록", description = "현재 사용자가 팔로우하는 사용자 목록을 커서 기반으로 조회합니다.")
    @GetMapping("/me/followings")
    public ApiResponse<UserListResponse> getFollowings(
            @CurrentUser Long userId,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "30") int limit
    ) {
        return ApiResponse.success(userService.getFollowings(userId, cursor, limit));
    }

    @Operation(summary = "다른 사용자의 팔로워 목록", description = "지정한 사용자를 팔로우하는 사용자 목록을 커서 기반으로 조회합니다.")
    @GetMapping("/{userId}/followers")
    public ApiResponse<UserListResponse> getUserFollowers(
            @CurrentUser Long currentUserId,
            @PathVariable Long userId,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "30") int limit
    ) {
        return ApiResponse.success(userService.getUserFollowers(currentUserId, userId, cursor, limit));
    }

    @Operation(summary = "다른 사용자의 팔로잉 목록", description = "지정한 사용자가 팔로우하는 사용자 목록을 커서 기반으로 조회합니다.")
    @GetMapping("/{userId}/followings")
    public ApiResponse<UserListResponse> getUserFollowings(
            @CurrentUser Long currentUserId,
            @PathVariable Long userId,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "30") int limit
    ) {
        return ApiResponse.success(userService.getUserFollowings(currentUserId, userId, cursor, limit));
    }

    @Operation(summary = "차단", description = "대상 사용자를 차단합니다. 이미 차단된 상태여도 현재 상태를 반환합니다.")
    @PostMapping("/{targetUserId}/block")
    public ApiResponse<ToggleResponse> block(@CurrentUser Long userId, @PathVariable Long targetUserId) {
        return ApiResponse.success(userService.block(userId, targetUserId));
    }

    @Operation(summary = "차단 해제", description = "대상 사용자 차단을 해제합니다. 이미 해제된 상태여도 현재 상태를 반환합니다.")
    @DeleteMapping("/{targetUserId}/block")
    public ApiResponse<ToggleResponse> unblock(@CurrentUser Long userId, @PathVariable Long targetUserId) {
        return ApiResponse.success(userService.unblock(userId, targetUserId));
    }

    @Operation(summary = "내가 차단한 사용자 목록", description = "현재 사용자가 차단한 사용자 목록을 조회합니다.")
    @GetMapping("/me/blocks")
    public ApiResponse<UserListResponse> getBlocks(@CurrentUser Long userId) {
        return ApiResponse.success(userService.getBlocks(userId));
    }

    @Operation(summary = "알림 설정 조회", description = "현재 사용자의 알림 수신 설정을 조회합니다.")
    @GetMapping("/me/notification-settings")
    public ApiResponse<NotificationSettingResponse> getNotificationSetting(@CurrentUser Long userId) {
        return ApiResponse.success(userService.getNotificationSetting(userId));
    }

    @Operation(summary = "알림 설정 수정", description = "현재 사용자의 알림 수신 설정을 수정합니다.")
    @PatchMapping("/me/notification-settings")
    public ApiResponse<NotificationSettingResponse> updateNotificationSetting(
            @CurrentUser Long userId,
            @RequestBody NotificationSettingRequest request
    ) {
        return ApiResponse.success(userService.updateNotificationSetting(userId, request));
    }

    @Operation(summary = "피드 설정 조회", description = "현재 사용자의 피드 노출 및 추천 설정을 조회합니다.")
    @GetMapping("/me/feed-settings")
    public ApiResponse<FeedSettingResponse> getFeedSetting(@CurrentUser Long userId) {
        return ApiResponse.success(userService.getFeedSetting(userId));
    }

    @Operation(summary = "피드 설정 수정", description = "현재 사용자의 피드 노출 및 추천 설정을 수정합니다.")
    @PatchMapping("/me/feed-settings")
    public ApiResponse<FeedSettingResponse> updateFeedSetting(
            @CurrentUser Long userId,
            @RequestBody FeedSettingRequest request
    ) {
        return ApiResponse.success(userService.updateFeedSetting(userId, request));
    }
}
