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
@Table(name = "user_feed_settings")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserFeedSetting extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, unique = true)
    private Long userId;

    @Column(nullable = false)
    private boolean showBookAdded = true;

    @Column(nullable = false)
    private boolean showReadingRecord = true;

    @Column(nullable = false)
    private boolean showSpaceVisit = true;

    @Column(nullable = false)
    private boolean showGuestbook = true;

    @Column(nullable = false)
    private boolean showReadingRecordComment = true;

    @Builder
    private UserFeedSetting(Long userId) {
        this.userId = userId;
    }

    public void update(Boolean showBookAdded, Boolean showReadingRecord, Boolean showSpaceVisit,
                       Boolean showGuestbook, Boolean showReadingRecordComment) {
        if (showBookAdded != null) {
            this.showBookAdded = showBookAdded;
        }
        if (showReadingRecord != null) {
            this.showReadingRecord = showReadingRecord;
        }
        if (showSpaceVisit != null) {
            this.showSpaceVisit = showSpaceVisit;
        }
        if (showGuestbook != null) {
            this.showGuestbook = showGuestbook;
        }
        if (showReadingRecordComment != null) {
            this.showReadingRecordComment = showReadingRecordComment;
        }
    }
}
