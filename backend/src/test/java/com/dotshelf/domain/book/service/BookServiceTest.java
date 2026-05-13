package com.dotshelf.domain.book.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.List;
import java.util.Optional;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.context.ApplicationEventPublisher;

import com.dotshelf.domain.book.dto.BookDtos.AddBookToShelfRequest;
import com.dotshelf.domain.book.dto.BookDtos.AddKakaoBookToShelfRequest;
import com.dotshelf.domain.book.dto.BookDtos.BookSearchResponse;
import com.dotshelf.domain.book.dto.BookDtos.MoveUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UpdateUserBookRequest;
import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.UserBook;
import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.BookReviewRepository;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.domain.bookshelf.entity.BookshelfItem;
import com.dotshelf.domain.bookshelf.repository.BookshelfItemRepository;
import com.dotshelf.domain.bookshelf.service.BookshelfService;
import com.dotshelf.domain.notification.event.NotificationEvents.BookAdded;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.support.TestFixtures;

@ExtendWith(MockitoExtension.class)
class BookServiceTest {

    @Mock
    private BookRepository bookRepository;

    @Mock
    private BookReviewRepository bookReviewRepository;

    @Mock
    private UserBookRepository userBookRepository;

    @Mock
    private BookshelfItemRepository bookshelfItemRepository;

    @Mock
    private BookshelfService bookshelfService;

    @Mock
    private SpaceService spaceService;

    @Mock
    private UserService userService;

    @Mock
    private ApplicationEventPublisher eventPublisher;

    @Mock
    private KakaoBookSearchClient kakaoBookSearchClient;

    @InjectMocks
    private BookService bookService;

    @Test
    @DisplayName("검색어가 비어 있으면 로컬/카카오 검색 없이 빈 검색 결과를 반환한다")
    void searchBooks_blankKeyword_returnsEmpty() {
        var response = bookService.searchBooks("   ", 1, 10);

        assertThat(response.books()).isEmpty();
        assertThat(response.page()).isEqualTo(1);
        assertThat(response.size()).isEqualTo(10);
        then(bookRepository).shouldHaveNoInteractions();
        then(kakaoBookSearchClient).shouldHaveNoInteractions();
    }

    @Test
    @DisplayName("도서 상세 조회는 조회수를 증가시키고 리뷰 수를 포함한다")
    void getBook_withUser_increasesViewCount() {
        Book book = TestFixtures.book(10L, "데미안");
        given(bookRepository.findById(10L)).willReturn(Optional.of(book));
        given(userBookRepository.findByUserIdAndBookId(1L, 10L)).willReturn(Optional.empty());
        given(bookReviewRepository.countByBookIdAndStatus(10L, "ACTIVE")).willReturn(3L);

        var response = bookService.getBook(1L, 10L);

        assertThat(response.id()).isEqualTo(10L);
        assertThat(response.reviewCount()).isEqualTo(3L);
        assertThat(book.getViewCount()).isEqualTo(1L);
    }

