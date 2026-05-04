package com.dotshelf.domain.user.dto;

import java.util.List;

import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.entity.UserFeedSetting;
import com.dotshelf.domain.user.entity.UserNotificationSetting;

import jakarta.validation.constraints.NotBlank;
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

    public record UpdateProfileRequest(
            @NotBlank(message = "닉네임은 필수입니다.")
            @Size(max = 50, message = "닉네임은 50자 이하입니다.")
            String nickname,
            @Size(max = 500, message = "프로필 이미지 URL은 500자 이하입니다.")
            String profileImageUrl,
            @Size(max = 255, message = "한 줄 소개는 255자 이하입니다.")
            String introduction
    ) {
    }

    public record NicknameAvailabilityResponse(boolean available) {
    }

    public record UserSummaryResponse(
            Long id,
            String nickname,
            String profileImageUrl,
            boolean mutual,
            boolean following,
            boolean blocked
    ) {

        public static UserSummaryResponse from(User user) {
            return from(user, false, false, false);
        }

        public static UserSummaryResponse from(User user, boolean mutual, boolean following, boolean blocked) {
            return new UserSummaryResponse(
                    user.getId(),
                    user.getNickname(),
                    user.getProfileImageUrl(),
                    mutual,
                    following,
                    blocked
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
