package com.dotshelf.domain.readingrecord.entity;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(
        name = "reading_record_likes",
        uniqueConstraints = @UniqueConstraint(columnNames = {"reading_record_id", "user_id"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ReadingRecordLike extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long readingRecordId;

    @Column(nullable = false)
    private Long userId;

    @Builder
    private ReadingRecordLike(Long readingRecordId, Long userId) {
        this.readingRecordId = readingRecordId;
        this.userId = userId;
    }
}
