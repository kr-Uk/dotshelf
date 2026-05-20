package com.dotshelf.domain.bookshelf.repository;

import java.util.List;
import java.util.Optional;
import java.util.Collection;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.bookshelf.entity.BookshelfItem;

public interface BookshelfItemRepository extends JpaRepository<BookshelfItem, Long> {

    List<BookshelfItem> findByBookshelfIdAndStatusOrderByBookshelfSectionIdAscSlotNoAsc(Long bookshelfId, String status);

    List<BookshelfItem> findByBookshelfIdInAndStatus(Collection<Long> bookshelfIds, String status);

    List<BookshelfItem> findByBookshelfSectionIdAndStatusOrderBySlotNoAscIdAsc(Long bookshelfSectionId, String status);

    Optional<BookshelfItem> findByUserBookIdAndStatus(Long userBookId, String status);

    List<BookshelfItem> findByUserBookIdInAndStatus(Collection<Long> userBookIds, String status);

    long countByBookshelfIdAndStatus(Long bookshelfId, String status);

    long countByBookshelfSectionIdAndStatus(Long bookshelfSectionId, String status);

    long countByBookshelfSectionIdAndStatusAndIdNot(Long bookshelfSectionId, String status, Long id);

    boolean existsByBookshelfIdAndBookshelfSectionIdAndSlotNoAndStatus(
            Long bookshelfId,
            Long bookshelfSectionId,
            Integer slotNo,
            String status
    );

    boolean existsByBookshelfIdAndBookshelfSectionIdAndSlotNoAndStatusAndIdNot(
            Long bookshelfId,
            Long bookshelfSectionId,
            Integer slotNo,
            String status,
            Long id
    );
}
