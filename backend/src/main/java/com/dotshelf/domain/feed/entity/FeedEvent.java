package com.dotshelf.domain.feed.entity;

import java.time.LocalDateTime;

import com.dotshelf.global.common.BaseEntity;
import com.dotshelf.global.common.VisibilityScope;

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
@Table(name = "feed_events")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class FeedEvent extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long actorUserId;

    private Long spaceId;

    private Long bookId;

    private Long readingRecordId;

    @Column(nullable = false, length = 50)
    private String eventType;

    @Column(nullable = false, length = 50)
    private String targetType;

    @Column(nullable = false)
    private Long targetId;

    @Column(nullable = false, length = 20)
    private String visibilityScope = "PUBLIC";

    @Column(nullable = false)
    private LocalDateTime occurredAt = LocalDateTime.now();

    @Builder
    private FeedEvent(Long actorUserId, Long spaceId, Long bookId, Long readingRecordId,
                      String eventType, String targetType, Long targetId, String visibilityScope) {
        this.actorUserId = actorUserId;
        this.spaceId = spaceId;
        this.bookId = bookId;
        this.readingRecordId = readingRecordId;
        this.eventType = eventType;
        this.targetType = targetType;
        this.targetId = targetId;
        this.visibilityScope = VisibilityScope.normalize(visibilityScope);
    }
}
