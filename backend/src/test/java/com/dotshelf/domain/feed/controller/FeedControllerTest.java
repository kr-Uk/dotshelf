package com.dotshelf.domain.feed.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.time.LocalDateTime;
import java.util.List;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import com.dotshelf.domain.feed.dto.FeedDtos.BookPromotionListResponse;
import com.dotshelf.domain.feed.dto.FeedDtos.BookPromotionResponse;
import com.dotshelf.domain.feed.dto.FeedDtos.FeedEventListResponse;
import com.dotshelf.domain.feed.dto.FeedDtos.FeedEventResponse;
import com.dotshelf.domain.feed.service.FeedService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class FeedControllerTest {

    @Mock
    private FeedService feedService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new FeedController(feedService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("팔로잉 피드는 CurrentUser를 서비스에 전달한다")
    void getFollowingFeed_passesCurrentUser() throws Exception {
        given(feedService.getFollowingFeed(1L))
                .willReturn(new FeedEventListResponse(List.of(feedEvent(10L))));

        mockMvc.perform(get("/v1/feed/following").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.events[0].id").value(10))
                .andExpect(jsonPath("$.result.events[0].eventType").value("BOOK_ADDED"));
    }

    @Test
    @DisplayName("추천 독서기록과 추천 도서는 envelope로 감싼 빈 목록도 반환한다")
    void recommendedFeeds_returnEnvelope() throws Exception {
        given(feedService.getRecommendedRecords()).willReturn(new FeedEventListResponse(List.of()));
        given(feedService.getRecommendedBooks()).willReturn(new FeedEventListResponse(List.of(feedEvent(11L))));

        mockMvc.perform(get("/v1/feed/recommended-records"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.events").isArray());

        mockMvc.perform(get("/v1/feed/recommended-books"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.events[0].id").value(11));
    }

    @Test
    @DisplayName("도서 프로모션 조회는 promotion 목록을 반환한다")
    void getBookPromotions_returnsPromotions() throws Exception {
        given(feedService.getBookPromotions())
                .willReturn(new BookPromotionListResponse(List.of(
                        new BookPromotionResponse(1L, 9L, "MAIN", "title", "desc", null, null, "sponsor")
                )));

        mockMvc.perform(get("/v1/feed/book-promotions"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.promotions[0].promotionType").value("MAIN"))
                .andExpect(jsonPath("$.result.promotions[0].bookId").value(9));
    }

    private FeedEventResponse feedEvent(Long id) {
        return new FeedEventResponse(id, 1L, "BOOK_ADDED", "USER_BOOK", 9L, LocalDateTime.now());
    }
}
