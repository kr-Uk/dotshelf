package com.dotshelf.domain.user.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import java.time.Duration;
import java.util.List;
import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.data.domain.Pageable;

import com.dotshelf.domain.user.dto.UserDtos.FeedSettingRequest;
import com.dotshelf.domain.user.dto.UserDtos.NotificationSettingRequest;
import com.dotshelf.domain.user.dto.UserDtos.ProfileImageConfirmRequest;
import com.dotshelf.domain.user.dto.UserDtos.ProfileImageUploadUrlRequest;
import com.dotshelf.domain.user.dto.UserDtos.UpdateProfileRequest;
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
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.storage.S3StorageProperties;
import com.dotshelf.global.storage.S3StorageService;
import com.dotshelf.global.storage.S3StorageService.ObjectMetadata;
import com.dotshelf.global.storage.S3StorageService.PresignedPut;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class UserServiceTest {

    @Mock
    private UserRepository userRepository;

    @Mock
    private FollowRepository followRepository;

    @Mock
    private UserBlockRepository userBlockRepository;

    @Mock
    private UserNotificationSettingRepository notificationSettingRepository;

    @Mock
    private UserFeedSettingRepository feedSettingRepository;

    @Mock
    private S3StorageService s3StorageService;

    @Mock
    private S3StorageProperties s3StorageProperties;

    @InjectMocks
    private UserService userService;

    @Test
    @DisplayName("프로필 수정은 닉네임을 변경하고 온보딩을 완료 처리한다")
    void updateMe_validRequest_updatesProfileAndCompletesOnboarding() {
        User user = TestFixtures.user(1L, "old");
        given(userRepository.findById(1L)).willReturn(Optional.of(user));
        given(userRepository.existsByNickname("new")).willReturn(false);

        var response = userService.updateMe(1L, new UpdateProfileRequest("new", "intro"));

        assertThat(response.nickname()).isEqualTo("new");
        assertThat(response.onboarded()).isTrue();
        assertThat(user.getProfileImageUrl()).isEqualTo("https://example.com/old.png");
        assertThat(user.getIntroduction()).isEqualTo("intro");
    }

    @Test
    @DisplayName("프로필 수정 시 새 닉네임이 이미 존재하면 DUPLICATE_NICKNAME을 던진다")
    void updateMe_duplicateNickname_throwsException() {
        User user = TestFixtures.user(1L, "old");
        given(userRepository.findById(1L)).willReturn(Optional.of(user));
        given(userRepository.existsByNickname("taken")).willReturn(true);

        assertThatThrownBy(() -> userService.updateMe(1L, new UpdateProfileRequest("taken", null)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.DUPLICATE_NICKNAME);
    }

    @Test
    @DisplayName("팔로우는 자기 자신을 대상으로 할 수 없다")
    void follow_self_throwsInvalidInput() {
        assertThatThrownBy(() -> userService.follow(1L, 1L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("팔로우 관계가 없으면 Follow를 저장하고 대상의 팔로워 수를 반환한다")
    void follow_notExisting_savesFollow() {
        given(userRepository.findById(2L)).willReturn(Optional.of(TestFixtures.user(2L, "target")));
        given(followRepository.existsByFollowerUserIdAndFollowingUserId(1L, 2L)).willReturn(false);
        given(followRepository.countByFollowingUserId(2L)).willReturn(1L);
        ArgumentCaptor<Follow> captor = ArgumentCaptor.forClass(Follow.class);

        var response = userService.follow(1L, 2L);

        assertThat(response.active()).isTrue();
        assertThat(response.count()).isEqualTo(1L);
        then(followRepository).should().save(captor.capture());
        assertThat(captor.getValue().getFollowerUserId()).isEqualTo(1L);
        assertThat(captor.getValue().getFollowingUserId()).isEqualTo(2L);
    }

    @Test
    @DisplayName("차단은 양방향 팔로우를 제거하고 차단 목록 수를 반환한다")
    void block_validTarget_deletesBothFollows() {
        given(userRepository.findById(2L)).willReturn(Optional.of(TestFixtures.user(2L, "target")));
        given(userBlockRepository.existsByBlockerUserIdAndBlockedUserId(1L, 2L)).willReturn(false);
        given(userBlockRepository.findByBlockerUserId(1L)).willReturn(List.of(TestFixtures.userBlock(1L, 2L)));

        var response = userService.block(1L, 2L);

        assertThat(response.active()).isTrue();
        assertThat(response.count()).isEqualTo(1L);
        then(userBlockRepository).should().save(any(UserBlock.class));
        then(followRepository).should().deleteByFollowerUserIdAndFollowingUserId(1L, 2L);
        then(followRepository).should().deleteByFollowerUserIdAndFollowingUserId(2L, 1L);
    }

    @Test
    @DisplayName("다른 사용자 프로필 조회는 팔로우/맞팔/차단 상태를 함께 반환한다")
    void getProfile_otherUser_includesRelationshipFlags() {
        given(userRepository.findById(2L)).willReturn(Optional.of(TestFixtures.user(2L, "target")));
        given(followRepository.existsByFollowerUserIdAndFollowingUserId(1L, 2L)).willReturn(true);
        given(followRepository.existsByFollowerUserIdAndFollowingUserId(2L, 1L)).willReturn(true);
        given(userBlockRepository.existsByBlockerUserIdAndBlockedUserId(1L, 2L)).willReturn(false);

        var response = userService.getProfile(1L, 2L);

        assertThat(response.following()).isTrue();
        assertThat(response.mutual()).isTrue();
        assertThat(response.blocked()).isFalse();
    }

    @Test
    @DisplayName("내 팔로워 목록은 limit+1 조회로 nextCursor와 관계 상태를 계산한다")
    void getFollowers_hasMore_returnsNextCursorAndRelationFlags() {
        Follow first = follow(10L, 2L, 1L);
        Follow second = follow(9L, 3L, 1L);
        given(followRepository.findFollowersPage(any(), any(), any(Pageable.class)))
                .willReturn(List.of(first, second));
        given(userRepository.findAllById(List.of(2L))).willReturn(List.of(TestFixtures.user(2L, "follower")));
        given(followRepository.findFollowingTargetIds(1L, List.of(2L))).willReturn(List.of(2L));
        given(followRepository.findFollowerTargetIds(1L, List.of(2L))).willReturn(List.of());
        given(userBlockRepository.findBlockedTargetIds(1L, List.of(2L))).willReturn(List.of());

        var response = userService.getFollowers(1L, null, 1);

        assertThat(response.users()).hasSize(1);
        assertThat(response.users().getFirst().id()).isEqualTo(2L);
        assertThat(response.users().getFirst().following()).isTrue();
        assertThat(response.users().getFirst().mutual()).isFalse();
        assertThat(response.nextCursor()).isEqualTo("10");
    }

    @Test
    @DisplayName("다른 사용자의 팔로잉 목록은 양방향 차단 관계면 BLOCKED_RELATION을 던진다")
    void getUserFollowings_blockedRelation_throwsBlockedRelation() {
        given(userBlockRepository.existsBlockBetween(1L, 2L)).willReturn(true);

        assertThatThrownBy(() -> userService.getUserFollowings(1L, 2L, null, 20))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.BLOCKED_RELATION);
        then(followRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("팔로워 목록 cursor가 숫자가 아니면 INVALID_INPUT을 던진다")
    void getFollowers_invalidCursor_throwsInvalidInput() {
        assertThatThrownBy(() -> userService.getFollowers(1L, "bad-cursor", 20))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
        then(followRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("언팔로우는 관계가 없어도 삭제를 시도하고 최신 팔로워 수를 반환한다")
    void unfollow_idempotentDeletesAndReturnsCount() {
        given(followRepository.countByFollowingUserId(2L)).willReturn(0L);

        var response = userService.unfollow(1L, 2L);

        assertThat(response.active()).isFalse();
        assertThat(response.count()).isZero();
        then(followRepository).should().deleteByFollowerUserIdAndFollowingUserId(1L, 2L);
    }

    @Test
    @DisplayName("이미 차단된 사용자를 다시 차단하면 중복 row를 저장하지 않는다")
    void block_existingBlock_doesNotSaveDuplicate() {
        given(userRepository.findById(2L)).willReturn(Optional.of(TestFixtures.user(2L, "target")));
        given(userBlockRepository.existsByBlockerUserIdAndBlockedUserId(1L, 2L)).willReturn(true);
        given(userBlockRepository.findByBlockerUserId(1L)).willReturn(List.of(TestFixtures.userBlock(1L, 2L)));

        var response = userService.block(1L, 2L);

        assertThat(response.active()).isTrue();
        assertThat(response.count()).isEqualTo(1L);
        then(userBlockRepository).should(never()).save(any(UserBlock.class));
    }

    @Test
    @DisplayName("차단 해제는 관계가 없어도 삭제를 시도하고 남은 차단 수를 반환한다")
    void unblock_idempotentDeletesAndReturnsCount() {
        given(userBlockRepository.findByBlockerUserId(1L)).willReturn(List.of());

        var response = userService.unblock(1L, 2L);

        assertThat(response.active()).isFalse();
        assertThat(response.count()).isZero();
        then(userBlockRepository).should().deleteByBlockerUserIdAndBlockedUserId(1L, 2L);
    }

    @Test
    @DisplayName("회원 탈퇴는 사용자 상태를 DELETED로 바꾸고 삭제 시각을 남긴다")
    void deleteMe_marksUserDeleted() {
        User user = TestFixtures.user(1L, "delete-target");
        given(userRepository.findById(1L)).willReturn(Optional.of(user));

        userService.deleteMe(1L);

        assertThat(user.getStatus()).isEqualTo("DELETED");
        assertThat(user.getDeletedAt()).isNotNull();
    }

    @Test
    @DisplayName("프로필 이미지 업로드 URL은 사용자 prefix key와 presigned PUT을 반환한다")
    void createProfileImageUploadUrl_validPng_returnsScopedPresignedUrl() {
        given(s3StorageProperties.presignTtlSeconds()).willReturn(300L);
        given(s3StorageService.presignPut(
                anyString(),
                eq("image/png"),
                eq(2048L),
                eq(Duration.ofSeconds(300))))
                .willAnswer(invocation -> new PresignedPut("https://upload.test/profile", invocation.getArgument(0), 300));

        var response = userService.createProfileImageUploadUrl(
                1L,
                new ProfileImageUploadUrlRequest("image/png", 2048L));

        assertThat(response.uploadUrl()).isEqualTo("https://upload.test/profile");
        assertThat(response.key()).startsWith("users/1/profile/");
        assertThat(response.key()).endsWith(".png");
        assertThat(response.expiresInSeconds()).isEqualTo(300L);
    }

    @Test
    @DisplayName("프로필 이미지 업로드 URL은 지원하지 않는 contentType이면 STORAGE_INVALID_CONTENT_TYPE을 던진다")
    void createProfileImageUploadUrl_invalidContentType_throwsInvalidContentType() {
        assertThatThrownBy(() -> userService.createProfileImageUploadUrl(
                1L,
                new ProfileImageUploadUrlRequest("image/gif", 1024L)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("프로필 이미지 업로드 URL은 최대 크기를 넘으면 STORAGE_FILE_TOO_LARGE를 던진다")
    void createProfileImageUploadUrl_tooLarge_throwsFileTooLarge() {
        assertThatThrownBy(() -> userService.createProfileImageUploadUrl(
                1L,
                new ProfileImageUploadUrlRequest("image/jpeg", 5L * 1024 * 1024 + 1)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_FILE_TOO_LARGE);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("프로필 이미지 confirm은 key prefix와 S3 metadata를 검증하고 profileImageUrl을 갱신한다")
    void confirmProfileImage_validObject_updatesProfileImageUrl() {
        User user = TestFixtures.user(1L, "profile");
        String key = "users/1/profile/a.webp";
        given(s3StorageService.headObject(key)).willReturn(new ObjectMetadata("image/webp", 2048L));
        given(userRepository.findById(1L)).willReturn(Optional.of(user));
        given(s3StorageService.publicUrl(key)).willReturn("https://cdn.test/" + key);

        var response = userService.confirmProfileImage(1L, new ProfileImageConfirmRequest(key));

        assertThat(response.profileImageUrl()).isEqualTo("https://cdn.test/" + key);
        assertThat(user.getProfileImageUrl()).isEqualTo("https://cdn.test/" + key);
        assertThat(user.getNickname()).isEqualTo("profile");
        assertThat(user.getIntroduction()).isEqualTo("profile introduction");
    }

    @Test
    @DisplayName("프로필 이미지 confirm은 다른 사용자의 key면 STORAGE_INVALID_KEY를 던진다")
    void confirmProfileImage_wrongUserPrefix_throwsInvalidKey() {
        assertThatThrownBy(() -> userService.confirmProfileImage(
                1L,
                new ProfileImageConfirmRequest("users/2/profile/a.png")))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_KEY);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("프로필 이미지 confirm은 S3 contentType이 이미지가 아니면 STORAGE_INVALID_CONTENT_TYPE을 던진다")
    void confirmProfileImage_invalidContentType_throwsInvalidContentType() {
        String key = "users/1/profile/a.png";
        given(s3StorageService.headObject(key)).willReturn(new ObjectMetadata("text/plain", 100L));

        assertThatThrownBy(() -> userService.confirmProfileImage(1L, new ProfileImageConfirmRequest(key)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);

        then(userRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("프로필 이미지 confirm은 S3 객체 크기가 한도를 넘으면 STORAGE_FILE_TOO_LARGE를 던진다")
    void confirmProfileImage_tooLarge_throwsFileTooLarge() {
        String key = "users/1/profile/a.png";
        given(s3StorageService.headObject(key)).willReturn(new ObjectMetadata("image/png", 5L * 1024 * 1024 + 1));

        assertThatThrownBy(() -> userService.confirmProfileImage(1L, new ProfileImageConfirmRequest(key)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_FILE_TOO_LARGE);

        then(userRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("차단 목록 조회는 차단된 사용자 요약과 blocked=true 관계 상태를 반환한다")
    void getBlocks_returnsBlockedUserSummaries() {
        given(userBlockRepository.findByBlockerUserId(1L)).willReturn(List.of(TestFixtures.userBlock(1L, 2L)));
        given(userRepository.findAllById(List.of(2L))).willReturn(List.of(TestFixtures.user(2L, "blocked")));
        given(followRepository.findFollowingTargetIds(1L, List.of(2L))).willReturn(List.of());
        given(followRepository.findFollowerTargetIds(1L, List.of(2L))).willReturn(List.of());
        given(userBlockRepository.findBlockedTargetIds(1L, List.of(2L))).willReturn(List.of(2L));

        var response = userService.getBlocks(1L);

        assertThat(response.users()).hasSize(1);
        assertThat(response.users().getFirst().id()).isEqualTo(2L);
        assertThat(response.users().getFirst().blocked()).isTrue();
    }

    @Test
    @DisplayName("닉네임 사용 가능 여부는 repository exists 결과를 반대로 반환한다")
    void checkNickname_returnsInverseOfExists() {
        given(userRepository.existsByNickname("free")).willReturn(false);
        given(userRepository.existsByNickname("taken")).willReturn(true);

        assertThat(userService.checkNickname("free").available()).isTrue();
        assertThat(userService.checkNickname("taken").available()).isFalse();
    }

    @Test
    @DisplayName("인증 userId가 없으면 UNAUTHORIZED를 던진다")
    void requireUserId_null_throwsUnauthorized() {
        assertThatThrownBy(() -> userService.requireUserId(null))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.UNAUTHORIZED);
    }

    @Test
    @DisplayName("알림 설정 row가 없으면 기본 설정 row를 생성해서 반환한다")
    void getNotificationSetting_missingRow_createsDefaultSetting() {
        UserNotificationSetting setting = TestFixtures.notificationSetting(1L);
        given(notificationSettingRepository.findByUserId(1L)).willReturn(Optional.empty());
        given(notificationSettingRepository.save(any(UserNotificationSetting.class))).willReturn(setting);

        var response = userService.getNotificationSetting(1L);

        assertThat(response.notifyNewGuestbook()).isTrue();
        then(notificationSettingRepository).should().save(any(UserNotificationSetting.class));
    }

    @Test
    @DisplayName("알림 설정 수정은 null이 아닌 필드만 반영한다")
    void updateNotificationSetting_updatesOnlyProvidedFields() {
        UserNotificationSetting setting = TestFixtures.notificationSetting(1L);
        given(notificationSettingRepository.findByUserId(1L)).willReturn(Optional.of(setting));

        var response = userService.updateNotificationSetting(
                1L,
                new NotificationSettingRequest(false, null, null, false, null, null)
        );

        assertThat(response.notifyNewGuestbook()).isFalse();
        assertThat(response.notifyGuestbookReply()).isTrue();
        assertThat(response.notifyReadingRecordComment()).isFalse();
    }

    @Test
    @DisplayName("피드 설정 수정은 null이 아닌 필드만 반영한다")
    void updateFeedSetting_updatesOnlyProvidedFields() {
        UserFeedSetting setting = TestFixtures.feedSetting(1L);
        given(feedSettingRepository.findByUserId(1L)).willReturn(Optional.of(setting));

        var response = userService.updateFeedSetting(
                1L,
                new FeedSettingRequest(false, null, false, null, true)
        );

        assertThat(response.showBookAdded()).isFalse();
        assertThat(response.showReadingRecord()).isTrue();
        assertThat(response.showSpaceVisit()).isFalse();
        assertThat(response.showReadingRecordComment()).isTrue();
    }

    @Test
    @DisplayName("피드 설정 row가 없으면 기본 설정 row를 생성해서 반환한다")
    void getFeedSetting_missingRow_createsDefaultSetting() {
        UserFeedSetting setting = TestFixtures.feedSetting(1L);
        given(feedSettingRepository.findByUserId(1L)).willReturn(Optional.empty());
        given(feedSettingRepository.save(any(UserFeedSetting.class))).willReturn(setting);

        var response = userService.getFeedSetting(1L);

        assertThat(response.showBookAdded()).isTrue();
        assertThat(response.showGuestbook()).isTrue();
        then(feedSettingRepository).should().save(any(UserFeedSetting.class));
    }

    private Follow follow(Long id, Long followerUserId, Long followingUserId) {
        Follow follow = TestFixtures.follow(followerUserId, followingUserId);
        TestFixtures.setId(follow, id);
        return follow;
    }
}
