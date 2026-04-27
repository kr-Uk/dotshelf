package com.dotshelf.domain.book.repository;

import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.book.entity.Book;

public interface BookRepository extends JpaRepository<Book, Long> {

    List<Book> findTop20ByTitleSearchContainingOrAuthorContainingIgnoreCase(String titleSearch, String author);

    Optional<Book> findByIsbn13(String isbn13);

    Optional<Book> findFirstByIsbn10(String isbn10);
}
