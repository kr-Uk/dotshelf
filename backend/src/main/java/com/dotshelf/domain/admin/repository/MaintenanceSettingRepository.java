package com.dotshelf.domain.admin.repository;

import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.admin.entity.MaintenanceSetting;

public interface MaintenanceSettingRepository extends JpaRepository<MaintenanceSetting, Long> {

    Optional<MaintenanceSetting> findFirstByOrderByIdDesc();
}
