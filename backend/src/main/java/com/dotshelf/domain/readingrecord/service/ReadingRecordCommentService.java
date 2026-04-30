package com.dotshelf.domain.readingrecord.service;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentListResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentRequest;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentResponse;
import com.dotshelf.domain.readingrecord.entity.ReadingRecord;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordComment;
import com.dotshelf.domain.readingrecord.entity.ReadingRecordCommentLike;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordCommentLikeRepository;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordCommentRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class ReadingRecordCommentService {

    private final ReadingRecordCommentRepository commentRepository;
    private final ReadingRecordCommentLikeRepository likeRepository;
    private final ReadingRecordService readingRecordService;
    private final UserService userService;

    public CommentListResponse getComments(Long viewerUserId, Long readingRecordId) {
        readingRecordService.getReadableRecord(viewerUserId, readingRecordId);
        return new CommentListResponse(commentRepository.findByReadingRecordIdOrderByIdAsc(readingRecordId).stream()
                .map(CommentResponse::from)
                .toList());
    }

    @Transactional
    public CommentResponse create(Long userId, Long readingRecordId, CommentRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        ReadingRecord record = readingRecordService.getReadableRecord(currentUserId, readingRecordId);
        int depth = 0;
        if (request.parentCommentId() != null) {
            ReadingRecordComment parent = getActiveComment(request.parentCommentId());
            if (!parent.getReadingRecordId().equals(readingRecordId)) {
                throw new CustomException(ErrorCode.INVALID_INPUT, "부모 댓글이 같은 독서기록에 속해야 합니다.");
            }
            if (parent.getDepth() != 0) {
                throw new CustomException(ErrorCode.INVALID_INPUT, "대댓글은 depth 1까지만 허용됩니다.");
            }
            depth = 1;
        }
        ReadingRecordComment comment = commentRepository.save(ReadingRecordComment.builder()
                .readingRecordId(readingRecordId)
                .userId(currentUserId)
                .parentCommentId(request.parentCommentId())
                .depth(depth)
                .content(request.content())
                .build());
        record.increaseCommentCount();
        return CommentResponse.from(comment);
    }

    @Transactional
    public void delete(Long userId, Long commentId) {
        Long currentUserId = userService.requireUserId(userId);
        ReadingRecordComment comment = getComment(commentId);
        if (!comment.getUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        comment.delete();
    }

    @Transactional
    public ToggleResponse like(Long userId, Long commentId) {
        Long currentUserId = userService.requireUserId(userId);
        ReadingRecordComment comment = getActiveComment(commentId);
        readingRecordService.getReadableRecord(currentUserId, comment.getReadingRecordId());
        if (!likeRepository.existsByCommentIdAndUserId(commentId, currentUserId)) {
            likeRepository.save(ReadingRecordCommentLike.builder()
                    .commentId(commentId)
                    .userId(currentUserId)
                    .build());
            comment.increaseLikeCount();
        }
        return new ToggleResponse(true, comment.getLikeCount());
    }

    @Transactional
    public ToggleResponse unlike(Long userId, Long commentId) {
        Long currentUserId = userService.requireUserId(userId);
        ReadingRecordComment comment = getActiveComment(commentId);
        readingRecordService.getReadableRecord(currentUserId, comment.getReadingRecordId());
        if (likeRepository.existsByCommentIdAndUserId(commentId, currentUserId)) {
            likeRepository.deleteByCommentIdAndUserId(commentId, currentUserId);
            comment.decreaseLikeCount();
        }
        return new ToggleResponse(false, comment.getLikeCount());
    }

    private ReadingRecordComment getComment(Long commentId) {
        return commentRepository.findById(commentId)
                .orElseThrow(() -> new CustomException(ErrorCode.COMMENT_NOT_FOUND));
    }

    private ReadingRecordComment getActiveComment(Long commentId) {
        ReadingRecordComment comment = getComment(commentId);
        if (comment.isDeleted()) {
            throw new CustomException(ErrorCode.COMMENT_NOT_FOUND);
        }
        return comment;
    }
}
