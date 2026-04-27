package com.dotshelf.domain.bookshelf.service;

import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.asset.entity.Asset;
import com.dotshelf.domain.asset.entity.UserAssetInventory;
import com.dotshelf.domain.asset.repository.AssetRepository;
import com.dotshelf.domain.asset.repository.UserAssetInventoryRepository;
import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.AssetSummaryResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookSummaryResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfRenderItemResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfRenderResponse;
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
import com.dotshelf.domain.space.entity.Space;
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
    private final UserBookRepository userBookRepository;
    private final BookRepository bookRepository;
    private final UserAssetInventoryRepository inventoryRepository;
    private final AssetRepository assetRepository;
    private final SpaceService spaceService;
    private final UserService userService;

    public BookshelfListResponse getBookshelves(Long spaceId) {
        return new BookshelfListResponse(bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(spaceId, "ACTIVE")
                .stream()
                .map(BookshelfResponse::from)
                .toList());
    }

    public BookshelfRenderResponse getItems(Long bookshelfId) {
        Bookshelf bookshelf = getBookshelf(bookshelfId);
        List<BookshelfSection> sections = sectionRepository.findByBookshelfIdOrderByRowNoAscColumnNoAsc(bookshelfId);
        List<BookshelfItem> items = bookshelfItemRepository.findByBookshelfIdAndStatusOrderByBookshelfSectionIdAscSlotNoAsc(bookshelfId, "ACTIVE");
        Map<Long, UserBook> userBooks = userBooksById(items);
        Map<Long, Book> books = booksById(userBooks);
        Map<Long, UserAssetInventory> inventories = inventoriesById(items);
        Map<Long, Asset> assets = assetsById(inventories);
        return new BookshelfRenderResponse(
                BookshelfResponse.from(bookshelf),
                sections.stream()
                        .map(SectionResponse::from)
                        .toList(),
                items.stream()
                        .map(item -> toRenderItem(item, userBooks, books, inventories, assets))
                        .toList()
        );
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

    public Bookshelf getOwnedBookshelf(Long userId, Long bookshelfId) {
        Bookshelf bookshelf = getBookshelf(bookshelfId);
        Space space = spaceService.getSpaceEntity(bookshelf.getSpaceId());
        if (!space.getOwnerUserId().equals(userId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        return bookshelf;
    }

    public void requireSectionInBookshelf(Long bookshelfId, Long sectionId) {
        if (sectionId == null) {
            throw new CustomException(ErrorCode.INVALID_INPUT);
        }
        BookshelfSection section = sectionRepository.findById(sectionId)
                .orElseThrow(() -> new CustomException(ErrorCode.RESOURCE_NOT_FOUND));
        if (!section.getBookshelfId().equals(bookshelfId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
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

    private Map<Long, UserBook> userBooksById(List<BookshelfItem> items) {
        List<Long> userBookIds = items.stream()
                .map(BookshelfItem::getUserBookId)
                .filter(id -> id != null)
                .distinct()
                .toList();
        if (userBookIds.isEmpty()) {
            return Map.of();
        }
        return userBookRepository.findAllById(userBookIds).stream()
                .collect(Collectors.toMap(UserBook::getId, Function.identity()));
    }

    private Map<Long, Book> booksById(Map<Long, UserBook> userBooks) {
        List<Long> bookIds = userBooks.values().stream()
                .map(UserBook::getBookId)
                .distinct()
                .toList();
        if (bookIds.isEmpty()) {
            return Map.of();
        }
        return bookRepository.findAllById(bookIds).stream()
                .collect(Collectors.toMap(Book::getId, Function.identity()));
    }

    private Map<Long, UserAssetInventory> inventoriesById(List<BookshelfItem> items) {
        List<Long> inventoryIds = items.stream()
                .map(BookshelfItem::getAssetInventoryId)
                .filter(id -> id != null)
                .distinct()
                .toList();
        if (inventoryIds.isEmpty()) {
            return Map.of();
        }
        return inventoryRepository.findAllById(inventoryIds).stream()
                .collect(Collectors.toMap(UserAssetInventory::getId, Function.identity()));
    }

    private Map<Long, Asset> assetsById(Map<Long, UserAssetInventory> inventories) {
        List<Long> assetIds = inventories.values().stream()
                .map(UserAssetInventory::getAssetId)
                .distinct()
                .toList();
        if (assetIds.isEmpty()) {
            return Map.of();
        }
        return assetRepository.findAllById(assetIds).stream()
                .collect(Collectors.toMap(Asset::getId, Function.identity()));
    }

    private BookshelfRenderItemResponse toRenderItem(
            BookshelfItem item,
            Map<Long, UserBook> userBooks,
            Map<Long, Book> books,
            Map<Long, UserAssetInventory> inventories,
            Map<Long, Asset> assets
    ) {
        UserBook userBook = userBooks.get(item.getUserBookId());
        Book book = userBook == null ? null : books.get(userBook.getBookId());
        UserAssetInventory inventory = inventories.get(item.getAssetInventoryId());
        Asset asset = inventory == null ? null : assets.get(inventory.getAssetId());
        return new BookshelfRenderItemResponse(
                item.getId(),
                item.getItemType(),
                item.getBookshelfSectionId(),
                item.getSlotNo(),
                item.getDisplayStyle(),
                item.getUserBookId(),
                book == null ? null : new BookSummaryResponse(
                        book.getId(),
                        book.getTitle(),
                        book.getAuthor(),
                        book.getCoverImageUrl()
                ),
                item.getAssetInventoryId(),
                asset == null ? null : new AssetSummaryResponse(
                        asset.getId(),
                        asset.getCode(),
                        asset.getName(),
                        asset.getPreviewImageUrl()
                )
        );
    }
}
