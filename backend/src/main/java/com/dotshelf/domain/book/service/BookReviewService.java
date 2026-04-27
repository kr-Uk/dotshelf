package com.dotshelf.domain.book.service;

import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.book.dto.BookDtos.BookReviewListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookReviewRequest;
import com.dotshelf.domain.book.dto.BookDtos.BookReviewResponse;
import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.BookReview;
import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.BookReviewRepository;
import com.dotshelf.domain.user.entity.User;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class BookReviewService {

    private static final String ACTIVE = "ACTIVE";
    private static final int DEFAULT_LIMIT = 20;
    private static final int MAX_LIMIT = 50;

    private final BookRepository bookRepository;
    private final BookReviewRepository bookReviewRepository;
    private final UserRepository userRepository;
    private final UserService userService;

    public BookReviewListResponse getReviews(Long bookId, String sort, int limit) {
        requireBook(bookId);
        int validLimit = validateLimit(limit);
        List<BookReview> sortedReviews = bookReviewRepository.findByBookIdAndStatus(bookId, ACTIVE).stream()
                .sorted(reviewComparator(sort))
                .toList();
        List<BookReview> reviews = sortedReviews.stream()
                .limit(validLimit)
                .toList();
        Map<Long, User> users = usersById(reviews);
        String nextCursor = sortedReviews.size() > validLimit && !reviews.isEmpty()
                ? "review_" + reviews.getLast().getId()
                : null;
        return new BookReviewListResponse(reviews.stream()
                .map(review -> BookReviewResponse.from(review, users.get(review.getUserId())))
                .toList(), nextCursor);
    }

    @Transactional
    public BookReviewResponse createReview(Long userId, Long bookId, BookReviewRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Book book = requireBook(bookId);
        bookReviewRepository.findByBookIdAndUserIdAndStatus(bookId, currentUserId, ACTIVE)
                .ifPresent(review -> {
                    throw new CustomException(ErrorCode.INVALID_INPUT, "이미 작성한 리뷰가 있습니다.");
                });
        BookReview review = bookReviewRepository.save(BookReview.builder()
                .bookId(bookId)
                .userId(currentUserId)
                .rating(request.rating())
                .content(request.content().trim())
                .build());
        book.addReviewRating(request.rating());
        User user = userRepository.findById(currentUserId)
                .orElseThrow(() -> new CustomException(ErrorCode.USER_NOT_FOUND));
        return BookReviewResponse.from(review, user);
    }

    private Book requireBook(Long bookId) {
        return bookRepository.findById(bookId)
                .orElseThrow(() -> new CustomException(ErrorCode.BOOK_NOT_FOUND));
    }

    private Map<Long, User> usersById(List<BookReview> reviews) {
        List<Long> userIds = reviews.stream()
                .map(BookReview::getUserId)
                .distinct()
                .toList();
        return userRepository.findAllById(userIds).stream()
                .collect(Collectors.toMap(User::getId, Function.identity()));
    }

    private Comparator<BookReview> reviewComparator(String sort) {
        if ("RATING_DESC".equalsIgnoreCase(sort)) {
            return Comparator.comparing(BookReview::getRating).reversed()
                    .thenComparing(BookReview::getId, Comparator.reverseOrder());
        }
        if ("RATING_ASC".equalsIgnoreCase(sort)) {
            return Comparator.comparing(BookReview::getRating)
                    .thenComparing(BookReview::getId, Comparator.reverseOrder());
        }
        if ("RECENT".equalsIgnoreCase(sort)) {
            return Comparator.comparing(BookReview::getId, Comparator.reverseOrder());
        }
        return Comparator.comparing(BookReview::getLikeCount).reversed()
                .thenComparing(BookReview::getId, Comparator.reverseOrder());
    }

    private int validateLimit(int limit) {
        if (limit <= 0) {
            return DEFAULT_LIMIT;
        }
        return Math.min(limit, MAX_LIMIT);
    }
}