    @Test
    @DisplayName("존재하지 않는 도서 상세 조회는 BOOK_NOT_FOUND를 던진다")
    void getBook_missingBook_throwsBookNotFound() {
        given(bookRepository.findById(99L)).willReturn(Optional.empty());

        assertThatThrownBy(() -> bookService.getBook(99L))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.BOOK_NOT_FOUND);
    }

    @Test
    @DisplayName("랭킹 조회에서 지원하지 않는 priceType은 INVALID_INPUT을 던진다")
    void getRanking_invalidPriceType_throwsException() {
        assertThatThrownBy(() -> bookService.getRanking(1L, "ALL", "INVALID", null, "POPULAR", 20))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("도서 검색은 로컬 결과를 우선하고 카카오 중복 결과를 ISBN 기준으로 제거한다")
    void searchBooks_mergesLocalAndKakaoResultsWithoutDuplicates() {
        Book localBook = TestFixtures.book(10L, "로컬 도서");
        BookSearchResponse duplicateFromKakao = new BookSearchResponse(
                null,
                "KAKAO",
                localBook.getIsbn13(),
                null,
                "카카오 중복",
                "author",
                "publisher",
                LocalDate.of(2024, 1, 1),
                "description",
                null,
                "https://kakao.example/books/duplicate"
        );
        BookSearchResponse kakaoOnly = new BookSearchResponse(
                null,
                "KAKAO",
                "9791190000001",
                null,
                "카카오 신규",
                "other",
                "publisher",
                LocalDate.of(2024, 1, 2),
                "description",
                null,
                "https://kakao.example/books/new"
        );
        given(bookRepository.findTop20ByTitleSearchContainingOrAuthorContainingIgnoreCase("로컬", "로컬"))
                .willReturn(List.of(localBook));
        given(kakaoBookSearchClient.search("로컬", 1, 10))
                .willReturn(new KakaoBookSearchClient.SearchResult(List.of(duplicateFromKakao, kakaoOnly), true));

        var response = bookService.searchBooks(" 로컬 ", 1, 10);

        assertThat(response.books()).extracting(BookSearchResponse::title)
                .containsExactly("로컬 도서", "카카오 신규");
        assertThat(response.isEnd()).isTrue();
    }

    @Test
    @DisplayName("도서 검색 page와 size가 허용 범위를 벗어나면 INVALID_INPUT을 던진다")
    void searchBooks_invalidPaging_throwsException() {
        assertThatThrownBy(() -> bookService.searchBooks("query", 0, 10))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);

        assertThatThrownBy(() -> bookService.searchBooks("query", 1, 51))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("추천 도서는 점수 내림차순으로 정렬하고 limit 만큼만 반환한다")
    void getRecommended_sortsByCalculatedScoreAndLimit() {
        Book low = TestFixtures.book(1L, "낮은 점수");
        Book high = TestFixtures.book(2L, "높은 점수");
        Book middle = TestFixtures.book(3L, "중간 점수");
        high.increaseBookshelfAddCount();
        high.increaseBookshelfAddCount();
        high.addReviewRating(BigDecimal.valueOf(5.0));
        middle.increaseViewCount();
        middle.increaseViewCount();
        given(bookRepository.findAll()).willReturn(List.of(low, high, middle));
        given(bookReviewRepository.findByBookIdInAndStatus(List.of(2L, 3L), "ACTIVE")).willReturn(List.of());

        var response = bookService.getRecommended(null, 2);

        assertThat(response.items()).extracting(item -> item.id()).containsExactly(2L, 3L);
        assertThat(response.nextCursor()).isNull();
    }

    @Test
    @DisplayName("랭킹 조회는 cursor 다음 항목부터 rank와 nextCursor를 계산한다")
    void getRanking_validCursor_returnsNextPageWithRank() {
        Book first = TestFixtures.book(3L, "최신 3");
        Book second = TestFixtures.book(2L, "최신 2");
        Book third = TestFixtures.book(1L, "최신 1");
        given(bookRepository.findAll()).willReturn(List.of(third, second, first));
        given(bookReviewRepository.findByBookIdInAndStatus(List.of(2L), "ACTIVE")).willReturn(List.of());

        var response = bookService.getRanking(null, "ALL", "ALL", "book_3", "RECENT", 1);

        assertThat(response.items()).hasSize(1);
        assertThat(response.items().getFirst().id()).isEqualTo(2L);
        assertThat(response.items().getFirst().rank()).isEqualTo(2);
        assertThat(response.nextCursor()).isEqualTo("book_2");
    }

    @Test
    @DisplayName("랭킹 cursor 형식이 올바르지 않으면 INVALID_INPUT을 던진다")
    void getRanking_invalidCursor_throwsInvalidInput() {
        given(bookRepository.findAll()).willReturn(List.of(TestFixtures.book(1L, "도서")));

        assertThatThrownBy(() -> bookService.getRanking(null, "ALL", "ALL", "bad_cursor", "RECENT", 20))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.INVALID_INPUT);
    }

    @Test
    @DisplayName("책장에 처음 추가하는 책은 UserBook과 BookshelfItem을 만들고 BookAdded 이벤트를 발행한다")
    void addToShelf_newBook_createsUserBookItemAndPublishesEvent() {
        Book book = TestFixtures.book(10L, "데미안");
        UserBook userBook = TestFixtures.userBook(20L, 1L, 10L);
        BookshelfItem item = TestFixtures.bookshelfItem(30L, 100L, 200L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookRepository.findById(10L)).willReturn(Optional.of(book));
        given(userBookRepository.findByUserIdAndBookId(1L, 10L)).willReturn(Optional.empty());
        given(userBookRepository.save(any(UserBook.class))).willReturn(userBook);
        given(bookshelfItemRepository.save(any(BookshelfItem.class))).willReturn(item);
        given(spaceService.findOrCreateMySpace(1L)).willReturn(TestFixtures.space(1L, 1L));
        ArgumentCaptor<BookAdded> eventCaptor = ArgumentCaptor.forClass(BookAdded.class);

        var response = bookService.addToShelf(
                1L,
                100L,
                new AddBookToShelfRequest(10L, 200L, 1, "FRONT", "SPINE")
        );

        assertThat(response.id()).isEqualTo(20L);
        assertThat(response.placement().bookshelfItemId()).isEqualTo(30L);
        assertThat(book.getBookshelfAddCount()).isEqualTo(1L);
        then(eventPublisher).should().publishEvent(eventCaptor.capture());
        assertThat(eventCaptor.getValue().senderUserId()).isEqualTo(1L);
        assertThat(eventCaptor.getValue().userBookId()).isEqualTo(20L);
    }

    @Test
    @DisplayName("이미 책장에 있는 책을 다시 배치하면 기존 아이템을 제거하지만 BookAdded 이벤트는 발행하지 않는다")
    void addToShelf_existingOnShelfWithActiveItem_replacesItemWithoutEvent() {
        Book book = TestFixtures.book(10L, "데미안");
        UserBook userBook = TestFixtures.userBook(20L, 1L, 10L);
        BookshelfItem oldItem = TestFixtures.bookshelfItem(29L, 99L, 199L, 20L);
        BookshelfItem newItem = TestFixtures.bookshelfItem(30L, 100L, 200L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookRepository.findById(10L)).willReturn(Optional.of(book));
        given(userBookRepository.findByUserIdAndBookId(1L, 10L)).willReturn(Optional.of(userBook));
        given(bookshelfItemRepository.findByUserBookIdAndStatus(20L, "ACTIVE")).willReturn(Optional.of(oldItem));
        given(bookshelfItemRepository.save(any(BookshelfItem.class))).willReturn(newItem);

        var response = bookService.addToShelf(
                1L,
                100L,
                new AddBookToShelfRequest(10L, 200L, 2, "BACK", "COVER")
        );

        assertThat(response.placement().bookshelfItemId()).isEqualTo(30L);
        assertThat(oldItem.getStatus()).isEqualTo("REMOVED");
        assertThat(book.getBookshelfAddCount()).isZero();
        then(eventPublisher).should(never()).publishEvent(any());
    }

    @Test
    @DisplayName("내 책 수정은 소유자 검증 후 읽기 상태와 별점을 반영한다")
    void updateUserBook_ownedBook_updatesFields() {
        UserBook userBook = TestFixtures.userBook(20L, 1L, 10L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userBookRepository.findById(20L)).willReturn(Optional.of(userBook));

        var response = bookService.updateUserBook(
                1L,
                20L,
                new UpdateUserBookRequest("READING", BigDecimal.valueOf(4.5), 99L)
        );

        assertThat(response.readingStatus()).isEqualTo("READING");
        assertThat(userBook.getOwnerRating()).isEqualByComparingTo("4.5");
        assertThat(userBook.getSelectedCoverProductId()).isEqualTo(99L);
    }

    @Test
    @DisplayName("다른 사용자의 UserBook 수정은 FORBIDDEN을 던진다")
    void updateUserBook_otherUser_throwsForbidden() {
        given(userService.requireUserId(2L)).willReturn(2L);
        given(userBookRepository.findById(20L)).willReturn(Optional.of(TestFixtures.userBook(20L, 1L, 10L)));

        assertThatThrownBy(() -> bookService.updateUserBook(2L, 20L, new UpdateUserBookRequest("READING", null, null)))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.FORBIDDEN);
    }

    @Test
    @DisplayName("카카오 책 추가는 기존 ISBN 도서를 재사용하고 새 Book을 만들지 않는다")
    void addKakaoBookToShelf_existingIsbn_reusesBook() {
        Book existing = TestFixtures.book(10L, "기존 카카오 도서");
        UserBook userBook = TestFixtures.userBook(20L, 1L, 10L);
        BookshelfItem item = TestFixtures.bookshelfItem(30L, 100L, 200L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookRepository.findByIsbn13(existing.getIsbn13())).willReturn(Optional.of(existing));
        given(userBookRepository.findByUserIdAndBookId(1L, 10L)).willReturn(Optional.empty());
        given(userBookRepository.save(any(UserBook.class))).willReturn(userBook);
        given(bookshelfItemRepository.save(any(BookshelfItem.class))).willReturn(item);
        given(spaceService.findOrCreateMySpace(1L)).willReturn(TestFixtures.space(1L, 1L));

        var response = bookService.addKakaoBookToShelf(
                1L,
                100L,
                new AddKakaoBookToShelfRequest(
                        existing.getIsbn13(),
                        null,
                        "기존 카카오 도서",
                        "author",
                        "publisher",
                        LocalDate.of(2024, 1, 1),
                        "description",
                        null,
                        "https://kakao.example/books/1",
                        200L,
                        1,
                        "FRONT",
                        "SPINE"
                )
        );

        assertThat(response.bookId()).isEqualTo(10L);
        then(bookRepository).shouldHaveNoMoreInteractions();
    }

    @Test
    @DisplayName("카카오 책 추가 시 기존 도서가 없으면 author 기본값과 길이 제한을 적용해 새 Book을 저장한다")
    void addKakaoBookToShelf_newBook_createsSanitizedBook() {
        UserBook userBook = TestFixtures.userBook(20L, 1L, 11L);
        BookshelfItem item = TestFixtures.bookshelfItem(30L, 100L, 200L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(bookRepository.findByIsbn13("9791190000002")).willReturn(Optional.empty());
        given(bookRepository.findTop20ByTitleSearchContainingOrAuthorContainingIgnoreCase("새책", "작가 미상"))
                .willReturn(List.of());
        given(bookRepository.save(any(Book.class))).willAnswer(invocation -> {
            Book book = invocation.getArgument(0);
            TestFixtures.setId(book, 11L);
            return book;
        });
        given(userBookRepository.findByUserIdAndBookId(1L, 11L)).willReturn(Optional.empty());
        given(userBookRepository.save(any(UserBook.class))).willReturn(userBook);
        given(bookshelfItemRepository.save(any(BookshelfItem.class))).willReturn(item);
        given(spaceService.findOrCreateMySpace(1L)).willReturn(TestFixtures.space(1L, 1L));
        ArgumentCaptor<Book> bookCaptor = ArgumentCaptor.forClass(Book.class);

        var response = bookService.addKakaoBookToShelf(
                1L,
                100L,
                new AddKakaoBookToShelfRequest(
                        "9791190000002",
                        null,
                        " 새 책 ",
                        " ",
                        " publisher ",
                        LocalDate.of(2024, 1, 1),
                        "description",
                        null,
                        "https://kakao.example/books/2",
                        200L,
                        1,
                        "FRONT",
                        "SPINE"
                )
        );

        assertThat(response.bookId()).isEqualTo(11L);
        then(bookRepository).should().save(bookCaptor.capture());
        assertThat(bookCaptor.getValue().getTitle()).isEqualTo("새 책");
        assertThat(bookCaptor.getValue().getAuthor()).isEqualTo("작가 미상");
        assertThat(bookCaptor.getValue().getPublisher()).isEqualTo("publisher");
    }

    @Test
    @DisplayName("내 책 이동은 기존 활성 배치가 있으면 새로 만들지 않고 위치만 변경한다")
    void move_existingActiveItem_movesPlacement() {
        UserBook userBook = TestFixtures.userBook(20L, 1L, 10L);
        BookshelfItem item = TestFixtures.bookshelfItem(30L, 100L, 200L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userBookRepository.findById(20L)).willReturn(Optional.of(userBook));
        given(bookshelfItemRepository.findByUserBookIdAndStatus(20L, "ACTIVE")).willReturn(Optional.of(item));

        var response = bookService.move(
                1L,
                20L,
                new MoveUserBookRequest(101L, 201L, 7, "BACK", "COVER")
        );

        assertThat(response.storageStatus()).isEqualTo("ON_SHELF");
        assertThat(response.placement().bookshelfId()).isEqualTo(101L);
        assertThat(response.placement().sectionId()).isEqualTo(201L);
        assertThat(response.placement().slotNo()).isEqualTo(7);
        then(bookshelfItemRepository).should().findByUserBookIdAndStatus(20L, "ACTIVE");
        then(bookshelfItemRepository).shouldHaveNoMoreInteractions();
    }

    @Test
    @DisplayName("내 책 보관은 UserBook을 ARCHIVED로 바꾸고 활성 책장 아이템을 제거한다")
    void archive_ownedBook_archivesAndRemovesActiveItem() {
        UserBook userBook = TestFixtures.userBook(20L, 1L, 10L);
        BookshelfItem item = TestFixtures.bookshelfItem(30L, 100L, 200L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userBookRepository.findById(20L)).willReturn(Optional.of(userBook));
        given(bookshelfItemRepository.findByUserBookIdAndStatus(20L, "ACTIVE")).willReturn(Optional.of(item));
        given(spaceService.findOrCreateMySpace(1L)).willReturn(TestFixtures.space(1L, 1L));

        var response = bookService.archive(1L, 20L);

        assertThat(response.storageStatus()).isEqualTo("ARCHIVED");
        assertThat(item.getStatus()).isEqualTo("REMOVED");
    }

    @Test
    @DisplayName("내 책 삭제는 UserBook을 DELETED로 바꾸고 활성 책장 아이템을 제거한다")
    void delete_ownedBook_deletesAndRemovesActiveItem() {
        UserBook userBook = TestFixtures.userBook(20L, 1L, 10L);
        BookshelfItem item = TestFixtures.bookshelfItem(30L, 100L, 200L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userBookRepository.findById(20L)).willReturn(Optional.of(userBook));
        given(bookshelfItemRepository.findByUserBookIdAndStatus(20L, "ACTIVE")).willReturn(Optional.of(item));
        given(spaceService.findOrCreateMySpace(1L)).willReturn(TestFixtures.space(1L, 1L));

        bookService.delete(1L, 20L);

        assertThat(userBook.getStorageStatus()).isEqualTo("DELETED");
        assertThat(userBook.getDeletedAt()).isNotNull();
        assertThat(item.getStatus()).isEqualTo("REMOVED");
    }

    @Test
    @DisplayName("내 책 좋아요/취소는 소유자 검증 후 liked 상태를 토글한다")
    void likeAndUnlike_ownedBook_togglesLikedState() {
        Book book = TestFixtures.book(10L, "도서");
        UserBook userBook = TestFixtures.userBook(20L, 1L, 10L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userBookRepository.findById(20L)).willReturn(Optional.of(userBook));
        given(bookRepository.findById(10L)).willReturn(Optional.of(book));

        var liked = bookService.like(1L, 20L);
        var unliked = bookService.unlike(1L, 20L);

        assertThat(liked.active()).isTrue();
        assertThat(liked.count()).isEqualTo(1L);
        assertThat(unliked.active()).isFalse();
        assertThat(unliked.count()).isZero();
        assertThat(userBook.isLiked()).isFalse();
        assertThat(book.getLikeCount()).isZero();
    }

    @Test
    @DisplayName("보관된 내 책 목록은 ARCHIVED 상태만 최신순 repository 결과로 응답한다")
    void getArchived_returnsArchivedBooks() {
        UserBook archived = TestFixtures.userBook(20L, 1L, 10L);
        archived.archive();
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userBookRepository.findByUserIdAndStorageStatusOrderByAddedAtDesc(1L, "ARCHIVED"))
                .willReturn(List.of(archived));

        var response = bookService.getArchived(1L);

        assertThat(response.books()).hasSize(1);
        assertThat(response.books().getFirst().storageStatus()).isEqualTo("ARCHIVED");
    }

    @Test
    @DisplayName("내 책 배치 목록은 storage/reading/bookshelf/section 필터를 적용한다")
    void getUserBooks_appliesPlacementFilters() {
        UserBook reading = TestFixtures.userBook(20L, 1L, 10L);
        reading.update("READING", null, null);
        UserBook done = TestFixtures.userBook(21L, 1L, 11L);
        done.update("DONE", null, null);
        BookshelfItem readingItem = TestFixtures.bookshelfItem(30L, 100L, 200L, 20L);
        given(userService.requireUserId(1L)).willReturn(1L);
        given(userBookRepository.findByUserIdAndStorageStatusNotOrderByAddedAtDesc(1L, "DELETED"))
                .willReturn(List.of(done, reading));
        given(bookshelfItemRepository.findByUserBookIdInAndStatus(List.of(20L), "ACTIVE"))
                .willReturn(List.of(readingItem));

        var response = bookService.getUserBooks(1L, "ON_SHELF", "READING", 100L, 200L, 10);

        assertThat(response.books()).hasSize(1);
        assertThat(response.books().getFirst().id()).isEqualTo(20L);
        assertThat(response.books().getFirst().bookshelfId()).isEqualTo(100L);
        assertThat(response.books().getFirst().sectionId()).isEqualTo(200L);
    }
}
