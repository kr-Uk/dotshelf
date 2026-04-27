package com.dotshelf.domain.book.entity;

import java.math.BigDecimal;
import java.time.LocalDateTime;

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
        name = "user_books",
        uniqueConstraints = @UniqueConstraint(columnNames = {"user_id", "book_id"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserBook extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false)
    private Long bookId;

    private Long selectedCoverProductId;

    @Column(nullable = false, length = 20)
    private String readingStatus = "UNREAD";

    @Column(nullable = false, length = 20)
    private String storageStatus = "ON_SHELF";

    @Column(precision = 2, scale = 1)
    private BigDecimal ownerRating;

    @Column(nullable = false)
    private boolean isLiked = false;

    @Column(nullable = false)
    private LocalDateTime addedAt = LocalDateTime.now();

    private LocalDateTime deletedAt;

    @Builder
    private UserBook(Long userId, Long bookId) {
        this.userId = userId;
        this.bookId = bookId;
    }

    public void update(String readingStatus, BigDecimal ownerRating, Long selectedCoverProductId) {
        if (readingStatus != null) {
            this.readingStatus = readingStatus;
        }
        if (ownerRating != null) {
            this.ownerRating = ownerRating;
        }
        if (selectedCoverProductId != null) {
            this.selectedCoverProductId = selectedCoverProductId;
        }
    }

    public void archive() {
        this.storageStatus = "ARCHIVED";
    }

    public void placeOnShelf() {
        this.storageStatus = "ON_SHELF";
    }

    public void delete() {
        this.storageStatus = "DELETED";
        this.deletedAt = LocalDateTime.now();
    }

    public void like() {
        this.isLiked = true;
    }

    public void unlike() {
        this.isLiked = false;
    }
}
