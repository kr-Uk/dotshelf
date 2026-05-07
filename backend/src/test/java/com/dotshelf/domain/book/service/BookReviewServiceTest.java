package com.dotshelf.domain.book.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;

import java.math.BigDecimal;
import java.util.List;
import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.dotshelf.domain.book.dto.BookDtos.BookReviewRequest;
import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.BookReview;
import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.BookReviewRepository;
import com.dotshelf.domain.user.repository.UserRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class BookReviewServiceTest {

    @Mock
    private BookRepository bookRepository;

    @Mock
    private BookReviewRepository bookReviewRepository;

    @Mock
    private UserRepository userRepository;

    @Mock
    private UserService userService;

    @InjectMocks
    private BookReviewService bookReviewService;

    @Test
    @DisplayName("리뷰 목록은 정렬/limit/cursor를 적용하고 다음 cursor를 만든다")
    void getReviews_appliesSortLimitAndCursor() {
        Book book = TestFixtures.book(10L, "데미안");
        BookReview high = review(1L, 10L, 1L, "high", "5.0");
        BookReview low = review(2L, 10L, 2L, "low", "3.0");
        BookReview middle = review(3L, 10L, 3L, "middle", "4.0");
        given(bookRepository.findById(10L)).willReturn(Optional.of(book));
        given(bookReviewRepository.findByBookIdAndStatus(10L, "ACTIVE"))
                .willReturn(List.of(low, high, middle));
        given(userRepository.findAllById(List.of(1L, 3L)))
                .willReturn(List.of(TestFixtures.user(1L, "one"), TestFixtures.user(3L, "three")));

        var response = bookReviewService.getReviews(10L, "RATING_DESC", null, 2);

        assertThat(response.reviews())
                .extracting("reviewId")
                .containsExactly(1L, 3L);
        assertThat(response.nextCursor()).isEqualTo("review_3");
    }

    @Test
    @DisplayName("리뷰 cursor 형식이 잘못되면 INVALID_INPUT을 던진다")
    void getReviews_invalidCursor_throwsInvalidInput() {
        given(bookRepository.findById(10L)).willReturn(Optional.of(TestFixtures.book(10L, "데미안")));
        given(bookReviewRepository.findByBookIdAndStatus(10L, "ACTIVE")).willReturn(List.of());

        assertThatThrownBy(() -> bookReviewService.getReviews(10L, "RECENT", "bad_1", 20))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("존재하지 않는 책의 리뷰 목록 조회는 BOOK_NOT_FOUND를 던진다")
    void getReviews_missingBook_throwsBookNotFound() {
        given(bookRepository.findById(99L)).willReturn(Optional.empty());

        assertThatThrownBy(() -> bookReviewService.getReviews(99L, "RECENT", null, 20))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.BOOK_NOT_FOUND);
    }

    @Test
    @DisplayName("리뷰 생성은 중복을 막고 content trim과 책 평점 집계를 반영한다")
    void createReview_success_savesTrimmedContentAndUpdatesBookRating() {
        Book book = TestFixtures.book(10L, "데미안");
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookRepository.findById(10L)).willReturn(Optional.of(book));
        given(bookReviewRepository.findByBookIdAndUserIdAndStatus(10L, 1L, "ACTIVE")).willReturn(Optional.empty());
        given(bookReviewRepository.save(any(BookReview.class))).willAnswer(invocation -> {
            BookReview saved = invocation.getArgument(0);
            TestFixtures.setId(saved, 100L);
            return saved;
        });
        given(userRepository.findById(1L)).willReturn(Optional.of(TestFixtures.user(1L, "writer")));
        ArgumentCaptor<BookReview> reviewCaptor = ArgumentCaptor.forClass(BookReview.class);

        var response = bookReviewService.createReview(
                1L,
                10L,
                new BookReviewRequest(BigDecimal.valueOf(4.5), "  좋았다  ")
        );

        assertThat(response.reviewId()).isEqualTo(100L);
        assertThat(response.content()).isEqualTo("좋았다");
        assertThat(book.getRatingCount()).isEqualTo(1);
        assertThat(book.getRatingSum()).isEqualByComparingTo("4.5");
        then(bookReviewRepository).should().save(reviewCaptor.capture());
        assertThat(reviewCaptor.getValue().getContent()).isEqualTo("좋았다");
    }

    @Test
    @DisplayName("이미 작성한 활성 리뷰가 있으면 새 리뷰를 저장하지 않는다")
    void createReview_duplicateActiveReview_throwsInvalidInput() {
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookRepository.findById(10L)).willReturn(Optional.of(TestFixtures.book(10L, "데미안")));
        given(bookReviewRepository.findByBookIdAndUserIdAndStatus(10L, 1L, "ACTIVE"))
                .willReturn(Optional.of(review(1L, 10L, 1L, "old", "4.0")));

        assertThatThrownBy(() -> bookReviewService.createReview(
                1L,
                10L,
                new BookReviewRequest(BigDecimal.valueOf(5.0), "new")
        ))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
        then(bookReviewRepository).should().findByBookIdAndUserIdAndStatus(10L, 1L, "ACTIVE");
        then(bookReviewRepository).shouldHaveNoMoreInteractions();
    }

    private BookReview review(Long id, Long bookId, Long userId, String content, String rating) {
        BookReview review = BookReview.builder()
                .bookId(bookId)
                .userId(userId)
                .content(content)
                .rating(new BigDecimal(rating))
                .build();
        TestFixtures.setId(review, id);
        return review;
    }
}
