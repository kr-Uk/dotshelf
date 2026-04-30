package com.dotshelf.domain.readingrecord.dto;

import java.time.Instant;
import java.util.List;

import com.dotshelf.domain.readingrecord.entity.ReadingRecord;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordComment;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

public final class ReadingRecordDtos {

    private ReadingRecordDtos() {
    }

    public record ReadingRecordRequest(
            @NotNull(message = "userBookId는 필수입니다.")
            Long userBookId,
            @Size(max = 150, message = "제목은 150자 이하입니다.")
            String title,
            @Size(max = 10000, message = "독서기록은 10000자 이하입니다.")
            String content,
            String visibilityScope
    ) {
    }

    public record ReadingRecordResponse(
            Long id,
            Long userBookId,
            String title,
            String content,
            String visibilityScope,
            int likeCount,
            int commentCount,
            Instant createdAt,
            Instant updatedAt
    ) {

        public static ReadingRecordResponse from(ReadingRecord record) {
            if (record == null) {
                return null;
            }
            return new ReadingRecordResponse(
                    record.getId(),
                    record.getUserBookId(),
                    record.getTitle(),
                    record.getContent(),
                    record.getVisibilityScope(),
                    record.getLikeCount(),
                    record.getCommentCount(),
                    record.getCreatedAt(),
                    record.getUpdatedAt()
            );
        }
    }

    /**
     * 대시보드 \"독후감 풀 목록\" 카드용 경량 DTO. v1.0 갱신 — 정렬·페이지네이션을 위해 추가.
     * book 정보(title)는 userBook → book join으로 채워서 N+1 차단.
     */
    public record ReadingRecordSummary(
            Long id,
            Long bookId,
            String bookTitle,
            String content,
            int likeCount,
            int commentCount,
            int viewCount,
            Instant createdAt
    ) {
    }

    public record ReadingRecordPageResponse(List<ReadingRecordSummary> items, String nextCursor) {
    }

    public record CommentRequest(
            Long parentCommentId,
            @NotBlank(message = "댓글 내용은 필수입니다.")
            @Size(max = 1000, message = "댓글은 1000자 이하입니다.")
            String content
    ) {
    }

    public record CommentResponse(
            Long id,
            Long readingRecordId,
            Long userId,
            Long parentCommentId,
            int depth,
            String content,
            int likeCount,
            boolean deleted
    ) {

        public static CommentResponse from(ReadingRecordComment comment) {
            return new CommentResponse(
                    comment.getId(),
                    comment.getReadingRecordId(),
                    comment.getUserId(),
                    comment.getParentCommentId(),
                    comment.getDepth(),
                    comment.getContent(),
                    comment.getLikeCount(),
                    comment.isDeleted()
            );
        }
    }

    public record CommentListResponse(List<CommentResponse> comments) {
    }
}
