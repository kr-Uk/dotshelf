package com.dotshelf.domain.space.repository;

import java.time.LocalDate;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.space.entity.SpaceVisit;

public interface SpaceVisitRepository extends JpaRepository<SpaceVisit, Long> {

    boolean existsBySpaceIdAndVisitorUserIdAndVisitedDate(Long spaceId, Long visitorUserId, LocalDate visitedDate);
}
