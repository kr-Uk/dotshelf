package com.dotshelf.domain.bookshelf.service;

import java.util.ArrayList;
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
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.ReadingRecordSummaryResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.ReorderBookshelvesRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.SectionAlignmentRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.SectionResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.UpdateBookshelfRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.UserBookSummaryResponse;
import com.dotshelf.domain.bookshelf.entity.Bookshelf;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.entity.BookshelfSection;
import com.dotshelf.domain.bookshelf.repository.BookshelfItemRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfRepository;
import com.dotshelf.domain.bookshelf.repository.BookshelfSectionRepository;
import com.dotshelf.domain.readingrecord.entity.ReadingRecord;
import com.dotshelf.domain.readingrecord.repository.ReadingRecordRepository;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.user.repository.FollowRepository;
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
    private final ReadingRecordRepository readingRecordRepository;
    private final SpaceService spaceService;
    private final FollowRepository followRepository;
    private final UserService userService;

    public BookshelfListResponse getBookshelves(Long spaceId) {
        return new BookshelfListResponse(bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(spaceId, "ACTIVE")
                .stream()
                .map(BookshelfResponse::from)
                .toList());
    }

    public BookshelfRenderResponse getItems(Long userId, Long bookshelfId) {
        Bookshelf bookshelf = getBookshelf(bookshelfId);
        requireReadable(userId, bookshelf);
        List<BookshelfSection> sections = sectionRepository.findByBookshelfIdOrderByRowNoAscColumnNoAsc(bookshelfId);
        List<BookshelfItem> items = bookshelfItemRepository.findByBookshelfIdAndStatusOrderByBookshelfSectionIdAscSlotNoAsc(bookshelfId, "ACTIVE");
        Map<Long, BookshelfSection> sectionsById = sections.stream()
                .collect(Collectors.toMap(BookshelfSection::getId, Function.identity()));
        Map<Long, UserBook> userBooks = userBooksById(items);
        Map<Long, Book> books = booksById(userBooks);
        Map<Long, ReadingRecord> readingRecords = readingRecordsByUserBookId(userBooks);
        Map<Long, UserAssetInventory> inventories = inventoriesById(items);
        Map<Long, Asset> assets = assetsById(inventories);
        return new BookshelfRenderResponse(
                BookshelfResponse.from(bookshelf),
                sections.stream()
                        .map(SectionResponse::from)
                        .toList(),
                items.stream()
                        .map(item -> toRenderItem(userId, item, sectionsById, userBooks, books, readingRecords, inventories, assets))
                        .toList()
        );
    }

    @Transactional
    public BookshelfResponse create(Long userId, CreateBookshelfRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        Space space = spaceService.getSpaceEntity(request.spaceId());
        if (!space.getOwnerUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        int displayOrder = bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(request.spaceId(), "ACTIVE").stream()
                .mapToInt(Bookshelf::getDisplayOrder)
                .max()
                .orElse(-1) + 1;
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
        Long currentUserId = userService.requireUserId(userId);
        Bookshelf bookshelf = getOwnedBookshelf(currentUserId, bookshelfId);
        bookshelf.update(request.name(), request.description(), request.visibilityScope(), request.skinInventoryId());
        return BookshelfResponse.from(bookshelf);
    }

    @Transactional
    public void delete(Long userId, Long bookshelfId) {
        Long currentUserId = userService.requireUserId(userId);
        getOwnedBookshelf(currentUserId, bookshelfId).delete();
    }

    @Transactional
    public void reorder(Long userId, ReorderBookshelvesRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        if (request.bookshelfIds() == null || request.bookshelfIds().isEmpty()) {
            return;
        }

        List<Bookshelf> shelves = new ArrayList<>();
        Long spaceId = null;
        for (Long bookshelfId : request.bookshelfIds()) {
            Bookshelf bookshelf = getOwnedBookshelf(currentUserId, bookshelfId);
            if (spaceId == null) {
                spaceId = bookshelf.getSpaceId();
            } else if (!spaceId.equals(bookshelf.getSpaceId())) {
                throw new CustomException(ErrorCode.INVALID_INPUT);
            }
            shelves.add(bookshelf);
        }

        relocateDeletedBookshelves(spaceId);

        for (int i = 0; i < shelves.size(); i++) {
            shelves.get(i).changeOrder(-(i + 1));
        }
        bookshelfRepository.flush();

        for (int i = 0; i < shelves.size(); i++) {
            shelves.get(i).changeOrder(i);
        }
    }

    @Transactional
    public SectionResponse updateSectionAlignment(Long userId, Long bookshelfId, Long sectionId, SectionAlignmentRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        getOwnedBookshelf(currentUserId, bookshelfId);
        requireSectionInBookshelf(bookshelfId, sectionId);
        BookshelfSection section = sectionRepository.findById(sectionId)
                .orElseThrow(() -> new CustomException(ErrorCode.RESOURCE_NOT_FOUND));
        section.changeAlignment(request.alignment());
        return SectionResponse.from(section);
    }

    @Transactional
    public BookshelfItemResponse addInteriorObject(Long userId, Long bookshelfId, InteriorObjectRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        getOwnedBookshelf(currentUserId, bookshelfId);
        requireSectionInBookshelf(bookshelfId, request.sectionId());
        UserAssetInventory inventory = getOwnedInventory(currentUserId, request.assetInventoryId());
        BookshelfItem item = bookshelfItemRepository.save(BookshelfItem.builder()
                .bookshelfId(bookshelfId)
                .bookshelfSectionId(request.sectionId())
                .assetInventoryId(inventory.getId())
                .itemType("DECOR")
                .slotNo(request.slotNo())
                .facingDirection(request.facingDirection())
                .displayStyle(request.displayStyle())
                .build());
        return BookshelfItemResponse.from(item);
    }

    @Transactional
    public BookshelfItemResponse moveItem(Long userId, Long itemId, Long bookshelfId, Long sectionId,
                                          Integer slotNo, String facingDirection, String displayStyle) {
        Long currentUserId = userService.requireUserId(userId);
        BookshelfItem item = bookshelfItemRepository.findById(itemId)
                .orElseThrow(() -> new CustomException(ErrorCode.BOOKSHELF_ITEM_NOT_FOUND));
        getOwnedBookshelf(currentUserId, item.getBookshelfId());
        getOwnedBookshelf(currentUserId, bookshelfId);
        requireSectionInBookshelf(bookshelfId, sectionId);
        item.move(bookshelfId, sectionId, slotNo, facingDirection, displayStyle);
        return BookshelfItemResponse.from(item);
    }

    @Transactional
    public BookshelfItemResponse removeItem(Long userId, Long itemId) {
        Long currentUserId = userService.requireUserId(userId);
        BookshelfItem item = bookshelfItemRepository.findById(itemId)
                .orElseThrow(() -> new CustomException(ErrorCode.BOOKSHELF_ITEM_NOT_FOUND));
        getOwnedBookshelf(currentUserId, item.getBookshelfId());
        item.remove();
        if ("BOOK".equalsIgnoreCase(item.getItemType()) && item.getUserBookId() != null) {
            userBookRepository.findById(item.getUserBookId())
                    .orElseThrow(() -> new CustomException(ErrorCode.USER_BOOK_NOT_FOUND))
                    .archive();
        }
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

    private void requireReadable(Long userId, Bookshelf bookshelf) {
        Space space = spaceService.getSpaceEntity(bookshelf.getSpaceId());
        if (space.getOwnerUserId().equals(userId)) {
            return;
        }
        if ("PUBLIC".equalsIgnoreCase(space.getVisibilityScope())
                && "PUBLIC".equalsIgnoreCase(bookshelf.getVisibilityScope())) {
            return;
        }
        throw new CustomException(ErrorCode.FORBIDDEN);
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

    private void relocateDeletedBookshelves(Long spaceId) {
        bookshelfRepository.findBySpaceIdOrderByDisplayOrderAsc(spaceId).stream()
                .filter(bookshelf -> !"ACTIVE".equalsIgnoreCase(bookshelf.getStatus()))
                .forEach(Bookshelf::moveToDeletedOrder);
        bookshelfRepository.flush();
    }

    private UserAssetInventory getOwnedInventory(Long userId, Long assetInventoryId) {
        UserAssetInventory inventory = inventoryRepository.findById(assetInventoryId)
                .orElseThrow(() -> new CustomException(ErrorCode.RESOURCE_NOT_FOUND));
        if (!inventory.getUserId().equals(userId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        return inventory;
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

    private Map<Long, ReadingRecord> readingRecordsByUserBookId(Map<Long, UserBook> userBooks) {
        List<Long> userBookIds = userBooks.values().stream()
                .map(UserBook::getId)
                .distinct()
                .toList();
        if (userBookIds.isEmpty()) {
            return Map.of();
        }
        return readingRecordRepository.findByUserBookIdInAndStatus(userBookIds, "ACTIVE").stream()
                .collect(Collectors.toMap(
                        ReadingRecord::getUserBookId,
                        Function.identity(),
                        (left, right) -> left.getUpdatedAt().isAfter(right.getUpdatedAt()) ? left : right
                ));
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
            Long viewerUserId,
            BookshelfItem item,
            Map<Long, BookshelfSection> sectionsById,
            Map<Long, UserBook> userBooks,
            Map<Long, Book> books,
            Map<Long, ReadingRecord> readingRecords,
            Map<Long, UserAssetInventory> inventories,
            Map<Long, Asset> assets
    ) {
        UserBook userBook = userBooks.get(item.getUserBookId());
        Book book = userBook == null ? null : books.get(userBook.getBookId());
        BookshelfSection section = sectionsById.get(item.getBookshelfSectionId());
        ReadingRecord record = userBook == null ? null : visibleReadingRecord(viewerUserId, userBook, readingRecords.get(userBook.getId()));
        UserAssetInventory inventory = inventories.get(item.getAssetInventoryId());
        Asset asset = inventory == null ? null : assets.get(inventory.getAssetId());
        return new BookshelfRenderItemResponse(
                item.getId(),
                item.getItemType(),
                item.getBookshelfSectionId(),
                section == null ? null : section.getRowNo(),
                section == null ? null : section.getColumnNo(),
                item.getSlotNo(),
                item.getFacingDirection(),
                item.getDisplayStyle(),
                item.getRotationDeg(),
                item.getUserBookId(),
                UserBookSummaryResponse.from(userBook),
                BookSummaryResponse.from(book),
                item.getAssetInventoryId(),
                asset == null ? null : new AssetSummaryResponse(
                        asset.getId(),
                        asset.getCode(),
                        asset.getName(),
                        asset.getPreviewImageUrl()
                ),
                ReadingRecordSummaryResponse.from(record)
        );
    }

    private ReadingRecord visibleReadingRecord(Long viewerUserId, UserBook userBook, ReadingRecord record) {
        if (record == null || userBook == null) {
            return null;
        }
        if (userBook.getUserId().equals(viewerUserId)) {
            return record;
        }
        if ("PUBLIC".equalsIgnoreCase(record.getVisibilityScope())) {
            return record;
        }
        if ("FOLLOWERS".equalsIgnoreCase(record.getVisibilityScope())
                && viewerUserId != null
                && followRepository.existsByFollowerUserIdAndFollowingUserId(viewerUserId, userBook.getUserId())) {
            return record;
        }
        return null;
    }
}
