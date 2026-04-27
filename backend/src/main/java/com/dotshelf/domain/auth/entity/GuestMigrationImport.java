package com.dotshelf.domain.auth.entity;

import java.time.LocalDateTime;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Lob;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "guest_migration_imports")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class GuestMigrationImport extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false, length = 30)
    private String migrationStatus = "PENDING";

    @Lob
    @Column(nullable = false)
    private String payload;

    private Long importedSpaceId;

    @Column(length = 500)
    private String failureReason;

    @Column(nullable = false)
    private LocalDateTime requestedAt = LocalDateTime.now();

    private LocalDateTime completedAt;

    @Builder
    private GuestMigrationImport(Long userId, String payload) {
        this.userId = userId;
        this.payload = payload;
    }
}
