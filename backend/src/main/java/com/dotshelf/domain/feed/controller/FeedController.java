package com.dotshelf.domain.feed.controller;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.feed.dto.FeedDtos.BookPromotionListResponse;
import com.dotshelf.domain.feed.dto.FeedDtos.FeedEventListResponse;
import com.dotshelf.domain.feed.service.FeedService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import lombok.RequiredArgsConstructor;

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
