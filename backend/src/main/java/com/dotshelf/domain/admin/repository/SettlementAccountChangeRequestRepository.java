package com.dotshelf.domain.admin.repository;

import java.util.List;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import com.dotshelf.domain.admin.entity.SettlementAccountChangeRequest;

public interface SettlementAccountChangeRequestRepository extends JpaRepository<SettlementAccountChangeRequest, Long> {

    List<SettlementAccountChangeRequest> findAllByOrderByIdDesc();

    Page<SettlementAccountChangeRequest> findAllByOrderByIdDesc(Pageable pageable);
}
