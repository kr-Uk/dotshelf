package com.dotshelf.domain.report.repository;

import java.util.List;

import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.JpaSpecificationExecutor;

import com.dotshelf.domain.report.entity.Report;

public interface ReportRepository extends JpaRepository<Report, Long>, JpaSpecificationExecutor<Report> {

    boolean existsByReporterUserIdAndTargetTypeAndTargetId(Long reporterUserId, String targetType, Long targetId);

    List<Report> findByReporterUserIdOrderByIdDesc(Long reporterUserId);

    List<Report> findByTargetTypeAndTargetIdOrderByIdDesc(String targetType, Long targetId);

    List<Report> findByReporterUserIdOrReportedUserIdOrderByIdDesc(
            Long reporterUserId,
            Long reportedUserId,
            Pageable pageable
    );

    long countByStatus(String status);

    long countByReportedUserIdOrTargetTypeIgnoreCaseAndTargetId(Long reportedUserId, String targetType, Long targetId);
}
