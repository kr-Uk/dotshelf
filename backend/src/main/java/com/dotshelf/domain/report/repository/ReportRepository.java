package com.dotshelf.domain.report.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.report.entity.Report;

public interface ReportRepository extends JpaRepository<Report, Long> {

    boolean existsByReporterUserIdAndTargetTypeAndTargetId(Long reporterUserId, String targetType, Long targetId);

    List<Report> findByReporterUserIdOrderByIdDesc(Long reporterUserId);

    List<Report> findByTargetTypeAndTargetIdOrderByIdDesc(String targetType, Long targetId);

    long countByStatus(String status);
}
