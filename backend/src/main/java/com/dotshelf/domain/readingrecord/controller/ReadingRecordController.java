package com.dotshelf.domain.readingrecord.controller;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentListResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentRequest;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.CommentResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordListResponse;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordRequest;
import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordResponse;
import com.dotshelf.domain.readingrecord.service.ReadingRecordCommentService;
import com.dotshelf.domain.readingrecord.service.ReadingRecordService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@RestController
@RequiredArgsConstructor
@RequestMapping("/v1")
public class ReadingRecordController {

    private final ReadingRecordService readingRecordService;
    private final ReadingRecordCommentService commentService;

    @GetMapping("/reading-records/{recordId}")
    public ApiResponse<ReadingRecordResponse> getRecord(@PathVariable Long recordId) {
        return ApiResponse.success(readingRecordService.getRecord(recordId));
    }

    @GetMapping("/users/{userId}/reading-records")
    public ApiResponse<ReadingRecordListResponse> getUserRecords(@PathVariable Long userId) {
        return ApiResponse.success(readingRecordService.getUserRecords(userId));
    }

    @PostMapping("/reading-records")
    public ApiResponse<ReadingRecordResponse> create(
            @CurrentUser Long userId,
            @RequestBody @Valid ReadingRecordRequest request
    ) {
        return ApiResponse.success(readingRecordService.create(userId, request));
    }

    @PatchMapping("/reading-records/{recordId}")
    public ApiResponse<ReadingRecordResponse> update(
            @CurrentUser Long userId,
            @PathVariable Long recordId,
            @RequestBody @Valid ReadingRecordRequest request
    ) {
        return ApiResponse.success(readingRecordService.update(userId, recordId, request));
    }

    @DeleteMapping("/reading-records/{recordId}")
    public ApiResponse<Void> delete(@CurrentUser Long userId, @PathVariable Long recordId) {
        readingRecordService.delete(userId, recordId);
        return ApiResponse.success();
    }

    @PostMapping("/reading-records/{recordId}/like")
    public ApiResponse<ToggleResponse> like(@CurrentUser Long userId, @PathVariable Long recordId) {
        return ApiResponse.success(readingRecordService.like(userId, recordId));
    }

    @DeleteMapping("/reading-records/{recordId}/like")
    public ApiResponse<ToggleResponse> unlike(@CurrentUser Long userId, @PathVariable Long recordId) {
        return ApiResponse.success(readingRecordService.unlike(userId, recordId));
    }

    @GetMapping("/reading-records/{recordId}/comments")
    public ApiResponse<CommentListResponse> getComments(@PathVariable Long recordId) {
        return ApiResponse.success(commentService.getComments(recordId));
    }

    @PostMapping("/reading-records/{recordId}/comments")
    public ApiResponse<CommentResponse> createComment(
            @CurrentUser Long userId,
            @PathVariable Long recordId,
            @RequestBody @Valid CommentRequest request
    ) {
        return ApiResponse.success(commentService.create(userId, recordId, request));
    }

    @DeleteMapping("/comments/{commentId}")
    public ApiResponse<Void> deleteComment(@CurrentUser Long userId, @PathVariable Long commentId) {
        commentService.delete(userId, commentId);
        return ApiResponse.success();
    }

    @PostMapping("/comments/{commentId}/like")
    public ApiResponse<ToggleResponse> likeComment(@CurrentUser Long userId, @PathVariable Long commentId) {
        return ApiResponse.success(commentService.like(userId, commentId));
    }

    @DeleteMapping("/comments/{commentId}/like")
    public ApiResponse<ToggleResponse> unlikeComment(@CurrentUser Long userId, @PathVariable Long commentId) {
        return ApiResponse.success(commentService.unlike(userId, commentId));
    }
}
