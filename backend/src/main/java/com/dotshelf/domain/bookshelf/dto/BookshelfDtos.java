package com.dotshelf.domain.bookshelf.dto;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;

import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.entity.BookshelfSection;
import com.dotshelf.domain.readingrecord.entity.ReadingRecord;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

public final class BookshelfDtos {

    private BookshelfDtos() {
    }

    public record BookshelfResponse(
            Long id,
            Long spaceId,
            String name,
            String description,
            String visibilityScope,
            int displayOrder,
            Long bodyInventoryId,
            Long skinInventoryId
    ) {

        public static BookshelfResponse from(Bookshelf bookshelf) {
            return new BookshelfResponse(
                    bookshelf.getId(),
                    bookshelf.getSpaceId(),
                    bookshelf.getName(),
                    bookshelf.getDescription(),
                    bookshelf.getVisibilityScope(),
                    bookshelf.getDisplayOrder(),
                    bookshelf.getBodyInventoryId(),
                    bookshelf.getSkinInventoryId()
            );
        }
    }

    public record BookshelfListResponse(List<BookshelfResponse> bookshelves) {
    }

    public record BookshelfRenderResponse(
            BookshelfResponse bookshelf,
            List<SectionResponse> sections,
            List<BookshelfRenderItemResponse> items
    ) {
    }

    public record BookshelfRenderItemResponse(
            Long bookshelfItemId,
            String itemType,
            Long sectionId,
            Integer sectionRow,
            Integer sectionColumn,
            Integer slotNo,
            String facingDirection,
            String displayStyle,
            BigDecimal rotationDeg,
            Long userBookId,
            UserBookSummaryResponse userBook,
            BookSummaryResponse book,
            Long assetInventoryId,
            AssetSummaryResponse asset,
            ReadingRecordSummaryResponse readingRecord
    ) {
    }

    public record BookSummaryResponse(
            Long bookId,
            String title,
            String author,
            String publisher,
            String description,
            String isbn13,
            String isbn10,
            String coverImageUrl
    ) {

        public static BookSummaryResponse from(Book book) {
            if (book == null) {
                return null;
            }
            return new BookSummaryResponse(
                    book.getId(),
                    book.getTitle(),
                    book.getAuthor(),
                    book.getPublisher(),
                    book.getDescription(),
                    book.getIsbn13(),
                    book.getIsbn10(),
                    book.getCoverImageUrl()
            );
        }
    }

    public record UserBookSummaryResponse(
            Long id,
            Long userId,
            Long bookId,
            String readingStatus,
            String storageStatus,
            BigDecimal ownerRating,
            boolean liked,
            Long selectedCoverProductId
    ) {

        public static UserBookSummaryResponse from(UserBook userBook) {
            if (userBook == null) {
                return null;
            }
            return new UserBookSummaryResponse(
                    userBook.getId(),
                    userBook.getUserId(),
                    userBook.getBookId(),
                    userBook.getReadingStatus(),
                    userBook.getStorageStatus(),
                    userBook.getOwnerRating(),
                    userBook.isLiked(),
                    userBook.getSelectedCoverProductId()
            );
        }
    }

    public record ReadingRecordSummaryResponse(
            Long id,
            Long userBookId,
            String title,
            String content,
            String visibilityScope,
            int likeCount,
            int commentCount,
            Instant createdAt,
            Instant updatedAt
    ) {

        public static ReadingRecordSummaryResponse from(ReadingRecord record) {
            if (record == null) {
                return null;
            }
            return new ReadingRecordSummaryResponse(
                    record.getId(),
                    record.getUserBookId(),
                    record.getTitle(),
                    record.getContent(),
                    record.getVisibilityScope(),
                    record.getLikeCount(),
                    record.getCommentCount(),
                    record.getCreatedAt(),
                    record.getUpdatedAt()
            );
        }
    }

    public record AssetSummaryResponse(
            Long assetId,
            String code,
            String name,
            String previewImageUrl
    ) {
    }

    public record CreateBookshelfRequest(
            @NotNull(message = "spaceId는 필수입니다.")
            Long spaceId,
            Long bodyInventoryId,
            String name,
            String description,
            String visibilityScope
    ) {
    }

    public record UpdateBookshelfRequest(
            String name,
            String description,
            String visibilityScope,
            Long skinInventoryId,
            Boolean clearSkinInventory
    ) {
    }

    public record ReorderBookshelvesRequest(List<Long> bookshelfIds) {
    }

    public record SectionAlignmentRequest(
            @NotBlank(message = "alignment는 필수입니다.")
            String alignment
    ) {
    }

    public record SectionResponse(Long id, int rowNo, int columnNo, int maxItemCount, String alignment) {

        public static SectionResponse from(BookshelfSection section) {
            return new SectionResponse(
                    section.getId(),
                    section.getRowNo(),
                    section.getColumnNo(),
                    section.getMaxItemCount(),
                    section.getAlignment()
            );
        }
    }

    public record BookshelfItemRequest(
            @NotNull(message = "bookshelfId는 필수입니다.")
            Long bookshelfId,
            @NotNull(message = "sectionId는 필수입니다.")
            Long sectionId,
            @NotNull(message = "slotNo는 필수입니다.")
            Integer slotNo,
            String facingDirection,
            String displayStyle
    ) {
    }

    public record InteriorObjectRequest(
            @NotNull(message = "assetInventoryId는 필수입니다.")
            Long assetInventoryId,
            @NotNull(message = "sectionId는 필수입니다.")
            Long sectionId,
            @NotNull(message = "slotNo는 필수입니다.")
            Integer slotNo,
            String facingDirection,
            String displayStyle
    ) {
    }

    public record BookshelfItemResponse(
            Long id,
            Long bookshelfId,
            Long sectionId,
            String itemType,
            Long userBookId,
            Long assetInventoryId,
            Integer slotNo,
            String facingDirection,
            String displayStyle,
            BigDecimal rotationDeg
    ) {

        public static BookshelfItemResponse from(BookshelfItem item) {
            return new BookshelfItemResponse(
                    item.getId(),
                    item.getBookshelfId(),
                    item.getBookshelfSectionId(),
                    item.getItemType(),
                    item.getUserBookId(),
                    item.getAssetInventoryId(),
                    item.getSlotNo(),
                    item.getFacingDirection(),
                    item.getDisplayStyle(),
                    item.getRotationDeg()
            );
        }
    }
}
