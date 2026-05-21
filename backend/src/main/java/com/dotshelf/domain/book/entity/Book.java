package com.dotshelf.domain.book.entity;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.Locale;
import java.util.regex.Pattern;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
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
@Table(name = "books")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Book extends BaseEntity {

    private static final Pattern TITLE_SEARCH_WHITESPACE = Pattern.compile("\\s+");

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(length = 13, unique = true)
    private String isbn13;

    @Column(length = 10)
    private String isbn10;

    @Column(nullable = false, length = 255)
    private String title;

    @Column(length = 255)
    private String titleSearch;

    @Column(nullable = false, length = 255)
    private String author;

    @Column(length = 255)
    private String publisher;

    private LocalDate publishedDate;

    @Column(columnDefinition = "TEXT")
    private String description;

    @Column(length = 500)
    private String coverImageUrl;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 16)
    private BookSource source = BookSource.LOCAL;

    @Column(length = 500)
    private String sourceUrl;

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
                 BookSource source, String sourceUrl,
                 Integer pageCount, String language) {
        this.isbn13 = isbn13;
        this.isbn10 = isbn10;
        this.title = title;
        this.titleSearch = normalizeTitleSearch(title);
        this.author = author;
        this.publisher = publisher;
        this.publishedDate = publishedDate;
        this.description = description;
        this.coverImageUrl = coverImageUrl;
        this.source = source == null ? BookSource.LOCAL : source;
        this.sourceUrl = sourceUrl;
        this.pageCount = pageCount;
        this.language = language;
    }

    public void increaseBookshelfAddCount() {
        this.bookshelfAddCount += 1;
    }

    public void increaseViewCount() {
        this.viewCount += 1;
    }

    public void increaseLikeCount() {
        this.likeCount += 1;
    }

    public void decreaseLikeCount() {
        this.likeCount = Math.max(0, this.likeCount - 1);
    }

    public void addReviewRating(BigDecimal rating) {
        this.ratingSum = this.ratingSum.add(rating);
        this.ratingCount += 1;
    }

    public static String normalizeTitleSearch(String title) {
        if (title == null) {
            return null;
        }
        return TITLE_SEARCH_WHITESPACE.matcher(title)
                .replaceAll("")
                .toLowerCase(Locale.ROOT);
    }
}
