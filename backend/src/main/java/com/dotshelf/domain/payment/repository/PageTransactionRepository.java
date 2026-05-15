package com.dotshelf.domain.payment.repository;

import java.time.Instant;
import java.util.List;

import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.payment.entity.PageTransaction;

public interface PageTransactionRepository extends JpaRepository<PageTransaction, Long> {

    List<PageTransaction> findByUserIdOrderByCreatedAtDesc(Long userId, Pageable pageable);

    List<PageTransaction> findByUserIdOrderByCreatedAtDescIdDesc(Long userId, Pageable pageable);

    @Query("""
            select t
            from PageTransaction t
            where t.userId = :userId
              and (t.createdAt < :cursorCreatedAt
                   or (t.createdAt = :cursorCreatedAt and t.id < :cursorId))
            order by t.createdAt desc, t.id desc
            """)
    List<PageTransaction> findUserTransactionsBeforeCursor(
            @Param("userId") Long userId,
            @Param("cursorCreatedAt") Instant cursorCreatedAt,
            @Param("cursorId") Long cursorId,
            Pageable pageable
    );
}
