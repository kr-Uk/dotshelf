package com.dotshelf.domain.readingrecord.controller;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentListResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentRequest;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordPageResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordRequest;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordResponse;
import com.dotshelf.domain.readingrecord.service.ReadingRecordCommentService;
import com.dotshelf.domain.readingrecord.service.ReadingRecordService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "ReadingRecord", description = "독서 기록 CRUD / 좋아요 / 댓글")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1")
public class ReadingRecordController {

    private final ReadingRecordService readingRecordService;
    private final ReadingRecordCommentService commentService;

    @Operation(summary = "독서 기록 상세 조회", description = "독서 기록 ID로 독서 기록 상세 내용과 현재 사용자의 상호작용 상태를 조회합니다.")
    @GetMapping("/reading-records/{recordId}")
    public ApiResponse<ReadingRecordResponse> getRecord(
            @CurrentUser Long userId,
            @PathVariable Long recordId
    ) {
        return ApiResponse.success(readingRecordService.getRecord(userId, recordId));
    }

    @Operation(summary = "내 책의 독서 기록 조회", description = "현재 사용자가 보유한 책 항목에 연결된 내 독서 기록을 조회합니다.")
    @GetMapping("/user-books/{userBookId}/reading-record")
    public ApiResponse<ReadingRecordResponse> getMyRecordByUserBook(
            @CurrentUser Long userId,
            @PathVariable Long userBookId
    ) {
        return ApiResponse.success(readingRecordService.getMyRecordByUserBook(userId, userBookId));
    }

    @Operation(summary = "사용자 독서 기록 목록 조회", description = "지정한 사용자의 공개 독서 기록을 정렬 조건과 커서 기반 페이지로 조회합니다.")
    @GetMapping("/users/{userId}/reading-records")
    public ApiResponse<ReadingRecordPageResponse> getUserRecords(
            @CurrentUser Long currentUserId,
            @PathVariable Long userId,
            @RequestParam(defaultValue = "LATEST") String sort,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(readingRecordService.getUserRecords(currentUserId, userId, sort, cursor, limit));
    }

    @Operation(summary = "독서 기록 작성", description = "현재 사용자의 책에 새 독서 기록을 작성합니다.")
    @PostMapping("/reading-records")
    public ApiResponse<ReadingRecordResponse> create(
            @CurrentUser Long userId,
            @RequestBody @Valid ReadingRecordRequest request
    ) {
        return ApiResponse.success(readingRecordService.create(userId, request));
    }

    @Operation(summary = "독서 기록 수정", description = "현재 사용자가 작성한 독서 기록 내용을 수정합니다.")
    @PatchMapping("/reading-records/{recordId}")
    public ApiResponse<ReadingRecordResponse> update(
            @CurrentUser Long userId,
            @PathVariable Long recordId,
            @RequestBody @Valid ReadingRecordRequest request
    ) {
        return ApiResponse.success(readingRecordService.update(userId, recordId, request));
    }

    @Operation(summary = "독서 기록 삭제", description = "현재 사용자가 작성한 독서 기록을 삭제합니다.")
    @DeleteMapping("/reading-records/{recordId}")
    public ApiResponse<Void> delete(@CurrentUser Long userId, @PathVariable Long recordId) {
        readingRecordService.delete(userId, recordId);
        return ApiResponse.success();
    }

    @Operation(summary = "독서 기록 좋아요", description = "지정한 독서 기록에 좋아요를 추가하고 현재 토글 상태를 반환합니다.")
    @PostMapping("/reading-records/{recordId}/like")
    public ApiResponse<ToggleResponse> like(@CurrentUser Long userId, @PathVariable Long recordId) {
        return ApiResponse.success(readingRecordService.like(userId, recordId));
    }

    @Operation(summary = "독서 기록 좋아요 취소", description = "지정한 독서 기록의 좋아요를 취소하고 현재 토글 상태를 반환합니다.")
    @DeleteMapping("/reading-records/{recordId}/like")
    public ApiResponse<ToggleResponse> unlike(@CurrentUser Long userId, @PathVariable Long recordId) {
        return ApiResponse.success(readingRecordService.unlike(userId, recordId));
    }

    @Operation(summary = "독서 기록 댓글 목록 조회", description = "지정한 독서 기록의 댓글 목록을 조회합니다.")
    @GetMapping("/reading-records/{recordId}/comments")
    public ApiResponse<CommentListResponse> getComments(
            @CurrentUser Long userId,
            @PathVariable Long recordId
    ) {
        return ApiResponse.success(commentService.getComments(userId, recordId));
    }

    @Operation(summary = "독서 기록 댓글 작성", description = "지정한 독서 기록에 현재 사용자의 댓글을 작성합니다.")
    @PostMapping("/reading-records/{recordId}/comments")
    public ApiResponse<CommentResponse> createComment(
            @CurrentUser Long userId,
            @PathVariable Long recordId,
            @RequestBody @Valid CommentRequest request
    ) {
        return ApiResponse.success(commentService.create(userId, recordId, request));
    }

    @Operation(summary = "댓글 삭제", description = "현재 사용자가 작성한 댓글을 삭제합니다.")
    @DeleteMapping("/comments/{commentId}")
    public ApiResponse<Void> deleteComment(@CurrentUser Long userId, @PathVariable Long commentId) {
        commentService.delete(userId, commentId);
        return ApiResponse.success();
    }

    @Operation(summary = "댓글 좋아요", description = "지정한 댓글에 좋아요를 추가하고 현재 토글 상태를 반환합니다.")
    @PostMapping("/comments/{commentId}/like")
    public ApiResponse<ToggleResponse> likeComment(@CurrentUser Long userId, @PathVariable Long commentId) {
        return ApiResponse.success(commentService.like(userId, commentId));
    }

    @Operation(summary = "댓글 좋아요 취소", description = "지정한 댓글의 좋아요를 취소하고 현재 토글 상태를 반환합니다.")
    @DeleteMapping("/comments/{commentId}/like")
    public ApiResponse<ToggleResponse> unlikeComment(@CurrentUser Long userId, @PathVariable Long commentId) {
        return ApiResponse.success(commentService.unlike(userId, commentId));
    }
}
