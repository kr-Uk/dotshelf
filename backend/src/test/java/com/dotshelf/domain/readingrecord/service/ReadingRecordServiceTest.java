package com.dotshelf.domain.readingrecord.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.context.ApplicationEventPublisher;

import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.book.service.BookService;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordRequest;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordSummary;
import com.dotshelf.domain.readingrecord.entity.ReadingRecord;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordLike;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordLikeRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordRepository;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class ReadingRecordServiceTest {

    @Mock
    private ReadingRecordRepository readingRecordRepository;

    @Mock
    private ReadingRecordLikeRepository likeRepository;

    @Mock
    private BookService bookService;

    @Mock
    private SpaceService spaceService;

    @Mock
    private UserService userService;

    @Mock
    private FollowRepository followRepository;

    @Mock
    private ApplicationEventPublisher eventPublisher;

    @InjectMocks
    private ReadingRecordService readingRecordService;

    @Test
    @DisplayName("내 userBook의 독서기록은 ACTIVE일 때 반환한다")
    void getMyRecordByUserBook_returnsActiveRecord() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookService.getUserBook(20L)).willReturn(TestFixtures.userBook(20L, 1L, 9L));
        given(readingRecordRepository.findByUserBookId(20L))
                .willReturn(Optional.of(TestFixtures.readingRecord(10L, 20L)));

        var response = readingRecordService.getMyRecordByUserBook(1L, 20L);

        assertThat(response.id()).isEqualTo(10L);
        assertThat(response.userBookId()).isEqualTo(20L);
    }

    @Test
    @DisplayName("다른 사용자의 userBook 독서기록 조회는 FORBIDDEN을 던진다")
    void getMyRecordByUserBook_otherOwnerThrowsForbidden() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookService.getUserBook(20L)).willReturn(TestFixtures.userBook(20L, 2L, 9L));

        assertThatThrownBy(() -> readingRecordService.getMyRecordByUserBook(1L, 20L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
    }

    @Test
    @DisplayName("목록 조회는 PUBLIC과 팔로워 공개만 viewer에게 노출한다")
    void getUserRecords_filtersVisibilityForFollower() {
        Instant now = Instant.now();
        given(readingRecordRepository.findUserRecordSummaries(2L)).willReturn(List.of(
                summary(1L, "PUBLIC", 1, now.minusSeconds(3)),
                summary(2L, "FOLLOWERS", 2, now.minusSeconds(2)),
                summary(3L, "PRIVATE", 3, now.minusSeconds(1))
        ));
        given(followRepository.existsByFollowerUserIdAndFollowingUserId(1L, 2L)).willReturn(true);

        var response = readingRecordService.getUserRecords(1L, 2L, "LATEST", null, 10);

        assertThat(response.items()).extracting(ReadingRecordSummary::id).containsExactly(2L, 1L);
    }

    @Test
    @DisplayName("목록 조회는 cursor 이후 항목과 nextCursor를 계산한다")
    void getUserRecords_appliesCursorAndNextCursor() {
        Instant now = Instant.now();
        given(readingRecordRepository.findUserRecordSummaries(2L)).willReturn(List.of(
                summary(1L, "PUBLIC", 1, now.minusSeconds(3)),
                summary(2L, "PUBLIC", 2, now.minusSeconds(2)),
                summary(3L, "PUBLIC", 3, now.minusSeconds(1))
        ));

        var response = readingRecordService.getUserRecords(null, 2L, "LATEST", "3", 1);

        assertThat(response.items()).extracting(ReadingRecordSummary::id).containsExactly(2L);
        assertThat(response.nextCursor()).isEqualTo("2");
    }

    @Test
    @DisplayName("기존 독서기록이 있으면 create가 새 row 대신 기존 row를 업데이트한다")
    void create_existingRecord_updates() {
        ReadingRecord existing = TestFixtures.readingRecord(10L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookService.getUserBook(20L)).willReturn(TestFixtures.userBook(20L, 1L, 9L));
        given(readingRecordRepository.findByUserBookId(20L)).willReturn(Optional.of(existing));

        var response = readingRecordService.create(1L, request(20L, "changed", "PRIVATE"));

        assertThat(response.title()).isEqualTo("changed");
        assertThat(response.visibilityScope()).isEqualTo("PRIVATE");
        then(readingRecordRepository).should().findByUserBookId(20L);
        then(readingRecordRepository).shouldHaveNoMoreInteractions();
    }

    @Test
    @DisplayName("신규 독서기록 생성은 소유자 검증 후 저장한다")
    void create_newRecord_saves() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookService.getUserBook(20L)).willReturn(TestFixtures.userBook(20L, 1L, 9L));
        given(readingRecordRepository.findByUserBookId(20L)).willReturn(Optional.empty());
        given(readingRecordRepository.save(any(ReadingRecord.class))).willAnswer(invocation -> {
            ReadingRecord record = invocation.getArgument(0);
            TestFixtures.setId(record, 10L);
            return record;
        });
        given(spaceService.findOrCreateMySpace(1L)).willReturn(TestFixtures.space(1L, 1L));

        var response = readingRecordService.create(1L, request(20L, "new", "PUBLIC"));

        assertThat(response.id()).isEqualTo(10L);
        assertThat(response.title()).isEqualTo("new");
    }

    @Test
    @DisplayName("수정/삭제는 record 소유자만 가능하다")
    void updateAndDelete_requireOwner() {
        ReadingRecord record = TestFixtures.readingRecord(10L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(readingRecordRepository.findById(10L)).willReturn(Optional.of(record));
        given(bookService.getUserBook(20L)).willReturn(TestFixtures.userBook(20L, 1L, 9L));
        given(spaceService.findOrCreateMySpace(1L)).willReturn(TestFixtures.space(1L, 1L));

        var response = readingRecordService.update(1L, 10L, request(20L, "updated", "FOLLOWERS"));
        readingRecordService.delete(1L, 10L);

        assertThat(response.title()).isEqualTo("updated");
        assertThat(record.getStatus()).isEqualTo("DELETED");
    }

    @Test
    @DisplayName("좋아요는 없을 때 생성하고 카운트 증가와 이벤트 발행을 수행한다")
    void like_createsLikeAndPublishesEvent() {
        ReadingRecord record = TestFixtures.readingRecord(10L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(readingRecordRepository.findById(10L)).willReturn(Optional.of(record));
        given(bookService.getUserBook(20L)).willReturn(TestFixtures.userBook(20L, 2L, 9L));
        given(likeRepository.existsByReadingRecordIdAndUserId(10L, 1L)).willReturn(false);
        ArgumentCaptor<ReadingRecordLike> captor = ArgumentCaptor.forClass(ReadingRecordLike.class);

        var response = readingRecordService.like(1L, 10L);

        assertThat(response.active()).isTrue();
        assertThat(response.count()).isEqualTo(1);
        then(likeRepository).should().save(captor.capture());
        assertThat(captor.getValue().getReadingRecordId()).isEqualTo(10L);
        then(eventPublisher).should().publishEvent(any(Object.class));
    }

    @Test
    @DisplayName("이미 좋아요한 독서기록은 중복 저장과 이벤트 발행을 하지 않는다")
    void like_existingLike_isIdempotent() {
        ReadingRecord record = TestFixtures.readingRecord(10L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(readingRecordRepository.findById(10L)).willReturn(Optional.of(record));
        given(bookService.getUserBook(20L)).willReturn(TestFixtures.userBook(20L, 2L, 9L));
        given(likeRepository.existsByReadingRecordIdAndUserId(10L, 1L)).willReturn(true);

        var response = readingRecordService.like(1L, 10L);

        assertThat(response.count()).isZero();
        then(likeRepository).should().existsByReadingRecordIdAndUserId(10L, 1L);
        then(likeRepository).shouldHaveNoMoreInteractions();
        then(eventPublisher).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("좋아요 취소는 존재할 때 삭제하고 카운트를 감소한다")
    void unlike_existingLike_deletesAndDecreases() {
        ReadingRecord record = TestFixtures.readingRecord(10L, 20L);
        record.increaseLikeCount();
        given(userService.requireUserId(1L)).willReturn(1L);
        given(readingRecordRepository.findById(10L)).willReturn(Optional.of(record));
        given(bookService.getUserBook(20L)).willReturn(TestFixtures.userBook(20L, 2L, 9L));
        given(likeRepository.existsByReadingRecordIdAndUserId(10L, 1L)).willReturn(true);

        var response = readingRecordService.unlike(1L, 10L);

        assertThat(response.active()).isFalse();
        assertThat(response.count()).isZero();
        then(likeRepository).should().deleteByReadingRecordIdAndUserId(10L, 1L);
    }

    private ReadingRecordRequest request(Long userBookId, String title, String visibility) {
        return new ReadingRecordRequest(userBookId, title, "content", visibility);
    }

    private ReadingRecordSummary summary(Long id, String visibility, int likeCount, Instant createdAt) {
        return new ReadingRecordSummary(id, 9L, "book-" + id, "content", visibility, likeCount, 0, 0, createdAt);
    }
}
