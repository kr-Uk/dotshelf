package com.dotshelf.domain.notification.service;

import org.springframework.scheduling.annotation.Async;
import org.springframework.stereotype.Component;
import org.springframework.transaction.event.TransactionPhase;
import org.springframework.transaction.event.TransactionalEventListener;

import com.dotshelf.domain.notification.event.NotificationEvents.BookAdded;
import com.dotshelf.domain.notification.event.NotificationEvents.GuestbookCreated;
import com.dotshelf.domain.notification.event.NotificationEvents.GuestbookReplyCreated;
import com.dotshelf.domain.notification.event.NotificationEvents.ReadingRecordCommentReplied;
import com.dotshelf.domain.notification.event.NotificationEvents.ReadingRecordCommented;
import com.dotshelf.domain.notification.event.NotificationEvents.ReadingRecordLiked;

import lombok.RequiredArgsConstructor;

@Component
@RequiredArgsConstructor
public class NotificationEventListener {

    private final NotificationService notificationService;

    @Async("notificationTaskExecutor")
    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    public void handle(GuestbookCreated event) {
        notificationService.notifyGuestbookCreated(
                event.receiverUserId(),
                event.senderUserId(),
                event.guestbookId()
        );
    }

    @Async("notificationTaskExecutor")
    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    public void handle(GuestbookReplyCreated event) {
        notificationService.notifyGuestbookReplyCreated(
                event.receiverUserId(),
                event.senderUserId(),
                event.replyId()
        );
    }

    @Async("notificationTaskExecutor")
    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    public void handle(BookAdded event) {
        notificationService.notifyBookAddedToFollowers(event.senderUserId(), event.userBookId(), event.bookTitle());
    }

    @Async("notificationTaskExecutor")
    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    public void handle(ReadingRecordLiked event) {
        notificationService.notifyReadingRecordLiked(
                event.receiverUserId(),
                event.senderUserId(),
                event.readingRecordId()
        );
    }

    @Async("notificationTaskExecutor")
    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    public void handle(ReadingRecordCommented event) {
        notificationService.notifyReadingRecordCommented(
                event.receiverUserId(),
                event.senderUserId(),
                event.commentId()
        );
    }

    @Async("notificationTaskExecutor")
    @TransactionalEventListener(phase = TransactionPhase.AFTER_COMMIT)
    public void handle(ReadingRecordCommentReplied event) {
        notificationService.notifyReadingRecordCommentReplied(
                event.receiverUserId(),
                event.senderUserId(),
                event.commentId()
        );
    }
}
