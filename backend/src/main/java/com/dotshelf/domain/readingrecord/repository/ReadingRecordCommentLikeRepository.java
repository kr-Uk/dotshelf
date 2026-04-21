package com.dotshelf.domain.readingrecord.repository;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.readingrecord.entity.ReadingRecordCommentLike;

public interface ReadingRecordCommentLikeRepository extends JpaRepository<ReadingRecordCommentLike, Long> {

    boolean existsByCommentIdAndUserId(Long commentId, Long userId);

    void deleteByCommentIdAndUserId(Long commentId, Long userId);
}
