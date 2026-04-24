package com.dotshelf.domain.readingrecord.repository;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.readingrecord.entity.ReadingRecordLike;

public interface ReadingRecordLikeRepository extends JpaRepository<ReadingRecordLike, Long> {

    boolean existsByReadingRecordIdAndUserId(Long readingRecordId, Long userId);

    void deleteByReadingRecordIdAndUserId(Long readingRecordId, Long userId);
}
