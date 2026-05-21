package com.dotshelf.domain.feed.entity;

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
@Table(name = "book_promotions")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class BookPromotion extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long bookId;

    @Column(nullable = false, length = 30)
    private String promotionType;

    @Column(nullable = false, length = 100)
    private String title;

    @Column(length = 500)
    private String description;

    @Column(length = 500)
    private String imageUrl;

    @Column(length = 500)
    private String landingUrl;

    @Column(length = 100)
    private String sponsorName;

    @Column(nullable = false)
    private int displayOrder = 0;

    @Column(nullable = false)
    private LocalDateTime startsAt;

    private LocalDateTime endsAt;

    @Column(nullable = false)
    private boolean isActive = true;

    @Builder
    private BookPromotion(Long bookId, String promotionType, String title, String description,
                          String imageUrl, String landingUrl, String sponsorName,
                          int displayOrder, LocalDateTime startsAt, LocalDateTime endsAt) {
        this.bookId = bookId;
        this.promotionType = promotionType;
        this.title = title;
        this.description = description;
        this.imageUrl = imageUrl;
        this.landingUrl = landingUrl;
        this.sponsorName = sponsorName;
        this.displayOrder = displayOrder;
        this.startsAt = startsAt;
        this.endsAt = endsAt;
    }
}
