package com.dotshelf.domain.guestbook.dto;

import java.util.List;

import com.dotshelf.domain.guestbook.entity.Guestbook;
import com.dotshelf.domain.guestbook.entity.GuestbookReply;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public final class GuestbookDtos {

    private GuestbookDtos() {
    }

    public record GuestbookRequest(
            Long postItInventoryId,
            @NotBlank(message = "방명록 내용은 필수입니다.")
            @Size(max = 30, message = "방명록은 30자 이하입니다.")
            String content,
            String visibilityScope
    ) {
    }

    public record GuestbookResponse(
            Long id,
            Long spaceId,
            Long writerUserId,
            Long postItInventoryId,
            String content,
            String visibilityScope,
            int replyCount
    ) {

        public static GuestbookResponse from(Guestbook guestbook) {
            return new GuestbookResponse(
                    guestbook.getId(),
                    guestbook.getSpaceId(),
                    guestbook.getWriterUserId(),
                    guestbook.getPostItInventoryId(),
                    guestbook.getContent(),
                    guestbook.getVisibilityScope(),
                    guestbook.getReplyCount()
            );
        }
    }

    public record GuestbookListResponse(List<GuestbookResponse> guestbooks) {
    }

    public record GuestbookReplyRequest(
            @NotBlank(message = "답장 내용은 필수입니다.")
            @Size(max = 1000, message = "답장은 1000자 이하입니다.")
            String content,
            String visibilityScope
    ) {
    }

    public record GuestbookReplyResponse(Long id, Long guestbookId, Long writerUserId, String content, String visibilityScope) {

        public static GuestbookReplyResponse from(GuestbookReply reply) {
            return new GuestbookReplyResponse(
                    reply.getId(),
                    reply.getGuestbookId(),
                    reply.getWriterUserId(),
                    reply.getContent(),
                    reply.getVisibilityScope()
            );
        }
    }
}
