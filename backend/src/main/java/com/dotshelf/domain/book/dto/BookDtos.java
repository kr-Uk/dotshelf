package com.dotshelf.domain.book.dto;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.List;

import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.UserBook;

import jakarta.validation.constraints.NotNull;

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

    public record BookListResponse(List<BookResponse> books) {
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
