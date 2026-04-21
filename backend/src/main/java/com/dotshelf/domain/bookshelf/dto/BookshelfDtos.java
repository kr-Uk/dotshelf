package com.dotshelf.domain.bookshelf.dto;

import java.util.List;

import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.entity.BookshelfSection;

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
            @NotBlank(message = "책장 이름은 필수입니다.")
            String name,
            String description,
            String visibilityScope,
            Long skinInventoryId
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
            String displayStyle
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
                    item.getDisplayStyle()
            );
        }
    }
}
