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
        name = "book_reviews",
        uniqueConstraints = @UniqueConstraint(columnNames = {"book_id", "user_id"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class BookReview extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long bookId;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false, precision = 2, scale = 1)
    private BigDecimal rating;

    @Column(nullable = false, length = 1000)
    private String content;

    @Column(nullable = false)
    private int likeCount = 0;

    @Column(nullable = false, length = 20)
    private String status = "ACTIVE";

    private LocalDateTime deletedAt;

    @Builder
    private BookReview(Long bookId, Long userId, BigDecimal rating, String content) {
        this.bookId = bookId;
        this.userId = userId;
        this.rating = rating;
        this.content = content;
    }

    public void delete() {
        this.status = "DELETED";
        this.deletedAt = LocalDateTime.now();
    }
}
