package com.dotshelf.domain.space.entity;

import java.time.LocalDateTime;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Lob;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(
        name = "space_layout_snapshots",
        uniqueConstraints = @UniqueConstraint(columnNames = {"space_id", "layout_version"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SpaceLayoutSnapshot extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long spaceId;

    @Column(nullable = false)
    private long layoutVersion;

    @Lob
    @Column(nullable = false)
    private String layoutPayload;

    @Column(nullable = false)
    private Long savedByUserId;

    @Column(nullable = false)
    private LocalDateTime savedAt = LocalDateTime.now();

    @Builder
    private SpaceLayoutSnapshot(Long spaceId, long layoutVersion, String layoutPayload, Long savedByUserId) {
        this.spaceId = spaceId;
        this.layoutVersion = layoutVersion;
        this.layoutPayload = layoutPayload;
        this.savedByUserId = savedByUserId;
    }
}
