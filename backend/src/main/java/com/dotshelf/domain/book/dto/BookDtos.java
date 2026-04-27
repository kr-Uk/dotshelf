package com.dotshelf.domain.book.dto;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.List;

import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.UserBook;

import jakarta.validation.constraints.AssertTrue;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

public final class BookDtos {

    private BookDtos() {
    }

    public record BookResponse(
            Long id,
            String isbn13,
            String title,
            String author,
            String publisher,
            LocalDate publishedDate,
            String description,
            String coverImageUrl
    ) {

        public static BookResponse from(Book book) {
            return new BookResponse(
                    book.getId(),
                    book.getIsbn13(),
                    book.getTitle(),
                    book.getAuthor(),
                    book.getPublisher(),
                    book.getPublishedDate(),
                    book.getDescription(),
                    book.getCoverImageUrl()
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
            String sourceUrl
    ) {

        public static BookSearchResponse from(Book book) {
            return new BookSearchResponse(
                    book.getId(),
                    "LOCAL",
                    book.getIsbn13(),
                    book.getIsbn10(),
                    book.getTitle(),
                    book.getAuthor(),
                    book.getPublisher(),
                    book.getPublishedDate(),
                    book.getDescription(),
                    book.getCoverImageUrl(),
                    null
            );
        }
    }

    public record BookSearchListResponse(
            List<BookSearchResponse> books,
            int page,
            int size,
            boolean isEnd
    ) {

        public static BookSearchListResponse empty(int page, int size) {
            return new BookSearchListResponse(List.of(), page, size, true);
        }
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
}
