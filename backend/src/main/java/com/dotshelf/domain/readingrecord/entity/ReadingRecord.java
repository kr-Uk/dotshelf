package com.dotshelf.domain.readingrecord.entity;

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
@Table(name = "reading_records")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ReadingRecord extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, unique = true)
    private Long userBookId;

    @Column(length = 150)
    private String title;

    @Column(columnDefinition = "TEXT")
    private String content;

    @Column(nullable = false, length = 20)
    private String visibilityScope = "PUBLIC";

    @Column(nullable = false)
    private int likeCount = 0;

    @Column(nullable = false)
    private int commentCount = 0;

    @Column(nullable = false)
    private int viewCount = 0;

    @Column(nullable = false, length = 20)
    private String status = "ACTIVE";

    private LocalDateTime deletedAt;

    @Builder
    private ReadingRecord(Long userBookId, String title, String content, String visibilityScope) {
        this.userBookId = userBookId;
        this.title = title;
        this.content = content;
        this.visibilityScope = VisibilityScope.normalize(visibilityScope);
    }

    public void update(String title, String content, String visibilityScope) {
        this.title = title;
        this.content = content;
        this.visibilityScope = VisibilityScope.normalize(visibilityScope);
        this.status = "ACTIVE";
        this.deletedAt = null;
    }

    public void delete() {
        this.status = "DELETED";
        this.deletedAt = LocalDateTime.now();
    }

    public void increaseLikeCount() {
        this.likeCount += 1;
    }

    public void decreaseLikeCount() {
        if (this.likeCount > 0) {
            this.likeCount -= 1;
        }
    }

    public void increaseCommentCount() {
        this.commentCount += 1;
    }
}
