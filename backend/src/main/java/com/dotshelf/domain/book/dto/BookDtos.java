package com.dotshelf.domain.book.dto;

import java.math.BigDecimal;
import java.time.Instant;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.List;

import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.BookReview;
import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.user.entity.User;

import jakarta.validation.constraints.AssertTrue;
import jakarta.validation.constraints.DecimalMax;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

public final class BookDtos {

    private BookDtos() {
    }

    public record BookResponse(
            Long id,
            String source,
            String sourceUrl,
            String bookId,
            String isbn,
            String isbn13,
            String isbn10,
            String title,
            String author,
            String publisher,
            LocalDate publishedDate,
            String description,
            String genre,
            String coverImageUrl,
            Integer pricePages,
            BigDecimal avgRating,
            long reviewCount,
            long viewCount,
            long recordCount,
            long placementCount,
            long purchaseCount,
            long likeCount,
            boolean hasPaidCover,
            boolean owned,
            Long userBookId,
            Integer rank
    ) {

        public static BookResponse from(Book book) {
            return from(book, false, null, 0, null);
        }

        public static BookResponse from(Book book, boolean owned, Long userBookId, long reviewCount, Integer rank) {
            return new BookResponse(
                    book.getId(),
                    book.getSource() == null ? "LOCAL" : book.getSource().name(),
                    book.getSourceUrl(),
                    String.valueOf(book.getId()),
                    primaryIsbn(book),
                    book.getIsbn13(),
                    book.getIsbn10(),
                    book.getTitle(),
                    book.getAuthor(),
                    book.getPublisher(),
                    book.getPublishedDate(),
                    book.getDescription(),
                    null,
                    book.getCoverImageUrl(),
                    0,
                    averageRating(book),
                    reviewCount,
                    book.getViewCount(),
                    book.getBookshelfAddCount(),
                    book.getBookshelfAddCount(),
                    0L,
                    book.getLikeCount(),
                    false,
                    owned,
                    userBookId,
                    rank
            );
        }
    }

    public record BookSearchResponse(
            Long id,
            String source,
            String isbn13,
            String isbn10,
            String title,
            String author,
            String publisher,
            LocalDate publishedDate,
            String description,
            String coverImageUrl,
            String sourceUrl,
            String bookId,
            String isbn,
            BigDecimal avgRating,
            long placementCount,
            long purchaseCount,
            long likeCount,
            long recordCount,
            boolean hasPaidCover
    ) {

        public BookSearchResponse(
                Long id,
                String source,
                String isbn13,
                String isbn10,
                String title,
                String author,
                String publisher,
                LocalDate publishedDate,
                String description,
                String coverImageUrl,
                String sourceUrl
        ) {
            this(
                    id,
                    source,
                    isbn13,
                    isbn10,
                    title,
                    author,
                    publisher,
                    publishedDate,
                    description,
                    coverImageUrl,
                    sourceUrl,
                    id == null ? null : String.valueOf(id),
                    isbn13 == null ? isbn10 : isbn13,
                    BigDecimal.ZERO,
                    0,
                    0,
                    0,
                    0,
                    false
            );
        }

        public static BookSearchResponse from(Book book) {
            return new BookSearchResponse(
                    book.getId(),
                    book.getSource() == null ? "LOCAL" : book.getSource().name(),
                    book.getIsbn13(),
                    book.getIsbn10(),
                    book.getTitle(),
                    book.getAuthor(),
                    book.getPublisher(),
                    book.getPublishedDate(),
                    book.getDescription(),
                    book.getCoverImageUrl(),
                    book.getSourceUrl(),
                    String.valueOf(book.getId()),
                    primaryIsbn(book),
                    averageRating(book),
                    book.getBookshelfAddCount(),
                    0,
                    book.getLikeCount(),
                    book.getBookshelfAddCount(),
                    false
            );
        }
    }

    public record BookSearchListResponse(
            List<BookSearchResponse> books,
            List<BookSearchResponse> items,
            int page,
            int size,
            boolean isEnd,
            String nextCursor
    ) {

        public BookSearchListResponse(List<BookSearchResponse> books, int page, int size, boolean isEnd) {
            this(books, books, page, size, isEnd, null);
        }

        public static BookSearchListResponse empty(int page, int size) {
            return new BookSearchListResponse(List.of(), page, size, true);
        }
    }

    public record BookListResponse(List<BookResponse> items, String nextCursor) {
    }

    public record BookReviewRequest(
            @NotNull(message = "rating은 필수입니다.")
            @DecimalMin(value = "0.5", message = "rating은 0.5 이상이어야 합니다.")
            @DecimalMax(value = "5.0", message = "rating은 5.0 이하여야 합니다.")
            BigDecimal rating,
            @NotBlank(message = "content는 필수입니다.")
            @Size(max = 1000, message = "content는 최대 1000자입니다.")
            String content
    ) {
    }

