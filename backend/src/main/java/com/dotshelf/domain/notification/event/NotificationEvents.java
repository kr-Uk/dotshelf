package com.dotshelf.domain.notification.event;

public final class NotificationEvents {

    private NotificationEvents() {
    }

    public record GuestbookCreated(Long receiverUserId, Long senderUserId, Long guestbookId, boolean anonymous) {
    }

    public record GuestbookReplyCreated(Long receiverUserId, Long senderUserId, Long replyId) {
    }

    public record BookAdded(Long senderUserId, Long userBookId, String bookTitle) {
    }

    public record ReadingRecordLiked(Long receiverUserId, Long senderUserId, Long readingRecordId) {
    }

    public record ReadingRecordCommented(Long receiverUserId, Long senderUserId, Long commentId) {
    }

    public record ReadingRecordCommentReplied(Long receiverUserId, Long senderUserId, Long commentId) {
    }
}
