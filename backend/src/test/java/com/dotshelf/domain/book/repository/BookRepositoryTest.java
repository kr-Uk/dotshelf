package com.dotshelf.domain.book.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.book.entity.BookReview;
import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class BookRepositoryTest {

    @Autowired
    private BookRepository bookRepository;

    @Autowired
    private UserBookRepository userBookRepository;

    @Autowired
    private BookReviewRepository bookReviewRepository;

    @Test
    @DisplayName("isbn13과 isbn10으로 책을 조회한다")
    void findByIsbn() {
        Book book = bookRepository.saveAndFlush(Book.builder()
                .isbn13("9781234567890")
                .isbn10("1234567890")
                .title("Clean Code")
                .author("author")
                .publisher("publisher")
                .build());

        assertThat(bookRepository.findByIsbn13(book.getIsbn13())).isPresent();
        assertThat(bookRepository.findFirstByIsbn10(book.getIsbn10())).isPresent();
    }

    @Test
    @DisplayName("isbn13은 unique 제약으로 중복 저장을 막는다")
    void uniqueIsbn13() {
        bookRepository.saveAndFlush(Book.builder()
                .isbn13("9781234567890")
                .title("Book A")
                .author("author")
                .publisher("publisher")
                .build());

        assertThatThrownBy(() -> bookRepository.saveAndFlush(Book.builder()
                .isbn13("9781234567890")
                .title("Book B")
                .author("author")
                .publisher("publisher")
                .build()))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("내 책은 사용자별 storageStatus와 최신순으로 조회한다")
    void userBook_findByUserAndStorageStatus() {
        userBookRepository.save(TestFixtures.userBook(null, 1L, 10L));
        var archived = TestFixtures.userBook(null, 1L, 11L);
        archived.archive();
        userBookRepository.save(archived);
        userBookRepository.flush();

        assertThat(userBookRepository.findByUserIdAndStorageStatusOrderByAddedAtDesc(1L, "ON_SHELF")).hasSize(1);
        assertThat(userBookRepository.findByUserIdAndStorageStatusNot(1L, "DELETED")).hasSize(2);
        assertThat(userBookRepository.countByUserIdAndStorageStatusNot(1L, "DELETED")).isEqualTo(2);
    }

    @Test
    @DisplayName("책 리뷰는 bookId+userId 중복을 unique 제약으로 막는다")
    void review_uniqueBookAndUser() {
        bookReviewRepository.saveAndFlush(TestFixtures.bookReview(null, 10L, 1L));

        assertThatThrownBy(() -> bookReviewRepository.saveAndFlush(TestFixtures.bookReview(null, 10L, 1L)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("ACTIVE 리뷰만 책 기준으로 조회하고 카운트한다")
    void review_findActiveByBook() {
        BookReview active = bookReviewRepository.save(TestFixtures.bookReview(null, 10L, 1L));
        BookReview deleted = TestFixtures.bookReview(null, 10L, 2L);
        deleted.delete();
        bookReviewRepository.save(deleted);
        bookReviewRepository.flush();

        assertThat(bookReviewRepository.findByBookIdAndStatus(10L, "ACTIVE")).extracting(BookReview::getId)
                .containsExactly(active.getId());
        assertThat(bookReviewRepository.countByBookIdAndStatus(10L, "ACTIVE")).isEqualTo(1);
    }
}
