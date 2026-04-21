package com.dotshelf.domain.readingrecord.repository;

import java.util.Collection;
import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.readingrecord.entity.ReadingRecord;

public interface ReadingRecordRepository extends JpaRepository<ReadingRecord, Long> {

    Optional<ReadingRecord> findByUserBookId(Long userBookId);

    List<ReadingRecord> findByUserBookIdInAndStatus(Collection<Long> userBookIds, String status);
}
