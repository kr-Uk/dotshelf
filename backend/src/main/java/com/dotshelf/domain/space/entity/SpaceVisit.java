package com.dotshelf.domain.space.entity;

import java.time.LocalDate;
import java.time.LocalDateTime;

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
        name = "space_visits",
        uniqueConstraints = @UniqueConstraint(columnNames = {"space_id", "visitor_user_id", "visited_date"})
)
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SpaceVisit extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long spaceId;

    @Column(nullable = false)
    private Long visitorUserId;

    @Column(nullable = false)
    private LocalDate visitedDate;

    @Column(nullable = false)
    private LocalDateTime visitedAt = LocalDateTime.now();

    @Builder
    private SpaceVisit(Long spaceId, Long visitorUserId, LocalDate visitedDate) {
        this.spaceId = spaceId;
        this.visitorUserId = visitorUserId;
        this.visitedDate = visitedDate;
    }
}
