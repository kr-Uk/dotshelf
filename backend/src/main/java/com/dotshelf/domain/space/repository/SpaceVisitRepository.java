package com.dotshelf.domain.space.repository;

import java.time.LocalDate;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.dotshelf.domain.space.entity.SpaceVisit;

public interface SpaceVisitRepository extends JpaRepository<SpaceVisit, Long> {

    boolean existsBySpaceIdAndVisitorUserIdAndVisitedDate(Long spaceId, Long visitorUserId, LocalDate visitedDate);

    long countByVisitedDate(LocalDate visitedDate);

    @Query("SELECT COUNT(DISTINCT v.visitorUserId) FROM SpaceVisit v "
            + "WHERE v.spaceId = :spaceId AND v.visitedDate >= :fromDate")
    long countDistinctVisitorsBySpaceIdSince(@Param("spaceId") Long spaceId, @Param("fromDate") LocalDate fromDate);
}
