package com.dotshelf.domain.book.service;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.book.dto.BookDtos.AddBookToShelfRequest;
import com.dotshelf.domain.book.dto.BookDtos.BookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookResponse;
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

    private final BookRepository bookRepository;
    private final UserBookRepository userBookRepository;
    private final BookshelfItemRepository bookshelfItemRepository;
    private final BookshelfService bookshelfService;
    private final UserService userService;

    public BookListResponse search(String keyword) {
        String query = keyword == null ? "" : keyword;
        return new BookListResponse(bookRepository.findTop20ByTitleContainingIgnoreCaseOrAuthorContainingIgnoreCase(query, query)
                .stream()
                .map(BookResponse::from)
                .toList());
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
}
