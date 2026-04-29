package com.dotshelf.domain.admin.repository;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.admin.entity.AdminAuditLog;

public interface AdminAuditLogRepository extends JpaRepository<AdminAuditLog, Long> {
}
