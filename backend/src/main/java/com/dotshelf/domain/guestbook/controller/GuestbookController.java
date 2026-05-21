package com.dotshelf.domain.guestbook.controller;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookListResponse;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookReplyRequest;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookReplyResponse;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookRequest;
import com.dotshelf.domain.guestbook.dto.GuestbookDtos.GuestbookResponse;
import com.dotshelf.domain.guestbook.service.GuestbookService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Guestbook", description = "공간 방명록 / 답글")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1")
public class GuestbookController {

    private final GuestbookService guestbookService;

    @Operation(summary = "공간 방명록 목록 조회", description = "지정한 공간의 방명록을 커서 기반으로 조회합니다.")
    @GetMapping("/spaces/{spaceId}/guestbooks")
    public ApiResponse<GuestbookListResponse> getGuestbooks(
            @CurrentUser Long currentUserId,
            @PathVariable Long spaceId,
            @org.springframework.web.bind.annotation.RequestParam(required = false) String cursor,
            @org.springframework.web.bind.annotation.RequestParam(defaultValue = "15") int limit
    ) {
        return ApiResponse.success(guestbookService.getGuestbooks(currentUserId, spaceId, cursor, limit));
    }

    @Operation(summary = "방명록 작성", description = "지정한 공간에 현재 사용자의 방명록을 작성합니다.")
    @PostMapping("/spaces/{spaceId}/guestbooks")
    public ApiResponse<GuestbookResponse> create(
            @CurrentUser Long userId,
            @PathVariable Long spaceId,
            @RequestBody @Valid GuestbookRequest request
    ) {
        return ApiResponse.success(guestbookService.create(userId, spaceId, request));
    }

    @Operation(summary = "방명록 삭제", description = "방명록 작성자 또는 권한 있는 사용자가 방명록을 삭제합니다.")
    @DeleteMapping("/guestbooks/{guestbookId}")
    public ApiResponse<Void> delete(@CurrentUser Long userId, @PathVariable Long guestbookId) {
        guestbookService.delete(userId, guestbookId);
        return ApiResponse.success();
    }

    @Operation(summary = "방명록 답글 작성", description = "지정한 방명록에 현재 사용자의 답글을 작성합니다.")
    @PostMapping("/guestbooks/{guestbookId}/replies")
    public ApiResponse<GuestbookReplyResponse> reply(
            @CurrentUser Long userId,
            @PathVariable Long guestbookId,
            @RequestBody @Valid GuestbookReplyRequest request
    ) {
        return ApiResponse.success(guestbookService.reply(userId, guestbookId, request));
    }

    @Operation(summary = "방명록 답글 삭제", description = "방명록 답글을 삭제합니다.")
    @DeleteMapping("/guestbooks/{guestbookId}/replies/{replyId}")
    public ApiResponse<Void> deleteReply(
            @CurrentUser Long userId,
            @PathVariable Long guestbookId,
            @PathVariable Long replyId
    ) {
        guestbookService.deleteReply(userId, guestbookId, replyId);
        return ApiResponse.success();
    }
}
