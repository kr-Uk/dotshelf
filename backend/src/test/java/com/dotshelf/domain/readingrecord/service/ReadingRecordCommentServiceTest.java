package com.dotshelf.domain.readingrecord.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

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

import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentRequest;
import com.dotshelf.domain.readingrecord.entity.ReadingRecord;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordComment;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordCommentLike;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordCommentLikeRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordCommentRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class ReadingRecordCommentServiceTest {

    @Mock
    private ReadingRecordCommentRepository commentRepository;

    @Mock
    private ReadingRecordCommentLikeRepository likeRepository;

    @Mock
    private ReadingRecordService readingRecordService;

    @Mock
    private UserService userService;

    @Mock
    private ApplicationEventPublisher eventPublisher;

    @InjectMocks
    private ReadingRecordCommentService commentService;

    @Test
    @DisplayName("댓글 목록은 읽기 권한 확인 후 id 오름차순 결과를 매핑한다")
    void getComments_checksReadableRecordAndMaps() {
        given(readingRecordService.getReadableRecord(1L, 10L)).willReturn(TestFixtures.readingRecord(10L, 20L));
        given(commentRepository.findByReadingRecordIdOrderByIdAsc(10L)).willReturn(List.of(
                TestFixtures.readingRecordComment(30L, 10L, 1L),
                TestFixtures.readingRecordComment(31L, 10L, 2L)
        ));

        var response = commentService.getComments(1L, 10L);

        assertThat(response.comments()).extracting("id").containsExactly(30L, 31L);
    }

    @Test
    @DisplayName("최상위 댓글 생성은 댓글 저장, commentCount 증가, 알림 이벤트 발행을 수행한다")
    void create_topLevelComment_savesAndPublishes() {
        ReadingRecord record = TestFixtures.readingRecord(10L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(readingRecordService.getReadableRecord(1L, 10L)).willReturn(record);
        given(readingRecordService.getRecordOwnerUserId(record)).willReturn(2L);
        given(commentRepository.save(any(ReadingRecordComment.class))).willAnswer(invocation -> {
            ReadingRecordComment comment = invocation.getArgument(0);
            TestFixtures.setId(comment, 30L);
            return comment;
        });
        ArgumentCaptor<ReadingRecordComment> captor = ArgumentCaptor.forClass(ReadingRecordComment.class);

        var response = commentService.create(1L, 10L, new CommentRequest(null, "hello"));

        assertThat(response.id()).isEqualTo(30L);
        assertThat(record.getCommentCount()).isEqualTo(1);
        then(commentRepository).should().save(captor.capture());
        assertThat(captor.getValue().getDepth()).isZero();
        then(eventPublisher).should().publishEvent(any(Object.class));
    }

    @Test
    @DisplayName("대댓글은 부모 댓글과 같은 독서기록에만 작성할 수 있다")
    void create_replyParentMustBelongSameRecord() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(readingRecordService.getReadableRecord(1L, 10L)).willReturn(TestFixtures.readingRecord(10L, 20L));
        given(commentRepository.findById(30L))
                .willReturn(Optional.of(TestFixtures.readingRecordComment(30L, 11L, 2L)));

        assertThatThrownBy(() -> commentService.create(1L, 10L, new CommentRequest(30L, "reply")))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("대댓글의 대댓글은 허용하지 않는다")
    void create_replyDepthCannotExceedOne() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(readingRecordService.getReadableRecord(1L, 10L)).willReturn(TestFixtures.readingRecord(10L, 20L));
        given(commentRepository.findById(30L))
                .willReturn(Optional.of(TestFixtures.replyComment(30L, 10L, 2L, 29L)));

        assertThatThrownBy(() -> commentService.create(1L, 10L, new CommentRequest(30L, "reply")))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("댓글 삭제는 작성자만 가능하다")
    void delete_otherWriterThrowsForbidden() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(commentRepository.findById(30L))
                .willReturn(Optional.of(TestFixtures.readingRecordComment(30L, 10L, 2L)));

        assertThatThrownBy(() -> commentService.delete(1L, 30L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
    }

    @Test
    @DisplayName("댓글 좋아요는 없을 때 생성하고 카운트를 증가한다")
    void like_createsCommentLike() {
        ReadingRecordComment comment = TestFixtures.readingRecordComment(30L, 10L, 2L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(commentRepository.findById(30L)).willReturn(Optional.of(comment));
        given(readingRecordService.getReadableRecord(1L, 10L)).willReturn(TestFixtures.readingRecord(10L, 20L));
        given(likeRepository.existsByCommentIdAndUserId(30L, 1L)).willReturn(false);
        ArgumentCaptor<ReadingRecordCommentLike> captor = ArgumentCaptor.forClass(ReadingRecordCommentLike.class);

        var response = commentService.like(1L, 30L);

        assertThat(response.active()).isTrue();
        assertThat(response.count()).isEqualTo(1);
        then(likeRepository).should().save(captor.capture());
        assertThat(captor.getValue().getCommentId()).isEqualTo(30L);
    }

    @Test
    @DisplayName("댓글 좋아요 취소는 존재할 때 삭제하고 카운트를 감소한다")
    void unlike_existingLike_deletesAndDecreases() {
        ReadingRecordComment comment = TestFixtures.readingRecordComment(30L, 10L, 2L);
        comment.increaseLikeCount();
        given(userService.requireUserId(1L)).willReturn(1L);
        given(commentRepository.findById(30L)).willReturn(Optional.of(comment));
        given(readingRecordService.getReadableRecord(1L, 10L)).willReturn(TestFixtures.readingRecord(10L, 20L));
        given(likeRepository.existsByCommentIdAndUserId(30L, 1L)).willReturn(true);

        var response = commentService.unlike(1L, 30L);

        assertThat(response.active()).isFalse();
        assertThat(response.count()).isZero();
        then(likeRepository).should().deleteByCommentIdAndUserId(30L, 1L);
    }
}
