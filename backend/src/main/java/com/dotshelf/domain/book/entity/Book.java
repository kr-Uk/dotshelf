package com.dotshelf.domain.book.entity;

import java.math.BigDecimal;
import java.time.LocalDate;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Lob;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "books")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Book extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(length = 13, unique = true)
    private String isbn13;

    @Column(length = 10)
    private String isbn10;

    @Column(nullable = false, length = 255)
    private String title;

    @Column(nullable = false, length = 255)
    private String author;

    @Column(length = 255)
    private String publisher;

    private LocalDate publishedDate;

    @Lob
    private String description;

    @Column(length = 500)
    private String coverImageUrl;

    private Integer pageCount;

    @Column(length = 20)
    private String language;

    @Column(nullable = false)
    private long viewCount = 0;

    @Column(nullable = false)
    private long likeCount = 0;

    @Column(nullable = false)
    private long bookshelfAddCount = 0;

    @Column(nullable = false, precision = 10, scale = 1)
    private BigDecimal ratingSum = BigDecimal.ZERO;

    @Column(nullable = false)
    private int ratingCount = 0;

    @Builder
    private Book(String isbn13, String isbn10, String title, String author, String publisher,
                 LocalDate publishedDate, String description, String coverImageUrl,
                 Integer pageCount, String language) {
        this.isbn13 = isbn13;
        this.isbn10 = isbn10;
        this.title = title;
        this.author = author;
        this.publisher = publisher;
        this.publishedDate = publishedDate;
        this.description = description;
        this.coverImageUrl = coverImageUrl;
        this.pageCount = pageCount;
        this.language = language;
    }

    public void increaseBookshelfAddCount() {
        this.bookshelfAddCount += 1;
    }
}
