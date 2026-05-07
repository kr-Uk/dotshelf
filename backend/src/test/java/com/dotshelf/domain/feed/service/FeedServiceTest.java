package com.dotshelf.domain.feed.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.time.LocalDateTime;
import java.util.List;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.feed.entity.BookPromotion;
import com.dotshelf.domain.feed.entity.FeedEvent;
import com.dotshelf.domain.feed.repository.BookPromotionRepository;
import com.dotshelf.domain.feed.repository.FeedEventRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class FeedServiceTest {

    @Mock
    private FeedEventRepository feedEventRepository;

    @Mock
    private BookPromotionRepository bookPromotionRepository;

    @Mock
    private UserService userService;

    @InjectMocks
    private FeedService feedService;

    @Test
    @DisplayName("팔로잉 피드는 인증 사용자를 요구하고 최신 피드 이벤트를 반환한다")
    void getFollowingFeed_requiresUserAndReturnsEvents() {
        FeedEvent event = FeedEvent.builder()
                .actorUserId(2L)
                .eventType("BOOK_ADDED")
                .targetType("USER_BOOK")
                .targetId(10L)
                .build();
        TestFixtures.setId(event, 1L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(feedEventRepository.findTop20ByOrderByOccurredAtDesc()).willReturn(List.of(event));

        var response = feedService.getFollowingFeed(1L);

        assertThat(response.events()).hasSize(1);
        assertThat(response.events().get(0).eventType()).isEqualTo("BOOK_ADDED");
        then(userService).should().requireUserId(1L);
    }

    @Test
    @DisplayName("도서 프로모션은 활성 프로모션을 displayOrder 순서로 조회한다")
    void getBookPromotions_returnsActivePromotions() {
        BookPromotion promotion = BookPromotion.builder()
                .bookId(10L)
                .promotionType("BANNER")
                .title("추천 도서")
                .description("설명")
                .imageUrl("https://example.com/banner.png")
                .landingUrl("https://example.com/books/10")
                .sponsorName("DotShelf")
                .displayOrder(1)
                .startsAt(LocalDateTime.now().minusDays(1))
                .build();
        TestFixtures.setId(promotion, 5L);
        given(bookPromotionRepository.findTop20ByIsActiveTrueAndStartsAtBeforeOrderByDisplayOrderAsc(org.mockito.ArgumentMatchers.any()))
                .willReturn(List.of(promotion));

        var response = feedService.getBookPromotions();

        assertThat(response.promotions()).hasSize(1);
        assertThat(response.promotions().get(0).title()).isEqualTo("추천 도서");
    }

    @Test
    @DisplayName("추천 피드 자리표시자 API는 현재 빈 목록을 반환한다")
    void recommendedFeeds_returnEmptyLists() {
        assertThat(feedService.getRecommendedRecords().events()).isEmpty();
        assertThat(feedService.getRecommendedBooks().events()).isEmpty();
    }
}
