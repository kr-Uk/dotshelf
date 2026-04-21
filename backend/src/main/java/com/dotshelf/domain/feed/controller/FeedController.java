package com.dotshelf.domain.feed.controller;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.feed.dto.FeedDtos.BookPromotionListResponse;
import com.dotshelf.domain.feed.dto.FeedDtos.FeedEventListResponse;
import com.dotshelf.domain.feed.service.FeedService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;

@Tag(name = "Feed", description = "팔로잉 피드 / 추천 피드 / 도서 프로모션")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/feed")
public class FeedController {

    private final FeedService feedService;

    @GetMapping("/following")
    public ApiResponse<FeedEventListResponse> getFollowingFeed(@CurrentUser Long userId) {
        return ApiResponse.success(feedService.getFollowingFeed(userId));
    }

    @GetMapping("/recommended-records")
    public ApiResponse<FeedEventListResponse> getRecommendedRecords() {
        return ApiResponse.success(feedService.getRecommendedRecords());
    }

    @GetMapping("/recommended-books")
    public ApiResponse<FeedEventListResponse> getRecommendedBooks() {
        return ApiResponse.success(feedService.getRecommendedBooks());
    }

    @GetMapping("/book-promotions")
    public ApiResponse<BookPromotionListResponse> getBookPromotions() {
        return ApiResponse.success(feedService.getBookPromotions());
    }
}
