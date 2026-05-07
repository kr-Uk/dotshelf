package com.dotshelf.domain.feed.controller;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.feed.dto.FeedDtos.BookPromotionListResponse;
import com.dotshelf.domain.feed.dto.FeedDtos.FeedEventListResponse;
import com.dotshelf.domain.feed.service.FeedService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;

@Tag(name = "Feed", description = "팔로잉 피드 / 추천 피드 / 도서 프로모션")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/feed")
public class FeedController {

    private final FeedService feedService;

    @Operation(summary = "팔로잉 피드 조회", description = "현재 사용자가 팔로우한 사용자의 활동 피드를 조회합니다.")
    @GetMapping("/following")
    public ApiResponse<FeedEventListResponse> getFollowingFeed(@CurrentUser Long userId) {
        return ApiResponse.success(feedService.getFollowingFeed(userId));
    }

    @Operation(summary = "추천 독서 기록 피드 조회", description = "서비스에서 추천하는 공개 독서 기록 피드를 조회합니다.")
    @GetMapping("/recommended-records")
    public ApiResponse<FeedEventListResponse> getRecommendedRecords() {
        return ApiResponse.success(feedService.getRecommendedRecords());
    }

    @Operation(summary = "추천 도서 피드 조회", description = "서비스에서 추천하는 도서 피드 항목을 조회합니다.")
    @GetMapping("/recommended-books")
    public ApiResponse<FeedEventListResponse> getRecommendedBooks() {
        return ApiResponse.success(feedService.getRecommendedBooks());
    }

    @Operation(summary = "도서 프로모션 조회", description = "피드 영역에 노출할 도서 프로모션 목록을 조회합니다.")
    @GetMapping("/book-promotions")
    public ApiResponse<BookPromotionListResponse> getBookPromotions() {
        return ApiResponse.success(feedService.getBookPromotions());
    }
}
