package com.dotshelf.domain.book.repository;

import java.util.Collection;
import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.book.entity.BookReview;

public interface BookReviewRepository extends JpaRepository<BookReview, Long> {

    List<BookReview> findByBookIdAndStatus(Long bookId, String status);

    List<BookReview> findByBookIdInAndStatus(Collection<Long> bookIds, String status);

    Optional<BookReview> findByBookIdAndUserIdAndStatus(Long bookId, Long userId, String status);

    long countByBookIdAndStatus(Long bookId, String status);
}
