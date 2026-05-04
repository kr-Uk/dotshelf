package com.dotshelf.domain.user.entity;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "user_notification_settings")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserNotificationSetting extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, unique = true)
    private Long userId;

    @Column(nullable = false)
    private boolean notifyNewGuestbook = true;

    @Column(nullable = false)
    private boolean notifyGuestbookReply = true;

    @Column(nullable = false)
    private boolean notifyReadingRecordReaction = true;

    @Column(nullable = false)
    private boolean notifyReadingRecordComment = true;

    @Column(nullable = false)
    private boolean notifyNewFollower = true;

    @Column(nullable = false)
    private boolean notifyBookAddedByOthers = true;

    @Builder
    private UserNotificationSetting(Long userId) {
        this.userId = userId;
    }

    public void update(Boolean notifyNewGuestbook, Boolean notifyGuestbookReply,
                       Boolean notifyReadingRecordReaction, Boolean notifyReadingRecordComment,
                       Boolean notifyNewFollower, Boolean notifyBookAddedByOthers) {
        if (notifyNewGuestbook != null) {
            this.notifyNewGuestbook = notifyNewGuestbook;
        }
        if (notifyGuestbookReply != null) {
            this.notifyGuestbookReply = notifyGuestbookReply;
        }
        if (notifyReadingRecordReaction != null) {
            this.notifyReadingRecordReaction = notifyReadingRecordReaction;
        }
        if (notifyReadingRecordComment != null) {
            this.notifyReadingRecordComment = notifyReadingRecordComment;
        }
        if (notifyNewFollower != null) {
            this.notifyNewFollower = notifyNewFollower;
        }
        if (notifyBookAddedByOthers != null) {
            this.notifyBookAddedByOthers = notifyBookAddedByOthers;
        }
    }
}
