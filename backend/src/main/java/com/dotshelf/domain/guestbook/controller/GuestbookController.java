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

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@RestController
@RequiredArgsConstructor
@RequestMapping("/v1")
public class GuestbookController {

    private final GuestbookService guestbookService;

    @GetMapping("/spaces/{spaceId}/guestbooks")
    public ApiResponse<GuestbookListResponse> getGuestbooks(@PathVariable Long spaceId) {
        return ApiResponse.success(guestbookService.getGuestbooks(spaceId));
    }

    @PostMapping("/spaces/{spaceId}/guestbooks")
    public ApiResponse<GuestbookResponse> create(
            @CurrentUser Long userId,
            @PathVariable Long spaceId,
            @RequestBody @Valid GuestbookRequest request
    ) {
        return ApiResponse.success(guestbookService.create(userId, spaceId, request));
    }

    @DeleteMapping("/guestbooks/{guestbookId}")
    public ApiResponse<Void> delete(@CurrentUser Long userId, @PathVariable Long guestbookId) {
        guestbookService.delete(userId, guestbookId);
        return ApiResponse.success();
    }

    @PostMapping("/guestbooks/{guestbookId}/replies")
    public ApiResponse<GuestbookReplyResponse> reply(
            @CurrentUser Long userId,
            @PathVariable Long guestbookId,
            @RequestBody @Valid GuestbookReplyRequest request
    ) {
        return ApiResponse.success(guestbookService.reply(userId, guestbookId, request));
    }
}
