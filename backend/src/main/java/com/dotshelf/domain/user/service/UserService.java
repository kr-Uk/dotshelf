package com.dotshelf.domain.user.service;

import java.time.Duration;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.UUID;
import java.util.function.Function;
import java.util.stream.Collectors;

import org.springframework.data.domain.PageRequest;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.repository.BookshelfItemRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfRepository;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.repository.SpaceRepository;
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
import com.dotshelf.global.storage.S3StorageProperties;
import com.dotshelf.global.storage.S3StorageService;
import com.dotshelf.global.storage.S3StorageService.ObjectMetadata;
import com.dotshelf.global.storage.S3StorageService.PresignedPut;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class UserService {

    // 프로필 이미지 — 너무 큰 파일은 막되 카메라 원본(보통 3~4MB)은 받을 수 있도록 5MB.
    private static final long MAX_PROFILE_IMAGE_SIZE_BYTES = 5L * 1024 * 1024;
    private static final String ACTIVE = "ACTIVE";
    private static final String DELETED = "DELETED";
    private static final Map<String, String> ALLOWED_PROFILE_IMAGE_TYPES = Map.of(
            "image/jpeg", "jpg",
            "image/png", "png",
            "image/webp", "webp");

    private final UserRepository userRepository;
    private final SpaceRepository spaceRepository;
    private final BookshelfRepository bookshelfRepository;
    private final BookshelfItemRepository bookshelfItemRepository;
    private final UserBookRepository userBookRepository;
    private final FollowRepository followRepository;
    private final UserBlockRepository userBlockRepository;
    private final UserNotificationSettingRepository notificationSettingRepository;
    private final UserFeedSettingRepository feedSettingRepository;
    private final S3StorageService s3StorageService;
    private final S3StorageProperties s3StorageProperties;

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
        // 프로필 이미지 URL 은 본 메서드로 변경하지 않는다 — S3 업로드/confirm 흐름으로만 갱신.
        // 닉네임/소개만 갱신하고 기존 이미지 URL 은 그대로 보존.
        user.updateProfile(request.nickname(), user.getProfileImageUrl(), request.introduction());
        completeOnboarding(user);
        return UserProfileResponse.from(user);
    }

    @Transactional
    public void completeOnboardingIfNeeded(Long userId) {
        completeOnboarding(getUser(requireUserId(userId)));
    }

    private static void completeOnboarding(User user) {
        if (!user.isOnboarded()) {
            user.completeOnboarding();
        }
    }

    @Transactional
    public void deleteMe(Long userId) {
        Long currentUserId = requireUserId(userId);
        removeShelfDataForWithdrawal(currentUserId);
        getUser(currentUserId).delete();
    }

    // Account withdrawal is a soft-delete flow. This cleanup is intentionally
    // scoped to the user's personal room/bookshelf state so re-login starts
    // from onboarding without resurrecting stale shelves; social identity and
    // broader account-owned records are handled by the user lifecycle policy.
    private void removeShelfDataForWithdrawal(Long userId) {
        List<Space> spaces = spaceRepository.findByOwnerUserIdAndDeletedAtIsNull(userId);
        List<Long> spaceIds = spaces.stream()
                .map(Space::getId)
                .toList();
        List<Bookshelf> bookshelves = spaceIds.isEmpty()
                ? List.of()
                : bookshelfRepository.findBySpaceIdInAndStatusOrderBySpaceIdAscDisplayOrderAsc(spaceIds, ACTIVE);
        List<Long> bookshelfIds = bookshelves.stream()
                .map(Bookshelf::getId)
                .toList();

        if (!bookshelfIds.isEmpty()) {
            bookshelfItemRepository.findByBookshelfIdInAndStatus(bookshelfIds, ACTIVE)
                    .forEach(BookshelfItem::remove);
        }

        bookshelves.forEach(Bookshelf::delete);
        spaces.forEach(Space::delete);
        userBookRepository.findByUserIdAndStorageStatusNot(userId, DELETED)
                .forEach(UserBook::delete);
    }

    /**
     * 프로필 이미지 presigned PUT URL 발급. 클라가 contentType / byteLength 신고 → 화이트리스트 검증 →
     * key("users/{userId}/profile/{uuid}.{ext}") 생성 → presigned URL 반환.
     * 클라는 응답의 uploadUrl 로 S3 에 직업로드한 뒤 confirm 으로 BE 에 알린다.
     */
    public ProfileImageUploadUrlResponse createProfileImageUploadUrl(Long userId, ProfileImageUploadUrlRequest request) {
        Long currentUserId = requireUserId(userId);
        if (!ALLOWED_PROFILE_IMAGE_TYPES.containsKey(request.contentType())) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
        }
        if (request.byteLength() > MAX_PROFILE_IMAGE_SIZE_BYTES) {
            throw new CustomException(ErrorCode.STORAGE_FILE_TOO_LARGE);
        }
        String extension = ALLOWED_PROFILE_IMAGE_TYPES.get(request.contentType());
        String key = "users/" + currentUserId + "/profile/" + UUID.randomUUID() + "." + extension;
        PresignedPut presigned = s3StorageService.presignPut(
                key,
                request.contentType(),
                request.byteLength(),
                Duration.ofSeconds(s3StorageProperties.presignTtlSeconds()));
        return ProfileImageUploadUrlResponse.from(presigned);
    }

    /**
     * 클라 직업로드 후 confirm. 검증 layer 3 단:
     *   1) key prefix → 다른 사용자의 객체를 자기 프로필로 등록하는 패턴 차단
     *   2) headObject → 객체 실존 + S3 가 받은 contentType / contentLength 확인
     *   3) 도메인 화이트리스트 재검증 → 1단계 발급 신고 값과 도메인 정책 일치 확인
     */
    @Transactional
    public UserProfileResponse confirmProfileImage(Long userId, ProfileImageConfirmRequest request) {
        Long currentUserId = requireUserId(userId);
        String prefix = "users/" + currentUserId + "/profile/";
        if (!request.key().startsWith(prefix)) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_KEY);
        }
        ObjectMetadata metadata = s3StorageService.headObject(request.key());
        if (!ALLOWED_PROFILE_IMAGE_TYPES.containsKey(metadata.contentType())) {
            throw new CustomException(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
        }
        if (metadata.contentLength() > MAX_PROFILE_IMAGE_SIZE_BYTES) {
            throw new CustomException(ErrorCode.STORAGE_FILE_TOO_LARGE);
        }
        User user = getUser(currentUserId);
        user.updateProfile(user.getNickname(), s3StorageService.publicUrl(request.key()), user.getIntroduction());
        return UserProfileResponse.from(user);
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

    private static final int DEFAULT_FOLLOW_LIMIT = 30;
    private static final int MAX_FOLLOW_LIMIT = 50;

    public UserListResponse getFollowers(Long userId, String cursor, int limit) {
        Long currentUserId = requireUserId(userId);
        return loadFollowersPage(currentUserId, currentUserId, cursor, limit);
    }

    public UserListResponse getFollowings(Long userId, String cursor, int limit) {
        Long currentUserId = requireUserId(userId);
        return loadFollowingsPage(currentUserId, currentUserId, cursor, limit);
    }

    public UserListResponse getUserFollowers(Long viewerUserId, Long targetUserId, String cursor, int limit) {
        ensureNotBlocked(viewerUserId, targetUserId);
        return loadFollowersPage(targetUserId, viewerUserId, cursor, limit);
    }

    public UserListResponse getUserFollowings(Long viewerUserId, Long targetUserId, String cursor, int limit) {
        ensureNotBlocked(viewerUserId, targetUserId);
        return loadFollowingsPage(targetUserId, viewerUserId, cursor, limit);
    }

    private UserListResponse loadFollowersPage(Long targetUserId, Long viewerUserId, String cursor, int limit) {
        int validLimit = clampLimit(limit);
        Long cursorId = parseCursorId(cursor);
        List<Follow> page = followRepository.findFollowersPage(
                targetUserId, cursorId, PageRequest.of(0, validLimit + 1));
        boolean hasMore = page.size() > validLimit;
        List<Follow> trimmed = hasMore ? page.subList(0, validLimit) : page;
        String nextCursor = hasMore ? String.valueOf(trimmed.get(trimmed.size() - 1).getId()) : null;
        List<Long> userIds = trimmed.stream().map(Follow::getFollowerUserId).toList();
        return new UserListResponse(summariesWithRelation(userIds, viewerUserId), nextCursor);
    }

    private UserListResponse loadFollowingsPage(Long targetUserId, Long viewerUserId, String cursor, int limit) {
        int validLimit = clampLimit(limit);
        Long cursorId = parseCursorId(cursor);
        List<Follow> page = followRepository.findFollowingsPage(
                targetUserId, cursorId, PageRequest.of(0, validLimit + 1));
        boolean hasMore = page.size() > validLimit;
        List<Follow> trimmed = hasMore ? page.subList(0, validLimit) : page;
        String nextCursor = hasMore ? String.valueOf(trimmed.get(trimmed.size() - 1).getId()) : null;
        List<Long> userIds = trimmed.stream().map(Follow::getFollowingUserId).toList();
        return new UserListResponse(summariesWithRelation(userIds, viewerUserId), nextCursor);
    }

    /** MR #60 #5 — 양방향 차단 단일 OR 쿼리. */
    private void ensureNotBlocked(Long viewerUserId, Long targetUserId) {
        if (viewerUserId == null || viewerUserId.equals(targetUserId)) {
            return;
        }
        if (userBlockRepository.existsBlockBetween(viewerUserId, targetUserId)) {
            throw new CustomException(ErrorCode.BLOCKED_RELATION);
        }
    }

    private static int clampLimit(int limit) {
        if (limit <= 0) return DEFAULT_FOLLOW_LIMIT;
        return Math.min(limit, MAX_FOLLOW_LIMIT);
    }

    /** MR #60 #6 — 잘못된 cursor는 silent 폴백 대신 명시적 INVALID_INPUT. */
    private static Long parseCursorId(String cursor) {
        if (cursor == null || cursor.isBlank()) return null;
        try {
            return Long.parseLong(cursor);
        } catch (NumberFormatException e) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "잘못된 cursor 형식입니다.");
        }
    }

    /** MR #60 #4 — 페이지 1회당 최대 30+ exists → user/follow/block 한 번에 IN 쿼리. */
    private List<UserSummaryResponse> summariesWithRelation(List<Long> targetUserIds, Long viewerUserId) {
        if (targetUserIds.isEmpty()) {
            return List.of();
        }
        Map<Long, User> userById = userRepository.findAllById(targetUserIds).stream()
                .collect(Collectors.toMap(User::getId, Function.identity()));

        if (viewerUserId == null) {
            return targetUserIds.stream()
                    .map(userById::get)
                    .filter(Objects::nonNull)
                    .map(UserSummaryResponse::from)
                    .toList();
        }

        Set<Long> following = new HashSet<>(followRepository.findFollowingTargetIds(viewerUserId, targetUserIds));
        Set<Long> followedBy = new HashSet<>(followRepository.findFollowerTargetIds(viewerUserId, targetUserIds));
        Set<Long> blocked = new HashSet<>(userBlockRepository.findBlockedTargetIds(viewerUserId, targetUserIds));

        return targetUserIds.stream()
                .map(userById::get)
                .filter(Objects::nonNull)
                .map(u -> {
                    if (u.getId().equals(viewerUserId)) {
                        return UserSummaryResponse.from(u);
                    }
                    boolean isFollowing = following.contains(u.getId());
                    boolean isFollowedBy = followedBy.contains(u.getId());
                    boolean isBlocked = blocked.contains(u.getId());
                    return UserSummaryResponse.from(u, isFollowing && isFollowedBy, isFollowing, isBlocked);
                })
                .toList();
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
        List<Long> blockedIds = userBlockRepository.findByBlockerUserId(currentUserId).stream()
                .map(UserBlock::getBlockedUserId)
                .toList();
        return new UserListResponse(summariesWithRelation(blockedIds, currentUserId), null);
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
                request.notifyReadingRecordComment(),
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
