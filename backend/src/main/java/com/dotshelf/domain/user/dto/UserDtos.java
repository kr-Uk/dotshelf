package com.dotshelf.domain.user.dto;

import java.util.List;

import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.entity.UserFeedSetting;
import com.dotshelf.domain.user.entity.UserNotificationSetting;
import com.dotshelf.global.storage.S3StorageService.PresignedPut;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.Size;

public final class UserDtos {

    private UserDtos() {
    }

    public record UserProfileResponse(
            Long id,
            String email,
            String nickname,
            String profileImageUrl,
            String introduction,
            boolean onboarded,
            boolean mutual,
            boolean following,
            boolean blocked
    ) {

        public static UserProfileResponse from(User user) {
            return from(user, false, false, false);
        }

        public static UserProfileResponse from(User user, boolean mutual, boolean following, boolean blocked) {
            return new UserProfileResponse(
                    user.getId(),
                    user.getEmail(),
                    user.getNickname(),
                    user.getProfileImageUrl(),
                    user.getIntroduction(),
                    user.isOnboarded(),
                    mutual,
                    following,
                    blocked
            );
        }
    }

    /**
     * 프로필 이미지 URL 은 이 요청으로 직접 바꾸지 않는다 — 임의 외부 URL 저장으로 S3 업로드 검증을 우회하지 못하게,
     * 변경은 반드시 POST /v1/users/me/profile-image/upload-url + PATCH /v1/users/me/profile-image 흐름을 거친다.
     */
    public record UpdateProfileRequest(
            @NotBlank(message = "닉네임은 필수입니다.")
            @Size(max = 50, message = "닉네임은 50자 이하입니다.")
            String nickname,
            @Size(max = 255, message = "한 줄 소개는 255자 이하입니다.")
            String introduction
    ) {
    }

    public record NicknameAvailabilityResponse(boolean available) {
    }

    /**
     * presigned PUT 발급 요청. 클라가 올릴 파일의 contentType / 크기를 미리 신고하면
     * BE 가 화이트리스트 검증 후 S3 PUT URL 을 발급한다.
     */
    public record ProfileImageUploadUrlRequest(
            @NotBlank(message = "contentType은 필수입니다.")
            String contentType,
            @NotNull(message = "byteLength는 필수입니다.")
            @Positive(message = "byteLength는 양수여야 합니다.")
            Long byteLength
    ) {
    }

    public record ProfileImageUploadUrlResponse(
            String uploadUrl,
            String key,
            long expiresInSeconds
    ) {

        public static ProfileImageUploadUrlResponse from(PresignedPut presigned) {
            return new ProfileImageUploadUrlResponse(
                    presigned.uploadUrl(),
                    presigned.key(),
                    presigned.expiresInSeconds());
        }
    }

    /**
     * presigned PUT 으로 클라가 S3 직업로드를 마친 뒤 BE 에 \"실제로 올렸다\"고 알리는 confirm.
     * BE 는 headObject 로 객체 존재를 확인한 뒤에만 DB 의 profileImageUrl 을 갱신한다.
     */
    public record ProfileImageConfirmRequest(
            @NotBlank(message = "key는 필수입니다.")
            String key
    ) {
    }

    public record UserSummaryResponse(
            Long id,
            String nickname,
            String profileImageUrl,
            boolean mutual,
            boolean following,
            boolean blocked,
            Long spaceId
    ) {

        public static UserSummaryResponse from(User user) {
            return from(user, false, false, false, null);
        }

        public static UserSummaryResponse from(User user, boolean mutual, boolean following, boolean blocked) {
            return from(user, mutual, following, blocked, null);
        }

        public static UserSummaryResponse from(User user, boolean mutual, boolean following, boolean blocked, Long spaceId) {
            return new UserSummaryResponse(
                    user.getId(),
                    user.getNickname(),
                    user.getProfileImageUrl(),
                    mutual,
                    following,
                    blocked,
                    spaceId
            );
        }
    }

    public record UserListResponse(List<UserSummaryResponse> users, String nextCursor) {

        public UserListResponse(List<UserSummaryResponse> users) {
            this(users, null);
        }
    }

    public record NotificationSettingRequest(
            Boolean notifyNewGuestbook,
            Boolean notifyGuestbookReply,
            Boolean notifyReadingRecordReaction,
            Boolean notifyReadingRecordComment,
            Boolean notifyNewFollower,
            Boolean notifyBookAddedByOthers
    ) {
    }

    public record NotificationSettingResponse(
            boolean notifyNewGuestbook,
            boolean notifyGuestbookReply,
            boolean notifyReadingRecordReaction,
            boolean notifyReadingRecordComment,
            boolean notifyNewFollower,
            boolean notifyBookAddedByOthers
    ) {

        public static NotificationSettingResponse from(UserNotificationSetting setting) {
            return new NotificationSettingResponse(
                    setting.isNotifyNewGuestbook(),
                    setting.isNotifyGuestbookReply(),
                    setting.isNotifyReadingRecordReaction(),
                    setting.isNotifyReadingRecordComment(),
                    setting.isNotifyNewFollower(),
                    setting.isNotifyBookAddedByOthers()
            );
        }
    }

    public record FeedSettingRequest(
            Boolean showBookAdded,
            Boolean showReadingRecord,
            Boolean showSpaceVisit,
            Boolean showGuestbook,
            Boolean showReadingRecordComment
    ) {
    }

    public record FeedSettingResponse(
            boolean showBookAdded,
            boolean showReadingRecord,
            boolean showSpaceVisit,
            boolean showGuestbook,
            boolean showReadingRecordComment
    ) {

        public static FeedSettingResponse from(UserFeedSetting setting) {
            return new FeedSettingResponse(
                    setting.isShowBookAdded(),
                    setting.isShowReadingRecord(),
                    setting.isShowSpaceVisit(),
                    setting.isShowGuestbook(),
                    setting.isShowReadingRecordComment()
            );
        }
    }
}
