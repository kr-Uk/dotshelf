package com.dotshelf.domain.book.repository;

import java.util.Collection;
import java.util.List;
import java.util.Optional;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.book.entity.Book;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookSummaryResponse;

public interface BookRepository extends JpaRepository<Book, Long> {

    List<Book> findTop20ByTitleSearchContainingOrAuthorContainingIgnoreCase(String titleSearch, String author);

    Page<Book> findByTitleContainingIgnoreCaseOrAuthorContainingIgnoreCase(
            String title,
            String author,
            Pageable pageable
    );

    Optional<Book> findByIsbn13(String isbn13);

    Optional<Book> findFirstByIsbn10(String isbn10);

    @Query("SELECT new com.dotshelf.domain.bookshelf.dto.BookshelfDtos$BookSummaryResponse("
            + "b.id, b.title, b.author, b.publisher, b.description, b.isbn13, b.isbn10, b.coverImageUrl) "
            + "FROM Book b WHERE b.id IN :bookIds")
    List<BookSummaryResponse> findBookshelfRenderSummariesByIdIn(@Param("bookIds") Collection<Long> bookIds);
}
