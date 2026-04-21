package com.dotshelf.domain.space.entity;

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
        name = "space_likes",
        uniqueConstraints = @UniqueConstraint(columnNames = {"space_id", "user_id"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SpaceLike extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long spaceId;

    @Column(nullable = false)
    private Long userId;

    @Builder
    private SpaceLike(Long spaceId, Long userId) {
        this.spaceId = spaceId;
        this.userId = userId;
    }
}
