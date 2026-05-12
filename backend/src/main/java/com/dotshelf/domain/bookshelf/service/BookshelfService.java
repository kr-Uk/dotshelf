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
import com.dotshelf.domain.readingrecord.repository.ReadingRecordRepository;
import com.dotshelf.domain.space.entity.Space;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.user.repository.FollowRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;

@Slf4j
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class BookshelfService {

    private static final int SECTION_ROWS = 4;
    private static final int SECTION_COLUMNS = 4;
    private static final int RESERVED_AD_ROW = SECTION_ROWS - 1;
    private static final int RESERVED_AD_COLUMN = SECTION_COLUMNS - 1;
    private static final String ACTIVE = "ACTIVE";

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
        return new BookshelfListResponse(bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(spaceId, ACTIVE)
                .stream()
                .map(BookshelfResponse::from)
                .toList());
    }

    public BookshelfRenderResponse getItems(Long userId, Long bookshelfId) {
        // 진단 로그 — 500 발생 시 어느 단계에서 끊겼는지 GlobalExceptionHandler 의 stack trace 와 함께 식별 가능.
        // 단계별 size 출력으로 데이터 깨짐(orphan FK) / 빈 결과 vs 예외 구분.
        // log.debug — getItems 는 메인/책장 동기화에서 빈도 높은 read endpoint 라 info 면 운영 로그 폭주 + userId
        // 식별자 누적. 의심 케이스(orphan FK)만 warn 으로 emit. (MR 113 review skh1205 [P3])
        log.debug("getItems userId={} bookshelfId={}", userId, bookshelfId);
        Bookshelf bookshelf = getBookshelf(bookshelfId);
        requireReadable(userId, bookshelf);
        List<BookshelfSection> sections = sectionRepository.findByBookshelfIdOrderByRowNoAscColumnNoAsc(bookshelfId);
        List<BookshelfItem> items = bookshelfItemRepository.findByBookshelfIdAndStatusOrderByBookshelfSectionIdAscSlotNoAsc(bookshelfId, ACTIVE);
        log.debug("getItems bookshelfId={} sections={} items={}", bookshelfId, sections.size(), items.size());
        Map<Long, BookshelfSection> sectionsById = sections.stream()
                .collect(Collectors.toMap(BookshelfSection::getId, Function.identity()));
        Map<Long, UserBook> userBooks = userBooksById(items);
        Map<Long, BookSummaryResponse> books = bookSummariesById(userBooks);
        Map<Long, ReadingRecordSummaryResponse> readingRecords = readingRecordSummariesByUserBookId(userBooks);
        Map<Long, UserAssetInventory> inventories = inventoriesById(items);
        Map<Long, Asset> assets = assetsById(inventories);
        log.debug("getItems bookshelfId={} userBooks={} books={} readingRecords={} inventories={} assets={}",
                bookshelfId, userBooks.size(), books.size(), readingRecords.size(), inventories.size(), assets.size());
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
        int displayOrder = bookshelfRepository.findBySpaceIdAndStatusOrderByDisplayOrderAsc(request.spaceId(), ACTIVE).stream()
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
        userService.completeOnboardingIfNeeded(currentUserId);
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
        requireAvailablePlacement(bookshelfId, request.sectionId(), request.slotNo(), null);
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
        requireAvailablePlacement(bookshelfId, sectionId, slotNo, item.getId());
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
        Bookshelf bookshelf = bookshelfRepository.findById(bookshelfId)
                .orElseThrow(() -> new CustomException(ErrorCode.BOOKSHELF_NOT_FOUND));
        if (!ACTIVE.equalsIgnoreCase(bookshelf.getStatus())) {
            throw new CustomException(ErrorCode.BOOKSHELF_NOT_FOUND);
        }
        return bookshelf;
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
        resolveSectionInBookshelf(bookshelfId, sectionId);
    }

    public void requireAvailablePlacement(Long bookshelfId, Long sectionId, Integer slotNo, Long existingItemId) {
        BookshelfSection section = resolveSectionInBookshelf(bookshelfId, sectionId);
        if (isReservedAdvertisementSection(section)) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "Reserved advertisement section cannot accept items.");
        }
        if (slotNo == null || slotNo < 0) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "Invalid bookshelf slot number.");
        }
        if (slotNo >= section.getMaxItemCount()) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "Bookshelf slot number exceeds section capacity.");
        }

        long activeItemCount = existingItemId == null
                ? bookshelfItemRepository.countByBookshelfSectionIdAndStatus(sectionId, ACTIVE)
                : bookshelfItemRepository.countByBookshelfSectionIdAndStatusAndIdNot(sectionId, ACTIVE, existingItemId);
        if (activeItemCount >= section.getMaxItemCount()) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "Bookshelf section is full.");
        }

        boolean occupied = existingItemId == null
                ? bookshelfItemRepository.existsByBookshelfIdAndBookshelfSectionIdAndSlotNoAndStatus(bookshelfId, sectionId, slotNo, ACTIVE)
                : bookshelfItemRepository.existsByBookshelfIdAndBookshelfSectionIdAndSlotNoAndStatusAndIdNot(
                        bookshelfId,
                        sectionId,
                        slotNo,
                        ACTIVE,
                        existingItemId);
        if (occupied) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "Bookshelf slot is already occupied.");
        }
    }

    private BookshelfSection resolveSectionInBookshelf(Long bookshelfId, Long sectionId) {
        if (sectionId == null) {
            throw new CustomException(ErrorCode.INVALID_INPUT);
        }
        BookshelfSection section = sectionRepository.findById(sectionId)
                .orElseThrow(() -> new CustomException(ErrorCode.RESOURCE_NOT_FOUND));
        if (!section.getBookshelfId().equals(bookshelfId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        return section;
    }

    private boolean isReservedAdvertisementSection(BookshelfSection section) {
        return section.getRowNo() == RESERVED_AD_ROW && section.getColumnNo() == RESERVED_AD_COLUMN;
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
                .filter(bookshelf -> !ACTIVE.equalsIgnoreCase(bookshelf.getStatus()))
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

    private Map<Long, BookSummaryResponse> bookSummariesById(Map<Long, UserBook> userBooks) {
        List<Long> bookIds = userBooks.values().stream()
                .map(UserBook::getBookId)
                .filter(id -> id != null)
                .distinct()
                .toList();
        if (bookIds.isEmpty()) {
            return Map.of();
        }
        return bookRepository.findBookshelfRenderSummariesByIdIn(bookIds).stream()
                .collect(Collectors.toMap(BookSummaryResponse::bookId, Function.identity()));
    }

    private Map<Long, ReadingRecordSummaryResponse> readingRecordSummariesByUserBookId(Map<Long, UserBook> userBooks) {
        List<Long> userBookIds = userBooks.values().stream()
                .map(UserBook::getId)
                .filter(id -> id != null)
                .distinct()
                .toList();
        if (userBookIds.isEmpty()) {
            return Map.of();
        }
        return readingRecordRepository.findBookshelfRenderSummariesByUserBookIdInAndStatus(userBookIds, ACTIVE).stream()
                .collect(Collectors.toMap(
                        ReadingRecordSummaryResponse::userBookId,
                        Function.identity(),
                        // 같은 user_book 에 ACTIVE record 가 둘 이상이면 더 최근 것 채택. updatedAt 이 null 인 케이스
                        // (직접 INSERT 시드 데이터 등) 에서 isAfter 가 NPE 던지지 않도록 null-safe 비교.
                        (left, right) -> {
                            java.time.Instant l = left.updatedAt();
                            java.time.Instant r = right.updatedAt();
                            if (l == null && r == null) return left;
                            if (l == null) return right;
                            if (r == null) return left;
                            return l.isAfter(r) ? left : right;
                        }
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
                .filter(id -> id != null)
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
            Map<Long, BookSummaryResponse> books,
            Map<Long, ReadingRecordSummaryResponse> readingRecords,
            Map<Long, UserAssetInventory> inventories,
            Map<Long, Asset> assets
    ) {
        UserBook userBook = getIfPresent(userBooks, item.getUserBookId());
        BookSummaryResponse book = userBook == null ? null : getIfPresent(books, userBook.getBookId());
        BookshelfSection section = getIfPresent(sectionsById, item.getBookshelfSectionId());
        ReadingRecordSummaryResponse record = userBook == null ? null : visibleReadingRecord(viewerUserId, userBook, getIfPresent(readingRecords, userBook.getId()));
        UserAssetInventory inventory = getIfPresent(inventories, item.getAssetInventoryId());
        Asset asset = inventory == null ? null : getIfPresent(assets, inventory.getAssetId());
        // Orphan FK 진단 — 깨진 참조 데이터를 운영자가 식별할 수 있도록 warn 로그 (NPE 자체는 위 null 가드가 막음).
        // userBookId/sectionId/inventoryId 가 not null 인데 매핑된 entity 가 없으면 FK 무결성 깨짐 의심.
        if (item.getUserBookId() != null && userBook == null) {
            log.warn("orphan userBook itemId={} userBookId={} bookshelfId={}",
                    item.getId(), item.getUserBookId(), item.getBookshelfId());
        }
        if (userBook != null && userBook.getBookId() != null && book == null) {
            log.warn("orphan book itemId={} userBookId={} bookId={}",
                    item.getId(), userBook.getId(), userBook.getBookId());
        }
        if (item.getBookshelfSectionId() != null && section == null) {
            log.warn("orphan section itemId={} sectionId={} bookshelfId={}",
                    item.getId(), item.getBookshelfSectionId(), item.getBookshelfId());
        }
        if (item.getAssetInventoryId() != null && inventory == null) {
            log.warn("orphan inventory itemId={} inventoryId={}", item.getId(), item.getAssetInventoryId());
        }
        if (inventory != null && inventory.getAssetId() != null && asset == null) {
            log.warn("orphan asset itemId={} inventoryId={} assetId={}",
                    item.getId(), inventory.getId(), inventory.getAssetId());
        }
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
                book,
                item.getAssetInventoryId(),
                asset == null ? null : new AssetSummaryResponse(
                        asset.getId(),
                        asset.getCode(),
                        asset.getName(),
                        asset.getPreviewImageUrl()
                ),
                record
        );
    }

    private static <K, V> V getIfPresent(Map<K, V> map, K key) {
        if (map == null || key == null) {
            return null;
        }
        return map.get(key);
    }

    private ReadingRecordSummaryResponse visibleReadingRecord(Long viewerUserId, UserBook userBook, ReadingRecordSummaryResponse record) {
        if (record == null || userBook == null) {
            return null;
        }
        if (viewerUserId != null && viewerUserId.equals(userBook.getUserId())) {
            return record;
        }
        if ("PUBLIC".equalsIgnoreCase(record.visibilityScope())) {
            return record;
        }
        if ("FOLLOWERS".equalsIgnoreCase(record.visibilityScope())
                && viewerUserId != null
                && followRepository.existsByFollowerUserIdAndFollowingUserId(viewerUserId, userBook.getUserId())) {
            return record;
        }
        return null;
    }
}
