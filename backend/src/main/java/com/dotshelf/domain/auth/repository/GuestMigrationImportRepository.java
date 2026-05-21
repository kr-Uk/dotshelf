package com.dotshelf.domain.auth.repository;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.auth.entity.GuestMigrationImport;

public interface GuestMigrationImportRepository extends JpaRepository<GuestMigrationImport, Long> {
}
