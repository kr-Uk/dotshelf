package com.dotshelf.domain.readingrecord.repository;

import java.util.Collection;
import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos.ReadingRecordSummary;
import com.dotshelf.domain.readingrecord.entity.ReadingRecord;

public interface ReadingRecordRepository extends JpaRepository<ReadingRecord, Long> {

    Optional<ReadingRecord> findByUserBookId(Long userBookId);

    List<ReadingRecord> findByUserBookIdInAndStatus(Collection<Long> userBookIds, String status);

    /**
     * 대시보드 \"독후감 풀 목록\" 정렬/페이지네이션용 단일 batch fetch.
     * UserBook + Book join으로 bookId/title까지 한 번에 projection.
     */
    @Query("SELECT new com.dotshelf.domain.readingrecord.dto.ReadingRecordDtos$ReadingRecordSummary("
            + "r.id, b.id, b.title, r.content, r.likeCount, r.commentCount, r.viewCount, r.createdAt) "
            + "FROM ReadingRecord r "
            + "JOIN com.dotshelf.domain.book.entity.UserBook ub ON r.userBookId = ub.id "
            + "JOIN com.dotshelf.domain.book.entity.Book b ON ub.bookId = b.id "
            + "WHERE ub.userId = :userId "
            + "AND r.status = 'ACTIVE' "
            + "AND ub.storageStatus <> 'DELETED'")
    List<ReadingRecordSummary> findUserRecordSummaries(@Param("userId") Long userId);
}
