package com.dotshelf.domain.bookshelf.repository;

import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.bookshelf.entity.BookshelfItem;

public interface BookshelfItemRepository extends JpaRepository<BookshelfItem, Long> {

    List<BookshelfItem> findByBookshelfIdAndStatusOrderByBookshelfSectionIdAscSlotNoAsc(Long bookshelfId, String status);

    Optional<BookshelfItem> findByUserBookIdAndStatus(Long userBookId, String status);

    long countByBookshelfIdAndStatus(Long bookshelfId, String status);
}
