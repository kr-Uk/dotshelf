package com.dotshelf.domain.book.service;

import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Optional;
import java.util.Set;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.book.dto.BookDtos.AddBookToShelfRequest;
import com.dotshelf.domain.book.dto.BookDtos.AddKakaoBookToShelfRequest;
import com.dotshelf.domain.book.dto.BookDtos.BookResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookSearchListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookSearchResponse;
import com.dotshelf.domain.book.dto.BookDtos.MoveUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UpdateUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UserBookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookResponse;
import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.repository.BookshelfItemRepository;
import com.dotshelf.domain.bookshelf.service.BookshelfService;
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
    private static final int MAX_TITLE_LENGTH = 255;
    private static final int MAX_AUTHOR_LENGTH = 255;
    private static final int MAX_PUBLISHER_LENGTH = 255;
    private static final int MAX_DESCRIPTION_LENGTH = 5000;
    private static final int MAX_COVER_IMAGE_URL_LENGTH = 500;

    private final BookRepository bookRepository;
    private final UserBookRepository userBookRepository;
    private final BookshelfItemRepository bookshelfItemRepository;
    private final BookshelfService bookshelfService;
    private final UserService userService;
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

        return new BookSearchListResponse(
                mergeSearchResults(localBooks, kakaoResult.books()),
                validPage,
                validSize,
                kakaoResult.isEnd()
        );
    }

    public BookResponse getBook(Long bookId) {
        return BookResponse.from(getBookEntity(bookId));
    }

    @Transactional
    public UserBookResponse addToShelf(Long userId, Long bookshelfId, AddBookToShelfRequest request) {
        Long currentUserId = userService.requireUserId(userId);
        bookshelfService.getBookshelf(bookshelfId);
        Book book = getBookEntity(request.bookId());
        UserBook userBook = userBookRepository.findByUserIdAndBookId(currentUserId, request.bookId())
                .orElseGet(() -> {
                    book.increaseBookshelfAddCount();
                    return userBookRepository.save(UserBook.builder()
                            .userId(currentUserId)
                            .bookId(request.bookId())
                            .build());
                });
        userBook.placeOnShelf();
        bookshelfItemRepository.findByUserBookIdAndStatus(userBook.getId(), "ACTIVE")
                .ifPresent(BookshelfItem::remove);
        bookshelfItemRepository.save(BookshelfItem.builder()
                .bookshelfId(bookshelfId)
                .bookshelfSectionId(request.sectionId())
                .itemType("BOOK")
                .userBookId(userBook.getId())
                .slotNo(request.slotNo())
                .displayStyle(request.displayStyle())
                .build());
        return UserBookResponse.from(userBook);
    }

    @Transactional
    public UserBookResponse addKakaoBookToShelf(Long userId, Long bookshelfId, AddKakaoBookToShelfRequest request) {
        Book book = findOrCreateBook(request);
        return addToShelf(userId, bookshelfId, new AddBookToShelfRequest(
                book.getId(),
                request.sectionId(),
                request.slotNo(),
                request.displayStyle()
        ));
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
        UserBook userBook = getOwnedUserBook(currentUserId, userBookId);
        userBook.placeOnShelf();
        BookshelfItem item = bookshelfItemRepository.findByUserBookIdAndStatus(userBookId, "ACTIVE")
                .orElseGet(() -> bookshelfItemRepository.save(BookshelfItem.builder()
                        .bookshelfId(request.bookshelfId())
                        .itemType("BOOK")
                        .userBookId(userBookId)
                        .build()));
        item.move(request.bookshelfId(), request.sectionId(), request.slotNo(), request.displayStyle());
        return UserBookResponse.from(userBook);
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

    @Transactional
    public ToggleResponse like(Long userId, Long userBookId) {
        Long currentUserId = userService.requireUserId(userId);
        UserBook userBook = getOwnedUserBook(currentUserId, userBookId);
        userBook.like();
        return new ToggleResponse(true, 1);
    }

    @Transactional
    public ToggleResponse unlike(Long userId, Long userBookId) {
        Long currentUserId = userService.requireUserId(userId);
        UserBook userBook = getOwnedUserBook(currentUserId, userBookId);
        userBook.unlike();
        return new ToggleResponse(false, 0);
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
                .orElseGet(() -> bookRepository.save(Book.builder()
                        .isbn13(trimToNull(request.isbn13()))
                        .isbn10(trimToNull(request.isbn10()))
                        .title(trimToLength(requiredText(request.title()), MAX_TITLE_LENGTH))
                        .author(trimToLength(authorName(request.author()), MAX_AUTHOR_LENGTH))
                        .publisher(trimToLength(trimToNull(request.publisher()), MAX_PUBLISHER_LENGTH))
                        .publishedDate(request.publishedDate())
                        .description(trimToLength(trimToNull(request.description()), MAX_DESCRIPTION_LENGTH))
                        .coverImageUrl(trimToLength(trimToNull(request.coverImageUrl()), MAX_COVER_IMAGE_URL_LENGTH))
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
