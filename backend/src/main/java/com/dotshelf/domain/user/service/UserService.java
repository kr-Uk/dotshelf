package com.dotshelf.domain.user.service;

import java.util.List;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.user.dto.UserDtos.FeedSettingRequest;
import com.dotshelf.domain.user.dto.UserDtos.FeedSettingResponse;
import com.dotshelf.domain.user.dto.UserDtos.NicknameAvailabilityResponse;
import com.dotshelf.domain.user.dto.UserDtos.NotificationSettingRequest;
import com.dotshelf.domain.user.dto.UserDtos.NotificationSettingResponse;
import com.dotshelf.domain.user.dto.UserDtos.UpdateProfileRequest;
import com.dotshelf.domain.user.dto.UserDtos.UserListResponse;
import com.dotshelf.domain.user.dto.UserDtos.UserProfileResponse;
import com.dotshelf.domain.user.dto.UserDtos.UserSummaryResponse;
import com.dotshelf.domain.user.entity.Follow;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.entity.UserBlock;
import com.dotshelf.domain.user.entity.UserFeedSetting;
import com.dotshelf.domain.user.entity.UserNotificationSetting;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.repository.UserFeedSettingRepository;
import com.dotshelf.domain.user.repository.UserNotificationSettingRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class UserService {

    private final UserRepository userRepository;
    private final FollowRepository followRepository;
    private final UserBlockRepository userBlockRepository;
    private final UserNotificationSettingRepository notificationSettingRepository;
    private final UserFeedSettingRepository feedSettingRepository;

    public UserProfileResponse getMe(Long userId) {
        return UserProfileResponse.from(getUser(requireUserId(userId)));
    }

    public UserProfileResponse getProfile(Long currentUserId, Long userId) {
        User user = getUser(userId);
        if (currentUserId == null || currentUserId.equals(userId)) {
            return UserProfileResponse.from(user);
        }
        boolean following = followRepository.existsByFollowerUserIdAndFollowingUserId(currentUserId, userId);
        boolean followedBy = followRepository.existsByFollowerUserIdAndFollowingUserId(userId, currentUserId);
        boolean blocked = userBlockRepository.existsByBlockerUserIdAndBlockedUserId(currentUserId, userId);
        return UserProfileResponse.from(user, following && followedBy, following, blocked);
    }

    @Transactional
    public UserProfileResponse updateMe(Long userId, UpdateProfileRequest request) {
        Long currentUserId = requireUserId(userId);
        User user = getUser(currentUserId);
        if (!user.getNickname().equals(request.nickname()) && userRepository.existsByNickname(request.nickname())) {
            throw new CustomException(ErrorCode.DUPLICATE_NICKNAME);
        }
        user.updateProfile(request.nickname(), request.profileImageUrl(), request.introduction());
        return UserProfileResponse.from(user);
    }

    @Transactional
    public void deleteMe(Long userId) {
        getUser(requireUserId(userId)).delete();
    }

    public NicknameAvailabilityResponse checkNickname(String nickname) {
        return new NicknameAvailabilityResponse(!userRepository.existsByNickname(nickname));
    }

    @Transactional
    public ToggleResponse follow(Long userId, Long targetUserId) {
        Long currentUserId = requireUserId(userId);
        validateNotSelf(currentUserId, targetUserId);
        getUser(targetUserId);
        if (!followRepository.existsByFollowerUserIdAndFollowingUserId(currentUserId, targetUserId)) {
            followRepository.save(Follow.builder()
                    .followerUserId(currentUserId)
                    .followingUserId(targetUserId)
                    .build());
        }
        return new ToggleResponse(true, followRepository.countByFollowingUserId(targetUserId));
    }

    @Transactional
    public ToggleResponse unfollow(Long userId, Long targetUserId) {
        Long currentUserId = requireUserId(userId);
        followRepository.deleteByFollowerUserIdAndFollowingUserId(currentUserId, targetUserId);
        return new ToggleResponse(false, followRepository.countByFollowingUserId(targetUserId));
    }

    public UserListResponse getFollowers(Long userId) {
        Long currentUserId = requireUserId(userId);
        List<UserSummaryResponse> users = followRepository.findByFollowingUserId(currentUserId).stream()
                .map(follow -> summaryWithRelation(getUser(follow.getFollowerUserId()), currentUserId))
                .toList();
        return new UserListResponse(users, null);
    }

    public UserListResponse getFollowings(Long userId) {
        Long currentUserId = requireUserId(userId);
        List<UserSummaryResponse> users = followRepository.findByFollowerUserId(currentUserId).stream()
                .map(follow -> summaryWithRelation(getUser(follow.getFollowingUserId()), currentUserId))
                .toList();
        return new UserListResponse(users, null);
    }

    private UserSummaryResponse summaryWithRelation(User target, Long viewerUserId) {
        if (viewerUserId == null || viewerUserId.equals(target.getId())) {
            return UserSummaryResponse.from(target);
        }
        boolean following = followRepository.existsByFollowerUserIdAndFollowingUserId(viewerUserId, target.getId());
        boolean followedBy = followRepository.existsByFollowerUserIdAndFollowingUserId(target.getId(), viewerUserId);
        boolean blocked = userBlockRepository.existsByBlockerUserIdAndBlockedUserId(viewerUserId, target.getId());
        return UserSummaryResponse.from(target, following && followedBy, following, blocked);
    }

    @Transactional
    public ToggleResponse block(Long userId, Long targetUserId) {
        Long currentUserId = requireUserId(userId);
        validateNotSelf(currentUserId, targetUserId);
        getUser(targetUserId);
        if (!userBlockRepository.existsByBlockerUserIdAndBlockedUserId(currentUserId, targetUserId)) {
            userBlockRepository.save(UserBlock.builder()
                    .blockerUserId(currentUserId)
                    .blockedUserId(targetUserId)
                    .build());
        }
        followRepository.deleteByFollowerUserIdAndFollowingUserId(currentUserId, targetUserId);
        followRepository.deleteByFollowerUserIdAndFollowingUserId(targetUserId, currentUserId);
        return new ToggleResponse(true, userBlockRepository.findByBlockerUserId(currentUserId).size());
    }

    @Transactional
    public ToggleResponse unblock(Long userId, Long targetUserId) {
        Long currentUserId = requireUserId(userId);
        userBlockRepository.deleteByBlockerUserIdAndBlockedUserId(currentUserId, targetUserId);
        return new ToggleResponse(false, userBlockRepository.findByBlockerUserId(currentUserId).size());
    }

    public UserListResponse getBlocks(Long userId) {
        Long currentUserId = requireUserId(userId);
        List<UserSummaryResponse> users = userBlockRepository.findByBlockerUserId(currentUserId).stream()
                .map(block -> summaryWithRelation(getUser(block.getBlockedUserId()), currentUserId))
                .toList();
        return new UserListResponse(users, null);
    }

    public NotificationSettingResponse getNotificationSetting(Long userId) {
        return NotificationSettingResponse.from(getOrCreateNotificationSetting(requireUserId(userId)));
    }

    @Transactional
    public NotificationSettingResponse updateNotificationSetting(Long userId, NotificationSettingRequest request) {
        UserNotificationSetting setting = getOrCreateNotificationSetting(requireUserId(userId));
        setting.update(
                request.notifyNewGuestbook(),
                request.notifyGuestbookReply(),
                request.notifyReadingRecordReaction(),
                request.notifyNewFollower(),
                request.notifyBookAddedByOthers()
        );
        return NotificationSettingResponse.from(setting);
    }

    public FeedSettingResponse getFeedSetting(Long userId) {
        return FeedSettingResponse.from(getOrCreateFeedSetting(requireUserId(userId)));
    }

    @Transactional
    public FeedSettingResponse updateFeedSetting(Long userId, FeedSettingRequest request) {
        UserFeedSetting setting = getOrCreateFeedSetting(requireUserId(userId));
        setting.update(
                request.showBookAdded(),
                request.showReadingRecord(),
                request.showSpaceVisit(),
                request.showGuestbook(),
                request.showReadingRecordComment()
        );
        return FeedSettingResponse.from(setting);
    }

    public User getUser(Long userId) {
        return userRepository.findById(userId)
                .orElseThrow(() -> new CustomException(ErrorCode.USER_NOT_FOUND));
    }

    public Long requireUserId(Long userId) {
        if (userId == null) {
            throw new CustomException(ErrorCode.UNAUTHORIZED);
        }
        return userId;
    }

    private UserNotificationSetting getOrCreateNotificationSetting(Long userId) {
        return notificationSettingRepository.findByUserId(userId)
                .orElseGet(() -> notificationSettingRepository.save(UserNotificationSetting.builder()
                        .userId(userId)
                        .build()));
    }

    private UserFeedSetting getOrCreateFeedSetting(Long userId) {
        return feedSettingRepository.findByUserId(userId)
                .orElseGet(() -> feedSettingRepository.save(UserFeedSetting.builder()
                        .userId(userId)
                        .build()));
    }

    private void validateNotSelf(Long userId, Long targetUserId) {
        if (userId.equals(targetUserId)) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "자기 자신에게 수행할 수 없는 요청입니다.");
        }
    }
}
