package com.dotshelf.domain.guestbook.dto;

import java.time.Instant;
import java.util.List;

import com.dotshelf.domain.guestbook.entity.Guestbook;
import com.dotshelf.domain.guestbook.entity.GuestbookReply;
import com.dotshelf.domain.user.entity.User;

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
            String visibilityScope,
            Boolean anonymous
    ) {
    }

    public record GuestbookResponse(
            Long id,
            Long spaceId,
            Long writerUserId,
            String writerNickname,
            String writerProfileImageUrl,
            boolean anonymous,
            Long postItInventoryId,
            String postItPreviewImageUrl,
            String content,
            String visibilityScope,
            int replyCount,
            GuestbookReplyResponse reply,
            Instant createdAt
    ) {

        public static GuestbookResponse from(Guestbook guestbook,
                                             User writer,
                                             GuestbookReply reply,
                                             User replyWriter,
                                             Long viewerUserId) {
            boolean showAsAnonymous = guestbook.isAnonymous()
                    && (viewerUserId == null || !viewerUserId.equals(guestbook.getWriterUserId()));
            String nickname = showAsAnonymous ? "익명"
                    : (writer == null ? null : writer.getNickname());
            String profileImage = showAsAnonymous ? null
                    : (writer == null ? null : writer.getProfileImageUrl());
            GuestbookReplyResponse replyResponse = reply == null
                    ? null
                    : GuestbookReplyResponse.from(reply, replyWriter);
            return new GuestbookResponse(
                    guestbook.getId(),
                    guestbook.getSpaceId(),
                    guestbook.getWriterUserId(),
                    nickname,
                    profileImage,
                    guestbook.isAnonymous(),
                    guestbook.getPostItInventoryId(),
                    null,
                    guestbook.getContent(),
                    guestbook.getVisibilityScope(),
                    guestbook.getReplyCount(),
                    replyResponse,
                    guestbook.getCreatedAt()
            );
        }
    }

    public record GuestbookListResponse(List<GuestbookResponse> guestbooks, String nextCursor) {

        public GuestbookListResponse(List<GuestbookResponse> guestbooks) {
            this(guestbooks, null);
        }
    }

    public record GuestbookReplyRequest(
            Long postItInventoryId,
            @NotBlank(message = "답장 내용은 필수입니다.")
            @Size(max = 30, message = "답장은 30자 이하입니다.")
            String content,
            String visibilityScope
    ) {
    }

    public record GuestbookReplyResponse(
            Long id,
            Long guestbookId,
            Long writerUserId,
            String writerNickname,
            Long postItInventoryId,
            String postItPreviewImageUrl,
            String content,
            String visibilityScope,
            Instant createdAt
    ) {

        public static GuestbookReplyResponse from(GuestbookReply reply, User writer) {
            return new GuestbookReplyResponse(
                    reply.getId(),
                    reply.getGuestbookId(),
                    reply.getWriterUserId(),
                    writer == null ? null : writer.getNickname(),
                    reply.getPostItInventoryId(),
                    null,
                    reply.getContent(),
                    reply.getVisibilityScope(),
                    reply.getCreatedAt()
            );
        }

        public static GuestbookReplyResponse from(GuestbookReply reply) {
            return from(reply, null);
        }
    }
}
