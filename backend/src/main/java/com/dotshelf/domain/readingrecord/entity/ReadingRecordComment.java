package com.dotshelf.domain.readingrecord.entity;

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
@Table(name = "reading_record_comments")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ReadingRecordComment extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long readingRecordId;

    @Column(nullable = false)
    private Long userId;

    private Long parentCommentId;

    @Column(nullable = false)
    private int depth = 0;

    @Column(nullable = false, length = 1000)
    private String content;

    @Column(nullable = false)
    private int likeCount = 0;

    @Column(nullable = false)
    private boolean isDeleted = false;

    @Builder
    private ReadingRecordComment(Long readingRecordId, Long userId, Long parentCommentId, int depth, String content) {
        this.readingRecordId = readingRecordId;
        this.userId = userId;
        this.parentCommentId = parentCommentId;
        this.depth = depth;
        this.content = content;
    }

    public void delete() {
        this.isDeleted = true;
        this.content = "삭제된 댓글입니다.";
    }

    public void increaseLikeCount() {
        this.likeCount += 1;
    }

    public void decreaseLikeCount() {
        if (this.likeCount > 0) {
            this.likeCount -= 1;
        }
    }
}
