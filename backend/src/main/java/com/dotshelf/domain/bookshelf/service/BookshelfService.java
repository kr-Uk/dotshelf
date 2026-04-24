package com.dotshelf.domain.bookshelf.service;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfItemResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfListResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.CreateBookshelfRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.InteriorObjectRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.ReorderBookshelvesRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.SectionAlignmentRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.SectionResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.UpdateBookshelfRequest;
import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.entity.BookshelfSection;
import com.dotshelf.domain.bookshelf.repository.BookshelfItemRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfSectionRepository;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class BookshelfService {

    private static final int SECTION_ROWS = 4;
    private static final int SECTION_COLUMNS = 4;

    private final BookshelfRepository bookshelfRepository;
    private final BookshelfSectionRepository sectionRepository;
    private final BookshelfItemRepository bookshelfItemRepository;
    private final SpaceService spaceService;
    private final UserService userService;

    public BookshelfListResponse getBookshelves(Long spaceId) {
        return new BookshelfListResponse(bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(spaceId, "ACTIVE")
                .stream()
                .map(BookshelfResponse::from)
                .toList());
    }

    @Transactional
    public BookshelfResponse create(Long userId, CreateBookshelfRequest request) {
        userService.requireUserId(userId);
        spaceService.getSpaceEntity(request.spaceId());
        int displayOrder = (int) bookshelfRepository.countBySpaceIdAndStatus(request.spaceId(), "ACTIVE");
        Bookshelf bookshelf = bookshelfRepository.save(Bookshelf.builder()
                .spaceId(request.spaceId())
                .bodyInventoryId(request.bodyInventoryId())
                .name(request.name() == null || request.name().isBlank() ? "새 책장" : request.name())
                .description(request.description())
                .visibilityScope(request.visibilityScope())
                .displayOrder(displayOrder)
                .build());
        createDefaultSections(bookshelf.getId());
        return BookshelfResponse.from(bookshelf);
    }

    @Transactional
    public BookshelfResponse update(Long userId, Long bookshelfId, UpdateBookshelfRequest request) {
        userService.requireUserId(userId);
        Bookshelf bookshelf = getBookshelf(bookshelfId);
        bookshelf.update(request.name(), request.description(), request.visibilityScope(), request.skinInventoryId());
        return BookshelfResponse.from(bookshelf);
    }

    @Transactional
    public void delete(Long userId, Long bookshelfId) {
        userService.requireUserId(userId);
        getBookshelf(bookshelfId).delete();
    }

    @Transactional
    public void reorder(Long userId, ReorderBookshelvesRequest request) {
        userService.requireUserId(userId);
        for (int i = 0; i < request.bookshelfIds().size(); i++) {
            getBookshelf(request.bookshelfIds().get(i)).changeOrder(i);
        }
    }

    @Transactional
    public SectionResponse updateSectionAlignment(Long userId, Long sectionId, SectionAlignmentRequest request) {
        userService.requireUserId(userId);
        BookshelfSection section = sectionRepository.findById(sectionId)
                .orElseThrow(() -> new CustomException(ErrorCode.RESOURCE_NOT_FOUND));
        section.changeAlignment(request.alignment());
        return SectionResponse.from(section);
    }

    @Transactional
    public BookshelfItemResponse addInteriorObject(Long userId, Long bookshelfId, InteriorObjectRequest request) {
        userService.requireUserId(userId);
        getBookshelf(bookshelfId);
        BookshelfItem item = bookshelfItemRepository.save(BookshelfItem.builder()
                .bookshelfId(bookshelfId)
                .bookshelfSectionId(request.sectionId())
                .assetInventoryId(request.assetInventoryId())
                .itemType("DECOR")
                .slotNo(request.slotNo())
                .displayStyle(request.displayStyle())
                .build());
        return BookshelfItemResponse.from(item);
    }

    @Transactional
    public BookshelfItemResponse moveItem(Long userId, Long itemId, Long bookshelfId, Long sectionId,
                                          Integer slotNo, String displayStyle) {
        userService.requireUserId(userId);
        BookshelfItem item = bookshelfItemRepository.findById(itemId)
                .orElseThrow(() -> new CustomException(ErrorCode.BOOKSHELF_ITEM_NOT_FOUND));
        item.move(bookshelfId, sectionId, slotNo, displayStyle);
        return BookshelfItemResponse.from(item);
    }

    public Bookshelf getBookshelf(Long bookshelfId) {
        return bookshelfRepository.findById(bookshelfId)
                .orElseThrow(() -> new CustomException(ErrorCode.BOOKSHELF_NOT_FOUND));
    }

    private void createDefaultSections(Long bookshelfId) {
        for (int row = 0; row < SECTION_ROWS; row++) {
            for (int column = 0; column < SECTION_COLUMNS; column++) {
                sectionRepository.save(BookshelfSection.builder()
                        .bookshelfId(bookshelfId)
                        .rowNo(row)
                        .columnNo(column)
                        .maxItemCount(20)
                        .build());
            }
        }
    }
}
