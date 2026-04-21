package com.dotshelf.domain.book.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.book.entity.Book;

public interface BookRepository extends JpaRepository<Book, Long> {

    List<Book> findTop20ByTitleContainingIgnoreCaseOrAuthorContainingIgnoreCase(String title, String author);
}