    public record BookReviewResponse(
            Long reviewId,
            Long bookId,
            Long userId,
            String nickname,
            String profileImageUrl,
            BigDecimal rating,
            String content,
            int likeCount,
            boolean liked,
            Instant createdAt,
            Instant updatedAt
    ) {

        public static BookReviewResponse from(BookReview review, User user) {
            return new BookReviewResponse(
                    review.getId(),
                    review.getBookId(),
                    review.getUserId(),
                    user == null ? null : user.getNickname(),
                    user == null ? null : user.getProfileImageUrl(),
                    review.getRating(),
                    review.getContent(),
                    review.getLikeCount(),
                    false,
                    review.getCreatedAt(),
                    review.getUpdatedAt()
            );
        }
    }

    public record BookReviewListResponse(List<BookReviewResponse> reviews, String nextCursor) {
    }

    public record AddBookToShelfRequest(
            @NotNull(message = "bookId는 필수입니다.")
            Long bookId,
            @NotNull(message = "sectionId는 필수입니다.")
            Long sectionId,
            @NotNull(message = "slotNo는 필수입니다.")
            Integer slotNo,
            String displayStyle
    ) {
    }

    public record AddKakaoBookToShelfRequest(
            @Size(max = 13, message = "isbn13은 최대 13자입니다.")
            String isbn13,
            @Size(max = 10, message = "isbn10은 최대 10자입니다.")
            String isbn10,
            @NotBlank(message = "title은 필수입니다.")
            String title,
            String author,
            String publisher,
            LocalDate publishedDate,
            String description,
            String coverImageUrl,
            @Size(max = 500, message = "sourceUrl은 최대 500자입니다.")
            String sourceUrl,
            @NotNull(message = "sectionId는 필수입니다.")
            Long sectionId,
            @NotNull(message = "slotNo는 필수입니다.")
            Integer slotNo,
            String displayStyle
    ) {

        @AssertTrue(message = "isbn10 또는 isbn13은 필수입니다.")
        public boolean isIsbnPresent() {
            return hasText(isbn13) || hasText(isbn10);
        }

        private boolean hasText(String value) {
            return value != null && !value.isBlank();
        }
    }

    public record UpdateUserBookRequest(
            String readingStatus,
            BigDecimal ownerRating,
            Long selectedCoverProductId
    ) {
    }

    public record MoveUserBookRequest(
            @NotNull(message = "bookshelfId는 필수입니다.")
            Long bookshelfId,
            @NotNull(message = "sectionId는 필수입니다.")
            Long sectionId,
            @NotNull(message = "slotNo는 필수입니다.")
            Integer slotNo,
            String displayStyle
    ) {
    }

    public record UserBookResponse(
            Long id,
            Long userId,
            Long bookId,
            String readingStatus,
            String storageStatus,
            BigDecimal ownerRating,
            boolean liked
    ) {

        public static UserBookResponse from(UserBook userBook) {
            return new UserBookResponse(
                    userBook.getId(),
                    userBook.getUserId(),
                    userBook.getBookId(),
                    userBook.getReadingStatus(),
                    userBook.getStorageStatus(),
                    userBook.getOwnerRating(),
                    userBook.isLiked()
            );
        }
    }

    public record UserBookListResponse(List<UserBookResponse> books) {
    }

    public record UserBookPlacementResponse(
            Long id,
            Long userId,
            Long bookId,
            String readingStatus,
            String storageStatus,
            BigDecimal ownerRating,
            boolean liked,
            Long bookshelfId,
            Long sectionId,
            Integer slotNo,
            String displayStyle
    ) {
    }

    public record UserBookPlacementListResponse(List<UserBookPlacementResponse> books, String nextCursor) {
    }

    /**
     * 대시보드 \"보유 책 풀 목록\" 카드용 경량 DTO. v1.0 갱신 — 정렬·페이지네이션을 위해 추가.
     * 카드에 노출되는 필드(cover, title, author, publisher, publishedDate, addedAt)만 포함.
     */
    public record OwnedBookSummary(
            Long userBookId,
            Long bookId,
            String title,
            String author,
            String publisher,
            LocalDate publishedDate,
            String coverImageUrl,
            LocalDateTime addedAt
    ) {
    }

    public record OwnedBookListResponse(List<OwnedBookSummary> items, String nextCursor) {
    }

    private static BigDecimal averageRating(Book book) {
        if (book.getRatingCount() == 0) {
            return BigDecimal.ZERO;
        }
        return book.getRatingSum()
                .divide(BigDecimal.valueOf(book.getRatingCount()), 1, java.math.RoundingMode.HALF_UP);
    }

    private static String primaryIsbn(Book book) {
        return book.getIsbn13() == null ? book.getIsbn10() : book.getIsbn13();
    }
}
