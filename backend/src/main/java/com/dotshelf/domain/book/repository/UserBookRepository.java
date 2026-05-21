package com.dotshelf.domain.book.repository;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.book.dto.BookDtos.OwnedBookSummary;
import com.dotshelf.domain.book.entity.UserBook;

public interface UserBookRepository extends JpaRepository<UserBook, Long> {

    Optional<UserBook> findByUserIdAndBookId(Long userId, Long bookId);

    List<UserBook> findByUserIdAndStorageStatusOrderByAddedAtDesc(Long userId, String storageStatus);

    List<UserBook> findByUserIdAndStorageStatusNot(Long userId, String storageStatus);

    List<UserBook> findByUserIdAndStorageStatusNotOrderByAddedAtDesc(Long userId, String storageStatus);

    long countByUserIdAndStorageStatusNot(Long userId, String storageStatus);

    long countByCreatedAtBetween(Instant start, Instant end);

    /**
     * 대시보드 \"보유 책 풀 목록\" 정렬/페이지네이션용 단일 batch fetch.
     * Book과 join해서 카드에 필요한 필드만 projection. v1.0은 메모리 정렬·슬라이스로 처리.
     */
    @Query("SELECT new com.dotshelf.domain.book.dto.BookDtos$OwnedBookSummary("
            + "ub.id, b.id, b.title, b.author, b.publisher, b.publishedDate, b.coverImageUrl, ub.addedAt) "
            + "FROM UserBook ub JOIN com.dotshelf.domain.book.entity.Book b ON ub.bookId = b.id "
            + "WHERE ub.userId = :userId AND ub.storageStatus <> 'DELETED'")
    List<OwnedBookSummary> findOwnedBookSummaries(@Param("userId") Long userId);
}
