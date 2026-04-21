package com.dotshelf.domain.feed.dto;

import java.time.LocalDateTime;
import java.util.List;

import com.dotshelf.domain.feed.entity.BookPromotion;
import com.dotshelf.domain.feed.entity.FeedEvent;

public final class FeedDtos {

    private FeedDtos() {
    }

    public record FeedEventResponse(
            Long id,
            Long actorUserId,
            String eventType,
            String targetType,
            Long targetId,
            LocalDateTime occurredAt
    ) {

        public static FeedEventResponse from(FeedEvent event) {
            return new FeedEventResponse(
                    event.getId(),
                    event.getActorUserId(),
                    event.getEventType(),
                    event.getTargetType(),
                    event.getTargetId(),
                    event.getOccurredAt()
            );
        }
    }

    public record FeedEventListResponse(List<FeedEventResponse> events) {
    }

    public record BookPromotionResponse(
            Long id,
            Long bookId,
            String promotionType,
            String title,
            String description,
            String imageUrl,
            String landingUrl,
            String sponsorName
    ) {

        public static BookPromotionResponse from(BookPromotion promotion) {
            return new BookPromotionResponse(
                    promotion.getId(),
                    promotion.getBookId(),
                    promotion.getPromotionType(),
                    promotion.getTitle(),
                    promotion.getDescription(),
                    promotion.getImageUrl(),
                    promotion.getLandingUrl(),
                    promotion.getSponsorName()
            );
        }
    }

    public record BookPromotionListResponse(List<BookPromotionResponse> promotions) {
    }
}
