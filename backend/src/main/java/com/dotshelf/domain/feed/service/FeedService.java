package com.dotshelf.domain.feed.service;

import java.time.LocalDateTime;
import java.util.List;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.feed.dto.FeedDtos.BookPromotionListResponse;
import com.dotshelf.domain.feed.dto.FeedDtos.BookPromotionResponse;
import com.dotshelf.domain.feed.dto.FeedDtos.FeedEventListResponse;
import com.dotshelf.domain.feed.dto.FeedDtos.FeedEventResponse;
import com.dotshelf.domain.feed.repository.BookPromotionRepository;
import com.dotshelf.domain.feed.repository.FeedEventRepository;
import com.dotshelf.domain.user.service.UserService;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class FeedService {

    private final FeedEventRepository feedEventRepository;
    private final BookPromotionRepository bookPromotionRepository;
    private final UserService userService;

    public FeedEventListResponse getFollowingFeed(Long userId) {
        userService.requireUserId(userId);
        return new FeedEventListResponse(feedEventRepository.findTop20ByOrderByOccurredAtDesc().stream()
                .map(FeedEventResponse::from)
                .toList());
    }

    public FeedEventListResponse getRecommendedRecords() {
        return new FeedEventListResponse(List.of());
    }

    public FeedEventListResponse getRecommendedBooks() {
        return new FeedEventListResponse(List.of());
    }

    public BookPromotionListResponse getBookPromotions() {
        return new BookPromotionListResponse(bookPromotionRepository
                .findTop20ByIsActiveTrueAndStartsAtBeforeOrderByDisplayOrderAsc(LocalDateTime.now())
                .stream()
                .map(BookPromotionResponse::from)
                .toList());
    }
}
