package com.dotshelf.domain.space.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import java.time.Duration;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.List;
import java.util.Optional;
import java.util.Set;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.mock.web.MockMultipartFile;

import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.dto.AssetDtos.DownloadManifestResponse;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.asset.repository.UserAssetInventoryRepository;
import com.dotshelf.domain.asset.service.AssetService;
import com.dotshelf.domain.book.dto.BookDtos.OwnedBookSummary;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.repository.BookshelfRepository;
import com.dotshelf.domain.space.dto.SpaceDtos.CategoryInventoryRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.GuestbookPostItWhitelistRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.ThemeApplyRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.ThemeApplyMode;
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailConfirmRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailUploadUrlRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.VisibilityRequest;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.entity.SpaceGuestbookPostItWhitelist;
import com.dotshelf.domain.space.entity.SpaceLayoutSnapshot;
import com.dotshelf.domain.space.entity.SpaceLike;
import com.dotshelf.domain.space.entity.SpaceVisit;
import com.dotshelf.domain.space.repository.SpaceGuestbookPostItWhitelistRepository;
import com.dotshelf.domain.space.repository.SpaceLayoutSnapshotRepository;
import com.dotshelf.domain.space.repository.SpaceLikeRepository;
import com.dotshelf.domain.space.repository.SpaceRepository;
import com.dotshelf.domain.space.repository.SpaceTemplateRepository;
import com.dotshelf.domain.space.repository.SpaceVisitRepository;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.repository.UserBlockRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.storage.S3StorageProperties;
import com.dotshelf.global.storage.S3StorageService;
import com.dotshelf.global.storage.S3StorageService.ObjectMetadata;
import com.dotshelf.global.storage.S3StorageService.PresignedPut;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class SpaceServiceTest {

    @Mock
    private SpaceRepository spaceRepository;

    @Mock
    private SpaceTemplateRepository spaceTemplateRepository;

    @Mock
    private SpaceVisitRepository spaceVisitRepository;

    @Mock
    private SpaceLikeRepository spaceLikeRepository;

    @Mock
    private SpaceLayoutSnapshotRepository layoutSnapshotRepository;

    @Mock
    private SpaceGuestbookPostItWhitelistRepository whitelistRepository;

    @Mock
    private FollowRepository followRepository;

    @Mock
    private UserBlockRepository userBlockRepository;

    @Mock
    private UserBookRepository userBookRepository;

    @Mock
    private UserRepository userRepository;

    @Mock
    private UserService userService;

    @Mock
    private AssetService assetService;

    @Mock
    private AssetRepository assetRepository;

    @Mock
    private UserAssetInventoryRepository userAssetInventoryRepository;

    @Mock
    private BookshelfRepository bookshelfRepository;

    @Mock
    private S3StorageService s3StorageService;

    @Mock
    private S3StorageProperties s3StorageProperties;

    @InjectMocks
    private SpaceService spaceService;

    @Test
    @DisplayName("내 공간 조회 시 공간이 없으면 기본 공간을 생성한다")
    void getMySpace_missingSpace_createsDefaultSpace() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.empty());
        given(spaceRepository.save(any(Space.class))).willAnswer(invocation -> {
            Space space = invocation.getArgument(0);
            TestFixtures.setId(space, 10L);
            return space;
        });
        ArgumentCaptor<Space> captor = ArgumentCaptor.forClass(Space.class);

        var response = spaceService.getMySpace(1L);

        assertThat(response.id()).isEqualTo(10L);
        assertThat(response.spaceName()).isEqualTo("나의 공간");
        then(spaceRepository).should().save(captor.capture());
        assertThat(captor.getValue().getOwnerUserId()).isEqualTo(1L);
    }

    @Test
    @DisplayName("공간 목록은 PUBLIC 공간만 대상으로 owner 닉네임 검색, 정렬, liked/following 상태를 채운다")
    void getSpaces_filtersPublicByOwnerKeywordAndDecoratesRelations() {
        Space likedSpace = TestFixtures.space(10L, 1L);
        likedSpace.like();
        likedSpace.increaseVisitCount();
        Space otherPublicSpace = TestFixtures.space(11L, 2L);
        Space privateSpace = TestFixtures.space(12L, 3L);
        privateSpace.changeVisibility("PRIVATE");
        given(spaceRepository.findAll()).willReturn(List.of(otherPublicSpace, privateSpace, likedSpace));
        given(userRepository.findAllById(any()))
                .willReturn(List.of(
                        TestFixtures.user(1L, "alice-reader"),
                        TestFixtures.user(2L, "bob"),
                        TestFixtures.user(3L, "alice-private")
                ));
        given(spaceLikeRepository.existsBySpaceIdAndUserId(10L, 99L)).willReturn(true);
        given(followRepository.existsByFollowerUserIdAndFollowingUserId(99L, 1L)).willReturn(true);
        given(followRepository.countByFollowingUserId(1L)).willReturn(7L);

        var response = spaceService.getSpaces(99L, "alice", "POPULAR", 20);

        assertThat(response.spaces()).hasSize(1);
        var item = response.spaces().getFirst();
        assertThat(item.spaceId()).isEqualTo(10L);
        assertThat(item.ownerNickname()).isEqualTo("alice-reader");
        assertThat(item.liked()).isTrue();
        assertThat(item.following()).isTrue();
        assertThat(item.followerCount()).isEqualTo(7L);
    }

    @Test
    @DisplayName("대시보드는 공간, owner 정보, 팔로워/팔로잉, 주간 방문자를 집계한다")
    void getDashboard_returnsOwnerAndCountSummary() {
        Space space = TestFixtures.space(10L, 1L);
        space.like();
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(userService.getUser(1L)).willReturn(TestFixtures.user(1L, "owner"));
        given(spaceLikeRepository.existsBySpaceIdAndUserId(10L, 2L)).willReturn(true);
        given(followRepository.countByFollowingUserId(1L)).willReturn(5L);
        given(followRepository.countByFollowerUserId(1L)).willReturn(3L);
        given(spaceVisitRepository.countDistinctVisitorsBySpaceIdSince(any(), any())).willReturn(9L);

        var response = spaceService.getDashboard(2L, 10L, "WEEK");

        assertThat(response.space().id()).isEqualTo(10L);
        assertThat(response.space().liked()).isTrue();
        assertThat(response.followerCount()).isEqualTo(5L);
        assertThat(response.followingCount()).isEqualTo(3L);
        assertThat(response.weeklyVisitorCount()).isEqualTo(9L);
        assertThat(response.ownerNickname()).isEqualTo("owner");
        assertThat(response.bookshelfCount()).isEqualTo(0L);
        assertThat(response.bookCount()).isEqualTo(0L);
        assertThat(response.readingRecordCount()).isEqualTo(0L);
    }

    @Test
    @DisplayName("비공개 공간 상세 조회는 소유자가 아니면 FORBIDDEN을 던진다")
    void getSpace_privateOther_throwsForbidden() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeVisibility("PRIVATE");
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));

        assertThatThrownBy(() -> spaceService.getSpace(2L, 10L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
    }

    @Test
    @DisplayName("비공개 공간 대시보드 조회는 소유자가 아니면 FORBIDDEN을 던진다")
    void getDashboard_privateOther_throwsForbidden() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeVisibility("PRIVATE");
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));

        assertThatThrownBy(() -> spaceService.getDashboard(2L, 10L, "WEEK"))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
        then(userService).should(never()).getUser(any());
    }

    @Test
    @DisplayName("공간 주인이 본인 공간을 방문하면 방문 수를 늘리지 않는다")
    void recordVisit_owner_doesNotIncreaseVisitCount() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));

        var response = spaceService.recordVisit(1L, 10L);

        assertThat(response.active()).isFalse();
        assertThat(response.count()).isZero();
        then(spaceVisitRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("방문자가 하루 첫 방문이면 방문 이력을 저장하고 visitCount를 증가시킨다")
    void recordVisit_firstVisit_savesVisitAndIncreasesCount() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(2L)).willReturn(2L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(spaceVisitRepository.existsBySpaceIdAndVisitorUserIdAndVisitedDate(any(), any(), any())).willReturn(false);
        ArgumentCaptor<SpaceVisit> captor = ArgumentCaptor.forClass(SpaceVisit.class);

        var response = spaceService.recordVisit(2L, 10L);

        assertThat(response.active()).isTrue();
        assertThat(response.count()).isEqualTo(1L);
        then(spaceVisitRepository).should().save(captor.capture());
        assertThat(captor.getValue().getVisitorUserId()).isEqualTo(2L);
    }

    @Test
    @DisplayName("비공개 공간 방문 기록은 소유자가 아니면 FORBIDDEN을 던진다")
    void recordVisit_privateOther_throwsForbidden() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeVisibility("PRIVATE");
        given(userService.requireUserId(2L)).willReturn(2L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));

        assertThatThrownBy(() -> spaceService.recordVisit(2L, 10L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
        then(spaceVisitRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("방문자가 같은 날 다시 방문하면 방문 이력을 중복 저장하지 않는다")
    void recordVisit_sameDayVisit_doesNotIncreaseAgain() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(2L)).willReturn(2L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(spaceVisitRepository.existsBySpaceIdAndVisitorUserIdAndVisitedDate(any(), any(), any())).willReturn(true);

        var response = spaceService.recordVisit(2L, 10L);

        assertThat(response.active()).isTrue();
        assertThat(response.count()).isZero();
        then(spaceVisitRepository).should(never()).save(any(SpaceVisit.class));
    }

    @Test
    @DisplayName("공간 좋아요는 중복 저장하지 않고 카운트를 증가시킨다")
    void like_notExisting_savesLikeAndIncreasesCount() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(2L)).willReturn(2L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(spaceLikeRepository.existsBySpaceIdAndUserId(10L, 2L)).willReturn(false);
        ArgumentCaptor<SpaceLike> captor = ArgumentCaptor.forClass(SpaceLike.class);

        var response = spaceService.like(2L, 10L);

        assertThat(response.active()).isTrue();
        assertThat(response.count()).isEqualTo(1L);
        then(spaceLikeRepository).should().save(captor.capture());
        assertThat(captor.getValue().getUserId()).isEqualTo(2L);
    }

    @Test
    @DisplayName("비공개 공간 좋아요는 소유자가 아니면 FORBIDDEN을 던진다")
    void like_privateOther_throwsForbidden() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeVisibility("PRIVATE");
        given(userService.requireUserId(2L)).willReturn(2L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));

        assertThatThrownBy(() -> spaceService.like(2L, 10L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
        then(spaceLikeRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("이미 좋아요한 공간은 좋아요 row를 중복 저장하지 않는다")
    void like_existingLike_doesNotSaveDuplicate() {
        Space space = TestFixtures.space(10L, 1L);
        space.like();
        given(userService.requireUserId(2L)).willReturn(2L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(spaceLikeRepository.existsBySpaceIdAndUserId(10L, 2L)).willReturn(true);

        var response = spaceService.like(2L, 10L);

        assertThat(response.active()).isTrue();
        assertThat(response.count()).isEqualTo(1L);
        then(spaceLikeRepository).should(never()).save(any(SpaceLike.class));
    }

    @Test
    @DisplayName("공간 좋아요 해제는 기존 row를 삭제하고 카운트를 감소시킨다")
    void unlike_existingLike_deletesAndDecreasesCount() {
        Space space = TestFixtures.space(10L, 1L);
        space.like();
        given(userService.requireUserId(2L)).willReturn(2L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(spaceLikeRepository.existsBySpaceIdAndUserId(10L, 2L)).willReturn(true);

        var response = spaceService.unlike(2L, 10L);

        assertThat(response.active()).isFalse();
        assertThat(response.count()).isZero();
        then(spaceLikeRepository).should().deleteBySpaceIdAndUserId(10L, 2L);
    }

    @Test
    @DisplayName("레이아웃 저장은 layoutVersion을 증가시키고 스냅샷을 저장한다")
    void saveMyLayout_incrementsVersionAndSavesSnapshot() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        ArgumentCaptor<SpaceLayoutSnapshot> captor = ArgumentCaptor.forClass(SpaceLayoutSnapshot.class);

        var response = spaceService.saveMyLayout(1L, new LayoutSaveRequest("{\"items\":[]}", 0L));

        assertThat(response.layoutVersion()).isEqualTo(1L);
        then(layoutSnapshotRepository).should().save(captor.capture());
        assertThat(captor.getValue().getLayoutPayload()).isEqualTo("{\"items\":[]}");
    }

    @Test
    @DisplayName("썸네일 업로드는 이미지 매직 바이트 검증 후 S3에 저장하고 thumbnailUrl과 layoutVersion을 갱신한다")
    void uploadThumbnail_validPng_uploadsToS3AndUpdatesSpace() {
        Space space = TestFixtures.space(10L, 1L);
        MockMultipartFile file = new MockMultipartFile(
                "file",
                "thumbnail.png",
                "image/png",
                new byte[] {(byte) 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A, 0x00});
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(s3StorageService.publicUrl(any())).willAnswer(invocation -> "https://cdn.test/" + invocation.getArgument(0));
        ArgumentCaptor<String> keyCaptor = ArgumentCaptor.forClass(String.class);

        var response = spaceService.uploadThumbnail(1L, file);

        assertThat(response.spaceId()).isEqualTo(10L);
        assertThat(response.layoutVersion()).isEqualTo(1L);
        assertThat(response.thumbnailKey()).matches("spaces/10/thumbnail/\\d+-[0-9a-f\\-]{36}\\.png");
        assertThat(response.thumbnailUrl()).isEqualTo("https://cdn.test/" + response.thumbnailKey());
        assertThat(space.getThumbnailKey()).isEqualTo(response.thumbnailKey());
        assertThat(space.getThumbnailUrl()).isEqualTo("https://cdn.test/" + response.thumbnailKey());
        then(s3StorageService).should().putObject(keyCaptor.capture(), eq(file), eq("image/png"));
        assertThat(keyCaptor.getValue()).isEqualTo(response.thumbnailKey());
    }

    @Test
    @DisplayName("썸네일 presigned URL 발급은 이미지 타입과 크기를 검증하고 S3 PUT URL을 반환한다")
    void createThumbnailUploadUrl_validJpeg_returnsPresignedUrl() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(s3StorageProperties.presignTtlSeconds()).willReturn(300L);
        given(s3StorageService.presignPut(
                any(),
                eq("image/jpeg"),
                eq(1234L),
                eq(Duration.ofSeconds(300))))
                .willAnswer(invocation -> new PresignedPut(
                        "https://upload.example.com/object",
                        invocation.getArgument(0),
                        300L));
        given(s3StorageService.publicUrl(any())).willAnswer(invocation -> "https://cdn.test/" + invocation.getArgument(0));

        var response = spaceService.createThumbnailUploadUrl(
                1L,
                new ThumbnailUploadUrlRequest("image/jpeg", 1234L));

        assertThat(response.uploadUrl()).isEqualTo("https://upload.example.com/object");
        assertThat(response.key()).matches("spaces/10/thumbnail/\\d+-[0-9a-f\\-]{36}\\.jpg");
        assertThat(response.publicUrl()).isEqualTo("https://cdn.test/" + response.key());
        assertThat(response.expiresInSeconds()).isEqualTo(300L);
    }

    @Test
    @DisplayName("썸네일 presigned URL 발급은 확정 전 중복 호출에도 서로 다른 key를 반환한다")
    void createThumbnailUploadUrl_repeatedBeforeConfirm_returnsUniqueKeys() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(s3StorageProperties.presignTtlSeconds()).willReturn(300L);
        given(s3StorageService.presignPut(any(), eq("image/png"), eq(1234L), eq(Duration.ofSeconds(300))))
                .willAnswer(invocation -> new PresignedPut("https://upload.example.com/object", invocation.getArgument(0), 300L));
        given(s3StorageService.publicUrl(any())).willAnswer(invocation -> "https://cdn.test/" + invocation.getArgument(0));

        var first = spaceService.createThumbnailUploadUrl(1L, new ThumbnailUploadUrlRequest("image/png", 1234L));
        var second = spaceService.createThumbnailUploadUrl(1L, new ThumbnailUploadUrlRequest("image/png", 1234L));

        assertThat(first.key()).matches("spaces/10/thumbnail/\\d+-[0-9a-f\\-]{36}\\.png");
        assertThat(second.key()).matches("spaces/10/thumbnail/\\d+-[0-9a-f\\-]{36}\\.png");
        assertThat(first.key()).isNotEqualTo(second.key());
        assertThat(space.getLayoutVersion()).isZero();
    }

    @Test
    @DisplayName("썸네일 presigned URL 발급은 허용되지 않은 이미지 타입을 거절한다")
    void createThumbnailUploadUrl_invalidType_throwsInvalidContentType() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));

        assertThatThrownBy(() -> spaceService.createThumbnailUploadUrl(
                1L,
                new ThumbnailUploadUrlRequest("text/plain", 1234L)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("썸네일 확정은 S3 객체 메타데이터 검증 후 thumbnailKey/url과 layoutVersion을 갱신한다")
    void confirmThumbnail_validObject_updatesSpace() {
        Space space = TestFixtures.space(10L, 1L);
        String key = "spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.png";
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(s3StorageService.headObject(key)).willReturn(new ObjectMetadata("image/png", 1024L));
        given(s3StorageService.readObjectHeadBytes(key, 12))
                .willReturn(new byte[] {(byte) 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A, 0x00});
        given(s3StorageService.publicUrl(key)).willReturn("https://cdn.test/" + key);

        var response = spaceService.confirmThumbnail(1L, new ThumbnailConfirmRequest(key));

        assertThat(response.spaceId()).isEqualTo(10L);
        assertThat(response.layoutVersion()).isEqualTo(1L);
        assertThat(response.thumbnailKey()).isEqualTo(key);
        assertThat(response.thumbnailUrl()).isEqualTo("https://cdn.test/" + key);
        assertThat(space.getThumbnailKey()).isEqualTo(key);
        assertThat(space.getThumbnailUrl()).isEqualTo("https://cdn.test/" + key);
    }

    @Test
    @DisplayName("썸네일 확정은 같은 key 재시도 시 멱등하게 현재 응답만 반환한다")
    void confirmThumbnail_sameCurrentKey_isIdempotent() {
        Space space = TestFixtures.space(10L, 1L);
        String key = "spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.png";
        space.updateThumbnail(key, "https://cdn.test/" + key);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));

        var response = spaceService.confirmThumbnail(1L, new ThumbnailConfirmRequest(key));

        assertThat(response.thumbnailKey()).isEqualTo(key);
        assertThat(response.layoutVersion()).isZero();
        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("썸네일 확정은 내 공간 prefix가 아닌 key를 거절한다")
    void confirmThumbnail_invalidKeyPrefix_throwsInvalidKey() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));

        assertThatThrownBy(() -> spaceService.confirmThumbnail(
                1L,
                new ThumbnailConfirmRequest("spaces/10/v1.png")))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_KEY);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("썸네일 확정은 현재 썸네일보다 오래된 발급 key를 거절한다")
    void confirmThumbnail_olderThanCurrentKey_throwsInvalidKey() {
        Space space = TestFixtures.space(10L, 1L);
        String currentKey = "spaces/10/thumbnail/1715000001000-123e4567-e89b-12d3-a456-426614174000.png";
        String staleKey = "spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174001.png";
        space.updateThumbnail(currentKey, "https://cdn.test/" + currentKey);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));

        assertThatThrownBy(() -> spaceService.confirmThumbnail(1L, new ThumbnailConfirmRequest(staleKey)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_KEY);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("썸네일 확정은 빈 S3 object를 거절한다")
    void confirmThumbnail_zeroLengthObject_throwsInvalidInput() {
        Space space = TestFixtures.space(10L, 1L);
        String key = "spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.png";
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(s3StorageService.headObject(key)).willReturn(new ObjectMetadata("image/png", 0L));

        assertThatThrownBy(() -> spaceService.confirmThumbnail(1L, new ThumbnailConfirmRequest(key)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("썸네일 확정은 metadata contentType과 실제 magic byte가 맞지 않으면 거절한다")
    void confirmThumbnail_spoofedContentType_throwsInvalidContentType() {
        Space space = TestFixtures.space(10L, 1L);
        String key = "spaces/10/thumbnail/1715000000000-123e4567-e89b-12d3-a456-426614174000.png";
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(s3StorageService.headObject(key)).willReturn(new ObjectMetadata("image/png", 1024L));
        given(s3StorageService.readObjectHeadBytes(key, 12)).willReturn(new byte[] {'n', 'o', 't'});

        assertThatThrownBy(() -> spaceService.confirmThumbnail(1L, new ThumbnailConfirmRequest(key)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);
    }

    @Test
    @DisplayName("썸네일 업로드는 파일이 비어 있으면 INVALID_INPUT을 던진다")
    void uploadThumbnail_emptyFile_throwsInvalidInput() {
        MockMultipartFile file = new MockMultipartFile("file", "empty.png", "image/png", new byte[] {});
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L))
                .willReturn(Optional.of(TestFixtures.space(10L, 1L)));

        assertThatThrownBy(() -> spaceService.uploadThumbnail(1L, file))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("썸네일 업로드는 최대 크기를 넘으면 STORAGE_FILE_TOO_LARGE를 던진다")
    void uploadThumbnail_tooLarge_throwsFileTooLarge() {
        MockMultipartFile file = new MockMultipartFile(
                "file",
                "large.png",
                "image/png",
                new byte[(int) (2L * 1024 * 1024 + 1)]);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L))
                .willReturn(Optional.of(TestFixtures.space(10L, 1L)));

        assertThatThrownBy(() -> spaceService.uploadThumbnail(1L, file))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_FILE_TOO_LARGE);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("썸네일 업로드는 contentType과 실제 파일 시그니처가 다르면 STORAGE_INVALID_CONTENT_TYPE을 던진다")
    void uploadThumbnail_mismatchedMagicBytes_throwsInvalidContentType() {
        MockMultipartFile file = new MockMultipartFile(
                "file",
                "fake.png",
                "image/png",
                new byte[] {'N', 'O', 'T', '_', 'P', 'N', 'G'});
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L))
                .willReturn(Optional.of(TestFixtures.space(10L, 1L)));

        assertThatThrownBy(() -> spaceService.uploadThumbnail(1L, file))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.STORAGE_INVALID_CONTENT_TYPE);

        then(s3StorageService).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("공개 범위 수정은 내 공간을 생성 또는 조회한 뒤 visibilityScope를 변경한다")
    void updateVisibility_changesScope() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));

        var response = spaceService.updateVisibility(1L, new VisibilityRequest("PRIVATE"));

        assertThat(response.visibilityScope()).isEqualTo("PRIVATE");
        assertThat(space.getVisibilityScope()).isEqualTo("PRIVATE");
    }

    @Test
    @DisplayName("방명록 공개 범위 수정은 guestbookVisibilityScope만 변경한다")
    void updateGuestbookVisibility_changesGuestbookScope() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));

        var response = spaceService.updateGuestbookVisibility(1L, new VisibilityRequest("FOLLOWERS"));

        assertThat(response.guestbookVisibilityScope()).isEqualTo("FOLLOWERS");
        assertThat(space.getVisibilityScope()).isEqualTo("PUBLIC");
    }

    @Test
    @DisplayName("구형 테마 적용도 보유 THEME 인벤토리를 검증한 뒤 저장한다")
    void applyTheme_verifiesOwnedThemeInventory() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userAssetInventoryRepository.findByIdAndUserId(30L, 1L))
                .willReturn(Optional.of(TestFixtures.inventory(30L, 1L, 50L)));
        given(assetRepository.findById(50L)).willReturn(Optional.of(TestFixtures.themeAsset(50L, 1L, "SPACE", true)));
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));

        var response = spaceService.applyTheme(1L, new ThemeApplyRequest(30L));

        assertThat(response.currentThemeInventoryId()).isEqualTo(30L);
        assertThat(space.getCurrentThemeInventoryId()).isEqualTo(30L);
    }

    @Test
    @DisplayName("구형 테마 적용도 보유하지 않은 인벤토리면 THEME_NOT_OWNED를 던진다")
    void applyTheme_notOwnedInventory_throwsThemeNotOwned() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userAssetInventoryRepository.findByIdAndUserId(30L, 1L)).willReturn(Optional.empty());

        assertThatThrownBy(() -> spaceService.applyTheme(1L, new ThemeApplyRequest(30L)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.THEME_NOT_OWNED);
    }

    @Test
    @DisplayName("구형 테마 적용도 THEME 타입이 아니면 CATEGORY_INVENTORY_TYPE_MISMATCH를 던진다")
    void applyTheme_assetTypeMismatch_throwsException() {
        Asset floorAsset = Asset.builder()
                .categoryId(1L)
                .code("floor")
                .name("floor")
                .assetScope("SPACE")
                .assetType("FLOOR")
                .pricePages(100)
                .build();
        TestFixtures.setId(floorAsset, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userAssetInventoryRepository.findByIdAndUserId(30L, 1L)).willReturn(Optional.of(TestFixtures.inventory(30L, 1L, 20L)));
        given(assetRepository.findById(20L)).willReturn(Optional.of(floorAsset));

        assertThatThrownBy(() -> spaceService.applyTheme(1L, new ThemeApplyRequest(30L)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.CATEGORY_INVENTORY_TYPE_MISMATCH);
    }

    @Test
    @DisplayName("KEEP_BLOCKERS_ONLY 테마 적용 모드는 아직 구현되지 않아 명시적으로 거절한다")
    void applyThemeWithMode_keepBlockersOnly_throwsNotImplemented() {
        assertThatThrownBy(() -> spaceService.applyThemeWithMode(1L, 99L, ThemeApplyMode.KEEP_BLOCKERS_ONLY))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.THEME_MODE_NOT_IMPLEMENTED);
    }

    @Test
    @DisplayName("RESET 테마 적용은 보유 THEME 인벤토리를 검증하고 기존 커스터마이징과 책장 스킨을 초기화한다")
    void applyThemeWithMode_reset_verifiesInventoryAndResetsCustomization() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeWallpaper(88L);
        Bookshelf bookshelf = Bookshelf.builder()
                .spaceId(10L)
                .name("shelf")
                .skinInventoryId(77L)
                .displayOrder(1)
                .build();
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userAssetInventoryRepository.findByIdAndUserId(30L, 1L))
                .willReturn(Optional.of(TestFixtures.inventory(30L, 1L, 50L)));
        given(assetRepository.findById(50L)).willReturn(Optional.of(TestFixtures.themeAsset(50L, 1L, "SPACE", true)));
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(bookshelfRepository.findBySpaceIdOrderByDisplayOrderAsc(10L)).willReturn(List.of(bookshelf));

        var response = spaceService.applyThemeWithMode(1L, 30L, ThemeApplyMode.RESET);

        assertThat(response.themeReset()).isTrue();
        assertThat(response.space().currentThemeInventoryId()).isEqualTo(30L);
        assertThat(response.space().currentWallpaperInventoryId()).isNull();
        assertThat(response.space().layoutVersion()).isEqualTo(1L);
        assertThat(bookshelf.getSkinInventoryId()).isNull();
    }

    @Test
    @DisplayName("카테고리별 적용 시 인벤토리 에셋 타입이 다르면 CATEGORY_INVENTORY_TYPE_MISMATCH를 던진다")
    void changeWallpaper_assetTypeMismatch_throwsException() {
        Space space = TestFixtures.space(10L, 1L);
        Asset floorAsset = Asset.builder()
                .categoryId(1L)
                .code("floor")
                .name("floor")
                .assetScope("SPACE")
                .assetType("FLOOR")
                .pricePages(100)
                .build();
        TestFixtures.setId(floorAsset, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(userAssetInventoryRepository.findByIdAndUserId(30L, 1L)).willReturn(Optional.of(TestFixtures.inventory(30L, 1L, 20L)));
        given(assetRepository.findById(20L)).willReturn(Optional.of(floorAsset));

        assertThatThrownBy(() -> spaceService.changeWallpaper(1L, new CategoryInventoryRequest(30L)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.CATEGORY_INVENTORY_TYPE_MISMATCH);
    }

    @Test
    @DisplayName("보유하지 않은 카테고리 인벤토리를 적용하면 THEME_NOT_OWNED를 던진다")
    void changeWallpaper_notOwnedInventory_throwsThemeNotOwned() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(userAssetInventoryRepository.findByIdAndUserId(30L, 1L)).willReturn(Optional.empty());

        assertThatThrownBy(() -> spaceService.changeWallpaper(1L, new CategoryInventoryRequest(30L)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.THEME_NOT_OWNED);
    }

    @Test
    @DisplayName("노트북 카테고리 적용은 NOTEBOOK 타입 인벤토리를 검증하고 공간에 저장한다")
    void changeNotebook_ownedNotebookInventory_updatesSpace() {
        Space space = TestFixtures.space(10L, 1L);
        Asset notebookAsset = Asset.builder()
                .categoryId(1L)
                .code("notebook")
                .name("notebook")
                .assetScope("SPACE")
                .assetType("NOTEBOOK")
                .pricePages(100)
                .build();
        TestFixtures.setId(notebookAsset, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(userAssetInventoryRepository.findByIdAndUserId(30L, 1L))
                .willReturn(Optional.of(TestFixtures.inventory(30L, 1L, 20L)));
        given(assetRepository.findById(20L)).willReturn(Optional.of(notebookAsset));

        var response = spaceService.changeNotebook(1L, new CategoryInventoryRequest(30L));

        assertThat(response.themeReset()).isFalse();
        assertThat(response.space().currentNotebookInventoryId()).isEqualTo(30L);
    }

    @Test
    @DisplayName("잠긴 전체 테마 상태에서 카테고리를 변경하면 전체 테마와 책장 스킨을 초기화한다")
    void changeFloor_lockedTheme_resetsThemeAndBookshelfSkins() {
        Space space = TestFixtures.space(10L, 1L);
        space.applyTheme(40L);
        Bookshelf bookshelf = Bookshelf.builder()
                .spaceId(10L)
                .name("shelf")
                .skinInventoryId(77L)
                .displayOrder(1)
                .build();
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(userAssetInventoryRepository.findById(40L)).willReturn(Optional.of(TestFixtures.inventory(40L, 1L, 50L)));
        given(assetRepository.findById(50L)).willReturn(Optional.of(TestFixtures.themeAsset(50L, 1L, "SPACE", false)));
        given(bookshelfRepository.findBySpaceIdOrderByDisplayOrderAsc(10L)).willReturn(List.of(bookshelf));

        var response = spaceService.changeFloor(1L, new CategoryInventoryRequest(null));

        assertThat(response.themeReset()).isTrue();
        assertThat(response.space().currentThemeInventoryId()).isNull();
        assertThat(response.space().layoutVersion()).isEqualTo(1L);
        assertThat(bookshelf.getSkinInventoryId()).isNull();
    }

    @Test
    @DisplayName("방명록 포스트잇 화이트리스트 row가 없으면 기본 허용으로 응답한다")
    void getGuestbookPostItWhitelist_missingRow_returnsAllowAll() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(whitelistRepository.findById(10L)).willReturn(Optional.empty());

        var response = spaceService.getGuestbookPostItWhitelist(1L);

        assertThat(response.allowAll()).isTrue();
        assertThat(response.allowedAssetIds()).isEmpty();
    }

    @Test
    @DisplayName("방명록 포스트잇 화이트리스트 수정은 새 row를 만들고 허용 asset id를 저장한다")
    void updateGuestbookPostItWhitelist_createsAndUpdatesRow() {
        Space space = TestFixtures.space(10L, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(spaceRepository.findFirstByOwnerUserIdAndDeletedAtIsNullOrderByIdAsc(1L)).willReturn(Optional.of(space));
        given(whitelistRepository.findById(10L)).willReturn(Optional.empty());
        given(whitelistRepository.save(any(SpaceGuestbookPostItWhitelist.class)))
                .willAnswer(invocation -> invocation.getArgument(0));

        var response = spaceService.updateGuestbookPostItWhitelist(
                1L,
                new GuestbookPostItWhitelistRequest(false, List.of(100L, 101L))
        );

        assertThat(response.allowAll()).isFalse();
        assertThat(response.allowedAssetIds()).containsExactlyInAnyOrder(100L, 101L);
    }

    @Test
    @DisplayName("포스트잇 화이트리스트가 allowAll=false이면 명시된 asset만 허용한다")
    void isPostItAllowed_restrictedWhitelistChecksAllowedAssetIds() {
        SpaceGuestbookPostItWhitelist whitelist = SpaceGuestbookPostItWhitelist.builder()
                .spaceId(10L)
                .allowAll(false)
                .allowedAssetIds(Set.of(100L, 101L))
                .build();
        given(whitelistRepository.findById(10L)).willReturn(Optional.of(whitelist));

        assertThat(spaceService.isPostItAllowed(10L, 100L)).isTrue();
        assertThat(spaceService.isPostItAllowed(10L, 999L)).isFalse();
        assertThat(spaceService.isPostItAllowed(10L, null)).isFalse();
    }

    @Test
    @DisplayName("공간 asset manifest는 소유자 적용 자산을 포함한다")
    void getVisibleAssetManifests_returnsOwnerAppliedAssetManifests() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeDesk(101L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(userBlockRepository.existsBlockBetween(2L, 1L)).willReturn(false);
        given(userAssetInventoryRepository.findByUserId(1L))
                .willReturn(List.of(TestFixtures.inventory(101L, 1L, 20L)));
        given(assetService.getDownloadManifestForVisibleAsset(20L))
                .willReturn(new DownloadManifestResponse(20L, "DESK", "NATURAL", 3, List.of()));

        var response = spaceService.getVisibleAssetManifests(2L, 10L);

        assertThat(response.spaceId()).isEqualTo(10L);
        assertThat(response.manifests()).singleElement()
                .satisfies(manifest -> {
                    assertThat(manifest.assetId()).isEqualTo(20L);
                    assertThat(manifest.inventoryId()).isEqualTo(101L);
                });
    }

    @Test
    @DisplayName("공간 asset manifest는 최신 layoutPayload의 asset 참조도 포함한다")
    void getVisibleAssetManifests_includesLayoutAssetReferences() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeDesk(101L);
        SpaceLayoutSnapshot snapshot = SpaceLayoutSnapshot.builder()
                .spaceId(10L)
                .layoutVersion(2L)
                .layoutPayload("""
                        {"items":[{"AssetId":"asset:30"},{"AssetId":"inv:102"}]}
                        """)
                .savedByUserId(1L)
                .build();
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(userBlockRepository.existsBlockBetween(2L, 1L)).willReturn(false);
        given(layoutSnapshotRepository.findFirstBySpaceIdOrderByLayoutVersionDesc(10L)).willReturn(Optional.of(snapshot));
        given(userAssetInventoryRepository.findByUserId(1L)).willReturn(List.of(
                TestFixtures.inventory(101L, 1L, 20L),
                TestFixtures.inventory(102L, 1L, 40L),
                TestFixtures.inventory(103L, 1L, 30L)));
        given(assetService.getDownloadManifestForVisibleAsset(20L))
                .willReturn(new DownloadManifestResponse(20L, "DESK", "NATURAL", 1, List.of()));
        given(assetService.getDownloadManifestForVisibleAsset(30L))
                .willReturn(new DownloadManifestResponse(30L, "PLANT", "NATURAL", 1, List.of()));
        given(assetService.getDownloadManifestForVisibleAsset(40L))
                .willReturn(new DownloadManifestResponse(40L, "RUG", "NATURAL", 1, List.of()));

        var response = spaceService.getVisibleAssetManifests(2L, 10L);

        assertThat(response.manifests())
                .extracting(DownloadManifestResponse::assetId)
                .containsExactlyInAnyOrder(20L, 30L, 40L);
        assertThat(response.manifests())
                .filteredOn(manifest -> manifest.assetId().equals(40L))
                .singleElement()
                .satisfies(manifest -> assertThat(manifest.inventoryId()).isEqualTo(102L));
    }

    @Test
    @DisplayName("visible asset manifest preserves distinct inventory ids for the same asset")
    void getVisibleAssetManifests_preservesDistinctInventoryIdsForSameAsset() {
        Space space = TestFixtures.space(10L, 1L);
        space.changeDesk(101L);
        SpaceLayoutSnapshot snapshot = SpaceLayoutSnapshot.builder()
                .spaceId(10L)
                .layoutVersion(2L)
                .layoutPayload("""
                        {"items":[{"AssetId":"inv:102"}]}
                        """)
                .savedByUserId(1L)
                .build();
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(userBlockRepository.existsBlockBetween(2L, 1L)).willReturn(false);
        given(layoutSnapshotRepository.findFirstBySpaceIdOrderByLayoutVersionDesc(10L)).willReturn(Optional.of(snapshot));
        given(userAssetInventoryRepository.findByUserId(1L)).willReturn(List.of(
                TestFixtures.inventory(101L, 1L, 20L),
                TestFixtures.inventory(102L, 1L, 20L)));
        given(assetService.getDownloadManifestForVisibleAsset(20L))
                .willReturn(new DownloadManifestResponse(20L, "DESK", "NATURAL", 1, List.of()));

        var response = spaceService.getVisibleAssetManifests(2L, 10L);

        assertThat(response.manifests())
                .extracting(DownloadManifestResponse::assetId)
                .containsExactly(20L, 20L);
        assertThat(response.manifests())
                .extracting(DownloadManifestResponse::inventoryId)
                .containsExactlyInAnyOrder(101L, 102L);
    }

    @Test
    @DisplayName("space asset manifest includes layout path references by unityResourceKey")
    void getVisibleAssetManifests_includesLegacyPathReferences() {
        Space space = TestFixtures.space(10L, 1L);
        SpaceLayoutSnapshot snapshot = SpaceLayoutSnapshot.builder()
                .spaceId(10L)
                .layoutVersion(2L)
                .layoutPayload("""
                        {"items":[{"AssetId":"path:Assets/Arts/Models/spooky_bed.glb"}]}
                        """)
                .savedByUserId(1L)
                .build();
        Asset asset = Asset.builder()
                .categoryId(100L)
                .code("SPOOKY_BED")
                .name("Spooky Bed")
                .assetScope("SPACE")
                .assetType("ASSET")
                .unityResourceKey("Assets/Arts/Models/spooky_bed.glb")
                .pricePages(0)
                .build();
        TestFixtures.setId(asset, 30L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(userBlockRepository.existsBlockBetween(2L, 1L)).willReturn(false);
        given(layoutSnapshotRepository.findFirstBySpaceIdOrderByLayoutVersionDesc(10L)).willReturn(Optional.of(snapshot));
        given(userAssetInventoryRepository.findByUserId(1L))
                .willReturn(List.of(TestFixtures.inventory(103L, 1L, 30L)));
        given(assetRepository.findByUnityResourceKeyIn(Set.of("Assets/Arts/Models/spooky_bed.glb")))
                .willReturn(List.of(asset));
        given(assetService.getDownloadManifestForVisibleAsset(30L))
                .willReturn(new DownloadManifestResponse(30L, "ASSET", "SPOOKY", 1, List.of()));

        var response = spaceService.getVisibleAssetManifests(2L, 10L);

        assertThat(response.manifests())
                .extracting(DownloadManifestResponse::assetId)
                .containsExactly(30L);
    }

    @Test
    @DisplayName("공간 layout 조회는 최신 스냅샷 payload를 반환한다")
    void getLayout_returnsLatestSnapshot() {
        Space space = TestFixtures.space(10L, 1L);
        SpaceLayoutSnapshot snapshot = SpaceLayoutSnapshot.builder()
                .spaceId(10L)
                .layoutVersion(3L)
                .layoutPayload("{\"items\":[{\"AssetId\":\"asset:30\"}]}")
                .savedByUserId(1L)
                .build();
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(userBlockRepository.existsBlockBetween(2L, 1L)).willReturn(false);
        given(layoutSnapshotRepository.findFirstBySpaceIdOrderByLayoutVersionDesc(10L)).willReturn(Optional.of(snapshot));

        var response = spaceService.getLayout(2L, 10L);

        assertThat(response.spaceId()).isEqualTo(10L);
        assertThat(response.layoutVersion()).isEqualTo(3L);
        assertThat(response.layoutPayload()).contains("asset:30");
    }

    @Test
    @DisplayName("보유 책 목록은 차단 관계면 SPACE_ACCESS_BLOCKED를 던진다")
    void getOwnedBooks_blockedRelation_throwsSpaceAccessBlocked() {
        Space space = TestFixtures.space(10L, 1L);
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(userBlockRepository.existsBlockBetween(2L, 1L)).willReturn(true);

        assertThatThrownBy(() -> spaceService.getOwnedBooks(2L, 10L, "TITLE_ASC", null, 20))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.SPACE_ACCESS_BLOCKED);
        then(userBookRepository).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("보유 책 목록은 정렬 후 cursor 다음 항목부터 limit 만큼 반환하고 nextCursor를 만든다")
    void getOwnedBooks_sortsAndSlicesAfterCursor() {
        Space space = TestFixtures.space(10L, 1L);
        OwnedBookSummary alpha = ownedBook(1L, "Alpha", LocalDate.of(2024, 1, 1), LocalDateTime.of(2026, 5, 1, 0, 0));
        OwnedBookSummary beta = ownedBook(2L, "Beta", LocalDate.of(2024, 1, 2), LocalDateTime.of(2026, 5, 2, 0, 0));
        OwnedBookSummary gamma = ownedBook(3L, "Gamma", LocalDate.of(2024, 1, 3), LocalDateTime.of(2026, 5, 3, 0, 0));
        given(spaceRepository.findById(10L)).willReturn(Optional.of(space));
        given(userBlockRepository.existsBlockBetween(2L, 1L)).willReturn(false);
        given(userBookRepository.findOwnedBookSummaries(1L)).willReturn(List.of(gamma, alpha, beta));

        var response = spaceService.getOwnedBooks(2L, 10L, "TITLE_ASC", "1", 1);

        assertThat(response.items()).hasSize(1);
        assertThat(response.items().getFirst().userBookId()).isEqualTo(2L);
        assertThat(response.items().getFirst().title()).isEqualTo("Beta");
        assertThat(response.nextCursor()).isEqualTo("2");
    }

    private OwnedBookSummary ownedBook(Long userBookId, String title, LocalDate publishedDate, LocalDateTime addedAt) {
        return new OwnedBookSummary(
                userBookId,
                100L + userBookId,
                title,
                "author",
                "publisher",
                publishedDate,
                "https://example.com/" + title + ".jpg",
                addedAt
        );
    }
}
