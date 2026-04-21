package com.dotshelf.domain.book.repository;

import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.book.entity.UserBook;

public interface UserBookRepository extends JpaRepository<UserBook, Long> {

    Optional<UserBook> findByUserIdAndBookId(Long userId, Long bookId);

    List<UserBook> findByUserIdAndStorageStatusOrderByAddedAtDesc(Long userId, String storageStatus);

    List<UserBook> findByUserIdAndStorageStatusNot(Long userId, String storageStatus);

    long countByUserIdAndStorageStatusNot(Long userId, String storageStatus);
}
