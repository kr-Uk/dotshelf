package com.dotshelf.domain.bookshelf.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.bookshelf.entity.BookshelfSection;

public interface BookshelfSectionRepository extends JpaRepository<BookshelfSection, Long> {

    List<BookshelfSection> findByBookshelfIdOrderByRowNoAscColumnNoAsc(Long bookshelfId);
}
