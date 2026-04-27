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
@Table(name = "guestbooks")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Guestbook extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long spaceId;

    @Column(nullable = false)
    private Long writerUserId;

    private Long postItInventoryId;

    @Column(nullable = false, length = 30)
    private String content;

    @Column(nullable = false, length = 20)
    private String visibilityScope = "PUBLIC";

    @Column(nullable = false)
    private int replyCount = 0;

    @Column(nullable = false, length = 20)
    private String status = "ACTIVE";

    private LocalDateTime deletedAt;

    @Builder
    private Guestbook(Long spaceId, Long writerUserId, Long postItInventoryId, String content, String visibilityScope) {
        this.spaceId = spaceId;
        this.writerUserId = writerUserId;
        this.postItInventoryId = postItInventoryId;
        this.content = content;
        this.visibilityScope = visibilityScope == null ? "PUBLIC" : visibilityScope;
    }

    public void increaseReplyCount() {
        this.replyCount += 1;
    }

    public void delete() {
        this.status = "DELETED";
        this.deletedAt = LocalDateTime.now();
    }
}
