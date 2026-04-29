package com.dotshelf.domain.admin.repository;

import java.util.List;

import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.admin.entity.SettlementPayoutBatch;

public interface SettlementPayoutBatchRepository extends JpaRepository<SettlementPayoutBatch, Long> {

    List<SettlementPayoutBatch> findAllByOrderByIdDesc();
}
