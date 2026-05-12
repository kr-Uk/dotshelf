package com.dotshelf.domain.book.service;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.function.Function;
import java.util.stream.Collectors;

import org.springframework.context.ApplicationEventPublisher;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.book.dto.BookDtos.AddBookToShelfRequest;
import com.dotshelf.domain.book.dto.BookDtos.AddKakaoBookToShelfRequest;
import com.dotshelf.domain.book.dto.BookDtos.BookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookSearchListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookSearchResponse;
import com.dotshelf.domain.book.dto.BookDtos.MoveUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UpdateUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UserBookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookPlacementListResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookPlacementResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookResponse;
import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.BookSource;
import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.BookReviewRepository;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.repository.BookshelfItemRepository;
import com.dotshelf.domain.bookshelf.service.BookshelfService;
import com.dotshelf.domain.notification.event.NotificationEvents.BookAdded;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class BookService {

    private static final int MIN_SEARCH_PAGE = 1;
    private static final int MAX_SEARCH_PAGE = 50;
    private static final int MIN_SEARCH_SIZE = 1;
    private static final int MAX_SEARCH_SIZE = 50;
    private static final int DEFAULT_LIST_LIMIT = 20;
    private static final int MAX_LIST_LIMIT = 50;
    private static final int MAX_TITLE_LENGTH = 255;
    private static final int MAX_AUTHOR_LENGTH = 255;
    private static final int MAX_PUBLISHER_LENGTH = 255;
    private static final int MAX_DESCRIPTION_LENGTH = 5000;
    private static final int MAX_COVER_IMAGE_URL_LENGTH = 500;
    private static final int MAX_SOURCE_URL_LENGTH = 500;
    private static final String ACTIVE = "ACTIVE";

    private final BookRepository bookRepository;
    private final BookReviewRepository bookReviewRepository;
    private final UserBookRepository userBookRepository;
    private final BookshelfItemRepository bookshelfItemRepository;
    private final BookshelfService bookshelfService;
    private final UserService userService;
    private final ApplicationEventPublisher eventPublisher;
    private final KakaoBookSearchClient kakaoBookSearchClient;

    public BookSearchListResponse searchBooks(String keyword, int page, int size) {
        int validPage = validateSearchPage(page);
        int validSize = validateSearchSize(size);
        String query = trimToNull(keyword);
        if (query == null) {
            return BookSearchListResponse.empty(validPage, validSize);
        }

        List<BookSearchResponse> localBooks = validPage == 1 ? searchLocalBooks(query) : List.of();
        KakaoBookSearchClient.SearchResult kakaoResult = kakaoBookSearchClient.search(query, validPage, validSize);
        List<BookSearchResponse> mergedBooks = mergeSearchResults(localBooks, kakaoResult.books());

        return new BookSearchListResponse(
                mergedBooks.stream()
                        .limit(validSize)
                        .toList(),
                validPage,
                validSize,
                kakaoResult.isEnd() && mergedBooks.size() <= validSize
        );
    }

    public BookResponse getBook(Long bookId) {
        return toBookResponse(getBookEntity(bookId), null, null);
    }

    @Transactional
    public BookResponse getBook(Long userId, Long bookId) {
        Book book = getBookEntity(bookId);
        book.increaseViewCount();
        return toBookResponse(book, userId, null);
    }

    public BookListResponse getRecommended(Long userId, int limit) {
        int validLimit = validateListLimit(limit);
        List<Book> books = bookRepository.findAll().stream()
                .sorted(Comparator.comparing(this::recommendedScore).reversed()
                        .thenComparing(Book::getId, Comparator.reverseOrder()))
                .limit(validLimit)
                .toList();
        return toBookListResponse(books, userId, false, 0, null);
    }

    public BookListResponse getRanking(
            Long userId,
            String genre,
            String priceType,
            String cursor,
            String sort,
            int limit
    ) {
        int validLimit = validateListLimit(limit);
        validateGenre(genre);
        validatePriceType(priceType);
        List<Book> sortedBooks = bookRepository.findAll().stream()
                .filter(book -> matchesPriceType(priceType))
                .sorted(rankingComparator(sort))
                .toList();
        int startIndex = cursorStartIndex(sortedBooks, cursor);
        List<Book> books = sortedBooks.stream()
                .skip(startIndex)
                .limit(validLimit)
                .toList();
        String nextCursor = startIndex + books.size() < sortedBooks.size() && !books.isEmpty()
                ? "book_" + books.getLast().getId()
                : null;
        return toBookListResponse(books, userId, true, startIndex, nextCursor);
    }

    @Transactional
    public UserBookResponse addToShelf(Long userId, Long bookshelfId, AddBookToShelfRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        bookshelfService.getOwnedBookshelf(currentUserId, bookshelfId);
        Book book = getBookEntity(request.bookId());
        return placeBookOnShelf(
                currentUserId,
                bookshelfId,
                book,
                request.sectionId(),
                request.slotNo(),
                request.facingDirection(),
                request.displayStyle()
        );
    }

    @Transactional
    public UserBookResponse addKakaoBookToShelf(Long userId, Long bookshelfId, AddKakaoBookToShelfRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        bookshelfService.getOwnedBookshelf(currentUserId, bookshelfId);
        Book book = findOrCreateBook(request);
        return placeBookOnShelf(
                currentUserId,
                bookshelfId,
                book,
                request.sectionId(),
                request.slotNo(),
                request.facingDirection(),
                request.displayStyle()
        );
    }

    private UserBookResponse placeBookOnShelf(
            Long currentUserId,
            Long bookshelfId,
            Book book,
            Long sectionId,
            Integer slotNo,
            String facingDirection,
            String displayStyle
    ) {
        Optional<UserBook> existingUserBook = userBookRepository.findByUserIdAndBookId(currentUserId, book.getId());
        Optional<BookshelfItem> activeItem = existingUserBook
                .flatMap(userBook -> bookshelfItemRepository.findByUserBookIdAndStatus(userBook.getId(), ACTIVE));
        bookshelfService.requireAvailablePlacement(
                bookshelfId,
                sectionId,
                slotNo,
                activeItem.map(BookshelfItem::getId).orElse(null));
        boolean createdUserBook = existingUserBook.isEmpty();
        UserBook userBook = existingUserBook
                .orElseGet(() -> {
                    book.increaseBookshelfAddCount();
                    return userBookRepository.save(UserBook.builder()
                            .userId(currentUserId)
                            .bookId(book.getId())
                            .build());
                });
        String previousStorageStatus = createdUserBook ? null : userBook.getStorageStatus();
        userBook.placeOnShelf();
        activeItem.ifPresent(BookshelfItem::remove);
        BookshelfItem item = bookshelfItemRepository.save(BookshelfItem.builder()
                .bookshelfId(bookshelfId)
                .bookshelfSectionId(sectionId)
                .itemType("BOOK")
                .userBookId(userBook.getId())
                .slotNo(slotNo)
                .facingDirection(facingDirection)
                .displayStyle(displayStyle)
                .build());
        if (isNewShelfAddition(createdUserBook, previousStorageStatus, activeItem.isPresent())) {
            eventPublisher.publishEvent(new BookAdded(currentUserId, userBook.getId(), book.getTitle()));
        }
        return UserBookResponse.from(userBook, item);
    }

    private static boolean isNewShelfAddition(boolean createdUserBook, String previousStorageStatus, boolean hadActiveItem) {
        return createdUserBook || !"ON_SHELF".equalsIgnoreCase(previousStorageStatus) || !hadActiveItem;
    }

    @Transactional
    public UserBookResponse updateUserBook(Long userId, Long userBookId, UpdateUserBookRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        UserBook userBook = getOwnedUserBook(currentUserId, userBookId);
        userBook.update(request.readingStatus(), request.ownerRating(), request.selectedCoverProductId());
        return UserBookResponse.from(userBook);
    }

    @Transactional
    public UserBookResponse move(Long userId, Long userBookId, MoveUserBookRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        bookshelfService.getOwnedBookshelf(currentUserId, request.bookshelfId());
        UserBook userBook = getOwnedUserBook(currentUserId, userBookId);
        userBook.placeOnShelf();
        BookshelfItem item = bookshelfItemRepository.findByUserBookIdAndStatus(userBookId, "ACTIVE")
                .orElseGet(() -> bookshelfItemRepository.save(BookshelfItem.builder()
                        .bookshelfId(request.bookshelfId())
                        .itemType("BOOK")
                        .userBookId(userBookId)
                        .build()));
        bookshelfService.requireAvailablePlacement(request.bookshelfId(), request.sectionId(), request.slotNo(), item.getId());
        item.move(request.bookshelfId(), request.sectionId(), request.slotNo(), request.facingDirection(), request.displayStyle());
        return UserBookResponse.from(userBook, item);
    }

    @Transactional
    public UserBookResponse archive(Long userId, Long userBookId) {
        Long currentUserId = userService.requireUserId(userId);
        UserBook userBook = getOwnedUserBook(currentUserId, userBookId);
        userBook.archive();
        bookshelfItemRepository.findByUserBookIdAndStatus(userBookId, "ACTIVE")
                .ifPresent(BookshelfItem::remove);
        return UserBookResponse.from(userBook);
    }

    @Transactional
    public void delete(Long userId, Long userBookId) {
        Long currentUserId = userService.requireUserId(userId);
        UserBook userBook = getOwnedUserBook(currentUserId, userBookId);
        userBook.delete();
        bookshelfItemRepository.findByUserBookIdAndStatus(userBookId, "ACTIVE")
                .ifPresent(BookshelfItem::remove);
    }

    public UserBookListResponse getArchived(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        return new UserBookListResponse(userBookRepository.findByUserIdAndStorageStatusOrderByAddedAtDesc(currentUserId, "ARCHIVED")
                .stream()
                .map(UserBookResponse::from)
                .toList());
    }

    public UserBookPlacementListResponse getUserBooks(
            Long userId,
            String storageStatus,
            String readingStatus,
            Long bookshelfId,
            Long sectionId,
            int limit
    ) {
        Long currentUserId = userService.requireUserId(userId);
        int validLimit = validateListLimit(limit);
        List<UserBook> userBooks = userBookRepository.findByUserIdAndStorageStatusNotOrderByAddedAtDesc(currentUserId, "DELETED")
                .stream()
                .filter(userBook -> matches(storageStatus, userBook.getStorageStatus()))
                .filter(userBook -> matches(readingStatus, userBook.getReadingStatus()))
                .toList();
        Map<Long, BookshelfItem> itemMap = activeItemMap(userBooks);
        List<UserBookPlacementResponse> books = userBooks.stream()
                .map(userBook -> toPlacementResponse(userBook, itemMap.get(userBook.getId())))
                .filter(response -> bookshelfId == null || bookshelfId.equals(response.bookshelfId()))
                .filter(response -> sectionId == null || sectionId.equals(response.sectionId()))
                .limit(validLimit)
                .toList();
        return new UserBookPlacementListResponse(books, null);
    }

    @Transactional
    public ToggleResponse like(Long userId, Long userBookId) {
        Long currentUserId = userService.requireUserId(userId);
        UserBook userBook = getOwnedUserBook(currentUserId, userBookId);
        Book book = getBookEntity(userBook.getBookId());
        if (!userBook.isLiked()) {
            userBook.like();
            book.increaseLikeCount();
        }
        return new ToggleResponse(true, book.getLikeCount());
    }

    @Transactional
    public ToggleResponse unlike(Long userId, Long userBookId) {
        Long currentUserId = userService.requireUserId(userId);
        UserBook userBook = getOwnedUserBook(currentUserId, userBookId);
        Book book = getBookEntity(userBook.getBookId());
        if (userBook.isLiked()) {
            userBook.unlike();
            book.decreaseLikeCount();
        }
        return new ToggleResponse(false, book.getLikeCount());
    }

    public Book getBookEntity(Long bookId) {
        return bookRepository.findById(bookId)
                .orElseThrow(() -> new CustomException(ErrorCode.BOOK_NOT_FOUND));
    }

    public UserBook getUserBook(Long userBookId) {
        return userBookRepository.findById(userBookId)
                .orElseThrow(() -> new CustomException(ErrorCode.USER_BOOK_NOT_FOUND));
    }

    private UserBook getOwnedUserBook(Long userId, Long userBookId) {
        UserBook userBook = getUserBook(userBookId);
        if (!userBook.getUserId().equals(userId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        return userBook;
    }

    private List<BookSearchResponse> searchLocalBooks(String query) {
        String titleSearch = Book.normalizeTitleSearch(query);
        return bookRepository.findTop20ByTitleSearchContainingOrAuthorContainingIgnoreCase(titleSearch, query)
                .stream()
                .map(BookSearchResponse::from)
                .toList();
    }

    private BookListResponse toBookListResponse(
            List<Book> books,
            Long userId,
            boolean includeRank,
            int rankOffset,
            String nextCursor
    ) {
        Map<Long, UserBook> ownedBooks = ownedBooks(userId, books);
        Map<Long, Long> reviewCounts = reviewCounts(books);
        List<BookResponse> items = new ArrayList<>();
        for (int i = 0; i < books.size(); i++) {
            Book book = books.get(i);
            UserBook userBook = ownedBooks.get(book.getId());
            items.add(BookResponse.from(
                    book,
                    userBook != null,
                    userBook == null ? null : userBook.getId(),
                    reviewCounts.getOrDefault(book.getId(), 0L),
                    includeRank ? rankOffset + i + 1 : null
            ));
        }
        return new BookListResponse(items, nextCursor);
    }

    private BookResponse toBookResponse(Book book, Long userId, Integer rank) {
        UserBook userBook = userId == null
                ? null
                : userBookRepository.findByUserIdAndBookId(userId, book.getId()).orElse(null);
        return BookResponse.from(
                book,
                userBook != null,
                userBook == null ? null : userBook.getId(),
                bookReviewRepository.countByBookIdAndStatus(book.getId(), ACTIVE),
                rank
        );
    }

    private Map<Long, UserBook> ownedBooks(Long userId, List<Book> books) {
        if (userId == null || books.isEmpty()) {
            return Map.of();
        }
        Set<Long> bookIds = books.stream()
                .map(Book::getId)
                .collect(Collectors.toSet());
        return userBookRepository.findByUserIdAndStorageStatusNot(userId, "DELETED").stream()
                .filter(userBook -> bookIds.contains(userBook.getBookId()))
                .collect(Collectors.toMap(UserBook::getBookId, Function.identity(), (left, right) -> left));
    }

    private Map<Long, Long> reviewCounts(List<Book> books) {
        if (books.isEmpty()) {
            return Map.of();
        }
        List<Long> bookIds = books.stream()
                .map(Book::getId)
                .toList();
        return bookReviewRepository.findByBookIdInAndStatus(bookIds, ACTIVE).stream()
                .collect(Collectors.groupingBy(review -> review.getBookId(), Collectors.counting()));
    }

    private Map<Long, BookshelfItem> activeItemMap(List<UserBook> userBooks) {
        List<Long> userBookIds = userBooks.stream()
                .map(UserBook::getId)
                .toList();
        if (userBookIds.isEmpty()) {
            return Map.of();
        }
        return bookshelfItemRepository.findByUserBookIdInAndStatus(userBookIds, ACTIVE).stream()
                .collect(Collectors.toMap(BookshelfItem::getUserBookId, Function.identity(), (left, right) -> left));
    }

    private UserBookPlacementResponse toPlacementResponse(UserBook userBook, BookshelfItem item) {
        return new UserBookPlacementResponse(
                userBook.getId(),
                userBook.getUserId(),
                userBook.getBookId(),
                userBook.getReadingStatus(),
                userBook.getStorageStatus(),
                userBook.getOwnerRating(),
                userBook.isLiked(),
                item == null ? null : item.getId(),
                item == null ? null : item.getBookshelfId(),
                item == null ? null : item.getBookshelfSectionId(),
                item == null ? null : item.getSlotNo(),
                item == null ? null : item.getFacingDirection(),
                item == null ? null : item.getDisplayStyle()
        );
    }

    private Comparator<Book> rankingComparator(String sort) {
        if ("RATING".equalsIgnoreCase(sort)) {
            return Comparator.comparing(this::averageRating)
                    .reversed()
                    .thenComparing(Book::getId, Comparator.reverseOrder());
        }
        if ("REVIEW".equalsIgnoreCase(sort)) {
            return Comparator.comparing(Book::getRatingCount)
                    .reversed()
                    .thenComparing(Book::getId, Comparator.reverseOrder());
        }
        if ("VIEW".equalsIgnoreCase(sort)) {
            return Comparator.comparing(Book::getViewCount)
                    .reversed()
                    .thenComparing(Book::getId, Comparator.reverseOrder());
        }
        if ("RECORD".equalsIgnoreCase(sort)) {
            return Comparator.comparing(Book::getBookshelfAddCount)
                    .reversed()
                    .thenComparing(Book::getId, Comparator.reverseOrder());
        }
        if ("RECENT".equalsIgnoreCase(sort)) {
            return Comparator.comparing(Book::getId, Comparator.reverseOrder());
        }
        return Comparator.comparing(this::popularScore)
                .reversed()
                .thenComparing(Book::getId, Comparator.reverseOrder());
    }

    private double recommendedScore(Book book) {
        return popularScore(book) + averageRating(book) * 10;
    }

    private double popularScore(Book book) {
        return book.getBookshelfAddCount() * 3.0
                + book.getLikeCount() * 2.0
                + book.getViewCount()
                + book.getRatingCount() * 2.0;
    }

    private double averageRating(Book book) {
        if (book.getRatingCount() == 0) {
            return 0;
        }
        return book.getRatingSum().doubleValue() / book.getRatingCount();
    }

    private boolean matches(String expected, String actual) {
        return expected == null || expected.isBlank() || expected.equalsIgnoreCase(actual);
    }

    private void validateGenre(String genre) {
        if (genre != null && !genre.isBlank() && !"ALL".equalsIgnoreCase(genre)) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "genre 필터는 현재 ALL만 지원합니다.");
        }
    }

    private boolean matchesPriceType(String priceType) {
        if (priceType == null || priceType.isBlank() || "ALL".equalsIgnoreCase(priceType)) {
            return true;
        }
        if ("FREE".equalsIgnoreCase(priceType)) {
            return true;
        }
        if ("PAID".equalsIgnoreCase(priceType)) {
            return false;
        }
        return false;
    }

    private void validatePriceType(String priceType) {
        if (priceType == null || priceType.isBlank()
                || "ALL".equalsIgnoreCase(priceType)
                || "FREE".equalsIgnoreCase(priceType)
                || "PAID".equalsIgnoreCase(priceType)) {
            return;
        }
        throw new CustomException(ErrorCode.INVALID_INPUT, "priceType은 ALL, FREE, PAID 중 하나여야 합니다.");
    }

    private int cursorStartIndex(List<Book> books, String cursor) {
        Long bookId = cursorId(cursor, "book_");
        if (bookId == null) {
            return 0;
        }
        for (int i = 0; i < books.size(); i++) {
            if (books.get(i).getId().equals(bookId)) {
                return i + 1;
            }
        }
        throw new CustomException(ErrorCode.INVALID_INPUT, "유효하지 않은 책 ranking cursor입니다.");
    }

    private Long cursorId(String cursor, String prefix) {
        if (cursor == null || cursor.isBlank()) {
            return null;
        }
        String value = cursor.trim();
        if (!value.startsWith(prefix)) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "cursor 형식이 올바르지 않습니다.");
        }
        try {
            return Long.parseLong(value.substring(prefix.length()));
        } catch (NumberFormatException e) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "cursor 형식이 올바르지 않습니다.");
        }
    }

    private List<BookSearchResponse> mergeSearchResults(
            List<BookSearchResponse> localBooks,
            List<BookSearchResponse> kakaoBooks
    ) {
        Set<String> seenKeys = new LinkedHashSet<>();
        ArrayList<BookSearchResponse> results = new ArrayList<>();
        localBooks.forEach(book -> addSearchResult(results, seenKeys, book));
        kakaoBooks.forEach(book -> addSearchResult(results, seenKeys, book));
        return List.copyOf(results);
    }

    private void addSearchResult(List<BookSearchResponse> results, Set<String> seenKeys, BookSearchResponse book) {
        String key = searchResultKey(book);
        if (seenKeys.add(key)) {
            results.add(book);
        }
    }

    private String searchResultKey(BookSearchResponse book) {
        String isbn13 = trimToNull(book.isbn13());
        if (isbn13 != null) {
            return "isbn13:" + isbn13;
        }
        String isbn10 = trimToNull(book.isbn10());
        if (isbn10 != null) {
            return "isbn10:" + isbn10;
        }
        return "title:" + Book.normalizeTitleSearch(book.title()) + ":" + normalizeAuthor(book.author());
    }

    private Book findOrCreateBook(AddKakaoBookToShelfRequest request) {
        return findBookByIsbn(request.isbn13(), request.isbn10())
                .or(() -> findBookByTitleAndAuthor(request.title(), request.author()))
                .orElseGet(() -> bookRepository.save(Book.builder()
                        .isbn13(trimToNull(request.isbn13()))
                        .isbn10(trimToNull(request.isbn10()))
                        .title(trimToLength(requiredText(request.title()), MAX_TITLE_LENGTH))
                        .author(trimToLength(authorName(request.author()), MAX_AUTHOR_LENGTH))
                        .publisher(trimToLength(trimToNull(request.publisher()), MAX_PUBLISHER_LENGTH))
                        .publishedDate(request.publishedDate())
                        .description(trimToLength(trimToNull(request.description()), MAX_DESCRIPTION_LENGTH))
                        .coverImageUrl(trimToLength(trimToNull(request.coverImageUrl()), MAX_COVER_IMAGE_URL_LENGTH))
                        .source(BookSource.KAKAO)
                        .sourceUrl(trimToLength(trimToNull(request.sourceUrl()), MAX_SOURCE_URL_LENGTH))
                        .build()));
    }

    private Optional<Book> findBookByIsbn(String isbn13, String isbn10) {
        String normalizedIsbn13 = trimToNull(isbn13);
        if (normalizedIsbn13 != null) {
            Optional<Book> book = bookRepository.findByIsbn13(normalizedIsbn13);
            if (book.isPresent()) {
                return book;
            }
        }

        String normalizedIsbn10 = trimToNull(isbn10);
        if (normalizedIsbn10 != null) {
            return bookRepository.findFirstByIsbn10(normalizedIsbn10);
        }
        return Optional.empty();
    }

    private Optional<Book> findBookByTitleAndAuthor(String title, String author) {
        String normalizedTitle = Book.normalizeTitleSearch(requiredText(title));
        String normalizedAuthor = normalizeAuthor(authorName(author));
        return bookRepository.findTop20ByTitleSearchContainingOrAuthorContainingIgnoreCase(normalizedTitle, authorName(author))
                .stream()
                .filter(book -> normalizedTitle.equals(Book.normalizeTitleSearch(book.getTitle())))
                .filter(book -> normalizedAuthor.equals(normalizeAuthor(book.getAuthor())))
                .findFirst();
    }

    private int validateSearchPage(int page) {
        if (page < MIN_SEARCH_PAGE || page > MAX_SEARCH_PAGE) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "page는 1~50 사이여야 합니다.");
        }
        return page;
    }

    private int validateSearchSize(int size) {
        if (size < MIN_SEARCH_SIZE || size > MAX_SEARCH_SIZE) {
            throw new CustomException(ErrorCode.INVALID_INPUT, "size는 1~50 사이여야 합니다.");
        }
        return size;
    }

    private int validateListLimit(int limit) {
        if (limit <= 0) {
            return DEFAULT_LIST_LIMIT;
        }
        return Math.min(limit, MAX_LIST_LIMIT);
    }

    private String requiredText(String value) {
        String text = trimToNull(value);
        if (text == null) {
            throw new CustomException(ErrorCode.INVALID_INPUT);
        }
        return text;
    }

    private String authorName(String author) {
        String value = trimToNull(author);
        return value == null ? "작가 미상" : value;
    }

    private String normalizeAuthor(String author) {
        String value = trimToNull(author);
        return value == null ? "" : value.toLowerCase(Locale.ROOT);
    }

    private String trimToLength(String value, int maxLength) {
        String text = trimToNull(value);
        if (text == null || text.length() <= maxLength) {
            return text;
        }
        return text.substring(0, maxLength);
    }

    private String trimToNull(String value) {
        if (value == null || value.isBlank()) {
            return null;
        }
        return value.trim();
    }
}
