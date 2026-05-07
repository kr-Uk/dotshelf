package com.dotshelf.domain.readingrecord.repository;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.test.context.ActiveProfiles;

import com.dotshelf.domain.book.repository.BookRepository;
import com.dotshelf.domain.book.repository.UserBookRepository;
import com.dotshelf.support.TestFixtures;

@DataJpaTest
@ActiveProfiles("test")
class ReadingRecordRepositoryTest {

    @Autowired
    private ReadingRecordRepository readingRecordRepository;

    @Autowired
    private ReadingRecordLikeRepository likeRepository;

    @Autowired
    private ReadingRecordCommentRepository commentRepository;

    @Autowired
    private ReadingRecordCommentLikeRepository commentLikeRepository;

    @Autowired
    private BookRepository bookRepository;

    @Autowired
    private UserBookRepository userBookRepository;

    @Test
    @DisplayName("독서기록은 userBookId unique 제약으로 하나만 허용한다")
    void readingRecord_uniqueUserBook() {
        readingRecordRepository.saveAndFlush(TestFixtures.readingRecord(null, 10L));

        assertThatThrownBy(() -> readingRecordRepository.saveAndFlush(TestFixtures.readingRecord(null, 10L)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("userBookId와 status 기준으로 독서기록을 조회한다")
    void findByUserBookIdInAndStatus() {
        var active = readingRecordRepository.save(TestFixtures.readingRecord(null, 10L));
        var deleted = TestFixtures.readingRecord(null, 11L);
        deleted.delete();
        readingRecordRepository.save(deleted);

        assertThat(readingRecordRepository.findByUserBookId(10L)).isPresent();
        assertThat(readingRecordRepository.findByUserBookIdInAndStatus(java.util.List.of(10L, 11L), "ACTIVE"))
                .extracting("id")
                .containsExactly(active.getId());
    }

    @Test
    @DisplayName("좋아요는 recordId+userId 중복을 unique 제약으로 막는다")
    void readingRecordLike_uniqueRecordAndUser() {
        likeRepository.saveAndFlush(TestFixtures.readingRecordLike(10L, 1L));

        assertThatThrownBy(() -> likeRepository.saveAndFlush(TestFixtures.readingRecordLike(10L, 1L)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("댓글은 readingRecordId 기준 id 오름차순으로 조회한다")
    void comments_orderedById() {
        var first = commentRepository.save(TestFixtures.readingRecordComment(null, 10L, 1L));
        var second = commentRepository.save(TestFixtures.readingRecordComment(null, 10L, 2L));
        commentRepository.save(TestFixtures.readingRecordComment(null, 11L, 3L));

        assertThat(commentRepository.findByReadingRecordIdOrderByIdAsc(10L))
                .extracting("id")
                .containsExactly(first.getId(), second.getId());
    }

    @Test
    @DisplayName("댓글 좋아요는 commentId+userId 중복을 unique 제약으로 막는다")
    void commentLike_uniqueCommentAndUser() {
        commentLikeRepository.saveAndFlush(TestFixtures.readingRecordCommentLike(10L, 1L));

        assertThatThrownBy(() -> commentLikeRepository.saveAndFlush(TestFixtures.readingRecordCommentLike(10L, 1L)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    @DisplayName("사용자 독서기록 summary projection은 userBook과 book을 join한다")
    void findUserRecordSummaries_joinsUserBookAndBook() {
        var book = bookRepository.save(TestFixtures.book(null, "Join Book"));
        var userBook = userBookRepository.save(TestFixtures.userBook(null, 1L, book.getId()));
        readingRecordRepository.saveAndFlush(TestFixtures.readingRecord(null, userBook.getId()));

        var summaries = readingRecordRepository.findUserRecordSummaries(1L);

        assertThat(summaries).hasSize(1);
        assertThat(summaries.get(0).bookId()).isEqualTo(book.getId());
        assertThat(summaries.get(0).bookTitle()).isEqualTo("Join Book");
    }
}
