package com.dotshelf.domain.readingrecord.entity;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(
        name = "reading_record_comment_likes",
        uniqueConstraints = @UniqueConstraint(columnNames = {"comment_id", "user_id"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ReadingRecordCommentLike extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long commentId;

    @Column(nullable = false)
    private Long userId;

    @Builder
    private ReadingRecordCommentLike(Long commentId, Long userId) {
        this.commentId = commentId;
        this.userId = userId;
    }
}
