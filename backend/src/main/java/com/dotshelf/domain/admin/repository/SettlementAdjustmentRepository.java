package com.dotshelf.domain.admin.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.admin.entity.SettlementAdjustment;

public interface SettlementAdjustmentRepository extends JpaRepository<SettlementAdjustment, Long> {

    List<SettlementAdjustment> findByUserIdOrderByIdDesc(Long userId);
}
