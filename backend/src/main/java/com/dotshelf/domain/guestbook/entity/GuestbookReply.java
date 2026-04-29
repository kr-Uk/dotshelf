package com.dotshelf.domain.guestbook.entity;

import java.time.LocalDateTime;

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
@Table(name = "guestbook_replies")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class GuestbookReply extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long guestbookId;

    @Column(nullable = false)
    private Long writerUserId;

    private Long postItInventoryId;

    @Column(nullable = false, length = 1000)
    private String content;

    @Column(nullable = false, length = 20)
    private String visibilityScope = "PUBLIC";

    private LocalDateTime deletedAt;

    @Builder
    private GuestbookReply(Long guestbookId, Long writerUserId, Long postItInventoryId,
                           String content, String visibilityScope) {
        this.guestbookId = guestbookId;
        this.writerUserId = writerUserId;
        this.postItInventoryId = postItInventoryId;
        this.content = content;
        this.visibilityScope = visibilityScope == null ? "PUBLIC" : visibilityScope;
    }
}
