package com.dotshelf.domain.readingrecord.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.readingrecord.entity.ReadingRecordComment;

public interface ReadingRecordCommentRepository extends JpaRepository<ReadingRecordComment, Long> {

    List<ReadingRecordComment> findByReadingRecordIdOrderByIdAsc(Long readingRecordId);
}
